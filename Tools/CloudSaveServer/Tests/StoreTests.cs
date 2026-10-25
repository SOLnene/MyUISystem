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
    sealed class StoreClock : TimeProvider
    {
        internal DateTimeOffset Now = new(2026, 10, 24, 15, 59, 59, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    static async Task ValidateStore(string root)
    {
        Directory.CreateDirectory(root);
        string source = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        File.Copy(Path.Combine(source, "store-config.json"), Path.Combine(root, "store-config.json"));
        File.Copy(Path.Combine(source, "store-items.json"), Path.Combine(root, "store-items.json"));
        var clock = new StoreClock();
        var configs = new StoreConfigStore(root);
        var definition = await configs.ReadAsync(default);
        Check(definition.Configuration.Items.Count == 30, "Server seed retains the current thirty store offers.");
        Check(definition.Period(clock.Now).NextRefreshAt == clock.Now.AddSeconds(1), "Daily refresh uses the configured server timezone.");
        var app = ServerApp.Build([], builder =>
        {
            builder.Logging.ClearProviders();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Urls"] = "http://127.0.0.1:0", ["SaveServer:ProjectId"] = Project,
                ["SaveServer:EnvironmentId"] = EnvironmentId, ["SaveServer:DatabasePath"] = Path.Combine(root, "store.db"),
                ["SaveServer:MaxSaveBytes"] = "1048576", ["EnableLegacyUnityAuthentication"] = "false", ["StoreConfigRoot"] = root
            });
            builder.Services.AddSingleton<TimeProvider>(clock);
        });
        await app.StartAsync();
        string address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        await using var server = new RunningServer(app, new HttpClient(new HttpClientHandler { UseProxy = false }) { BaseAddress = new(address) });
        var accounts = app.Services.GetRequiredService<AccountStore>();
        var saves = app.Services.GetRequiredService<SaveStore>();
        accounts.Register(new("store-buyer", "abcdefgh"));
        var account = accounts.Login(new("store-buyer", "abcdefgh"))!;
        string owner = "local:" + account.Uid;
        accounts.Register(new("store-other", "abcdefgh"));
        var other = accounts.Login(new("store-other", "abcdefgh"))!;
        var initial = JsonNode.Parse(DrawSave)!;
        initial["currencies"]!["items"]!.AsArray().Add(new JsonObject { ["itemId"] = 202, ["amount"] = 1000 });
        await Receipt(await Put(server.Client, account.AccessToken, 0, Guid.NewGuid(), initial.ToJsonString()), 1);

        async Task<HttpResponseMessage> Send(HttpMethod method, string path, string? token, object? body = null)
        {
            using var request = new HttpRequestMessage(method, path);
            if (token != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (body != null) request.Content = JsonContent.Create(body);
            return await server.Client.SendAsync(request);
        }
        async Task<StoreSnapshot> Snapshot()
        {
            using var response = await Send(HttpMethod.Get, "/v1/store", account.AccessToken);
            Check(response.IsSuccessStatusCode, "Store listing is authenticated and available.");
            return (await response.Content.ReadFromJsonAsync<StoreSnapshot>())!;
        }
        async Task<StorePurchaseReply> Buy(StorePurchaseRequest request)
        {
            using var response = await Send(HttpMethod.Post, "/v1/store/purchase", account.AccessToken, request);
            Check(response.IsSuccessStatusCode, "Store purchase succeeds: " + await response.Content.ReadAsStringAsync());
            return (await response.Content.ReadFromJsonAsync<StorePurchaseReply>())!;
        }
        async Task Reject(StorePurchaseRequest request, string code, HttpStatusCode status = HttpStatusCode.Conflict)
        {
            var before = saves.Load(owner);
            string priorReceipt = JsonSerializer.Serialize(saves.FindStorePurchase(owner, request.RequestId));
            using var response = await Send(HttpMethod.Post, "/v1/store/purchase", account.AccessToken, request);
            var error = await response.Content.ReadFromJsonAsync<ApiError>();
            Check(response.StatusCode == status && error!.Code == code, "Rejected store request: " + code);
            Check(saves.Load(owner) == before && JsonSerializer.Serialize(saves.FindStorePurchase(owner, request.RequestId)) == priorReceipt,
                "Rejected purchase changes neither save nor receipt.");
        }
        using (var unauthorized = await Send(HttpMethod.Get, "/v1/store", null))
            Check(unauthorized.StatusCode == HttpStatusCode.Unauthorized, "Store listing requires login.");
        var first = await Snapshot();
        Check(first.Items.Count == 30 && first.Items.Single(i => i.Offer.StoreItemId == 1020).RemainingLimit == 10,
            "Server publishes contents and remaining quantities.");
        var purchase = new StorePurchaseRequest(Guid.NewGuid(), 1020, 2, 1, first.ConfigVersion);
        var bought = await Buy(purchase);
        var state = JsonNode.Parse(bought.State.SaveJson)!;
        int Balance(int id) => state["currencies"]!["items"]!.AsArray().Single(i => i!["itemId"]!.GetValue<int>() == id)!["amount"]!.GetValue<int>();
        Check(Balance(201) == 680 && Balance(223) == 22 && bought.Operation.TotalPrice == 320 && bought.Operation.GrantedAmount == 2,
            "Server deducts configured price and grants the configured quantity atomically.");
        Check(bought.State.Revision == 2 && !bought.Replayed, "Purchase advances the shared save revision once.");
        var retried = await Buy(purchase);
        Check(retried.Replayed && retried.State == bought.State, "A repeated purchase never deducts or grants twice.");
        await Reject(purchase with { Count = 1 }, "request_id_reused");
        await Reject(new(Guid.NewGuid(), 1020, 1, 1, first.ConfigVersion), "save_conflict");
        await Reject(new(Guid.NewGuid(), 1020, 9, 2, first.ConfigVersion), "store_limit_exceeded");
        await Reject(new(Guid.NewGuid(), 1020, 5, 2, first.ConfigVersion), "store_insufficient_currency");
        await Reject(new(Guid.NewGuid(), 1020, 0, 2, first.ConfigVersion), "invalid_store_request", HttpStatusCode.BadRequest);
        using (var receipt = await Send(HttpMethod.Get, "/v1/store/requests/" + purchase.RequestId, account.AccessToken))
            Check((await receipt.Content.ReadFromJsonAsync<StorePurchaseReply>())!.Replayed, "Purchase receipt can be recovered separately.");
        using (var hidden = await Send(HttpMethod.Get, "/v1/store/requests/" + purchase.RequestId, other.AccessToken))
            Check(hidden.StatusCode == HttpStatusCode.NotFound, "Other accounts cannot read purchase receipts.");
        Check((await Snapshot()).Items.Single(i => i.Offer.StoreItemId == 1020).RemainingLimit == 8, "Remaining quantities survive subsequent listing requests.");

        // An ordinary save may still contain legacy fields during migration; limits always come from the server counter table.
        state["store"] = new JsonObject { ["records"] = new JsonArray() };
        await Receipt(await Put(server.Client, account.AccessToken, 2, Guid.NewGuid(), state.ToJsonString()), 3);
        Check((await Snapshot()).Items.Single(i => i.Offer.StoreItemId == 1020).RemainingLimit == 8,
            "Ordinary save uploads cannot reset migrated store purchase counters.");
        var book = await Buy(new(Guid.NewGuid(), 1001, 2, 3, first.ConfigVersion));
        var bookState = JsonNode.Parse(book.State.SaveJson)!;
        Check(bookState["inventory"]!["stacks"]!.AsArray().Single(i => i!["itemId"]!.GetValue<int>() == 502)!["count"]!.GetValue<int>() == 2,
            "Stack rewards use the configured item quantity.");
        Check(bookState["inventory"]!["unseenMaterialIds"]!.AsArray().Any(i => i!.GetValue<int>() == 502), "New materials keep the inventory attention marker.");

        string path = Path.Combine(root, "store-config.json");
        var changed = definition.Configuration with { Items = definition.Configuration.Items.Select(i => i.StoreItemId == 1020 ? i with { Price = 1, DailyPurchaseLimit = 3 } : i).ToList() };
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(changed, GachaJson));
        var updated = await Snapshot();
        Check(updated.ConfigVersion != first.ConfigVersion && updated.Items.Single(i => i.Offer.StoreItemId == 1020) is { RemainingLimit: 1, Offer.Price: 1 },
            "Content edits without a version bump invalidate stale prices and update limits.");
        await Reject(new(Guid.NewGuid(), 1020, 1, 4, first.ConfigVersion), "store_config_changed");
        var sold = await Buy(new(Guid.NewGuid(), 1020, 1, 4, updated.ConfigVersion));
        Check((await Snapshot()).Items.Single(i => i.Offer.StoreItemId == 1020).RemainingLimit == 0, "Configured limit produces sold-out stock.");
        clock.Now = clock.Now.AddSeconds(1);
        var refreshed = await Snapshot();
        Check(refreshed.PeriodKey != first.PeriodKey && refreshed.Items.Single(i => i.Offer.StoreItemId == 1020).RemainingLimit == 3,
            "Crossing the server daily boundary replenishes stock without another client write.");
        var duplicateAfterReset = await Buy(purchase);
        Check(duplicateAfterReset.Replayed && saves.Load(owner)!.Revision == sold.State.Revision,
            "Retrying yesterday's purchase does not consume today's stock.");
        var concurrent = Enumerable.Range(0, 2).Select(_ => Send(HttpMethod.Post, "/v1/store/purchase", account.AccessToken,
            new StorePurchaseRequest(Guid.NewGuid(), 1020, 1, 5, refreshed.ConfigVersion))).ToArray();
        var responses = await Task.WhenAll(concurrent);
        Check(responses.Count(r => r.IsSuccessStatusCode) == 1 && responses.Count(r => r.StatusCode == HttpStatusCode.Conflict) == 1,
            "Concurrent requests with the same revision settle only once.");
        foreach (var response in responses) response.Dispose();
        changed = changed with { Items = changed.Items.Select(i => i.StoreItemId == 1020 ? i with { Enabled = false } : i).ToList() };
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(changed, GachaJson));
        Check((await Snapshot()).Items.All(i => i.Offer.StoreItemId != 1020), "Disabled offers disappear from server listings.");
        Check((await Buy(purchase)).Replayed, "Delisting does not destroy an existing purchase receipt.");
        await File.WriteAllTextAsync(path, "{invalid");
        using (var unavailable = await Send(HttpMethod.Get, "/v1/store", account.AccessToken))
            Check(unavailable.StatusCode == HttpStatusCode.ServiceUnavailable, "Invalid configuration fails closed.");
        Check((await Buy(purchase)).Replayed, "Configuration failure still permits durable receipt recovery.");
        Console.WriteLine("PASS: store contents, prices, currency and stack grants, limits, server refresh, hot reload, concurrency and durable retries.");
    }
}
