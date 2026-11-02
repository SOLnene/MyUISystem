using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CloudSaveServer;
using Microsoft.Extensions.DependencyInjection;

internal static partial class Program
{
    static async Task ValidateSocial(string root)
    {
        Directory.CreateDirectory(root); string path = Path.Combine(root, "social.db");
        string configs = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        string alice, bob, carol;
        await using (var server = await RunningServer.Start(path, gachaRoot: configs, legacy: false))
        {
            var accounts = server.App.Services.GetRequiredService<AccountStore>(); var social = server.App.Services.GetRequiredService<SocialStore>();
            alice = accounts.Register(new("social_alice", "abcdefgh"))!.Uid;
            bob = accounts.Register(new("social_bob", "abcdefgh"))!.Uid;
            carol = accounts.Register(new("social_carol", "abcdefgh"))!.Uid;
            var a = accounts.Login(new("social_alice", "abcdefgh"))!; var b = accounts.Login(new("social_bob", "abcdefgh"))!;
            async Task<HttpResponseMessage> Send(HttpMethod method, string route, string? token, object? body = null)
            {
                using var request = new HttpRequestMessage(method, route);
                if (token != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                if (body != null) request.Content = JsonContent.Create(body);
                return await server.Client.SendAsync(request);
            }
            async Task CheckStatus(HttpMethod method, string route, string? token, object? body, HttpStatusCode expected)
            { using var response = await Send(method, route, token, body); Check(response.StatusCode == expected, route + " expected " + expected + ", actual " + response.StatusCode + ": " + await response.Content.ReadAsStringAsync()); }
            await CheckStatus(HttpMethod.Get,"/v1/friends",null,null,HttpStatusCode.Unauthorized);
            Check(social.List(long.Parse(alice)).Friends.Length == 0, "New account has no fabricated friends.");
            await CheckStatus(HttpMethod.Get,"/v1/players/abc",a.AccessToken,null,HttpStatusCode.BadRequest);
            await CheckStatus(HttpMethod.Get,"/v1/players/999999999999",a.AccessToken,null,HttpStatusCode.NotFound);
            using(var response=await Send(HttpMethod.Get,"/v1/players/"+bob,a.AccessToken))
            {
                string json=await response.Content.ReadAsStringAsync();Check(response.IsSuccessStatusCode&&json.Contains("social_bob")&&!json.Contains("password")&&!json.Contains("accessToken"),"Lookup returns only public identity.");
            }
            await CheckStatus(HttpMethod.Post,"/v1/friends/requests",a.AccessToken,new{uid=alice},HttpStatusCode.BadRequest);
            await CheckStatus(HttpMethod.Get,"/v1/friends/"+bob+"/messages",a.AccessToken,null,HttpStatusCode.Forbidden);
            await CheckStatus(HttpMethod.Post,"/v1/friends/requests",a.AccessToken,new{uid=bob},HttpStatusCode.OK);
            await CheckStatus(HttpMethod.Post,"/v1/friends/requests",a.AccessToken,new{uid=bob},HttpStatusCode.OK);
            Check(social.List(long.Parse(bob)).Requests.Length==1,"Repeated application is stored once.");
            await CheckStatus(HttpMethod.Post,"/v1/friends/requests/"+bob+"/respond",a.AccessToken,new{accept=true},HttpStatusCode.NotFound);
            await CheckStatus(HttpMethod.Post,"/v1/friends/requests/"+alice+"/respond",b.AccessToken,new{accept=false},HttpStatusCode.OK);
            Check(social.List(long.Parse(bob)).Requests.Length==0&&social.List(long.Parse(alice)).Friends.Length==0,"Declining creates no relationship.");
            await CheckStatus(HttpMethod.Post,"/v1/friends/requests",a.AccessToken,new{uid=bob},HttpStatusCode.OK);
            await CheckStatus(HttpMethod.Post,"/v1/friends/requests/"+alice+"/respond",b.AccessToken,new{accept=true},HttpStatusCode.OK);
            await CheckStatus(HttpMethod.Post,"/v1/friends/requests/"+alice+"/respond",b.AccessToken,new{accept=true},HttpStatusCode.OK);
            Check(social.List(long.Parse(alice)).Friends.Single().Uid==bob&&social.List(long.Parse(bob)).Friends.Single().Uid==alice,"Accept atomically creates both directions.");
            string id=Guid.NewGuid().ToString("D");long messageId;
            using(var response=await Send(HttpMethod.Post,"/v1/friends/"+bob+"/messages",a.AccessToken,new{clientId=id,text="你好，测试长消息 <b>原样文本</b>"}))
            { Check(response.IsSuccessStatusCode,"Friend message accepted.");messageId=JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetInt64(); }
            await CheckStatus(HttpMethod.Post,"/v1/friends/"+bob+"/messages",a.AccessToken,new{clientId=id,text="你好，测试长消息 <b>原样文本</b>"},HttpStatusCode.OK);
            await CheckStatus(HttpMethod.Post,"/v1/friends/"+bob+"/messages",a.AccessToken,new{clientId=id,text="changed"},HttpStatusCode.Conflict);
            Check(social.Messages(long.Parse(bob),long.Parse(alice),0).Length==1,"Lost response retry cannot duplicate a message.");
            await CheckStatus(HttpMethod.Post,"/v1/friends/"+bob+"/messages",a.AccessToken,new{clientId=Guid.NewGuid().ToString("D"),text=" "},HttpStatusCode.BadRequest);
            await CheckStatus(HttpMethod.Post,"/v1/friends/"+bob+"/messages",a.AccessToken,new{clientId=Guid.NewGuid().ToString("D"),text=new string('x',1001)},HttpStatusCode.BadRequest);
            var reply=social.Send(long.Parse(bob),long.Parse(alice),Guid.NewGuid().ToString("D"),"收到");
            Check(reply.Id>messageId&&social.Messages(long.Parse(alice),long.Parse(bob),messageId).Single().Text=="收到","Incremental history returns ordered new messages.");
            try{social.Messages(long.Parse(carol),long.Parse(alice),0);Check(false,"Unrelated account must not read chat");}catch(SocialFailure e){Check(e.Status==403,"Third-party access denied.");}
        }
        await using(var server=await RunningServer.Start(path,gachaRoot:configs,legacy:false))
        {
            var social=server.App.Services.GetRequiredService<SocialStore>();
            Check(social.List(long.Parse(alice)).Friends.Single().Uid==bob,"Friendship survives restart.");
            Check(social.Messages(long.Parse(alice),long.Parse(bob),0).Length==2,"Both messages survive restart.");
        }
        Console.WriteLine("PASS: real-account lookup, friendship requests/accept/reject, private messages, duplicate suppression and restart persistence.");
    }
}
