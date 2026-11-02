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
using ServerJson = CloudSaveServer.GachaJson;

internal static partial class Program
{
    static async Task ValidateWeaponRefinement(string root)
    {
        Directory.CreateDirectory(root);
        string source = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        string configPath = Path.Combine(root, "weapon-refine-config.json");
        File.Copy(Path.Combine(source, "weapon-refine-config.json"), configPath);
        var config = await new WeaponRefineConfigStore(root).ReadAsync(default);
        var rules = config.Rules;
        Check(rules.Weapons.Length == 16 && rules.RefinementCap == 5, "Refinement preserves the existing catalog and rank cap.");
        foreach (var weapon in rules.Weapons)
            for (int level = 1; level < rules.RefinementCap; level++)
                Check(rules.Cost(weapon.ItemId, level) == (int)MathF.Round(500 * weapon.Stars * weapon.Stars * MathF.Pow(level, 1.15f)),
                    "Refinement prices match the original Unity formula at every supported star and rank.");
        var app = ServerApp.Build([], builder =>
        {
            builder.Logging.ClearProviders();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Urls"] = "http://127.0.0.1:0", ["SaveServer:ProjectId"] = Project,
                ["SaveServer:EnvironmentId"] = EnvironmentId, ["SaveServer:DatabasePath"] = Path.Combine(root, "refine.db"),
                ["SaveServer:MaxSaveBytes"] = "1048576", ["EnableLegacyUnityAuthentication"] = "false", ["WeaponRefineConfigRoot"] = root
            });
        });
        await app.StartAsync();
        string address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        await using var server = new RunningServer(app, new HttpClient(new HttpClientHandler { UseProxy = false }) { BaseAddress = new(address) });
        var accounts = app.Services.GetRequiredService<AccountStore>(); var saves = app.Services.GetRequiredService<SaveStore>();
        accounts.Register(new("refine-owner", "abcdefgh")); var account = accounts.Login(new("refine-owner", "abcdefgh"))!;
        accounts.Register(new("refine-other", "abcdefgh")); var other = accounts.Login(new("refine-other", "abcdefgh"))!;
        string owner = "local:" + account.Uid;
        int itemId = rules.Weapons.First(w => w.Stars == 3).ItemId;
        int otherItemId = rules.Weapons.First(w => w.ItemId != itemId).ItemId;
        JsonObject Equip(long id, int definition = 0, int refinement = 1) => new()
        {
            ["instanceId"] = id, ["itemId"] = definition == 0 ? itemId : definition,
            ["level"] = 10, ["exp"] = 123, ["rank"] = 0, ["refinementLevel"] = refinement
        };
        var initial = JsonNode.Parse(DrawSave)!.AsObject();
        initial["currencies"]!["items"]!.AsArray().Add(new JsonObject { ["itemId"] = 202, ["amount"] = 1000000 });
        initial["inventory"]!["equips"] = new JsonArray(Equip(1), Equip(2, refinement: 2), Equip(3), Equip(4),
            Equip(5, otherItemId), Equip(6), Equip(7), Equip(8), Equip(9), Equip(10));
        initial["inventory"]!["unseenEquipInstanceIds"] = new JsonArray(2, 3, 4, 5, 6, 7, 8, 9, 10);
        initial["characters"]!["characters"]!.AsArray().Add(new JsonObject { ["characterKey"] = "target-owner", ["equippedWeaponInstanceId"] = 1 });
        initial["characters"]!["characters"]!.AsArray().Add(new JsonObject { ["characterKey"] = "material-owner", ["equippedWeaponInstanceId"] = 4 });
        await Receipt(await Put(server.Client, account.AccessToken, 0, Guid.NewGuid(), initial.ToJsonString()), 1);

        async Task<HttpResponseMessage> Send(HttpMethod method, string path, string? token, object? body = null)
        {
            using var request = new HttpRequestMessage(method, path);
            if (token != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (body != null) request.Content = JsonContent.Create(body);
            return await server.Client.SendAsync(request);
        }
        async Task Reject(WeaponRefineRequest request, string code, HttpStatusCode status = HttpStatusCode.Conflict)
        {
            var before = saves.Load(owner);
            using var response = await Send(HttpMethod.Post, "/v1/weapons/refine", account.AccessToken, request);
            var error = await response.Content.ReadFromJsonAsync<ApiError>();
            Check(response.StatusCode == status && error!.Code == code, "Refinement rejects " + code + "; received " + error?.Code);
            Check(saves.Load(owner) == before, "Rejected refinement preserves wallet, all materials, rank and revision.");
        }
        async Task<WeaponRefineReply> Refine(WeaponRefineRequest request)
        {
            using var response = await Send(HttpMethod.Post, "/v1/weapons/refine", account.AccessToken, request);
            Check(response.IsSuccessStatusCode, "Refinement succeeds: " + await response.Content.ReadAsStringAsync());
            return (await response.Content.ReadFromJsonAsync<WeaponRefineReply>())!;
        }
        using (var unauthorized = await Send(HttpMethod.Get, "/v1/weapons/refine/config", null))
            Check(unauthorized.StatusCode == HttpStatusCode.Unauthorized, "Refinement config requires authentication.");
        using (var response = await Send(HttpMethod.Get, "/v1/weapons/refine/config", account.AccessToken))
            Check((await response.Content.ReadFromJsonAsync<WeaponRefineConfig>())!.ConfigVersion == config.ConfigVersion,
                "Refinement config version covers the exact server file.");
        var request = new WeaponRefineRequest(Guid.NewGuid(), 1, [2, 3], 1, config.ConfigVersion);
        await Reject(request with { WeaponInstanceId = 99 }, "refine_weapon_missing");
        await Reject(request with { MaterialInstanceIds = [99] }, "refine_material_missing");
        await Reject(request with { MaterialInstanceIds = [4] }, "refine_material_equipped");
        await Reject(request with { MaterialInstanceIds = [5] }, "refine_material_mismatch");
        await Reject(request with { MaterialInstanceIds = [1] }, "invalid_refine_request", HttpStatusCode.BadRequest);
        await Reject(request with { MaterialInstanceIds = [2, 2] }, "invalid_refine_request", HttpStatusCode.BadRequest);
        await Reject(request with { MaterialInstanceIds = [] }, "invalid_refine_request", HttpStatusCode.BadRequest);
        await Reject(request with { MaterialInstanceIds = null! }, "invalid_refine_request", HttpStatusCode.BadRequest);
        await Reject(request with { MaterialInstanceIds = [2, 3, 6, 7, 8] }, "invalid_refine_request", HttpStatusCode.BadRequest);
        await Reject(request with { ConfigVersion = new string('A', 64) }, "refine_config_changed");
        var forged = JsonSerializer.SerializeToNode(request, ServerJson.Options)!.AsObject();
        forged["goldCost"] = 0; forged["refinementLevel"] = 5;
        using (var response = await Send(HttpMethod.Post, "/v1/weapons/refine", account.AccessToken, forged))
            Check(response.StatusCode == HttpStatusCode.BadRequest && saves.Load(owner)!.Revision == 1,
                "Client-supplied costs and outcomes are rejected before settlement.");
        var accepted = await Refine(request);
        int cost = rules.PreviewCost(itemId, 1, 2);
        var state = JsonNode.Parse(accepted.State.SaveJson)!;
        var target = state["inventory"]!["equips"]!.AsArray().Single(e => e!["instanceId"]!.GetValue<long>() == 1)!;
        Check(accepted.Operation.OldRefinementLevel == 1 && accepted.Operation.RefinementLevel == 3 && accepted.Operation.GoldCost == cost,
            "Each selected same-definition weapon adds one refinement, matching the existing rule.");
        Check(target["level"]!.GetValue<int>() == 10 && target["exp"]!.GetValue<int>() == 123 && target["rank"]!.GetValue<int>() == 0,
            "Refinement preserves level, EXP and promotion rank.");
        Check(state["inventory"]!["equips"]!.AsArray().All(e => e!["instanceId"]!.GetValue<long>() is not (2 or 3)) &&
            state["currencies"]!["items"]!.AsArray().Single(c => c!["itemId"]!.GetValue<int>() == 202)!["amount"]!.GetValue<int>() == 1000000 - cost,
            "Selected instances and gold are consumed in the same transaction.");
        Check(state["inventory"]!["unseenEquipInstanceIds"]!.AsArray().All(i => i!.GetValue<long>() is not (2 or 3)),
            "Consumed materials also leave the unseen-item list.");
        Check((await Refine(request)).Replayed, "A lost reply retries the same operation without a second deduction.");
        await Reject(request with { MaterialInstanceIds = [6] }, "request_id_reused");
        await Reject(request with { RequestId = Guid.NewGuid(), MaterialInstanceIds = [6] }, "save_conflict");
        using (var recovered = await Send(HttpMethod.Get, "/v1/weapons/refine/requests/" + request.RequestId, account.AccessToken))
            Check((await recovered.Content.ReadFromJsonAsync<WeaponRefineReply>())!.Replayed, "GET recovers the refinement result.");
        using (var hidden = await Send(HttpMethod.Get, "/v1/weapons/refine/requests/" + request.RequestId, other.AccessToken))
            Check(hidden.StatusCode == HttpStatusCode.NotFound, "Another account cannot read the receipt.");
        var next = request with { RequestId = Guid.NewGuid(), MaterialInstanceIds = [6], ExpectedRevision = 2 };
        await Reject(next with { MaterialInstanceIds = [6, 7, 8] }, "refine_material_limit");
        var same = await Task.WhenAll(Refine(next), Refine(next));
        Check(same.Count(r => r.Replayed) == 1 && saves.Load(owner)!.Revision == 3,
            "Concurrent identical IDs settle once and return one replay.");
        var distinct = await Task.WhenAll(Send(HttpMethod.Post, "/v1/weapons/refine", account.AccessToken,
            next with { RequestId = Guid.NewGuid(), MaterialInstanceIds = [7], ExpectedRevision = 3 }),
            Send(HttpMethod.Post, "/v1/weapons/refine", account.AccessToken,
            next with { RequestId = Guid.NewGuid(), MaterialInstanceIds = [8], ExpectedRevision = 3 }));
        Check(distinct.Count(r => r.IsSuccessStatusCode) == 1 && distinct.Count(r => r.StatusCode == HttpStatusCode.Conflict) == 1 && saves.Load(owner)!.Revision == 4,
            "Concurrent distinct IDs at the same revision cannot consume both materials.");
        foreach (var response in distinct) response.Dispose();
        await Reject(next with { RequestId = Guid.NewGuid(), MaterialInstanceIds = [10], ExpectedRevision = 4 }, "refine_level_limit");
        var current = JsonNode.Parse(saves.Load(owner)!.SaveJson)!;
        current["currencies"]!["items"]!.AsArray().Single(c => c!["itemId"]!.GetValue<int>() == 202)!["amount"] = 0;
        await Receipt(await Put(server.Client, account.AccessToken, 4, Guid.NewGuid(), current.ToJsonString()), 5);
        var emptyWallet = next with { RequestId = Guid.NewGuid(), WeaponInstanceId = 9, MaterialInstanceIds = [10], ExpectedRevision = 5 };
        await Reject(emptyWallet, "refine_insufficient_gold");
        var reopened = new SaveStore(app.Services.GetRequiredService<ServerSettings>()); reopened.Initialize();
        var persisted = reopened.FindWeaponRefinement(owner, request.RequestId)!.Operation;
        Check(persisted.RequestId == accepted.Operation.RequestId && persisted.RefinementLevel == accepted.Operation.RefinementLevel &&
            persisted.MaterialInstanceIds.SequenceEqual(accepted.Operation.MaterialInstanceIds),
            "Committed receipts survive a new store instance opening the database.");
        File.WriteAllText(configPath, "{ broken");
        using (var unavailable = await Send(HttpMethod.Get, "/v1/weapons/refine/config", account.AccessToken))
            Check(unavailable.StatusCode == HttpStatusCode.ServiceUnavailable, "Malformed refinement config fails closed.");
        Check((await Refine(request)).Replayed, "Receipts recover while current config is unavailable.");
        File.Copy(Path.Combine(source, "weapon-refine-config.json"), configPath, true);
        var changed = JsonNode.Parse(File.ReadAllText(configPath))!;
        changed["costsByStars"]![0]![0] = -1; File.WriteAllText(configPath, changed.ToJsonString());
        using (var invalid = await Send(HttpMethod.Get, "/v1/weapons/refine/config", account.AccessToken))
            Check(invalid.StatusCode == HttpStatusCode.ServiceUnavailable, "Semantically invalid rules fail closed.");
        changed["costsByStars"]![0]![0] = 1; File.WriteAllText(configPath, changed.ToJsonString());
        await Reject(emptyWallet, "refine_config_changed");
        var free = JsonNode.Parse(File.ReadAllText(configPath))!;
        free["costsByStars"]![rules.Weapon(itemId).Stars - 1]![0] = 0;
        File.WriteAllText(configPath, free.ToJsonString());
        var freeConfig = await new WeaponRefineConfigStore(root).ReadAsync(default);
        var freeResult = await Refine(emptyWallet with { RequestId = Guid.NewGuid(), ConfigVersion = freeConfig.ConfigVersion });
        Check(freeResult.Operation.GoldCost == 0 && freeResult.Operation.RefinementLevel == 2,
            "A configured free refinement still consumes exactly the selected material.");
    }
}
