using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CloudSaveServer;
using Game.Domain.Promote;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

internal static partial class Program
{
    static async Task ValidatePromotion(string root)
    {
        Directory.CreateDirectory(root);
        string source = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        string configPath = Path.Combine(root, "promotion-config.json");
        File.Copy(Path.Combine(source, "promotion-config.json"), configPath);
        var config = await new PromoteConfigStore(root).ReadAsync(default); var rules = config.Rules;
        Check(rules.Characters.Length == 76 && rules.Weapons.Length == 16, "Both promotion target catalogs exported.");
        foreach (var character in rules.Characters)
        for (int rank = 0; rank < 6; rank++)
            Check(rules.Resolve(new PromotionTarget { Kind = "character", CharacterKey = character.Key }, 0, rank).Rule.Materials.Length > 0, "Every character resolves its explicit promotion rule.");
        foreach (var weapon in rules.Weapons)
        for (int rank = 0; rank < 6; rank++)
            Check(rules.Resolve(new PromotionTarget { Kind = "weapon", WeaponInstanceId = 1 }, weapon.ItemId, rank).GoldCost >= 0, "Every weapon resolves rank cost.");
        var app = ServerApp.Build([], builder =>
        {
            builder.Logging.ClearProviders();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Urls"] = "http://127.0.0.1:0", ["SaveServer:ProjectId"] = Project,
                ["SaveServer:EnvironmentId"] = EnvironmentId, ["SaveServer:DatabasePath"] = Path.Combine(root, "promote.db"),
                ["SaveServer:MaxSaveBytes"] = "1048576", ["EnableLegacyUnityAuthentication"] = "false", ["PromoteConfigRoot"] = root
            });
        });
        await app.StartAsync();
        string address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        await using var server = new RunningServer(app, new HttpClient(new HttpClientHandler { UseProxy = false }) { BaseAddress = new(address) });
        var accounts = app.Services.GetRequiredService<AccountStore>(); var saves = app.Services.GetRequiredService<SaveStore>();
        accounts.Register(new("promote-owner", "abcdefgh")); var account = accounts.Login(new("promote-owner", "abcdefgh"))!;
        accounts.Register(new("promote-other", "abcdefgh")); var other = accounts.Login(new("promote-other", "abcdefgh"))!;
        string owner = "local:" + account.Uid, key = rules.Characters[0].Key; int itemId = rules.Weapons.First(w => w.Rarity == 3).ItemId;
        JsonObject Weapon(long id) => new() { ["instanceId"] = id, ["itemId"] = itemId, ["level"] = 20, ["exp"] = 456, ["rank"] = 0, ["refinementLevel"] = 3 };
        var materials = rules.RuleSets[0].Ranks[0].Materials;
        var initial = new JsonObject
        {
            ["version"] = 8,
            ["currencies"] = new JsonObject { ["items"] = new JsonArray(new JsonObject { ["itemId"] = 202, ["amount"] = 1000000 }) },
            ["inventory"] = new JsonObject { ["stacks"] = new JsonArray(materials.Select(m => (JsonNode)new JsonObject { ["itemId"] = m.ItemId, ["count"] = m.Count * 4 }).ToArray()),
                ["equips"] = new JsonArray(Weapon(1), Weapon(2), Weapon(3)), ["unseenMaterialIds"] = new JsonArray(materials.Select(m => (JsonNode)JsonValue.Create(m.ItemId)).ToArray()) },
            ["characters"] = new JsonObject { ["characters"] = new JsonArray(new JsonObject { ["characterKey"] = key, ["level"] = 20, ["exp"] = 123,
                ["rank"] = 0, ["talentLevel"] = 2, ["talentTokenCount"] = 3, ["equippedWeaponInstanceId"] = 1 }) },
            ["achievements"] = new JsonObject { ["progress"] = new JsonArray() }
        };
        await Receipt(await Put(server.Client, account.AccessToken, 0, Guid.NewGuid(), initial.ToJsonString()), 1);
        async Task<HttpResponseMessage> Send(HttpMethod method, string path, string? token, object? body = null)
        {
            using var message = new HttpRequestMessage(method, path);
            if (token != null) message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (body != null) message.Content = JsonContent.Create(body);
            return await server.Client.SendAsync(message);
        }
        async Task Reject(PromoteRequest request, string code, HttpStatusCode status = HttpStatusCode.Conflict)
        {
            var before = saves.Load(owner);
            using var response = await Send(HttpMethod.Post, "/v1/progression/promote", account.AccessToken, request);
            var error = await response.Content.ReadFromJsonAsync<ApiError>();
            Check(response.StatusCode == status && error?.Code == code, "Promotion rejected: " + code + " got " + error?.Code);
            Check(saves.Load(owner) == before, "Rejected promotion preserves all state and revision.");
        }
        async Task<PromoteReply> Promote(PromoteRequest request)
        {
            using var response = await Send(HttpMethod.Post, "/v1/progression/promote", account.AccessToken, request);
            Check(response.IsSuccessStatusCode, "Promotion succeeds: " + await response.Content.ReadAsStringAsync());
            return (await response.Content.ReadFromJsonAsync<PromoteReply>())!;
        }
        using (var anonymous = await Send(HttpMethod.Get, "/v1/progression/promote/config", null))
            Check(anonymous.StatusCode == HttpStatusCode.Unauthorized, "Promotion config requires authentication.");
        var request = new PromoteRequest(Guid.NewGuid(), new PromotionTarget { Kind = "character", CharacterKey = key }, 1, config.ConfigVersion);
        await Reject(request with { Target = new PromotionTarget { Kind = "unknown" } }, "invalid_promote_request", HttpStatusCode.BadRequest);
        await Reject(request with { Target = new PromotionTarget { Kind = "weapon", CharacterKey = key, WeaponInstanceId = 1 } }, "invalid_promote_request", HttpStatusCode.BadRequest);
        await Reject(request with { Target = new PromotionTarget { Kind = "weapon", WeaponInstanceId = 99 } }, "promote_target_missing");
        await Reject(request with { ConfigVersion = new string('A',64) }, "promote_config_changed");
        var accepted = await Promote(request); var state = JsonNode.Parse(accepted.State.SaveJson)!;
        Check(accepted.Operation.Rank == 1 && accepted.Operation.Level == 20 && accepted.Operation.Exp == 123, "Character promotion preserves level and EXP.");
        Check(state["characters"]!["characters"]![0]!["talentLevel"]!.GetValue<int>() == 2 && state["characters"]!["characters"]![0]!["equippedWeaponInstanceId"]!.GetValue<int>() == 1,
            "Character promotion preserves talent and weapon binding.");
        int characterCost = rules.Resolve(request.Target, 0, 0).GoldCost;
        Check(state["currencies"]!["items"]![0]!["amount"]!.GetValue<int>() == 1000000 - characterCost, "Server determines character fee.");
        var replay = await Promote(request); Check(replay.Replayed && replay.State == accepted.State, "Same request is charged once.");
        await Reject(request with { Target = new PromotionTarget { Kind = "weapon", WeaponInstanceId = 1 } }, "request_id_reused");
        using (var hidden = await Send(HttpMethod.Get, "/v1/progression/promote/requests/" + request.RequestId, other.AccessToken))
            Check(hidden.StatusCode == HttpStatusCode.NotFound, "Promotion receipts isolated by account.");
        File.Move(configPath, configPath + ".hidden");
        try
        {
            Check((await Promote(request)).Replayed, "Committed receipt replay ignores config outage.");
            using var recovered = await Send(HttpMethod.Get, "/v1/progression/promote/requests/" + request.RequestId, account.AccessToken);
            Check((await recovered.Content.ReadFromJsonAsync<PromoteReply>())!.Replayed, "GET restores committed result.");
        }
        finally { File.Move(configPath + ".hidden", configPath); }
        var weaponRequest = request with { RequestId = Guid.NewGuid(), Target = new PromotionTarget { Kind = "weapon", WeaponInstanceId = 1 }, ExpectedRevision = 2 };
        var weaponReply = await Promote(weaponRequest); state = JsonNode.Parse(weaponReply.State.SaveJson)!;
        Check(weaponReply.Operation.Rank == 1 && weaponReply.Operation.Exp == 0 && weaponReply.Operation.OldExp == 456, "Weapon promotion clears EXP through same transaction pipeline.");
        Check(state["inventory"]!["equips"]![0]!["refinementLevel"]!.GetValue<int>() == 3 && state["characters"]!["characters"]![0]!["rank"]!.GetValue<int>() == 1,
            "Weapon promotion preserves refinement and owning character state.");
        foreach (var material in materials)
            Check(state["inventory"]!["stacks"]!.AsArray().Single(s => s!["itemId"]!.GetValue<int>() == material.ItemId)!["count"]!.GetValue<int>() == material.Count * 2,
                "Both target types consume configured stack quantities.");
        var concurrent = await Task.WhenAll(Send(HttpMethod.Post, "/v1/progression/promote", account.AccessToken,
                weaponRequest with { RequestId = Guid.NewGuid(), Target = new PromotionTarget { Kind = "weapon", WeaponInstanceId = 2 }, ExpectedRevision = 3 }),
            Send(HttpMethod.Post, "/v1/progression/promote", account.AccessToken,
                weaponRequest with { RequestId = Guid.NewGuid(), Target = new PromotionTarget { Kind = "weapon", WeaponInstanceId = 3 }, ExpectedRevision = 3 }));
        Check(concurrent.Count(r => r.IsSuccessStatusCode) == 1 && concurrent.Count(r => r.StatusCode == HttpStatusCode.Conflict) == 1,
            "Concurrent same-revision promotions settle one transaction.");
        foreach (var response in concurrent) response.Dispose();
        var current = JsonNode.Parse(saves.Load(owner)!.SaveJson)!; var characterState = current["characters"]!["characters"]![0]!;
        characterState["level"] = 19; characterState["rank"] = 0;
        await Receipt(await Put(server.Client, account.AccessToken, 4, Guid.NewGuid(), current.ToJsonString()), 5);
        await Reject(request with { RequestId = Guid.NewGuid(), ExpectedRevision = 5 }, "promote_level_required");
        characterState["level"] = 90; characterState["rank"] = 6;
        await Receipt(await Put(server.Client, account.AccessToken, 5, Guid.NewGuid(), current.ToJsonString()), 6);
        await Reject(request with { RequestId = Guid.NewGuid(), ExpectedRevision = 6 }, "promote_rank_limit");
        characterState["level"] = 20; characterState["rank"] = 0; current["inventory"]!["stacks"] = new JsonArray();
        await Receipt(await Put(server.Client, account.AccessToken, 6, Guid.NewGuid(), current.ToJsonString()), 7);
        await Reject(request with { RequestId = Guid.NewGuid(), ExpectedRevision = 7 }, "promote_insufficient_materials");
        current["inventory"]!["stacks"] = initial["inventory"]!["stacks"]!.DeepClone(); current["currencies"]!["items"]![0]!["amount"] = 0;
        await Receipt(await Put(server.Client, account.AccessToken, 7, Guid.NewGuid(), current.ToJsonString()), 8);
        await Reject(request with { RequestId = Guid.NewGuid(), ExpectedRevision = 8 }, "promote_insufficient_gold");
    }
}
