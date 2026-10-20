using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using CloudSaveServer;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

internal static partial class Program
{
    static readonly JsonSerializerOptions GachaJson = new(JsonSerializerDefaults.Web);

    static async Task ValidateGacha(string root)
    {
        Directory.CreateDirectory(root);
        string source = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        foreach (string file in Directory.GetFiles(source, "gacha-*.json"))
            File.Copy(file, Path.Combine(root, Path.GetFileName(file)));
        // Live schedules change independently; boundary and reload tests need fixed seed pools.
        var seedSchedule = new GachaSchedule(1, new[] { "CharPool_Hutao", "CharPool_Eula", "WeaponPool_01" }
            .Select(key => new GachaScheduleSlot(key, true, null, null)).ToList());
        await File.WriteAllTextAsync(Path.Combine(root, "gacha-schedule.json"),
            JsonSerializer.Serialize(seedSchedule, GachaJson));
        var store = new GachaConfigStore(root);
        var now = new DateTimeOffset(2026, 10, 13, 0, 0, 0, TimeSpan.Zero);
        var initial = await store.ReadCurrentAsync(now, CancellationToken.None);
        Check(initial.Version > 0 && initial.Pools.Take(3).Select(p => p.GachaKey).SequenceEqual(
            new[] { "CharPool_Hutao", "CharPool_Eula", "WeaponPool_01" }), "Seed pool order must match the Unity assets.");
        int seedCount = initial.Pools.Count;
        Check(initial.Pools[0].Entries.Single(e => e.EntryKey == "hutao").Weight == 1 &&
            initial.Pools[0].PityCount == 90, "Seed weights and pity must match Unity.");

        string schedulePath = Path.Combine(root, "gacha-schedule.json");
        string originalSchedule = await File.ReadAllTextAsync(schedulePath);
        var schedule = JsonSerializer.Deserialize<GachaSchedule>(originalSchedule, GachaJson)!;
        async Task SetSchedule(GachaSchedule value) =>
            await File.WriteAllTextAsync(schedulePath, JsonSerializer.Serialize(value, GachaJson));
        var timed = schedule with { Pools = schedule.Pools.Select((p, i) => i == 0
            ? p with { StartsAtUtc = now.AddHours(1).ToString("O"), EndsAtUtc = now.AddHours(2).ToString("O") } : p).ToList() };
        await SetSchedule(timed);
        Check((await store.ReadCurrentAsync(now, default)).Pools.Count == seedCount - 1, "Future pools must stay closed.");
        Check((await store.ReadCurrentAsync(now.AddHours(1), default)).Pools.Count == seedCount, "Start time is inclusive.");
        Check((await store.ReadCurrentAsync(now.AddHours(2), default)).Pools.Count == seedCount - 1, "End time is exclusive.");
        await SetSchedule(schedule with { Pools = schedule.Pools.Select(p => p with { Enabled = false }).ToList() });
        Check((await store.ReadCurrentAsync(now, default)).Pools.Count == 0, "No active pool is a valid result.");
        await SetSchedule(schedule);
        using (var cancelled = new CancellationTokenSource())
        {
            cancelled.Cancel();
            bool stopped = false;
            try { await store.ReadCurrentAsync(now, cancelled.Token); } catch (OperationCanceledException) { stopped = true; }
            Check(stopped, "Cancelled configuration reads must stop.");
        }

        using var rsa = RSA.Create(2048);
        var key = new RsaSecurityKey(rsa) { KeyId = "gacha-test-key" };
        var signing = new OpenIdConnectConfiguration { Issuer = UnityTokenAuthentication.Issuer };
        signing.SigningKeys.Add(key);
        await using var server = await RunningServer.Start(Path.Combine(root, "test.db"),
            new StaticConfigurationManager<OpenIdConnectConfiguration>(signing), root);
        string token = Token(key, "gacha-reader");
        async Task<HttpResponseMessage> Fetch(string? credential)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/gacha/config");
            if (credential != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential);
            return await server.Client.SendAsync(request);
        }
        using (var unauthenticated = await Fetch(null))
            Check(unauthenticated.StatusCode == HttpStatusCode.Unauthorized, "Gacha config requires authentication.");
        using (var wrongProject = await Fetch(Token(key, "gacha-reader", project: Guid.NewGuid().ToString())))
            Check(wrongProject.StatusCode == HttpStatusCode.Unauthorized, "Gacha endpoint must enforce project scope.");
        using (var response = await Fetch(token))
        {
            var config = await response.Content.ReadFromJsonAsync<GachaConfigSnapshot>();
            Check(response.IsSuccessStatusCode && config?.Pools.Count == seedCount && response.Headers.CacheControl?.NoStore == true,
                "Authenticated config must contain current definitions and avoid stale HTTP caches.");
        }

