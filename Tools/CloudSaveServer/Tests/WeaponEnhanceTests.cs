using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using CloudSaveServer;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

internal static partial class Program
{
    static async Task ValidateWeaponEnhancement(string root)
    {
        Directory.CreateDirectory(root);
        string source = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        string configPath = Path.Combine(root, "weapon-enhance-config.json");
        File.Copy(Path.Combine(source, "weapon-enhance-config.json"), configPath);
        var config = await new WeaponEnhanceConfigStore(root).ReadAsync(default);
        var rules = config.Rules;
        Check(rules.Weapons.Length == 16 && rules.GoldPerExp == 1, "Enhancement exports existing weapon definitions and costs.");
        foreach (var weapon in rules.Weapons)
        {
            Check(rules.MaterialExp(weapon.ItemId, 1, 0, 0) == rules.MaterialBaseExp[weapon.Stars - 1], "Each material uses configured star EXP.");
            for (int rank = 0; rank < rules.RankMaxLevels.Length; rank++)
            {
                int cap = rules.RankMaxLevels[rank];
                var full = rules.Preview(weapon.ItemId, cap, 0, rank, int.MaxValue);
                Check(full.Level == cap && full.GainedExp == 0, "A capped weapon consumes no material EXP.");
                var boundary = rules.Preview(weapon.ItemId, cap - 1, 0, rank, int.MaxValue);
                Check(boundary.Level == cap && boundary.Exp == rules.RequiredExp(weapon.ItemId, cap) - 1 &&
                    boundary.GainedExp == rules.RequiredExp(weapon.ItemId, cap - 1) + boundary.Exp,
                    "Overflow is capped and charged only for effective EXP at every rank.");
            }
        }
        var app = ServerApp.Build([], builder =>
        {
            builder.Logging.ClearProviders();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Urls"] = "http://127.0.0.1:0", ["SaveServer:ProjectId"] = Project,
                ["SaveServer:EnvironmentId"] = EnvironmentId, ["SaveServer:DatabasePath"] = Path.Combine(root, "enhance.db"),
                ["SaveServer:MaxSaveBytes"] = "1048576", ["EnableLegacyUnityAuthentication"] = "false", ["WeaponEnhanceConfigRoot"] = root
            });
        });
        await app.StartAsync();
        string address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        await using var server = new RunningServer(app, new HttpClient(new HttpClientHandler { UseProxy = false }) { BaseAddress = new(address) });
        var accounts = app.Services.GetRequiredService<AccountStore>(); var saves = app.Services.GetRequiredService<SaveStore>();
        accounts.Register(new("enhance-owner", "abcdefgh")); var account = accounts.Login(new("enhance-owner", "abcdefgh"))!;
        accounts.Register(new("enhance-other", "abcdefgh")); var other = accounts.Login(new("enhance-other", "abcdefgh"))!;
        string owner = "local:" + account.Uid;
        int itemId = rules.Weapons.First(w => w.Stars == 3).ItemId;
        JsonObject Equip(long id, int level = 1, int exp = 0, int rank = 0) => new()
        {
            ["instanceId"] = id, ["itemId"] = itemId, ["level"] = level, ["exp"] = exp, ["rank"] = rank, ["refinementLevel"] = 3
        };
        var initial = JsonNode.Parse(DrawSave)!.AsObject();
        initial["currencies"]!["items"]!.AsArray().Add(new JsonObject { ["itemId"] = 202, ["amount"] = 1000000 });
        initial["inventory"]!["equips"] = new JsonArray(Equip(1), Equip(2, 2, 25), Equip(3), Equip(4), Equip(5), Equip(6));
        initial["inventory"]!["unseenEquipInstanceIds"] = new JsonArray(2, 3, 4, 5, 6);
        initial["characters"]!["characters"]!.AsArray().Add(new JsonObject { ["characterKey"] = "test", ["equippedWeaponInstanceId"] = 4 });
        await Receipt(await Put(server.Client, account.AccessToken, 0, Guid.NewGuid(), initial.ToJsonString()), 1);

        async Task<HttpResponseMessage> Send(HttpMethod method, string path, string? token, object? body = null)
        {
            using var request = new HttpRequestMessage(method, path);
            if (token != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (body != null) request.Content = JsonContent.Create(body);
            return await server.Client.SendAsync(request);
        }
        async Task Reject(WeaponEnhanceRequest request, string code, HttpStatusCode status = HttpStatusCode.Conflict)
        {
            var before = saves.Load(owner);
            using var response = await Send(HttpMethod.Post, "/v1/weapons/enhance", account.AccessToken, request);
            var error = await response.Content.ReadFromJsonAsync<ApiError>();
            Check(response.StatusCode == status && error!.Code == code, "Enhancement rejected: " + code + " received " + error?.Code);
            Check(saves.Load(owner) == before, "Rejected enhancement preserves wallet, inventory, EXP and revision.");
        }
        async Task<WeaponEnhanceReply> Enhance(WeaponEnhanceRequest request)
        {
            using var response = await Send(HttpMethod.Post, "/v1/weapons/enhance", account.AccessToken, request);
            Check(response.IsSuccessStatusCode, "Enhancement succeeds: " + await response.Content.ReadAsStringAsync());
            return (await response.Content.ReadFromJsonAsync<WeaponEnhanceReply>())!;
        }
        using (var unauthorized = await Send(HttpMethod.Get, "/v1/weapons/enhance/config", null))
            Check(unauthorized.StatusCode == HttpStatusCode.Unauthorized, "Enhancement rules require authentication.");
        using (var response = await Send(HttpMethod.Get, "/v1/weapons/enhance/config", account.AccessToken))
            Check((await response.Content.ReadFromJsonAsync<WeaponEnhanceConfig>())!.ConfigVersion == config.ConfigVersion,
                "HTTP config token covers the exact server rules.");
        var request = new WeaponEnhanceRequest(Guid.NewGuid(), 1, [2], 1, config.ConfigVersion);
        await Reject(request with { WeaponInstanceId = 99 }, "enhance_weapon_missing");
        await Reject(request with { MaterialInstanceIds = [99] }, "enhance_material_missing");
        await Reject(request with { MaterialInstanceIds = [4] }, "enhance_material_equipped");
        await Reject(request with { MaterialInstanceIds = [1] }, "invalid_enhance_request", HttpStatusCode.BadRequest);
        await Reject(request with { MaterialInstanceIds = [2, 2] }, "invalid_enhance_request", HttpStatusCode.BadRequest);
        await Reject(request with { MaterialInstanceIds = [] }, "invalid_enhance_request", HttpStatusCode.BadRequest);
        await Reject(request with { MaterialInstanceIds = null! }, "invalid_enhance_request", HttpStatusCode.BadRequest);
        await Reject(request with { MaterialInstanceIds = Enumerable.Range(10, 41).Select(i => (long)i).ToArray() }, "invalid_enhance_request", HttpStatusCode.BadRequest);
        await Reject(request with { ConfigVersion = new string('A', 64) }, "enhance_config_changed");
        var accepted = await Enhance(request);
        int materialExp = rules.MaterialExp(itemId, 2, 25, 0);
        var predicted = rules.Preview(itemId, 1, 0, 0, materialExp);
        var state = JsonNode.Parse(accepted.State.SaveJson)!;
        var target = state["inventory"]!["equips"]!.AsArray().Single(e => e!["instanceId"]!.GetValue<long>() == 1)!;
        Check(accepted.Operation.Level == predicted.Level && accepted.Operation.Exp == predicted.Exp &&
            accepted.Operation.GainedExp == materialExp && accepted.Operation.GoldCost == materialExp,
            "Server independently computes target EXP, recycled material EXP and coin cost.");
        Check(target["refinementLevel"]!.GetValue<int>() == 3 && target["rank"]!.GetValue<int>() == 0,
            "Enhancement preserves refinement and rank.");
        Check(state["inventory"]!["equips"]!.AsArray().All(e => e!["instanceId"]!.GetValue<long>() != 2) &&
            state["currencies"]!["items"]!.AsArray().Single(c => c!["itemId"]!.GetValue<int>() == 202)!["amount"]!.GetValue<int>() == 1000000 - materialExp,
            "Material removal and wallet deduction commit together.");
        Check(state["inventory"]!["unseenEquipInstanceIds"]!.AsArray().All(i => i!.GetValue<long>() != 2) &&
            state["achievements"]!["progress"]![0]!["value"]!.GetValue<int>() == 1,
            "Consumed item attention and achievement progress commit once.");
        var replay = await Enhance(request);
        Check(replay.Replayed && replay.State == accepted.State, "Lost-reply retries cannot double consume or charge.");
        await Reject(request with { MaterialInstanceIds = [3] }, "request_id_reused");
        await Reject(request with { RequestId = Guid.NewGuid(), MaterialInstanceIds = [3] }, "save_conflict");
        using (var recovered = await Send(HttpMethod.Get, "/v1/weapons/enhance/requests/" + request.RequestId, account.AccessToken))
            Check((await recovered.Content.ReadFromJsonAsync<WeaponEnhanceReply>())!.Replayed, "GET recovers enhancement receipt.");
        using (var hidden = await Send(HttpMethod.Get, "/v1/weapons/enhance/requests/" + request.RequestId, other.AccessToken))
            Check(hidden.StatusCode == HttpStatusCode.NotFound, "Other accounts cannot recover this receipt.");
        var next = request with { RequestId = Guid.NewGuid(), MaterialInstanceIds = [3], ExpectedRevision = 2 };
        var concurrent = await Task.WhenAll(Send(HttpMethod.Post, "/v1/weapons/enhance", account.AccessToken, next),
            Send(HttpMethod.Post, "/v1/weapons/enhance", account.AccessToken, next with { RequestId = Guid.NewGuid(), MaterialInstanceIds = [5] }));
        Check(concurrent.Count(r => r.IsSuccessStatusCode) == 1 && concurrent.Count(r => r.StatusCode == HttpStatusCode.Conflict) == 1,
            "Concurrent distinct requests at the same save revision settle exactly one enhancement.");
        foreach (var response in concurrent) response.Dispose();
        Check(saves.Load(owner)!.Revision == 3, "Concurrent enhancement advances the revision only once.");

        var current = JsonNode.Parse(saves.Load(owner)!.SaveJson)!;
        current["currencies"]!["items"]!.AsArray().Single(c => c!["itemId"]!.GetValue<int>() == 202)!["amount"] = 0;
        await Receipt(await Put(server.Client, account.AccessToken, 3, Guid.NewGuid(), current.ToJsonString()), 4);
        var remaining = current["inventory"]!["equips"]!.AsArray().Where(e => e!["instanceId"]!.GetValue<long>() is 3 or 5)
            .Select(e => e!["instanceId"]!.GetValue<long>()).Single();
        await Reject(next with { RequestId = Guid.NewGuid(), MaterialInstanceIds = [remaining], ExpectedRevision = 4 }, "enhance_insufficient_gold");
        current["inventory"]!["equips"]!.AsArray().Single(e => e!["instanceId"]!.GetValue<long>() == 1)!["level"] = 20;
        await Receipt(await Put(server.Client, account.AccessToken, 4, Guid.NewGuid(), current.ToJsonString()), 5);
        await Reject(next with { RequestId = Guid.NewGuid(), MaterialInstanceIds = [remaining], ExpectedRevision = 5 }, "enhance_level_limit");
        File.WriteAllText(configPath, "{ broken");
        using (var unavailable = await Send(HttpMethod.Get, "/v1/weapons/enhance/config", account.AccessToken))
            Check(unavailable.StatusCode == HttpStatusCode.ServiceUnavailable, "Malformed server rules fail closed.");
        Check((await Enhance(request)).Replayed, "Receipts recover even while current config is invalid.");
        File.Copy(Path.Combine(source, "weapon-enhance-config.json"), configPath, true);
        var changed = JsonNode.Parse(File.ReadAllText(configPath))!; changed["goldPerExp"] = 2;
        File.WriteAllText(configPath, changed.ToJsonString());
        await Reject(next with { RequestId = Guid.NewGuid(), MaterialInstanceIds = [remaining], ExpectedRevision = 5 }, "enhance_config_changed");
    }
}
