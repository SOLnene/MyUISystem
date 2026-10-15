using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace CloudSaveServer;

internal sealed class SaveStore(ServerSettings settings)
{
    SqliteConnection Open()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = settings.DatabasePath, Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true, DefaultTimeout = 5
        }.ToString());
        connection.Open();
        return connection;
    }

    internal void Initialize()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(settings.DatabasePath)!);
        using var connection = Open();
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=FULL;";
        pragma.ExecuteNonQuery();
        pragma.CommandText = "PRAGMA user_version;";
        if (Convert.ToInt32(pragma.ExecuteScalar()) is not (0 or 1))
            throw new InvalidOperationException("Unsupported save database schema.");
        using var transaction = connection.BeginTransaction(deferred: false);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS server_scope (
                id INTEGER PRIMARY KEY CHECK (id = 1), project_id TEXT NOT NULL, environment_id TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS saves (
                player_id TEXT PRIMARY KEY, save_json TEXT NOT NULL,
                revision INTEGER NOT NULL CHECK (revision > 0), updated_at_utc TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS write_receipts (
                player_id TEXT NOT NULL REFERENCES saves(player_id), request_id TEXT NOT NULL,
                request_hash TEXT NOT NULL, revision INTEGER NOT NULL, updated_at_utc TEXT NOT NULL,
                PRIMARY KEY (player_id, request_id));
            INSERT OR IGNORE INTO server_scope VALUES (1, $project, $environment);
            PRAGMA user_version=1;
            """;
        command.Parameters.AddWithValue("$project", settings.ProjectId);
        command.Parameters.AddWithValue("$environment", settings.EnvironmentId);
        command.ExecuteNonQuery();
        command.CommandText = "SELECT project_id, environment_id FROM server_scope WHERE id=1;";
        using (var reader = command.ExecuteReader())
        {
            if (!reader.Read() || reader.GetString(0) != settings.ProjectId || reader.GetString(1) != settings.EnvironmentId)
                throw new InvalidOperationException("This database belongs to a different Unity project/environment.");
        }
        transaction.Commit();
    }

    internal SaveSnapshot? Load(string playerId)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT save_json, revision, updated_at_utc FROM saves WHERE player_id=$player;";
        command.Parameters.AddWithValue("$player", playerId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? new SaveSnapshot(reader.GetString(0), reader.GetInt64(1), reader.GetString(2)) : null;
    }

    internal StoreWriteResult Write(string playerId, SaveWriteRequest request)
    {
        using var connection = Open();
        // BEGIN IMMEDIATE serializes the absence check and first insert as well as later updates.
        using var transaction = connection.BeginTransaction(deferred: false);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.Parameters.AddWithValue("$player", playerId);
        command.Parameters.AddWithValue("$request", request.RequestId!.Value.ToString("D"));
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            request.ExpectedRevision!.Value.ToString(CultureInfo.InvariantCulture) + "\n" + request.SaveJson)));
        command.CommandText = """
            SELECT request_hash, revision, updated_at_utc FROM write_receipts
            WHERE player_id=$player AND request_id=$request;
            """;
        using (var reader = command.ExecuteReader())
        {
            // Check retries before revision comparison, including after another device has written.
            if (reader.Read())
                return reader.GetString(0) == hash
                    ? new(new WriteReceipt(reader.GetInt64(1), reader.GetString(2), true), null, null)
                    : new(null, "request_id_reused", null);
        }
        command.CommandText = "SELECT revision FROM saves WHERE player_id=$player;";
        object? value = command.ExecuteScalar();
        long current = value == null ? 0 : Convert.ToInt64(value);
        if (current != request.ExpectedRevision)
            return new(null, "save_conflict", current);
        long next = checked(current + 1);
        string timestamp = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        command.Parameters.AddWithValue("$json", request.SaveJson!);
        command.Parameters.AddWithValue("$next", next);
        command.Parameters.AddWithValue("$time", timestamp);
        command.Parameters.AddWithValue("$hash", hash);
        command.Parameters.AddWithValue("$oldestRevision", next - 256);
        command.Parameters.AddWithValue("$oldestTime", DateTimeOffset.UtcNow.AddDays(-7).ToString("O", CultureInfo.InvariantCulture));
        command.CommandText = """
            INSERT INTO saves (player_id, save_json, revision, updated_at_utc) VALUES ($player, $json, $next, $time)
            ON CONFLICT(player_id) DO UPDATE SET save_json=$json, revision=$next, updated_at_utc=$time;
            INSERT INTO write_receipts VALUES ($player, $request, $hash, $next, $time);
            DELETE FROM write_receipts WHERE player_id=$player AND revision<$next
                AND (revision<=$oldestRevision OR updated_at_utc<$oldestTime);
            """;
        command.ExecuteNonQuery();
        // The save and its retry receipt either both commit or neither does.
        transaction.Commit();
        return new(new WriteReceipt(next, timestamp, false), null, null);
    }

    internal void Backup(string destinationPath)
    {
        if (!File.Exists(settings.DatabasePath))
            throw new FileNotFoundException("The save database does not exist.");
        if (File.Exists(destinationPath))
            throw new IOException("Backup destination already exists; choose a new filename.");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destinationPath))!);
        using var source = Open();
        using var destination = new SqliteConnection(new SqliteConnectionStringBuilder
            { DataSource = destinationPath, Pooling = false }.ToString());
        destination.Open();
        // SQLite's backup API includes committed WAL pages; copying only the live .db file would not.
        source.BackupDatabase(destination);
    }
}
