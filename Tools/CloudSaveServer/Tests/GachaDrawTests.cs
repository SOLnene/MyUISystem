using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using CloudSaveServer;
using Microsoft.Extensions.DependencyInjection;

internal static partial class Program
{
    const string DrawSave = """
        {"version":8,"currencies":{"items":[{"itemId":1,"amount":100}]},"inventory":{"stacks":[{"itemId":2,"count":3}],"equips":[]},
        "characters":{"characters":[]},"gacha":{"pityCounters":[],"eventPityCounters":[]},"achievements":{"progress":[],"claimedIds":[]}}
        """;

    static async Task ValidateDraws(string root)
    {
        Directory.CreateDirectory(root);
        string source = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        foreach (var file in Directory.GetFiles(source, "gacha-*.json"))
            File.Copy(file, Path.Combine(root, Path.GetFileName(file)));
        await File.WriteAllTextAsync(Path.Combine(root, "gacha-common-test.json"), """
            {"rules":{"pityGroup":"test-shared","fiveStarProbability":0.006,"fourStarProbability":0.051,
            "fiveStarPityCount":1,"fourStarPityCount":1,"featuredFiveStarProbability":1},
            "threeStarWeapons":["Sword_Dawn"],"fourStarWeapons":["Sword_Zephyrus"],"standardFiveStarCharacters":["mona"]}
            """);
        await File.WriteAllTextAsync(Path.Combine(root, "gacha-test-role.json"),
            """{"gachaKey":"test-role","commonPool":"test","featuredFiveStarKey":"eula"}""");
        await File.WriteAllTextAsync(Path.Combine(root, "gacha-test-weapon.json"),
            """{"gachaKey":"test-weapon","pityCount":3,"entries":[{"entryKey":"Sword_Dawn","entryType":1,"weight":1,"rarity":3}]}""");
        var schedule = new GachaSchedule(9, [new("test-role", true, null, null), new("test-weapon", true, null, null)]);
        string schedulePath = Path.Combine(root, "gacha-schedule.json");
        await File.WriteAllTextAsync(schedulePath, JsonSerializer.Serialize(schedule, CloudSaveServer.GachaJson.Options));
        string databasePath = Path.Combine(root, "draws.db");
        AccountLoginReply account;
        GachaDrawRequest original;
        long finalRevision;
        await using (var server = await RunningServer.Start(databasePath, gachaRoot: root, legacy: false))
        {
            var accounts = server.App.Services.GetRequiredService<AccountStore>();
            _ = accounts.Register(new("draw-test", "abcdefgh"));
            account = accounts.Login(new("draw-test", "abcdefgh"))!;
            string token = account.AccessToken;
            string owner = "local:" + account.Uid;
            var store = server.App.Services.GetRequiredService<SaveStore>();
            await Receipt(await Put(server.Client, token, 0, Guid.NewGuid(), DrawSave), 1);
            original = new(Guid.NewGuid(), "test-role", 10, 1, 9);
            using (var noAuth = await Draw(server.Client, null, original))
                Check(noAuth.StatusCode == HttpStatusCode.Unauthorized, "Draw requires authentication.");
            foreach (var invalid in new[] { original with { Count = 0 }, original with { Count = 2 },
                original with { Count = -1 }, original with { Count = int.MaxValue },
                original with { RequestId = Guid.Empty }, original with { GachaKey = "../test" },
                original with { ExpectedRevision = 0 }, original with { ConfigVersion = 0 } })
            {
                using var response = await Draw(server.Client, token, invalid);
                Check(response.StatusCode == HttpStatusCode.BadRequest, "Malformed draw must not mutate state.");
            }
            var reply = await DrawOk(server.Client, token, original);
            Check(reply.Operation.Results.Count == 10 && reply.State.Revision == 2 && !reply.Replayed,
                "Ten draws commit once with ten ordered results.");
            var save = JsonNode.Parse(reply.State.SaveJson)!;
            var characters = save["characters"]!["characters"]!.AsArray();
            var equips = save["inventory"]!["equips"]!.AsArray();
            Check(characters.Count == 1 && characters[0]!["talentTokenCount"]!.GetValue<int>() == 9,
                "A new character followed by duplicates grants nine tokens.");
            Check(equips.Count == 1 && characters[0]!["equippedWeaponInstanceId"]!.GetValue<long>() ==
                equips[0]!["instanceId"]!.GetValue<long>(), "A ten-draw batch grants the starter exactly once.");
            Check(save["inventory"]!["unseenEquipInstanceIds"]?.AsArray().Count is null or 0,
                "Three-star starter weapons keep the existing no-attention behavior.");
            Check(JsonNode.DeepEquals(save["currencies"], JsonNode.Parse(DrawSave)!["currencies"]) &&
                JsonNode.DeepEquals(save["inventory"]!["stacks"], JsonNode.Parse(DrawSave)!["inventory"]!["stacks"]),
                "Free gacha leaves currencies and existing materials unchanged.");
            Check(save["achievements"]!["progress"]![0]!["value"]!.GetValue<int>() == 10,
                "Gacha progress is counted once.");
            var repeated = await DrawOk(server.Client, token, original);
            Check(repeated.Replayed && repeated.State.Revision == 2 &&
                JsonSerializer.Serialize(repeated.Operation) == JsonSerializer.Serialize(reply.Operation),
                "A lost reply is recovered without rerolling or granting again.");
            using (var reused = await Draw(server.Client, token, original with { Count = 1 }))
                Check(reused.StatusCode == HttpStatusCode.Conflict, "Reusing an ID with different parameters is rejected.");
            var other = accounts.Register(new("draw-other", "abcdefgh"))!;
            string otherToken = accounts.Login(new("draw-other", "abcdefgh"))!.AccessToken;
            using (var hidden = await FindDraw(server.Client, otherToken, original.RequestId))
                Check(hidden.StatusCode == HttpStatusCode.NotFound, "Another account cannot read a draw receipt.");
            using (var visible = await FindDraw(server.Client, token, original.RequestId))
                Check(visible.IsSuccessStatusCode, "An owner can recover a committed receipt.");

            // Legacy non-gacha workflows remain writable in this scoped migration.
            save["currencies"]!["items"]![0]!["amount"] = 123;
            await Receipt(await Put(server.Client, token, 2, Guid.NewGuid(), save.ToJsonString()), 3);
            var tampered = save.DeepClone();
            tampered["gacha"]!["eventPityCounters"]![0]!["fiveStarCount"] = 20;
            using (var blocked = await Put(server.Client, token, 3, Guid.NewGuid(), tampered.ToJsonString()))
                Check(blocked.StatusCode == HttpStatusCode.Conflict, "Whole-save uploads cannot reset server-owned pity.");
            var older = await DrawOk(server.Client, token, original);
            Check(older.Operation.CommittedRevision == 2 && older.State.Revision == 3,
                "Replay returns the original operation and current state separately.");

            await File.WriteAllTextAsync(schedulePath, "invalid-json");
            Check((await DrawOk(server.Client, token, original)).Replayed,
                "Committed requests remain recoverable while config is unavailable.");
            using (var unavailable = await Draw(server.Client, token, new(Guid.NewGuid(), "test-role", 1, 3, 9)))
                Check(unavailable.StatusCode == HttpStatusCode.ServiceUnavailable, "New draws fail without valid config.");
            await File.WriteAllTextAsync(schedulePath, JsonSerializer.Serialize(schedule with
                { Pools = [new("test-role", false, null, null), schedule.Pools[1]] }, CloudSaveServer.GachaJson.Options));
            using (var closed = await Draw(server.Client, token, new(Guid.NewGuid(), "test-role", 1, 3, 9)))
                Check(closed.StatusCode == HttpStatusCode.Conflict, "Closed pools cannot start new draws.");
            await File.WriteAllTextAsync(schedulePath, JsonSerializer.Serialize(schedule, CloudSaveServer.GachaJson.Options));
            using (var staleConfig = await Draw(server.Client, token, new(Guid.NewGuid(), "test-weapon", 1, 3, 8)))
                Check(staleConfig.StatusCode == HttpStatusCode.Conflict, "A changed config requires confirmation.");

            var raceA = new GachaDrawRequest(Guid.NewGuid(), "test-weapon", 1, 3, 9);
            var raceB = raceA with { RequestId = Guid.NewGuid() };
            var race = await Task.WhenAll(Draw(server.Client, token, raceA), Draw(server.Client, token, raceB));
            Check(race.Count(r => r.IsSuccessStatusCode) == 1 && race.Count(r => r.StatusCode == HttpStatusCode.Conflict) == 1,
                "Competing devices cannot commit against the same revision.");
            foreach (var response in race) response.Dispose();
            var same = new GachaDrawRequest(Guid.NewGuid(), "test-weapon", 10, 4, 9);
            var duplicates = await Task.WhenAll(DrawOk(server.Client, token, same), DrawOk(server.Client, token, same));
            Check(duplicates.Count(r => r.Replayed) == 1 && store.Load(owner)!.Revision == 5,
                "Concurrent retries grant a batch once.");
            var afterWeapons = JsonNode.Parse(store.Load(owner)!.SaveJson)!["inventory"]!["equips"]!.AsArray();
            Check(afterWeapons.Count == 12 && afterWeapons.Select(e => e!["instanceId"]!.GetValue<long>()).Distinct().Count() == 12,
                "Starter and drawn weapons have unique persisted instance IDs.");
            finalRevision = store.Load(owner)!.Revision;

            var future = DrawSave.Replace("\"version\":8", "\"version\":99");
            await Receipt(await Put(server.Client, otherToken, 0, Guid.NewGuid(), future), 1);
            using (var unsupported = await Draw(server.Client, otherToken, new(Guid.NewGuid(), "test-role", 1, 1, 9)))
                Check(unsupported.StatusCode == HttpStatusCode.Conflict,
                    "Opaque storage accepts future versions, but settlement requires a known layout.");

            string beforeFailure = store.Load(owner)!.SaveJson;
            var broken = new GachaCatalog(Path.Combine(root, "broken-catalog.json"));
            await File.WriteAllTextAsync(Path.Combine(root, "broken-catalog.json"), """
                {"schemaVersion":1,"weapons":[],"characters":[{"key":"furina","weaponType":"Sword","starterWeaponKey":"missing"}]}
                """);
            var badPool = new GachaPoolConfig("broken", 1, [new("furina", 0, 1, 5)]);
            var failedRequest = new GachaDrawRequest(Guid.NewGuid(), "broken", 1, finalRevision, 9);
            bool failed = false;
            try { store.DrawGacha(owner, failedRequest, badPool, broken); } catch (InvalidDataException) { failed = true; }
            Check(failed && store.Load(owner)!.SaveJson == beforeFailure &&
                store.FindGacha(owner, failedRequest.RequestId) == null, "Failed reward creation rolls back state and receipt.");
        }
        await using (var restarted = await RunningServer.Start(databasePath, gachaRoot: root, legacy: false))
        {
            var receipt = await DrawOk(restarted.Client, account.AccessToken, original);
            Check(receipt.Replayed && receipt.State.Revision == finalRevision &&
                receipt.Operation.CommittedRevision == 2, "Restart preserves results and does not reroll old commands.");
        }
        var attentionSave = JsonNode.Parse(DrawSave)!.AsObject();
        long attentionId = 1;
        new GachaCatalog(Path.Combine(root, "gacha-catalog.json")).Grant(attentionSave,
            [new("Sword_Zephyrus", 1, 1, 4), new("Sword_Dawn", 1, 1, 3)], ref attentionId);
        Check(attentionSave["inventory"]!["unseenEquipInstanceIds"]!.AsArray().Count == 1 &&
            attentionSave["inventory"]!["unseenEquipInstanceIds"]![0]!.GetValue<long>() == 1,
            "Only four-star-or-higher awards add weapon attention markers.");
        ValidatePityRules();
        Console.WriteLine("PASS: gacha settlement, starter/duplicate grants, durable retries, concurrency, ownership and legacy-save compatibility.");
    }

