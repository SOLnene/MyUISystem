using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CloudSaveServer;
using Microsoft.Extensions.DependencyInjection;

internal static partial class Program
{
    static async Task ValidateGachaPayments(string root)
    {
        Directory.CreateDirectory(root);
        string source = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        foreach (var file in Directory.GetFiles(source, "gacha-*.json"))
            File.Copy(file, Path.Combine(root, Path.GetFileName(file)));
        await File.WriteAllTextAsync(Path.Combine(root, "gacha-schedule.json"), JsonSerializer.Serialize(
            new GachaSchedule(7, [new("CharPool_Furina", true, null, null), new("WeaponPool_01", true, null, null)]), GachaJson));
        var config = await new GachaConfigStore(root).ReadCurrentAsync(DateTimeOffset.UtcNow, default);
        Check(config.Pools[0].Cost == new GachaPaymentConfig(223,160) &&
            config.Pools[1].Cost == new GachaPaymentConfig(224,160), "Config publishes the correct fate and conversion price.");
        await File.WriteAllTextAsync(Path.Combine(root, "payment-display-config.json"), JsonSerializer.Serialize(config, GachaJson));
        string commonPath = Path.Combine(root, "gacha-common-character-event.json");
        string commonJson = await File.ReadAllTextAsync(commonPath);
        var shared = JsonNode.Parse(commonJson)!;
        var ordinarySource = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(root, "gacha-WeaponPool_01.json")))!;
        Check(shared["cost"]?["fateItemId"]?.GetValue<int>() == 223 &&
            ordinarySource["cost"]?["fateItemId"]?.GetValue<int>() == 224,
            "Authoring files explicitly declare their fate instead of relying on code defaults.");
        string schedulePath = Path.Combine(root, "gacha-schedule.json");
        string scheduleJson = await File.ReadAllTextAsync(schedulePath);
        try
        {
            await File.WriteAllTextAsync(schedulePath, JsonSerializer.Serialize(new GachaSchedule(7,
                [new("CharPool_Furina", true, null, null), new("CharPool_Citlali", true, null, null),
                 new("WeaponPool_01", true, null, null)]), GachaJson));
            shared["cost"]!["primogemsPerFate"] = 200;
            await File.WriteAllTextAsync(commonPath, shared.ToJsonString());
            var updated = await new GachaConfigStore(root).ReadCurrentAsync(DateTimeOffset.UtcNow, default);
            Check(updated.Pools.Take(2).All(pool => pool.Cost == new GachaPaymentConfig(223, 200)),
                "All event pools inherit a shared payment update even without an end date.");
            Check(updated.Pools[2].Cost == new GachaPaymentConfig(224, 160),
                "Updating the event rule leaves the ordinary pool payment unchanged.");
            shared["cost"]!["fateItemId"] = 201;
            await File.WriteAllTextAsync(commonPath, shared.ToJsonString());
            bool rejected = false;
            try { await new GachaConfigStore(root).ReadCurrentAsync(DateTimeOffset.UtcNow, default); }
            catch (InvalidDataException) { rejected = true; }
            Check(rejected, "An invalid shared fate cannot be published to the client.");
        }
        finally
        {
            await File.WriteAllTextAsync(commonPath, commonJson);
            await File.WriteAllTextAsync(schedulePath, scheduleJson);
        }
        foreach (var cost in new[] { new GachaPaymentConfig(0,160),new(202,160),new(223,0),new(224,int.MaxValue) })
        {
            bool failed=false;try{cost.Validate();}catch(InvalidDataException){failed=true;}
            Check(failed,"Invalid currency or conversion price is rejected.");
        }
        var oldRequest = new GachaDrawRequest(Guid.NewGuid(),"legacy",1,1,7);
        string legacyJson = JsonSerializer.Serialize(new { oldRequest.RequestId, oldRequest.GachaKey, oldRequest.Count,
            oldRequest.ExpectedRevision, oldRequest.ConfigVersion }, GachaJson);
        Check(oldRequest.Fingerprint()==Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(legacyJson))),
            "Absent payment fields preserve pre-upgrade request fingerprints.");

        string db=Path.Combine(root,"payments.db");
        GachaDrawRequest recoveredRequest=null!;AccountLoginReply recoveredAccount=null!;string settledJson="";
        await using(var server=await RunningServer.Start(db,gachaRoot:root,legacy:false))
        {
            var accounts=server.App.Services.GetRequiredService<AccountStore>();
            var store=server.App.Services.GetRequiredService<SaveStore>();
            async Task<AccountLoginReply> Account(string name,int fates,int primogems,int standard=20)
            {
                _=accounts.Register(new(name,"abcdefgh"));var account=accounts.Login(new(name,"abcdefgh"))!;
                var save=JsonNode.Parse(DrawSave)!;
                foreach(var item in save["currencies"]!["items"]!.AsArray())
                {
                    int id=item!["itemId"]!.GetValue<int>();
                    if(id==201)item["amount"]=primogems;if(id==223)item["amount"]=fates;if(id==224)item["amount"]=standard;
                }
                await Receipt(await Put(server.Client,account.AccessToken,0,Guid.NewGuid(),save.ToJsonString()),1);
                return account;
            }
            async Task Reject(AccountLoginReply account,GachaDrawRequest request,string code,HttpStatusCode status=HttpStatusCode.Conflict)
            {
                string owner="local:"+account.Uid;var before=store.Load(owner)!;
                string priorReceipt=JsonSerializer.Serialize(store.FindGacha(owner,request.RequestId));
                using var response=await Draw(server.Client,account.AccessToken,request);
                var body=JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
                Check(response.StatusCode==status && body["code"]!.GetValue<string>()==code,"Expected payment failure: "+code);
                Check(store.Load(owner)==before && JsonSerializer.Serialize(store.FindGacha(owner,request.RequestId))==priorReceipt,
                    "Rejected payment leaves wallet, rewards, pity, revision and receipt untouched.");
            }
            var ticket=await Account("payment-ticket",10,0);
            using (var request = new HttpRequestMessage(HttpMethod.Get, "/v1/gacha/config"))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ticket.AccessToken);
                using var response = await server.Client.SendAsync(request);
                var published = await response.Content.ReadFromJsonAsync<GachaConfigSnapshot>();
                Check(response.IsSuccessStatusCode && published!.Pools[0].Cost == config.Pools[0].Cost &&
                    published.Pools[1].Cost == config.Pools[1].Cost,
                    "The client endpoint publishes resolved event and ordinary payment rules.");
            }
            var ticketRequest=new GachaDrawRequest(Guid.NewGuid(),"CharPool_Furina",10,1,7,1);
            var ticketReply=await DrawOk(server.Client,ticket.AccessToken,ticketRequest);
            Check(ticketReply.Operation.Payment is {FatesSpent:10,PrimogemsSpent:0,FatesRemaining:0,PrimogemsRemaining:0},
                "Enough fates require no Primogems or exchange confirmation.");
            var replay=await DrawOk(server.Client,ticket.AccessToken,ticketRequest);
            Check(replay.Replayed && replay.Operation.Payment==ticketReply.Operation.Payment && replay.State==ticketReply.State,
                "A duplicate ticket request never charges twice.");
            await Reject(ticket,ticketRequest with {MaxPrimogemCost=160},"request_id_reused");

            var mixed=await Account("payment-mixed",3,2000);
            var mixedRequest=new GachaDrawRequest(Guid.NewGuid(),"CharPool_Furina",10,1,7,1,1120);
            var mixedReply=await DrawOk(server.Client,mixed.AccessToken,mixedRequest);
            Check(mixedReply.Operation.Payment is {FateItemId:223,FatesSpent:3,PrimogemsSpent:1120,FatesRemaining:0,PrimogemsRemaining:880},
                "Only the seven-fate shortfall converts at 160 Primogems each.");
            recoveredRequest=mixedRequest;recoveredAccount=mixed;settledJson=mixedReply.State.SaveJson;

            var consent=await Account("payment-consent",3,2000);
            await Reject(consent,new(Guid.NewGuid(),"CharPool_Furina",10,1,7,1),"gacha_payment_confirmation_required");
            await Reject(consent,new(Guid.NewGuid(),"CharPool_Furina",10,1,7,1,1119),"gacha_payment_confirmation_required");
            var poor=await Account("payment-poor",3,1119);
            await Reject(poor,new(Guid.NewGuid(),"CharPool_Furina",10,1,7,1,1120),"gacha_insufficient_currency");
            var empty=await Account("payment-empty",0,0,100);
            await Reject(empty,new(Guid.NewGuid(),"CharPool_Furina",1,1,7,1,160),"gacha_insufficient_currency");
            var standard=await DrawOk(server.Client,empty.AccessToken,new(Guid.NewGuid(),"WeaponPool_01",1,1,7,1));
            Check(standard.Operation.Payment is {FateItemId:224,FatesSpent:1,FatesRemaining:99},
                "The ordinary weapon pool consumes Acquaint Fate instead of event fates.");

            var oldClient=await Account("payment-old-client",20,2000);
            using(var request=new HttpRequestMessage(HttpMethod.Post,"/v1/gacha/draw")
                {Content=JsonContent.Create(new GachaDrawRequest(Guid.NewGuid(),"CharPool_Furina",1,1,7))})
            {
                request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",oldClient.AccessToken);
                using var response=await server.Client.SendAsync(request);
                Check(response.StatusCode==HttpStatusCode.Conflict &&
                    (await response.Content.ReadAsStringAsync()).Contains("gacha_client_upgrade_required"),
                    "An old client cannot silently start a paid draw.");
                Check(store.Load("local:"+oldClient.Uid)!.Revision==1,"An unsupported client cannot charge the wallet.");
            }
            foreach(bool duplicate in new[]{false,true})
            {
                var invalid=await Account(duplicate?"payment-duplicate":"payment-negative",10,2000);
                var state=JsonNode.Parse(store.Load("local:"+invalid.Uid)!.SaveJson)!;
                var items=state["currencies"]!["items"]!.AsArray();
                if(duplicate)items.Add(items.First(x=>x!["itemId"]!.GetValue<int>()==223)!.DeepClone());
                else items.First(x=>x!["itemId"]!.GetValue<int>()==223)!["amount"]=-1;
                await Receipt(await Put(server.Client,invalid.AccessToken,1,Guid.NewGuid(),state.ToJsonString()),2);
                await Reject(invalid,new(Guid.NewGuid(),"CharPool_Furina",1,2,7,1),"invalid_gacha_currency",HttpStatusCode.BadRequest);
            }
        }
        await using(var restarted=await RunningServer.Start(db,gachaRoot:root,legacy:false))
        {
            var reply=await DrawOk(restarted.Client,recoveredAccount.AccessToken,recoveredRequest);
            Check(reply.Replayed && reply.State.SaveJson==settledJson && reply.Operation.Payment!.PrimogemsSpent==1120,
                "Restart and lost-response recovery preserve the original mixed payment exactly.");
        }
        Console.WriteLine("PASS: gacha ticket/Primogem payment, consent ceilings, no-charge failures, legacy fingerprint and restart recovery.");
    }
}
