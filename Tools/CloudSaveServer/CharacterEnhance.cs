using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Enhance;

namespace CloudSaveServer;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record CharacterEnhanceRequest(Guid RequestId, string CharacterKey, CharacterEnhanceBookSelection[] Books,
    long ExpectedRevision, string ConfigVersion)
{
    internal bool IsValid => RequestId != Guid.Empty && !string.IsNullOrWhiteSpace(CharacterKey) && CharacterKey.Length <= 128 &&
        ExpectedRevision is > 0 and < long.MaxValue && ConfigVersion is { Length: 64 } && ConfigVersion.All(Uri.IsHexDigit) &&
        Books is { Length: > 0 and <= 3 } && Books.All(b => b != null && b.ItemId is >= 501 and <= 503 && b.Count is > 0 and <= 100000) &&
        Books.Select(b => b.ItemId).Distinct().Count() == Books.Length;
    internal string Fingerprint() => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(this, GachaJson.Options)));
}
internal sealed record CharacterEnhanceConfig(string ConfigVersion, CharacterEnhanceRules Rules);
internal sealed record CharacterEnhanceOperation(Guid RequestId, string CharacterKey, CharacterEnhanceBookSelection[] Books,
    string ConfigVersion, long CommittedRevision, int OldLevel, int OldExp, int Level, int Exp, int GainedExp, int GoldCost);
internal sealed record CharacterEnhanceReply(CharacterEnhanceOperation Operation, SaveSnapshot State, bool Replayed);
internal sealed class CharacterEnhanceFailure(string code, int status = 409, long? revision = null) : Exception(code)
{
    internal string Code { get; } = code;
    internal int Status { get; } = status;
    internal long? Revision { get; } = revision;
}
internal sealed class CharacterEnhanceConfigStore(string root)
{
    internal async Task<CharacterEnhanceConfig> ReadAsync(CancellationToken token)
    {
        byte[] bytes = await File.ReadAllBytesAsync(Path.Combine(root, "character-enhance-config.json"), token);
        var rules = JsonSerializer.Deserialize<CharacterEnhanceRules>(bytes, GachaJson.Options)
            ?? throw new InvalidDataException("Missing character enhancement rules.");
        rules.Validate();
        return new(Convert.ToHexString(SHA256.HashData(bytes)), rules);
    }
}
internal static class CharacterEnhanceEndpoints
{
    internal static void Map(WebApplication app)
    {
        var group = app.MapGroup("/v1/characters/enhance").RequireAuthorization().RequireRateLimiting("saves");
        group.MapGet("/config", async (HttpContext context, CharacterEnhanceConfigStore configs) =>
        {
            try { return Results.Ok(await configs.ReadAsync(context.RequestAborted)); }
            catch (Exception error) when (ConfigError(error)) { return Unavailable(context, error); }
        });
        group.MapPost("", async (CharacterEnhanceRequest request, HttpContext context, SaveStore saves, CharacterEnhanceConfigStore configs) =>
        {
            if (!request.IsValid) return Failure(context, new("invalid_character_enhance_request", 400));
            string player = context.User.FindFirst("sub")!.Value;
            try
            {
                // Recover committed receipts before reading today's rules; config changes must not prevent recovery.
                var replay = saves.FindCharacterEnhancement(player, request.RequestId, request.Fingerprint());
                if (replay != null) return Results.Ok(replay);
                var config = await configs.ReadAsync(context.RequestAborted);
                if (request.ConfigVersion != config.ConfigVersion) throw new CharacterEnhanceFailure("character_enhance_config_changed");
                return Results.Ok(saves.EnhanceCharacter(player, request, config));
            }
            catch (CharacterEnhanceFailure error) { return Failure(context, error); }
            catch (OverflowException) { return Failure(context, new("character_enhance_value_overflow", 400)); }
            catch (Exception error) when (ConfigError(error)) { return Unavailable(context, error); }
        });
        group.MapGet("/requests/{requestId:guid}", (Guid requestId, HttpContext context, SaveStore saves) =>
        {
            var reply = saves.FindCharacterEnhancement(context.User.FindFirst("sub")!.Value, requestId);
            return reply == null ? Failure(context, new("character_enhance_request_not_found", 404)) : Results.Ok(reply);
        });
    }
    static bool ConfigError(Exception error) => error is IOException or UnauthorizedAccessException or JsonException;
    static IResult Failure(HttpContext context, CharacterEnhanceFailure error) => Results.Json(new ApiError(error.Code,
        "Character enhancement could not be completed.", context.TraceIdentifier, error.Revision), statusCode: error.Status);
    static IResult Unavailable(HttpContext context, Exception error)
    {
        context.RequestServices.GetRequiredService<ILogger<CharacterEnhanceConfigStore>>().LogError(error, "Character enhancement configuration unavailable.");
        return Failure(context, new("character_enhance_config_unavailable", 503));
    }
}