        await SetSchedule(schedule with { Version = 2, Pools = [schedule.Pools[2], schedule.Pools[1]] });
        using (var response = await Fetch(token))
        {
            var config = await response.Content.ReadFromJsonAsync<GachaConfigSnapshot>();
            Check(config?.Version == 2 && config.Pools.Select(p => p.GachaKey).SequenceEqual(
                new[] { "WeaponPool_01", "CharPool_Eula" }), "Live edits must update pool selection/order without restart.");
        }
        await SetSchedule(schedule);
        string poolPath = Path.Combine(root, "gacha-CharPool_Hutao.json");
        string originalPool = await File.ReadAllTextAsync(poolPath);
        var pool = initial.Pools[0] with { EventRules = null };
        async Task SetPool(GachaPoolConfig value) =>
            await File.WriteAllTextAsync(poolPath, JsonSerializer.Serialize(value, GachaJson));
        await SetPool(pool with { PityCount = 7, Entries = pool.Entries.Select(e => e with { Weight = 3 }).ToList() });
        using (var response = await Fetch(token))
        {
            var config = await response.Content.ReadFromJsonAsync<GachaConfigSnapshot>();
            Check(config?.Pools[0].PityCount == 7 && config.Pools[0].Entries.All(e => e.Weight == 3),
                "Live pool edits must update definitions without restarting.");
        }
        async Task ExpectUnavailable(string reason)
        {
            using var response = await Fetch(token);
            var error = await response.Content.ReadFromJsonAsync<ApiError>();
            Check(response.StatusCode == HttpStatusCode.ServiceUnavailable && error?.Code == "gacha_config_unavailable" &&
                !error.Message.Contains(root), reason);
        }
        await SetPool(pool with { Entries = pool.Entries.Select(e => e with { Weight = 0 }).ToList() });
        await ExpectUnavailable("Zero weights must fail rather than serve invalid config.");
        await SetPool(pool with { Entries = pool.Entries.Select(e => e with { Weight = int.MaxValue }).ToList() });
        await ExpectUnavailable("Overflowing total weights must fail.");
        await SetPool(pool with { PityCount = 0 });
        await ExpectUnavailable("Invalid pity must fail.");
        await File.WriteAllTextAsync(poolPath, originalPool);
        await SetSchedule(schedule with { Pools = [schedule.Pools[0], schedule.Pools[0]] });
        await ExpectUnavailable("Duplicate schedule keys must fail.");
        await SetSchedule(schedule with { Pools = [schedule.Pools[0] with { GachaKey = "../outside" }] });
        await ExpectUnavailable("Pool keys cannot traverse directories.");
        await SetSchedule(schedule with { Pools = [schedule.Pools[0] with { StartsAtUtc = "2026-10-13T08:00:00+08:00" }] });
        await ExpectUnavailable("Schedule timestamps must explicitly use UTC.");
        await File.WriteAllTextAsync(schedulePath, "{broken");
        await ExpectUnavailable("Malformed configuration must fail without local fallback.");
        await File.WriteAllTextAsync(schedulePath, originalSchedule);
        using (var recovered = await Fetch(token))
            Check(recovered.IsSuccessStatusCode, "A corrected file must recover without restarting the server.");
        Console.WriteLine("PASS: gacha auth, schedule boundaries, live reload, definition validation and recovery.");
    }
}

