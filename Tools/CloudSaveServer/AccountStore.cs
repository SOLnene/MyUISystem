using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;

namespace CloudSaveServer;

internal sealed class AccountStore(ServerSettings settings, TimeProvider clock)
{
    readonly PasswordHasher<string> hasher = new();
    readonly string dummyHash = new PasswordHasher<string>().HashPassword("", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));

    SqliteConnection Open()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = settings.DatabasePath, ForeignKeys = true, DefaultTimeout = 5 }.ToString());
        connection.Open();
        return connection;
    }

    internal void Initialize()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        // Additive tables leave legacy saves and their receipt history untouched.
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS account_sequence (id INTEGER PRIMARY KEY CHECK(id=1), next_uid INTEGER NOT NULL);
            INSERT OR IGNORE INTO account_sequence VALUES (1,100000001);
            CREATE TABLE IF NOT EXISTS accounts (
                uid INTEGER PRIMARY KEY CHECK(uid>=100000001), username TEXT NOT NULL,
                normalized_username TEXT NOT NULL UNIQUE, password_hash TEXT NOT NULL, created_at_utc TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS account_sessions (
                session_id TEXT PRIMARY KEY, uid INTEGER NOT NULL REFERENCES accounts(uid),
                access_hash TEXT NOT NULL UNIQUE, refresh_hash TEXT NOT NULL UNIQUE,
                access_expires INTEGER NOT NULL, refresh_expires INTEGER NOT NULL);
            """;
        command.ExecuteNonQuery();
    }

    internal static string? ValidateCredentials(AccountCredentials request, bool register)
    {
        if (request.Username == null || !Regex.IsMatch(request.Username.Trim(), @"\A[a-zA-Z0-9_.@-]{3,20}\z"))
            return "invalid_username";
        if (request.Password == null || request.Password.Length < (register ? 8 : 1) || request.Password.Length > 64)
            return "invalid_password";
        return null;
    }

    internal AccountIdentity? Register(AccountCredentials request)
    {
        string username = request.Username!.Trim();
        string normalized = username.ToUpperInvariant();
        string hash = hasher.HashPassword(normalized, request.Password!);
        using var connection = Open();
        using var transaction = connection.BeginTransaction(deferred: false);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT 1 FROM accounts WHERE normalized_username=$name";
        command.Parameters.AddWithValue("$name", normalized);
        if (command.ExecuteScalar() != null) return null;
        // UID allocation and account creation commit together; concurrent registration cannot reuse a UID.
        command.CommandText = "SELECT next_uid FROM account_sequence WHERE id=1";
        long uid = Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        if (uid == long.MaxValue) throw new InvalidOperationException("UID capacity exhausted.");
        command.Parameters.AddWithValue("$next", uid + 1);
        command.CommandText = "UPDATE account_sequence SET next_uid=$next WHERE id=1";
        command.ExecuteNonQuery();
        command.Parameters.AddWithValue("$uid", uid);
        command.Parameters.AddWithValue("$username", username);
        command.Parameters.AddWithValue("$hash", hash);
        command.Parameters.AddWithValue("$created", clock.GetUtcNow().ToString("O"));
        command.CommandText = "INSERT INTO accounts VALUES($uid,$username,$name,$hash,$created)";
        command.ExecuteNonQuery();
        transaction.Commit();
        return new AccountIdentity(uid.ToString(CultureInfo.InvariantCulture), username);
    }

    internal AccountLoginReply? Login(AccountCredentials request)
    {
        string normalized = request.Username!.Trim().ToUpperInvariant();
        long uid = 0;
        string username = "", hash = dummyHash;
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT uid,username,password_hash FROM accounts WHERE normalized_username=$name";
        command.Parameters.AddWithValue("$name", normalized);
        using (var reader = command.ExecuteReader())
            if (reader.Read()) { uid = reader.GetInt64(0); username = reader.GetString(1); hash = reader.GetString(2); }
        var verification = hasher.VerifyHashedPassword(normalized, hash, request.Password!);
        if (uid == 0 || verification == PasswordVerificationResult.Failed) return null;
        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            command.CommandText = "UPDATE accounts SET password_hash=$hash WHERE uid=$uid";
            command.Parameters.AddWithValue("$hash", hasher.HashPassword(normalized, request.Password!));
            command.Parameters.AddWithValue("$uid", uid);
            command.ExecuteNonQuery();
        }
        string access = NewToken("as_"), refresh = NewToken("rs_");
        var now = clock.GetUtcNow();
        command.Parameters.Clear();
        command.Parameters.AddWithValue("$session", Guid.NewGuid().ToString("N"));
        command.Parameters.AddWithValue("$uid", uid);
        command.Parameters.AddWithValue("$access", HashToken(access));
        command.Parameters.AddWithValue("$refresh", HashToken(refresh));
        command.Parameters.AddWithValue("$accessExpiry", now.AddMinutes(30).ToUnixTimeSeconds());
        command.Parameters.AddWithValue("$refreshExpiry", now.AddDays(30).ToUnixTimeSeconds());
        command.CommandText = "INSERT INTO account_sessions VALUES($session,$uid,$access,$refresh,$accessExpiry,$refreshExpiry)";
        command.ExecuteNonQuery();
        return Reply(uid, username, access, refresh, now.AddMinutes(30), now.AddDays(30));
    }

    internal AccountLoginReply? Refresh(string? refreshToken)
    {
        if (!IsToken(refreshToken, "rs_")) return null;
        using var connection = Open();
        using var transaction = connection.BeginTransaction(deferred: false);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        var now = clock.GetUtcNow();
        command.Parameters.AddWithValue("$refresh", HashToken(refreshToken!));
        command.Parameters.AddWithValue("$now", now.ToUnixTimeSeconds());
        command.CommandText = """
            SELECT a.uid,a.username,s.refresh_expires FROM account_sessions s JOIN accounts a ON a.uid=s.uid
            WHERE s.refresh_hash=$refresh AND s.refresh_expires>$now
            """;
        long uid, refreshExpiry;
        string username;
        using (var reader = command.ExecuteReader())
        {
            if (!reader.Read()) return null;
            uid = reader.GetInt64(0); username = reader.GetString(1); refreshExpiry = reader.GetInt64(2);
        }
        string access = NewToken("as_");
        var accessExpiry = DateTimeOffset.FromUnixTimeSeconds(Math.Min(now.AddMinutes(30).ToUnixTimeSeconds(), refreshExpiry));
        command.Parameters.AddWithValue("$access", HashToken(access));
        command.Parameters.AddWithValue("$expiry", accessExpiry.ToUnixTimeSeconds());
        // Keep the refresh credential until logout/expiry so a lost refresh response remains recoverable.
        command.CommandText = "UPDATE account_sessions SET access_hash=$access,access_expires=$expiry WHERE refresh_hash=$refresh";
        command.ExecuteNonQuery();
        transaction.Commit();
        return Reply(uid, username, access, refreshToken!, accessExpiry, DateTimeOffset.FromUnixTimeSeconds(refreshExpiry));
    }

    internal AccountIdentity? ValidateAccess(string? accessToken)
    {
        if (!IsToken(accessToken, "as_")) return null;
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.Parameters.AddWithValue("$access", HashToken(accessToken!));
        command.Parameters.AddWithValue("$now", clock.GetUtcNow().ToUnixTimeSeconds());
        command.CommandText = """
            SELECT a.uid,a.username FROM account_sessions s JOIN accounts a ON a.uid=s.uid
            WHERE s.access_hash=$access AND s.access_expires>$now AND s.refresh_expires>$now
            """;
        using var reader = command.ExecuteReader();
        return reader.Read() ? new AccountIdentity(reader.GetInt64(0).ToString(CultureInfo.InvariantCulture), reader.GetString(1)) : null;
    }

    internal void Logout(string? refreshToken)
    {
        if (!IsToken(refreshToken, "rs_")) return;
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM account_sessions WHERE refresh_hash=$refresh";
        command.Parameters.AddWithValue("$refresh", HashToken(refreshToken!));
        command.ExecuteNonQuery();
    }

    static AccountLoginReply Reply(long uid, string username, string access, string refresh, DateTimeOffset expiry, DateTimeOffset refreshExpiry) =>
        new(uid.ToString(CultureInfo.InvariantCulture), username, access, refresh, expiry.ToUnixTimeSeconds(), refreshExpiry.ToUnixTimeSeconds());
    static string NewToken(string prefix) => prefix + Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
    internal static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    static bool IsToken(string? token, string prefix) => token != null && token.Length == 67 && token.StartsWith(prefix, StringComparison.Ordinal) &&
        token.AsSpan(3).IndexOfAnyExcept("0123456789abcdef") < 0;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record AccountCredentials(string? Username, string? Password);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record AccountRefreshRequest(string? RefreshToken);
internal sealed record AccountIdentity(string Uid, string Username);
internal sealed record AccountLoginReply(string Uid, string Username, string AccessToken, string RefreshToken,
    long AccessExpiresAt, long RefreshExpiresAt);
