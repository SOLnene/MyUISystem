using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Promote;

namespace CloudSaveServer;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record PromoteRequest(Guid RequestId, PromotionTarget Target, long ExpectedRevision, string ConfigVersion)
{
    internal bool IsValid => RequestId != Guid.Empty && Target?.IsValid == true && ExpectedRevision is > 0 and < long.MaxValue &&
        ConfigVersion is { Length: 64 } && ConfigVersion.All(Uri.IsHexDigit);
    internal string Fingerprint() => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(this, GachaJson.Options)));
}
internal sealed record PromoteConfig(string ConfigVersion, PromotionRules Rules);
internal sealed record PromoteOperation(Guid RequestId, PromotionTarget Target, string ConfigVersion, long CommittedRevision,
    int Level, int OldRank, int Rank, int OldExp, int Exp, int GoldCost, PromotionMaterial[] Materials);
internal sealed record PromoteReply(PromoteOperation Operation, SaveSnapshot State, bool Replayed);
internal sealed class PromoteFailure(string code, int status = 409, long? revision = null) : Exception(code)
{
    internal string Code { get; } = code;
    internal int Status { get; } = status;
    internal long? Revision { get; } = revision;
}
internal sealed class PromoteConfigStore(string root)
{
    internal async Task<PromoteConfig> ReadAsync(CancellationToken token)
    {
        byte[] bytes = await File.ReadAllBytesAsync(Path.Combine(root,"promotion-config.json"),token);
        var rules = JsonSerializer.Deserialize<PromotionRules>(bytes,GachaJson.Options) ?? throw new InvalidDataException("Missing promotion config.");
        rules.Validate(); return new(Convert.ToHexString(SHA256.HashData(bytes)),rules);
    }
}
internal static class PromoteEndpoints
{
    internal static void Map(WebApplication app)
    {
        var group = app.MapGroup("/v1/progression/promote").RequireAuthorization().RequireRateLimiting("saves");
        group.MapGet("/config",async(HttpContext context,PromoteConfigStore configs) =>
        {
            try { return Results.Ok(await configs.ReadAsync(context.RequestAborted)); }
            catch(Exception error) when(ConfigError(error)) { return Unavailable(context,error); }
        });
        group.MapPost("",async(PromoteRequest request,HttpContext context,SaveStore saves,PromoteConfigStore configs) =>
        {
            if(!request.IsValid) return Failure(context,new("invalid_promote_request",400));
            string player=context.User.FindFirst("sub")!.Value;
            try
            {
                var replay=saves.FindPromotion(player,request.RequestId,request.Fingerprint());
                if(replay!=null)return Results.Ok(replay);
                var config=await configs.ReadAsync(context.RequestAborted);
                if(config.ConfigVersion!=request.ConfigVersion)throw new PromoteFailure("promote_config_changed");
                return Results.Ok(saves.Promote(player,request,config));
            }
            catch(PromoteFailure error){return Failure(context,error);}
            catch(OverflowException){return Failure(context,new("promote_value_overflow",400));}
            catch(Exception error)when(ConfigError(error)){return Unavailable(context,error);}
        });
        group.MapGet("/requests/{requestId:guid}",(Guid requestId,HttpContext context,SaveStore saves) =>
        {
            var reply=saves.FindPromotion(context.User.FindFirst("sub")!.Value,requestId);
            return reply==null?Failure(context,new("promote_request_not_found",404)):Results.Ok(reply);
        });
    }
    static bool ConfigError(Exception error)=>error is IOException or UnauthorizedAccessException or JsonException;
    static IResult Failure(HttpContext context,PromoteFailure error)=>Results.Json(new ApiError(error.Code,
        "Promotion could not be completed.",context.TraceIdentifier,error.Revision),statusCode:error.Status);
    static IResult Unavailable(HttpContext context,Exception error)
    {
        context.RequestServices.GetRequiredService<ILogger<PromoteConfigStore>>().LogError(error,"Promotion configuration unavailable.");
        return Failure(context,new("promote_config_unavailable",503));
    }
}
