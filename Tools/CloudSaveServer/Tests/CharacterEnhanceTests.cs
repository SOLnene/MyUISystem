using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CloudSaveServer;
using Game.Domain.Enhance;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

internal static partial class Program
{
    static async Task ValidateCharacterEnhancement(string root)
    {
        Directory.CreateDirectory(root);
        string source = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        string path = Path.Combine(root, "character-enhance-config.json");
        File.Copy(Path.Combine(source, "character-enhance-config.json"), path);
        var config = await new CharacterEnhanceConfigStore(root).ReadAsync(default);
        var rules = config.Rules; string key = rules.Characters[0].Key;
        Check(rules.Characters.Length == 76 && rules.Books.Length == 3, "Character config exports current definitions and three books.");
        foreach (var character in rules.Characters.GroupBy(c => c.Rarity).Select(g => g.First()))
        for (int rank = 0; rank < rules.RankMaxLevels.Length; rank++)
        {
            int cap = rules.RankMaxLevels[rank];
            Check(rules.Preview(character.Key, cap, 0, rank, 100).GainedExp == 0, "Capped character accepts no upgrade.");
            var forecast = rules.Preview(character.Key, cap - 1, 0, rank, int.MaxValue);
            Check(forecast.Level == cap && forecast.Exp == rules.RequiredExp(character.Key, cap) - 1, "Overflow preserves existing rank cap semantics.");
        }
        var app = ServerApp.Build([], builder =>
        {
            builder.Logging.ClearProviders();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Urls"] = "http://127.0.0.1:0", ["SaveServer:ProjectId"] = Project,
                ["SaveServer:EnvironmentId"] = EnvironmentId, ["SaveServer:DatabasePath"] = Path.Combine(root, "character.db"),
                ["SaveServer:MaxSaveBytes"] = "1048576", ["EnableLegacyUnityAuthentication"] = "false", ["CharacterEnhanceConfigRoot"] = root
            });
        });
        await app.StartAsync();
        string address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        await using var server = new RunningServer(app, new HttpClient(new HttpClientHandler { UseProxy = false }) { BaseAddress = new(address) });
        var accounts = app.Services.GetRequiredService<AccountStore>(); var saves = app.Services.GetRequiredService<SaveStore>();
        accounts.Register(new("character-owner", "abcdefgh")); var account = accounts.Login(new("character-owner", "abcdefgh"))!;
        accounts.Register(new("character-other", "abcdefgh")); var other = accounts.Login(new("character-other", "abcdefgh"))!;
        string owner = "local:" + account.Uid;
        var initial = new JsonObject
        {
            ["version"] = 8,
            ["currencies"] = new JsonObject { ["items"] = new JsonArray(new JsonObject { ["itemId"] = 202, ["amount"] = 1000000 }) },
            ["inventory"] = new JsonObject { ["stacks"] = new JsonArray(new JsonObject { ["itemId"] = 501, ["count"] = 2 },
                new JsonObject { ["itemId"] = 502, ["count"] = 1 }, new JsonObject { ["itemId"] = 503, ["count"] = 10 }),
                ["equips"] = new JsonArray(), ["unseenMaterialIds"] = new JsonArray(501, 502, 503) },
            ["characters"] = new JsonObject { ["characters"] = new JsonArray(new JsonObject { ["characterKey"] = key,
                ["level"] = 1, ["exp"] = 0, ["rank"] = 0, ["talentLevel"] = 2, ["talentTokenCount"] = 3, ["equippedWeaponInstanceId"] = 0 }) },
            ["achievements"] = new JsonObject { ["progress"] = new JsonArray() }
        };
        await Receipt(await Put(server.Client, account.AccessToken, 0, Guid.NewGuid(), initial.ToJsonString()), 1);
        async Task<HttpResponseMessage> Send(HttpMethod method, string route, string? token, object? body = null)
        {
            using var message = new HttpRequestMessage(method, route);
            if (token != null) message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (body != null) message.Content = JsonContent.Create(body);
            return await server.Client.SendAsync(message);
        }
        async Task Reject(CharacterEnhanceRequest request, string code, HttpStatusCode status = HttpStatusCode.Conflict)
        {
            var before = saves.Load(owner);
            using var response = await Send(HttpMethod.Post, "/v1/characters/enhance", account.AccessToken, request);
            var error = await response.Content.ReadFromJsonAsync<ApiError>();
            Check(response.StatusCode == status && error?.Code == code, "Character rejection: " + code + " got " + error?.Code);
            Check(saves.Load(owner) == before, "Rejected upgrade leaves books, gold, character, achievements and revision unchanged.");
        }
        async Task<CharacterEnhanceReply> Enhance(CharacterEnhanceRequest request)
        {
            using var response = await Send(HttpMethod.Post, "/v1/characters/enhance", account.AccessToken, request);
            Check(response.IsSuccessStatusCode, "Character upgrade succeeds: " + await response.Content.ReadAsStringAsync());
            return (await response.Content.ReadFromJsonAsync<CharacterEnhanceReply>())!;
        }
        CharacterEnhanceBookSelection Book(int id, int count) => new() { ItemId = id, Count = count };
        var request = new CharacterEnhanceRequest(Guid.NewGuid(), key, [Book(501, 1), Book(502, 1)], 1, config.ConfigVersion);
        using (var unauthorized = await Send(HttpMethod.Get, "/v1/characters/enhance/config", null))
            Check(unauthorized.StatusCode == HttpStatusCode.Unauthorized, "Character config requires authentication.");
        using (var response = await Send(HttpMethod.Get, "/v1/characters/enhance/config", account.AccessToken))
            Check((await response.Content.ReadFromJsonAsync<CharacterEnhanceConfig>())!.ConfigVersion == config.ConfigVersion, "Client receives exact config version.");
        await Reject(request with { CharacterKey = "missing" }, "character_enhance_character_missing");
        await Reject(request with { Books = [] }, "invalid_character_enhance_request", HttpStatusCode.BadRequest);
        await Reject(request with { Books = [Book(501, 1), Book(501, 1)] }, "invalid_character_enhance_request", HttpStatusCode.BadRequest);
        await Reject(request with { Books = [Book(504, 1)] }, "invalid_character_enhance_request", HttpStatusCode.BadRequest);
        await Reject(request with { Books = [Book(501, -1)] }, "invalid_character_enhance_request", HttpStatusCode.BadRequest);
        await Reject(request with { Books = [Book(501, 3)] }, "character_enhance_insufficient_books");
        await Reject(request with { ConfigVersion = new string('A', 64) }, "character_enhance_config_changed");
        var accepted = await Enhance(request);
        var predicted = rules.Preview(key, 1, 0, 0, 600);
        var state = JsonNode.Parse(accepted.State.SaveJson)!;
        Check(accepted.Operation.Level == predicted.Level && accepted.Operation.Exp == predicted.Exp && accepted.Operation.GoldCost == 600,
            "Server computes experience and fee independently of client.");
        Check(state["currencies"]!["items"]![0]!["amount"]!.GetValue<int>() == 999400 &&
            state["inventory"]!["stacks"]!.AsArray().Single(s => s!["itemId"]!.GetValue<int>() == 501)!["count"]!.GetValue<int>() == 1 &&
            state["inventory"]!["stacks"]!.AsArray().All(s => s!["itemId"]!.GetValue<int>() != 502), "Gold and exact book quantities deducted together.");
        Check(state["characters"]!["characters"]![0]!["talentLevel"]!.GetValue<int>() == 2 &&
            state["characters"]!["characters"]![0]!["talentTokenCount"]!.GetValue<int>() == 3, "Unrelated character fields preserved.");
        Check(state["achievements"]!["progress"]![0]!["value"]!.GetValue<int>() == 1 &&
            state["inventory"]!["unseenMaterialIds"]!.AsArray().All(n => n!.GetValue<int>() != 502), "Achievement and exhausted-material attention commit once.");
        var replay = await Enhance(request);
        Check(replay.Replayed && replay.State == accepted.State, "Retry cannot charge or consume twice.");
        await Reject(request with { Books = [Book(501, 1)] }, "request_id_reused");
        await Reject(request with { RequestId = Guid.NewGuid() }, "save_conflict");
        using (var hidden = await Send(HttpMethod.Get, "/v1/characters/enhance/requests/" + request.RequestId, other.AccessToken))
            Check(hidden.StatusCode == HttpStatusCode.NotFound, "Other player cannot access receipt.");
        File.Move(path, path + ".hidden");
        try
        {
            Check((await Enhance(request)).Replayed, "Committed POST recovers even when config unavailable.");
            using var recovered = await Send(HttpMethod.Get, "/v1/characters/enhance/requests/" + request.RequestId, account.AccessToken);
            Check((await recovered.Content.ReadFromJsonAsync<CharacterEnhanceReply>())!.Replayed, "GET recovers committed operation without config.");
        }
        finally { File.Move(path + ".hidden", path); }
        var next = request with { RequestId = Guid.NewGuid(), ExpectedRevision = 2, Books = [Book(503, 1)] };
        var concurrent = await Task.WhenAll(Send(HttpMethod.Post, "/v1/characters/enhance", account.AccessToken, next),
            Send(HttpMethod.Post, "/v1/characters/enhance", account.AccessToken, next with { RequestId = Guid.NewGuid() }));
        Check(concurrent.Count(r => r.IsSuccessStatusCode) == 1 && concurrent.Count(r => r.StatusCode == HttpStatusCode.Conflict) == 1,
            "Same-revision concurrent operations settle exactly once.");
        foreach (var response in concurrent) response.Dispose();
        var current = JsonNode.Parse(saves.Load(owner)!.SaveJson)!;
        current["currencies"]!["items"]![0]!["amount"] = 0;
        await Receipt(await Put(server.Client, account.AccessToken, 3, Guid.NewGuid(), current.ToJsonString()), 4);
        await Reject(next with { RequestId = Guid.NewGuid(), ExpectedRevision = 4 }, "character_enhance_insufficient_gold");
        current["characters"]!["characters"]![0]!["level"] = 20;
        current["characters"]!["characters"]![0]!["exp"] = 0;
        await Receipt(await Put(server.Client, account.AccessToken, 4, Guid.NewGuid(), current.ToJsonString()), 5);
        await Reject(next with { RequestId = Guid.NewGuid(), ExpectedRevision = 5 }, "character_enhance_level_limit");
    }
}
