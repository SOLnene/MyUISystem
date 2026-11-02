using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Enhance;

namespace CloudSaveServer;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record WeaponRefineRequest(Guid RequestId, long WeaponInstanceId, long[] MaterialInstanceIds,
    long ExpectedRevision, string ConfigVersion)
{
    internal bool IsValid => RequestId != Guid.Empty && WeaponInstanceId > 0 && ExpectedRevision is > 0 and < long.MaxValue &&
        MaterialInstanceIds is { Length: > 0 and <= 4 } && MaterialInstanceIds.All(i => i > 0 && i != WeaponInstanceId) &&
        MaterialInstanceIds.Distinct().Count() == MaterialInstanceIds.Length && ConfigVersion is { Length: 64 } && ConfigVersion.All(Uri.IsHexDigit);
    internal string Fingerprint() => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(this, GachaJson.Options)));
}
internal sealed record WeaponRefineConfig(string ConfigVersion, WeaponRefineRules Rules);
internal sealed record WeaponRefineOperation(Guid RequestId, long WeaponInstanceId, long[] MaterialInstanceIds,
    string ConfigVersion, long CommittedRevision, int OldRefinementLevel, int RefinementLevel, int GoldCost);
internal sealed record WeaponRefineReply(WeaponRefineOperation Operation, SaveSnapshot State, bool Replayed);
internal sealed class WeaponRefineFailure(string code, int status = 409, long? revision = null) : Exception(code)
{
    internal string Code { get; } = code;
    internal int Status { get; } = status;
    internal long? Revision { get; } = revision;
}
internal sealed class WeaponRefineConfigStore(string root)
{
    internal async Task<WeaponRefineConfig> ReadAsync(CancellationToken cancellationToken)
    {
        byte[] bytes = await File.ReadAllBytesAsync(Path.Combine(root, "weapon-refine-config.json"), cancellationToken);
        var rules = JsonSerializer.Deserialize<WeaponRefineRules>(bytes, GachaJson.Options)
            ?? throw new InvalidDataException("Missing refinement rules.");
        rules.Validate();
        return new(Convert.ToHexString(SHA256.HashData(bytes)), rules);
    }
}
internal static class WeaponRefineEndpoints
{
    internal static void Map(WebApplication app)
    {
        var group = app.MapGroup("/v1/weapons/refine").RequireAuthorization().RequireRateLimiting("saves");
        group.MapGet("/config", async (HttpContext context, WeaponRefineConfigStore configs) =>
        {
            try { return Results.Ok(await configs.ReadAsync(context.RequestAborted)); }
            catch (Exception error) when (ConfigError(error)) { return Unavailable(context, error); }
        });
        group.MapPost("", async (WeaponRefineRequest request, HttpContext context, SaveStore saves, WeaponRefineConfigStore configs) =>
        {
            if (!request.IsValid) return Failure(context, new("invalid_refine_request", 400));
            string player = context.User.FindFirst("sub")!.Value;
            try
            {
                // Resolve an existing receipt before reading current rules, including after a config outage.
                var replay = saves.FindWeaponRefinement(player, request.RequestId, request.Fingerprint());
                if (replay != null) return Results.Ok(replay);
                var config = await configs.ReadAsync(context.RequestAborted);
                if (request.ConfigVersion != config.ConfigVersion) throw new WeaponRefineFailure("refine_config_changed");
                return Results.Ok(saves.RefineWeapon(player, request, config));
            }
            catch (WeaponRefineFailure error) { return Failure(context, error); }
            catch (OverflowException) { return Failure(context, new("refine_value_overflow", 400)); }
            catch (Exception error) when (ConfigError(error)) { return Unavailable(context, error); }
        });
        group.MapGet("/requests/{requestId:guid}", (Guid requestId, HttpContext context, SaveStore saves) =>
        {
            var receipt = saves.FindWeaponRefinement(context.User.FindFirst("sub")!.Value, requestId);
            return receipt == null ? Failure(context, new("refine_request_not_found", 404)) : Results.Ok(receipt);
        });
    }
    static bool ConfigError(Exception error) => error is IOException or UnauthorizedAccessException or JsonException or InvalidDataException;
    static IResult Failure(HttpContext context, WeaponRefineFailure error) => Results.Json(new ApiError(error.Code,
        "Weapon refinement could not be completed.", context.TraceIdentifier, error.Revision), statusCode: error.Status);
    static IResult Unavailable(HttpContext context, Exception error)
    {
        context.RequestServices.GetRequiredService<ILogger<WeaponRefineConfigStore>>().LogError(error, "Refinement configuration unavailable.");
        return Failure(context, new("refine_config_unavailable", 503));
    }
}
