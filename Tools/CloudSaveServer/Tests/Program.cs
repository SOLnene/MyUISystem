using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CloudSaveServer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

internal static partial class Program
{
    const string Project = "56bad169-df5f-443f-b41b-394a4c0d5f6d";
    const string EnvironmentId = "31ff77d2-4511-4d76-9b95-f94d65c87a06";
    const string FirstSave = "{\"version\":7,\"currencies\":{\"items\":[]},\"test\":\"A 中文\"}";
    static int passed;

    static async Task<int> Main(string[] args)
    {
        try
        {
            // Setup failures must be reported like test failures instead of crashing the runner.
            string artifactRoot = Path.GetFullPath(args.FirstOrDefault(a => a != "--live-auth") ??
                Path.Combine(AppContext.BaseDirectory, "validation"));
            Directory.CreateDirectory(artifactRoot);
            string runRoot = Path.Combine(artifactRoot, "run-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(runRoot);
            await ValidateLocal(Path.Combine(runRoot, "test.db"));
            await ValidateGacha(Path.Combine(runRoot, "gacha"));
            await ValidateCharacterEvents(Path.Combine(runRoot, "character-events"));
            await ValidateAccounts(Path.Combine(runRoot, "accounts"));
            await ValidateDraws(Path.Combine(runRoot, "draws"));
            if (args.Contains("--live-auth")) await ValidateUnity(Path.Combine(runRoot, "live.db"));
            Console.WriteLine($"PASS: {passed} checks. Test databases: {runRoot}");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL: " + exception.Message);
            return 1;
        }
        finally { SqliteConnection.ClearAllPools(); }
    }

    static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        passed++;
    }

