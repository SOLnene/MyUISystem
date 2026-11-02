using System.Globalization;
using Microsoft.Data.Sqlite;

namespace CloudSaveServer;

internal sealed record SocialPlayer(string Uid, string Name, string Relationship);
internal sealed record SocialState(SocialPlayer[] Friends, SocialPlayer[] Requests);
internal sealed record SocialMessage(long Id, string SenderUid, string ClientId, string Text, string SentAtUtc);
internal sealed class SocialFailure(string code, int status = 409) : Exception(code)
{ internal int Status => status; }

internal sealed class SocialStore(ServerSettings settings)
{
    T Use<T>(bool write, Func<SqliteCommand, T> action)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            { DataSource = settings.DatabasePath, ForeignKeys = true, DefaultTimeout = 5 }.ToString());
        connection.Open();
        using var transaction = connection.BeginTransaction(deferred: !write);
        using var command = connection.CreateCommand(); command.Transaction = transaction;
        var result = action(command); transaction.Commit(); return result;
    }

    internal void Initialize() => Use(true, command =>
    {
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS friend_requests (
                sender INTEGER NOT NULL REFERENCES accounts(uid), recipient INTEGER NOT NULL REFERENCES accounts(uid),
                created_at TEXT NOT NULL, PRIMARY KEY(sender,recipient), CHECK(sender<>recipient));
            CREATE TABLE IF NOT EXISTS friends (
                owner INTEGER NOT NULL REFERENCES accounts(uid), peer INTEGER NOT NULL REFERENCES accounts(uid),
                created_at TEXT NOT NULL, PRIMARY KEY(owner,peer), CHECK(owner<>peer));
            CREATE TABLE IF NOT EXISTS friend_messages (
                id INTEGER PRIMARY KEY AUTOINCREMENT, sender INTEGER NOT NULL REFERENCES accounts(uid),
                recipient INTEGER NOT NULL REFERENCES accounts(uid), client_id TEXT NOT NULL,
                body TEXT NOT NULL, sent_at TEXT NOT NULL, UNIQUE(sender,client_id));
            CREATE INDEX IF NOT EXISTS friend_messages_pair ON friend_messages(sender,recipient,id);
            CREATE INDEX IF NOT EXISTS friend_requests_recipient ON friend_requests(recipient);
            """;
        return command.ExecuteNonQuery();
    });

    internal static long Uid(string? text) => long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out long uid)
        && uid >= 100000001 ? uid : throw new SocialFailure("invalid_uid", 400);

    static void Pair(SqliteCommand command, long owner, long peer)
    { command.Parameters.AddWithValue("$owner", owner); command.Parameters.AddWithValue("$peer", peer); }
    static bool Exists(SqliteCommand command, string sql)
    { command.CommandText = sql; return command.ExecuteScalar() != null; }
    static void RequireFriends(SqliteCommand command)
    {
        if (!Exists(command, "SELECT 1 FROM friends WHERE owner=$owner AND peer=$peer"))
            throw new SocialFailure("friend_required", 403);
    }
    static void RequireCapacity(SqliteCommand command)
    {
        command.CommandText = "SELECT MAX((SELECT COUNT(*) FROM friends WHERE owner=$owner),(SELECT COUNT(*) FROM friends WHERE owner=$peer))";
        if (Convert.ToInt32(command.ExecuteScalar()) >= 200) throw new SocialFailure("friend_limit");
    }

    internal SocialPlayer Find(long owner, long peer) => Use<SocialPlayer>(false, command =>
    {
        Pair(command, owner, peer); command.CommandText = "SELECT username FROM accounts WHERE uid=$peer";
        string name = command.ExecuteScalar() as string ?? throw new SocialFailure("player_not_found", 404);
        string relationship = owner == peer ? "self" : Exists(command, "SELECT 1 FROM friends WHERE owner=$owner AND peer=$peer") ? "friend"
            : Exists(command, "SELECT 1 FROM friend_requests WHERE sender=$owner AND recipient=$peer") ? "sent"
            : Exists(command, "SELECT 1 FROM friend_requests WHERE sender=$peer AND recipient=$owner") ? "received" : "none";
        return new(peer.ToString(CultureInfo.InvariantCulture), name, relationship);
    });

    internal SocialState List(long owner) => Use<SocialState>(false, command =>
    {
        command.Parameters.AddWithValue("$owner", owner);
        SocialPlayer[] Read(string sql, string relationship)
        {
            command.CommandText = sql; using var reader = command.ExecuteReader(); var rows = new List<SocialPlayer>();
            while (reader.Read()) rows.Add(new(reader.GetInt64(0).ToString(CultureInfo.InvariantCulture), reader.GetString(1), relationship));
            return rows.ToArray();
        }
        var friends = Read("SELECT a.uid,a.username FROM friends f JOIN accounts a ON a.uid=f.peer WHERE f.owner=$owner ORDER BY a.username,a.uid", "friend");
        var requests = Read("SELECT a.uid,a.username FROM friend_requests r JOIN accounts a ON a.uid=r.sender WHERE r.recipient=$owner ORDER BY r.created_at,a.uid", "received");
        return new(friends, requests);
    });

    internal object Request(long owner, long peer) => Use(true, command =>
    {
        if (owner == peer) throw new SocialFailure("cannot_add_self", 400);
        Pair(command, owner, peer);
        if (!Exists(command, "SELECT 1 FROM accounts WHERE uid=$peer")) throw new SocialFailure("player_not_found", 404);
        if (Exists(command, "SELECT 1 FROM friends WHERE owner=$owner AND peer=$peer")) return new { status = "friend" };
        if (Exists(command, "SELECT 1 FROM friend_requests WHERE sender=$peer AND recipient=$owner")) throw new SocialFailure("incoming_request_exists");
        RequireCapacity(command);
        command.CommandText = "SELECT COUNT(*) FROM friend_requests WHERE sender=$owner OR recipient=$peer";
        if (Convert.ToInt32(command.ExecuteScalar()) >= 200) throw new SocialFailure("request_limit");
        command.Parameters.AddWithValue("$time", DateTimeOffset.UtcNow.ToString("O"));
        command.CommandText = "INSERT OR IGNORE INTO friend_requests VALUES($owner,$peer,$time)";
        command.ExecuteNonQuery(); return new { status = "sent" };
    });

    internal object Respond(long owner, long peer, bool accept) => Use(true, command =>
    {
        Pair(command, owner, peer);
        if (accept && Exists(command, "SELECT 1 FROM friends WHERE owner=$owner AND peer=$peer")) return new { accepted = true };
        if (!Exists(command, "SELECT 1 FROM friend_requests WHERE sender=$peer AND recipient=$owner")) throw new SocialFailure("request_not_found", 404);
        if (accept)
        {
            RequireCapacity(command); command.Parameters.AddWithValue("$time", DateTimeOffset.UtcNow.ToString("O"));
            // Both owners gain the relationship in the same transaction as removing the request.
            command.CommandText = "INSERT INTO friends VALUES($owner,$peer,$time); INSERT INTO friends VALUES($peer,$owner,$time);";
            command.ExecuteNonQuery();
        }
        command.CommandText = "DELETE FROM friend_requests WHERE sender=$peer AND recipient=$owner";
        command.ExecuteNonQuery(); return new { accepted = accept };
    });

    static SocialMessage ReadMessage(SqliteDataReader reader) => new(reader.GetInt64(0), reader.GetInt64(1).ToString(CultureInfo.InvariantCulture),
        reader.GetString(2), reader.GetString(3), reader.GetString(4));

    internal SocialMessage[] Messages(long owner, long peer, long after) => Use(false, command =>
    {
        if (after < 0) throw new SocialFailure("invalid_cursor", 400);
        Pair(command, owner, peer); RequireFriends(command); command.Parameters.AddWithValue("$after", after);
        command.CommandText = "SELECT id,sender,client_id,body,sent_at FROM friend_messages WHERE " +
            "((sender=$owner AND recipient=$peer) OR (sender=$peer AND recipient=$owner)) AND id>$after ORDER BY id " +
            (after == 0 ? "DESC" : "ASC") + " LIMIT 100";
        using var reader = command.ExecuteReader(); var rows = new List<SocialMessage>();
        while (reader.Read()) rows.Add(ReadMessage(reader));
        if (after == 0) rows.Reverse(); return rows.ToArray();
    });

    internal SocialMessage Send(long owner, long peer, string? clientId, string? text) => Use(true, command =>
    {
        if (!Guid.TryParseExact(clientId, "D", out var id) || id == Guid.Empty || string.IsNullOrWhiteSpace(text) ||
            text.Length > 1000 || text.Any(c => char.IsControl(c) && c != '\n' && c != '\t')) throw new SocialFailure("invalid_message", 400);
        text = text.Trim(); Pair(command, owner, peer); RequireFriends(command);
        command.Parameters.AddWithValue("$client", id.ToString("D"));
        command.CommandText = "SELECT id,sender,client_id,body,sent_at,recipient FROM friend_messages WHERE sender=$owner AND client_id=$client";
        using (var reader = command.ExecuteReader())
            if (reader.Read())
            {
                if (reader.GetString(3) != text || reader.GetInt64(5) != peer) throw new SocialFailure("message_id_reused");
                return ReadMessage(reader);
            }
        command.Parameters.AddWithValue("$text", text); command.Parameters.AddWithValue("$time", DateTimeOffset.UtcNow.ToString("O"));
        command.CommandText = "INSERT INTO friend_messages(sender,recipient,client_id,body,sent_at) VALUES($owner,$peer,$client,$text,$time); SELECT last_insert_rowid();";
        long messageId = Convert.ToInt64(command.ExecuteScalar());
        return new(messageId, owner.ToString(CultureInfo.InvariantCulture), id.ToString("D"), text, (string)command.Parameters["$time"].Value!);
    });
}