    static void ValidatePityRules()
    {
        var entries = new List<GachaEntryConfig> { new("Sword_Dawn",1,1,3), new("Sword_Zephyrus",1,1,4),
            new("eula",0,1,5), new("mona",0,1,5), new("furina",0,1,5) };
        var rules = new CharacterEventRules("shared", .006m, .051m, 10, "eula", .5m);
        var pool = new GachaPoolConfig("a", 90, entries, rules);
        var state = new GachaPity();
        state.EventPityCounters.Add(new EventPity { GroupKey="shared", FiveStarCount=89, FourStarCount=9,
            FeaturedFiveStarGuaranteed=true });
        var guaranteed = GachaRoller.Draw(pool,1,state,max=>max-1);
        Check(guaranteed[0].EntryKey=="eula" && state.EventPityCounters[0].FiveStarCount==0 &&
            state.EventPityCounters[0].FourStarCount==0, "Ninetieth draw wins over simultaneous four-star pity and resets both.");
        state.EventPityCounters[0].FourStarCount=9;
        Check(GachaRoller.Draw(pool,1,state,max=>max-1)[0].Rarity==4 &&
            state.EventPityCounters[0].FiveStarCount==1, "Tenth four-star-or-above pity preserves five-star progress.");
        var sequence = new Queue<int>(new[]{0,int.MaxValue-1,0});
        Check(GachaRoller.Draw(pool,1,state,max=>sequence.Dequeue())[0].EntryKey=="mona" &&
            state.EventPityCounters[0].FeaturedFiveStarGuaranteed, "Losing the featured roll sets the next featured guarantee.");
        var nextPool=pool with {GachaKey="b",EventRules=rules with{FeaturedFiveStarKey="furina"}};
        state.EventPityCounters[0].FiveStarCount=89;
        Check(GachaRoller.Draw(nextPool,1,state,max=>max-1)[0].EntryKey=="furina" &&
            state.EventPityCounters.Count==1, "Pools in one group share the featured guarantee.");
        var legacy = new GachaPity();
        legacy.PityCounters.Add(new PoolPity { GachaKey="CharPool_Eula", Count=20 });
        legacy.PityCounters.Add(new PoolPity { GachaKey="CharPool_Hutao", Count=10 });
        var unified = pool with { GachaKey="CharPool_Eula", EventRules=rules with { PityGroup="CharacterEvent" } };
        GachaRoller.Draw(unified,1,legacy,max=>max-1);
        Check(legacy.EventPityCounters.Single().FiveStarCount==21 && legacy.PityCounters[0].Count==20,
            "An old save inherits the maximum independent character counter once, without summing or deleting history.");
        legacy.EventPityCounters[0].FiveStarCount=1;
        legacy.EventPityCounters[0].FeaturedFiveStarGuaranteed=true;
        GachaRoller.Draw(unified,1,legacy,max=>max-1);
        Check(legacy.EventPityCounters.Single().FiveStarCount==2 &&
            legacy.EventPityCounters.Single().FeaturedFiveStarGuaranteed,
            "Existing shared pity and featured guarantee take precedence over legacy counters.");
        var capped = new GachaPity();
        capped.PityCounters.Add(new PoolPity { GachaKey="CharPool_Hutao", Count=int.MaxValue });
        Check(GachaRoller.Draw(unified,1,capped,max=>max-1).Single().Rarity==5,
            "Oversized legacy counters are safely capped before the first unified draw.");
    }

    static async Task<HttpResponseMessage> Draw(HttpClient client, string? token, GachaDrawRequest value)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post,"/v1/gacha/draw") {Content=JsonContent.Create(value)};
        if(token!=null)request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
        return await client.SendAsync(request);
    }
    static async Task<GachaDrawReply> DrawOk(HttpClient client,string token,GachaDrawRequest value)
    {
        using var response=await Draw(client,token,value);
        Check(response.IsSuccessStatusCode,"Draw failed: "+response.StatusCode+" "+await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<GachaDrawReply>())!;
    }
    static async Task<HttpResponseMessage> FindDraw(HttpClient client,string token,Guid id)
    {
        using var request=new HttpRequestMessage(HttpMethod.Get,"/v1/gacha/requests/"+id);
        request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
        return await client.SendAsync(request);
    }
}