    static string Token(SecurityKey key, string player, string project = Project,
        string environment = EnvironmentId, string issuer = UnityTokenAuthentication.Issuer,
        bool expired = false, bool signed = true)
    {
        var claims = new[] { new Claim("sub", player), new Claim("project_id", project),
            new Claim("aud", "upid:" + Project), new Claim("aud", "envId:" + environment) };
        var jwt = new JwtSecurityToken(issuer, claims: claims,
            notBefore: DateTime.UtcNow.AddHours(-2), expires: expired ? DateTime.UtcNow.AddMinutes(-5) : DateTime.UtcNow.AddHours(1),
            signingCredentials: signed ? new SigningCredentials(key, SecurityAlgorithms.RsaSha256) : null);
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    static async Task<HttpResponseMessage> Put(HttpClient client, string token, long revision, Guid id, string json)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, "/v1/save");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new SaveWriteRequest(revision, id, json));
        return await client.SendAsync(request);
    }

    static async Task<HttpResponseMessage> Get(HttpClient client, string? token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/save");
        if (token != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }

    static async Task<WriteReceipt> Receipt(HttpResponseMessage response, long revision)
    {
        using (response)
        {
            Check(response.StatusCode == HttpStatusCode.OK, "Expected a successful write.");
            var receipt = (await response.Content.ReadFromJsonAsync<WriteReceipt>())!;
            Check(receipt.Revision == revision, "Unexpected write revision.");
            return receipt;
        }
    }

    static async Task ValidateLocal(string path)
    {
        using var rsa = RSA.Create(2048);
        var key = new RsaSecurityKey(rsa) { KeyId = "test-only-key" };
        var configuration = new OpenIdConnectConfiguration { Issuer = UnityTokenAuthentication.Issuer };
        configuration.SigningKeys.Add(key);
        var manager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
        string a = Token(key, "player-a"), b = Token(key, "player-b");
        await using (var server = await RunningServer.Start(path, manager))
        {
            using var noAuth = await Get(server.Client, null);
            Check(noAuth.StatusCode == HttpStatusCode.Unauthorized, "Missing token must fail.");
            using var bad = await Get(server.Client, "invalid-token");
            Check(bad.StatusCode == HttpStatusCode.Unauthorized, "Malformed token must fail.");
            foreach (var token in new[] { Token(key, "player-a", project: Guid.NewGuid().ToString()),
                Token(key, "player-a", environment: Guid.NewGuid().ToString()),
                Token(key, "player-a", issuer: "https://attacker.invalid"),
                Token(key, "player-a", expired: true), Token(key, "player-a", signed: false), Token(key, "") })
            {
                using var invalid = await Get(server.Client, token);
                Check(invalid.StatusCode == HttpStatusCode.Unauthorized, "Invalid claims/signature must fail.");
            }
            // Alter a signed player's ID without recomputing the signature.
            string[] segments = a.Split('.');
            var payload = Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(segments[1])).Replace("player-a", "player-b");
            segments[1] = Base64UrlEncoder.Encode(payload);
            using var forged = await Get(server.Client, string.Join('.', segments));
            Check(forged.StatusCode == HttpStatusCode.Unauthorized, "Forged player ID must fail.");
            using var missing = await Get(server.Client, a);
            Check(missing.StatusCode == HttpStatusCode.NotFound, "A new account must report absence.");
            var id = Guid.NewGuid();
            var first = await Receipt(await Put(server.Client, a, 0, id, FirstSave), 1);
            Check(!first.Replayed, "First write cannot be a replay.");
            var replay = await Receipt(await Put(server.Client, a, 0, id, FirstSave), 1);
            Check(replay.Replayed && replay.UpdatedAtUtc == first.UpdatedAtUtc, "Retry must return original receipt.");
            using var reused = await Put(server.Client, a, 1, id, FirstSave);
            Check(reused.StatusCode == HttpStatusCode.Conflict, "A reused ID with different content must fail.");
            using var isolation = await Get(server.Client, b);
            Check(isolation.StatusCode == HttpStatusCode.NotFound, "Player B cannot see player A's save.");
            await Receipt(await Put(server.Client, b, 0, id, "{\"version\":7,\"test\":\"B\"}"), 1);
            var updateId = Guid.NewGuid();
            await Receipt(await Put(server.Client, a, 1, updateId, FirstSave), 2);
            using var conflict = await Put(server.Client, a, 1, Guid.NewGuid(), FirstSave);
            Check(conflict.StatusCode == HttpStatusCode.Conflict, "Stale revisions must fail.");
            var oldReplay = await Receipt(await Put(server.Client, a, 0, id, FirstSave), 1);
            Check(oldReplay.Replayed, "An earlier acknowledged request remains replayable after later writes.");
            foreach (string json in new[] { "not-json", "[]", "{\"version\":0}", "{\"version\":\"7\"}", "{}", "{\"version\":7,\"x\":\"" + new string('x', 5000) + "\"}" })
            {
                using var invalid = await Put(server.Client, a, 2, Guid.NewGuid(), json);
                Check(invalid.StatusCode == HttpStatusCode.BadRequest, "Invalid/oversized save must fail.");
            }
            using var unknownRequest = new HttpRequestMessage(HttpMethod.Put, "/v1/save");
            unknownRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", a);
            unknownRequest.Content = JsonContent.Create(new { expectedRevision = 2, requestId = Guid.NewGuid(), saveJson = FirstSave, playerId = "player-b" });
            using var unknown = await server.Client.SendAsync(unknownRequest);
            Check(unknown.StatusCode == HttpStatusCode.BadRequest, "Client-supplied ownership fields must be rejected.");
            using var missingFieldsRequest = new HttpRequestMessage(HttpMethod.Put, "/v1/save");
            missingFieldsRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", a);
            missingFieldsRequest.Content = JsonContent.Create(new { saveJson = FirstSave });
            using var missingFields = await server.Client.SendAsync(missingFieldsRequest);
            Check(missingFields.StatusCode == HttpStatusCode.BadRequest, "Missing revision/request ID must fail.");
            await CheckRace(server.Client, Token(key, "race-create"), 0);
            await CheckRace(server.Client, a, 2);
            string limitedPlayer = Token(key, "rate-limit");
            for (int i = 0; i < 120; i++)
            {
                using var response = await Get(server.Client, limitedPlayer);
                Check(response.StatusCode == HttpStatusCode.NotFound, "Requests below the rate limit must be served.");
            }
            using var limited = await Get(server.Client, limitedPlayer);
            Check((int)limited.StatusCode == 429 && limited.Headers.RetryAfter != null,
                "Rate limiting must include retry guidance.");
            Console.WriteLine("PASS: signature/scope validation, isolation, retry receipts, input checks and concurrent writes.");
        }
        await using (var unavailable = await RunningServer.Start(Path.ChangeExtension(path, ".unavailable.db"), new UnavailableKeys()))
        {
            using var response = await Get(unavailable.Client, a);
            var error = await response.Content.ReadFromJsonAsync<ApiError>();
            Check((int)response.StatusCode == 503 && error?.Code == "auth_unavailable",
                "A signing-key outage must be retryable, distinct from an invalid token.");
        }
        await using (var restarted = await RunningServer.Start(path, manager))
        {
            using var response = await Get(restarted.Client, a);
            var save = await response.Content.ReadFromJsonAsync<SaveSnapshot>();
            Check(response.StatusCode == HttpStatusCode.OK && save?.Revision == 3 && save.SaveJson == FirstSave,
                "Restart must preserve the committed save.");
            var store = restarted.App.Services.GetRequiredService<SaveStore>();
            string backup = Path.ChangeExtension(path, ".backup.db");
            store.Backup(backup);
            var backupSettings = Settings(backup);
            var restored = new SaveStore(backupSettings);
            restored.Initialize();
            Check(restored.Load("player-a")?.Revision == 3, "Live WAL backup must restore committed saves.");
            var wrongScope = Settings(backup);
            wrongScope.EnvironmentId = Guid.NewGuid().ToString();
            bool rejected = false;
            try { new SaveStore(wrongScope).Initialize(); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "A database cannot be shared across project/environment configurations.");
            // Exercise history pruning without spending the HTTP rate-limit budget.
            for (long revision = 0; revision < 260; revision++)
                Check(store.Write("retention", new SaveWriteRequest(revision, Guid.NewGuid(), FirstSave)).Receipt?.Revision == revision + 1,
                    "Receipt pruning must not affect writes.");
            using var connection = new SqliteConnection("Data Source=" + path);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM write_receipts WHERE player_id='retention';";
            Check(Convert.ToInt64(command.ExecuteScalar()) == 256, "Receipt history must stay bounded.");
            command.CommandText = "PRAGMA integrity_check;";
            Check((string?)command.ExecuteScalar() == "ok", "Database integrity check failed.");
            Console.WriteLine("PASS: restart persistence, live backup, database scope and bounded receipt history.");
        }
    }

    static async Task CheckRace(HttpClient client, string token, long revision)
    {
        var responses = await Task.WhenAll(Put(client, token, revision, Guid.NewGuid(), FirstSave),
            Put(client, token, revision, Guid.NewGuid(), FirstSave));
        try
        {
            Check(responses.Count(r => r.StatusCode == HttpStatusCode.OK) == 1 &&
                responses.Count(r => r.StatusCode == HttpStatusCode.Conflict) == 1,
                "Concurrent creation/update must have exactly one winner.");
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    static async Task ValidateUnity(string path)
    {
        using var auth = new HttpClient { BaseAddress = new Uri(UnityTokenAuthentication.Issuer), Timeout = TimeSpan.FromSeconds(30) };
        auth.DefaultRequestHeaders.Add("ProjectId", Project);
        var players = new List<(string Id, string Token)>();
        try
        {
            for (int i = 0; i < 2; i++)
            {
                string username = "csv" + Guid.NewGuid().ToString("N")[..16];
                string password = "Aa1!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
                using var signup = await auth.PostAsJsonAsync("/v1/authentication/usernamepassword/sign-up", new { username, password });
                Check(signup.IsSuccessStatusCode, "Unity test signup failed (credentials not logged).");
                using var json = JsonDocument.Parse(await signup.Content.ReadAsStringAsync());
                string player = json.RootElement.GetProperty("userId").GetString()!;
                string token = json.RootElement.GetProperty("idToken").GetString()!;
                players.Add((player, token));
                using var signin = await auth.PostAsJsonAsync("/v1/authentication/usernamepassword/sign-in", new { username, password });
                Check(signin.IsSuccessStatusCode, "Unity test signin failed.");
                using var login = JsonDocument.Parse(await signin.Content.ReadAsStringAsync());
                Check(login.RootElement.GetProperty("userId").GetString() == player, "Unity signin returned another player.");
                players[i] = (player, login.RootElement.GetProperty("idToken").GetString()!);
            }
            await using (var server = await RunningServer.Start(path))
            {
                using var absent = await Get(server.Client, players[0].Token);
                Check(absent.StatusCode == HttpStatusCode.NotFound, "A real Unity token must validate and report no save.");
                var id = Guid.NewGuid();
                await Receipt(await Put(server.Client, players[0].Token, 0, id, FirstSave), 1);
                var replay = await Receipt(await Put(server.Client, players[0].Token, 0, id, FirstSave), 1);
                Check(replay.Replayed, "Real-token retry must be idempotent.");
                using var other = await Get(server.Client, players[1].Token);
                Check(other.StatusCode == HttpStatusCode.NotFound, "Real Unity accounts must be isolated.");
            }
            await using (var restarted = await RunningServer.Start(path))
            {
                using var loaded = await Get(restarted.Client, players[0].Token);
                var save = await loaded.Content.ReadFromJsonAsync<SaveSnapshot>();
                Check(loaded.StatusCode == HttpStatusCode.OK && save?.SaveJson == FirstSave && save.Revision == 1,
                    "Real-token save must survive server restart.");
            }
            Console.WriteLine("PASS: real Unity signup/signin, official JWKS verification, isolated saves and restart.");
        }
        finally
        {
            bool cleanupFailed = false;
            foreach (var player in players)
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Delete, "/v1/users/" + player.Id);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", player.Token);
                    using var response = await auth.SendAsync(request);
                    if (!response.IsSuccessStatusCode) cleanupFailed = true;
                }
                catch (HttpRequestException) { cleanupFailed = true; }
            }
            if (cleanupFailed) throw new InvalidOperationException("Temporary Unity player cleanup failed; investigate before rerunning.");
            Console.WriteLine("Temporary Unity test players deleted; no passwords or tokens were saved.");
        }
    }

    static ServerSettings Settings(string path) => new()
        { ProjectId = Project, EnvironmentId = EnvironmentId, DatabasePath = path, MaxSaveBytes = 4096 };

    sealed class UnavailableKeys : IConfigurationManager<OpenIdConnectConfiguration>
    {
        public Task<OpenIdConnectConfiguration> GetConfigurationAsync(CancellationToken cancellationToken) =>
            Task.FromException<OpenIdConnectConfiguration>(new InvalidOperationException("Configuration fetch failed.",
                new SigningKeysUnavailableException("Test signing-key outage.")));
        public void RequestRefresh() { }
    }

    sealed class RunningServer(WebApplication app, HttpClient client) : IAsyncDisposable
    {
        internal WebApplication App { get; } = app;
        internal HttpClient Client { get; } = client;

        internal static async Task<RunningServer> Start(string path,
            IConfigurationManager<OpenIdConnectConfiguration>? keys = null, string? gachaRoot = null, bool legacy = true,
            TimeProvider? clock = null)
        {
            var app = ServerApp.Build([], builder =>
            {
                // Tests log only their summaries, without touching the Windows event log.
                builder.Logging.ClearProviders();
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Urls"] = "http://127.0.0.1:0", ["SaveServer:ProjectId"] = Project,
                    ["SaveServer:EnvironmentId"] = EnvironmentId, ["SaveServer:DatabasePath"] = path,
                    ["SaveServer:MaxSaveBytes"] = "4096", ["Logging:LogLevel:Default"] = "Critical",
                    ["EnableLegacyUnityAuthentication"] = legacy.ToString(),
                    ["Logging:LogLevel:Microsoft.AspNetCore"] = "Critical"
                });
                if (keys != null) builder.Services.AddSingleton(keys);
                if (clock != null) builder.Services.AddSingleton(clock);
                if (gachaRoot != null)
                    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["GachaConfigRoot"] = gachaRoot });
            });
            await app.StartAsync();
            string url = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            return new RunningServer(app, new HttpClient(new HttpClientHandler { UseProxy = false })
                { BaseAddress = new Uri(url), Timeout = TimeSpan.FromSeconds(30) });
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await App.StopAsync();
            await App.DisposeAsync();
        }
    }
}
