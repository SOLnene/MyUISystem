using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Enhance;

namespace CloudSaveServer;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record WeaponEnhanceRequest(Guid RequestId, long WeaponInstanceId, long[] MaterialInstanceIds,
    long ExpectedRevision, string ConfigVersion)
{
    internal bool IsValid => RequestId != Guid.Empty && WeaponInstanceId > 0 && ExpectedRevision is > 0 and < long.MaxValue &&
        MaterialInstanceIds is { Length: > 0 and <= 40 } && MaterialInstanceIds.All(i => i > 0 && i != WeaponInstanceId) &&
        MaterialInstanceIds.Distinct().Count() == MaterialInstanceIds.Length && ConfigVersion is { Length: 64 } && ConfigVersion.All(Uri.IsHexDigit);
    internal string Fingerprint() => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(this, GachaJson.Options)));
}
internal sealed record WeaponEnhanceConfig(string ConfigVersion, WeaponEnhanceRules Rules);
internal sealed record WeaponEnhanceOperation(Guid RequestId, long WeaponInstanceId, long[] MaterialInstanceIds,
    string ConfigVersion, long CommittedRevision, int OldLevel, int OldExp, int Level, int Exp, int GainedExp, int GoldCost);
internal sealed record WeaponEnhanceReply(WeaponEnhanceOperation Operation, SaveSnapshot State, bool Replayed);
internal sealed class WeaponEnhanceFailure(string code, int status = 409, long? revision = null) : Exception(code)
{
    internal string Code { get; } = code;
    internal int Status { get; } = status;
    internal long? Revision { get; } = revision;
}
internal sealed class WeaponEnhanceConfigStore(string root)
{
    internal async Task<WeaponEnhanceConfig> ReadAsync(CancellationToken cancellationToken)
    {
        byte[] bytes = await File.ReadAllBytesAsync(Path.Combine(root, "weapon-enhance-config.json"), cancellationToken);
        var rules = JsonSerializer.Deserialize<WeaponEnhanceRules>(bytes, GachaJson.Options)
            ?? throw new InvalidDataException("Missing enhancement rules.");
        rules.Validate();
        return new(Convert.ToHexString(SHA256.HashData(bytes)), rules);
    }
}
internal static class WeaponEnhanceEndpoints
{
    internal static void Map(WebApplication app)
    {
        var group = app.MapGroup("/v1/weapons/enhance").RequireAuthorization().RequireRateLimiting("saves");
        group.MapGet("/config", async (HttpContext context, WeaponEnhanceConfigStore configs) =>
        {
            try { return Results.Ok(await configs.ReadAsync(context.RequestAborted)); }
            catch (Exception error) when (ConfigError(error)) { return Unavailable(context, error); }
        });
        group.MapPost("", async (WeaponEnhanceRequest request, HttpContext context, SaveStore saves, WeaponEnhanceConfigStore configs) =>
        {
            if (!request.IsValid) return Failure(context, new("invalid_enhance_request", 400));
            string player = context.User.FindFirst("sub")!.Value;
            try
            {
                // Receipt lookup precedes current config checks: recovery still works after a rules change or outage.
                var replay = saves.FindWeaponEnhancement(player, request.RequestId, request.Fingerprint());
                if (replay != null) return Results.Ok(replay);
                var config = await configs.ReadAsync(context.RequestAborted);
                if (request.ConfigVersion != config.ConfigVersion) throw new WeaponEnhanceFailure("enhance_config_changed");
                return Results.Ok(saves.EnhanceWeapon(player, request, config));
            }
            catch (WeaponEnhanceFailure error) { return Failure(context, error); }
            catch (OverflowException) { return Failure(context, new("enhance_value_overflow", 400)); }
            catch (Exception error) when (ConfigError(error)) { return Unavailable(context, error); }
        });
        group.MapGet("/requests/{requestId:guid}", (Guid requestId, HttpContext context, SaveStore saves) =>
        {
            var receipt = saves.FindWeaponEnhancement(context.User.FindFirst("sub")!.Value, requestId);
            return receipt == null ? Failure(context, new("enhance_request_not_found", 404)) : Results.Ok(receipt);
        });
    }
    static bool ConfigError(Exception error) => error is IOException or UnauthorizedAccessException or JsonException;
    static IResult Failure(HttpContext context, WeaponEnhanceFailure error) => Results.Json(new ApiError(error.Code,
        "Weapon enhancement could not be completed.", context.TraceIdentifier, error.Revision), statusCode: error.Status);
    static IResult Unavailable(HttpContext context, Exception error)
    {
        context.RequestServices.GetRequiredService<ILogger<WeaponEnhanceConfigStore>>().LogError(error, "Enhancement configuration unavailable.");
        return Failure(context, new("enhance_config_unavailable", 503));
    }
}
