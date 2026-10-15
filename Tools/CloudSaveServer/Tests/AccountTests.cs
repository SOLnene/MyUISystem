using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CloudSaveServer;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

internal static partial class Program
{
    sealed class AccountTestClock : TimeProvider
    {
        internal DateTimeOffset Now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    static async Task ValidateAccounts(string root)
    {
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "accounts.db");
        string configRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        var clock = new AccountTestClock();
        AccountIdentity a, b;
        AccountLoginReply session;
        await using (var server = await RunningServer.Start(path, gachaRoot: configRoot, legacy: false, clock: clock))
        {
            var store = server.App.Services.GetRequiredService<AccountStore>();
            Check(AccountStore.ValidateCredentials(new("abc", "abcdefgh"), true) == null,
                "Registration must accept lowercase-only passwords.");
            Check(AccountStore.ValidateCredentials(new("abc", new string('x', 64)), true) == null,
                "Registration must accept 64 characters.");
            foreach (string password in new[] { "short", new string('x', 65) })
                Check(AccountStore.ValidateCredentials(new("abc", password), true) == "invalid_password", "Password length must be bounded.");
            Check(AccountStore.ValidateCredentials(new("a", "abcdefgh"), true) == "invalid_username", "Username rules must be enforced.");
            using var signup = await server.Client.PostAsJsonAsync("/v1/auth/register", new AccountCredentials("testuser", "abcdefgh"));
            a = (await signup.Content.ReadFromJsonAsync<AccountIdentity>())!;
            Check(signup.StatusCode == HttpStatusCode.Created && a.Uid == "100000001", "First UID must be assigned by the server.");
            using var duplicate = await server.Client.PostAsJsonAsync("/v1/auth/register", new AccountCredentials("TESTUSER", "abcdefgh"));
            Check(duplicate.StatusCode == HttpStatusCode.Conflict, "Usernames must be unique ignoring ASCII case.");
            b = store.Register(new("testuser-b", "abcdefgh"))!;
            Check(b.Uid == "100000002", "Duplicate registration cannot consume or reuse an identity.");
            using (var connection = new SqliteConnection("Data Source=" + path))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT password_hash FROM accounts ORDER BY uid";
                using var reader = command.ExecuteReader();
                reader.Read(); string first = reader.GetString(0); reader.Read(); string second = reader.GetString(0);
                Check(first != "abcdefgh" && second != "abcdefgh" && first != second, "Passwords must use independent salted hashes.");
            }
            using var wrong = await server.Client.PostAsJsonAsync("/v1/auth/login", new AccountCredentials("testuser", "wrongpass"));
            using var unknown = await server.Client.PostAsJsonAsync("/v1/auth/login", new AccountCredentials("missing", "wrongpass"));
            Check(wrong.StatusCode == HttpStatusCode.Unauthorized && unknown.StatusCode == HttpStatusCode.Unauthorized,
                "Unknown user and incorrect password must return the same failure.");
            using var login = await server.Client.PostAsJsonAsync("/v1/auth/login", new AccountCredentials("TESTUSER", "abcdefgh"));
            session = (await login.Content.ReadFromJsonAsync<AccountLoginReply>())!;
            Check(login.IsSuccessStatusCode && session.Uid == a.Uid && session.Username == "testuser", "Login must restore the original UID and name.");
            using var noToken = await Get(server.Client, "not-a-token");
            Check(noToken.StatusCode == HttpStatusCode.Unauthorized, "Opaque account tokens must be validated.");
            using var missing = await Get(server.Client, session.AccessToken);
            Check(missing.StatusCode == HttpStatusCode.NotFound, "New local accounts cannot inherit old saves implicitly.");
            await Receipt(await Put(server.Client, session.AccessToken, 0, Guid.NewGuid(), FirstSave), 1);
            var other = store.Login(new("testuser-b", "abcdefgh"))!;
            using var otherSave = await Get(server.Client, other.AccessToken);
            Check(otherSave.StatusCode == HttpStatusCode.NotFound, "Local accounts must have isolated saves.");
            using var malicious = await server.Client.PostAsJsonAsync("/v1/auth/login", new { username = "testuser", password = "abcdefgh", uid = b.Uid });
            Check(malicious.StatusCode == HttpStatusCode.BadRequest, "Clients cannot submit their own UID.");
            using var gacha = new HttpRequestMessage(HttpMethod.Get, "/v1/gacha/config");
            gacha.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
            using var gachaResponse = await server.Client.SendAsync(gacha);
            Check(gachaResponse.IsSuccessStatusCode, "The same account session must authorize gacha configuration.");
            using (var connection = new SqliteConnection("Data Source=" + path))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT access_hash,refresh_hash FROM account_sessions WHERE uid=100000001";
                using var reader = command.ExecuteReader(); reader.Read();
                Check(reader.GetString(0) != session.AccessToken && reader.GetString(1) != session.RefreshToken,
                    "Session secrets must not be stored as plaintext in the server database.");
            }
            clock.Now = clock.Now.AddMinutes(31);
            using var expired = await Get(server.Client, session.AccessToken);
            Check(expired.StatusCode == HttpStatusCode.Unauthorized, "Expired access must fail even before client refresh.");
            using var refresh = await server.Client.PostAsJsonAsync("/v1/auth/refresh", new AccountRefreshRequest(session.RefreshToken));
            var refreshed = (await refresh.Content.ReadFromJsonAsync<AccountLoginReply>())!;
            Check(refresh.IsSuccessStatusCode && refreshed.Uid == a.Uid && refreshed.AccessToken != session.AccessToken,
                "Refresh must renew access without changing UID.");
            using var retry = await server.Client.PostAsJsonAsync("/v1/auth/refresh", new AccountRefreshRequest(session.RefreshToken));
            session = (await retry.Content.ReadFromJsonAsync<AccountLoginReply>())!;
            Check(retry.IsSuccessStatusCode, "A lost refresh reply must not permanently lose the remembered credential.");
            var races = await Task.WhenAll(Enumerable.Range(0, 8).Select(i => Task.Run(() => store.Register(new("raceuser" + i, "abcdefgh")))));
            Check(races.All(x => x != null) && races.Select(x => x!.Uid).Distinct().Count() == races.Length,
                "Concurrent registrations must receive distinct UIDs.");
            var duplicates = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(() => store.Register(new("sameuser", "abcdefgh")))));
            Check(duplicates.Count(x => x != null) == 1, "A registration race must create one account for a username.");
            var saveStore = server.App.Services.GetRequiredService<SaveStore>();
            saveStore.Write("legacy-unity-player", new SaveWriteRequest(0, Guid.NewGuid(), FirstSave));
            Check(saveStore.Load("local:" + a.Uid)?.SaveJson == FirstSave && saveStore.Load("legacy-unity-player")?.SaveJson == FirstSave,
                "Local account ownership and retained legacy ownership must stay separate.");
        }
        await using (var restarted = await RunningServer.Start(path, gachaRoot: configRoot, legacy: false, clock: clock))
        {
            using var loaded = await Get(restarted.Client, session.AccessToken);
            var save = await loaded.Content.ReadFromJsonAsync<SaveSnapshot>();
            Check(loaded.IsSuccessStatusCode && save?.SaveJson == FirstSave, "Account session and save must survive server restart.");
            var store = restarted.App.Services.GetRequiredService<AccountStore>();
            Check(store.Login(new("testuser", "abcdefgh"))?.Uid == a.Uid, "Re-login after restart must return the same UID.");
            using var logout = await restarted.Client.PostAsJsonAsync("/v1/auth/logout", new AccountRefreshRequest(session.RefreshToken));
            Check(logout.StatusCode == HttpStatusCode.NoContent, "Logout must revoke the selected server session.");
            using var revokedAccess = await Get(restarted.Client, session.AccessToken);
            using var revokedRefresh = await restarted.Client.PostAsJsonAsync("/v1/auth/refresh", new AccountRefreshRequest(session.RefreshToken));
            Check(revokedAccess.StatusCode == HttpStatusCode.Unauthorized && revokedRefresh.StatusCode == HttpStatusCode.Unauthorized,
                "Logout must revoke access and refresh together.");
            Check(store.Login(new("testuser-b", "abcdefgh"))?.Uid == b.Uid, "Logout cannot affect another account.");
            var expiring = store.Login(new("testuser", "abcdefgh"))!;
            clock.Now = clock.Now.AddDays(31);
            Check(store.Refresh(expiring.RefreshToken) == null, "Remembered credentials must expire.");
            Check(restarted.App.Services.GetRequiredService<SaveStore>().Load("legacy-unity-player")?.SaveJson == FirstSave,
                "Legacy save bytes must survive account initialization and restart.");
            for (int i = 0; i < 20; i++)
            {
                using var response = await restarted.Client.PostAsJsonAsync("/v1/auth/login", new AccountCredentials("x", "x"));
                if (i == 19) Check((int)response.StatusCode == 429, "Account endpoints must rate-limit abuse.");
            }
        }
        Console.WriteLine("PASS: local account registration, salted passwords, UID allocation, session refresh/revocation, isolation and restart.");
    }
}
