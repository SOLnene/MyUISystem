using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CloudSaveServer;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record StorePurchaseRequest(Guid RequestId, int StoreItemId, int Count,
    long ExpectedRevision, string ConfigVersion)
{
    internal bool IsValid => RequestId != Guid.Empty && StoreItemId > 0 && Count is > 0 and <= 1000000 &&
        ExpectedRevision is > 0 and < long.MaxValue && ConfigVersion is { Length: 64 } &&
        ConfigVersion.All(Uri.IsHexDigit);
    internal string Fingerprint() => Convert.ToHexString(SHA256.HashData(
        JsonSerializer.SerializeToUtf8Bytes(this, GachaJson.Options)));
}
internal sealed record StoreOperation(Guid RequestId, int StoreItemId, int Count, string ConfigVersion,
    long CommittedRevision, int ItemId, int GrantedAmount, int CostItemId, int TotalPrice);
internal sealed record StorePurchaseReply(StoreOperation Operation, SaveSnapshot State, bool Replayed);
internal sealed class StoreFailure(string code, int status = 409, long? revision = null) : Exception(code)
{
    internal string Code { get; } = code;
    internal int Status { get; } = status;
    internal long? Revision { get; } = revision;
}

internal static class StoreEndpoints
{
    internal static void Map(WebApplication app)
    {
        var group = app.MapGroup("/v1/store").RequireAuthorization().RequireRateLimiting("saves");
        group.MapGet("", async (HttpContext context, SaveStore saves, StoreConfigStore configs, TimeProvider clock) =>
        {
            try
            {
                var config = await configs.ReadAsync(context.RequestAborted);
                return Results.Ok(saves.ReadStore(context.User.FindFirst("sub")!.Value, config, clock.GetUtcNow()));
            }
            catch (StoreFailure error) { return Failure(context, error); }
            catch (Exception error) when (ConfigurationError(error)) { return Unavailable(context, error); }
        });
        group.MapPost("/purchase", async (StorePurchaseRequest request, HttpContext context, SaveStore saves,
            StoreConfigStore configs, TimeProvider clock) =>
        {
            if (!request.IsValid) return Failure(context, new StoreFailure("invalid_store_request", 400));
            string player = context.User.FindFirst("sub")!.Value;
            try
            {
                // A retry recovers the original transaction even after a refresh, price change or delisting.
                var replay = saves.FindStorePurchase(player, request.RequestId, request.Fingerprint());
                if (replay != null) return Results.Ok(replay);
                var config = await configs.ReadAsync(context.RequestAborted);
                if (config.Token != request.ConfigVersion) throw new StoreFailure("store_config_changed");
                var catalog = await configs.ReadCatalogAsync(context.RequestAborted);
                return Results.Ok(saves.PurchaseStore(player, request, config, catalog, clock.GetUtcNow()));
            }
            catch (StoreFailure error) { return Failure(context, error); }
            catch (OverflowException) { return Failure(context, new StoreFailure("store_quantity_too_large", 400)); }
            catch (Exception error) when (ConfigurationError(error)) { return Unavailable(context, error); }
        });
        group.MapGet("/requests/{requestId:guid}", (Guid requestId, HttpContext context, SaveStore saves) =>
        {
            var receipt = saves.FindStorePurchase(context.User.FindFirst("sub")!.Value, requestId);
            return receipt == null ? Failure(context, new StoreFailure("store_request_not_found", 404)) : Results.Ok(receipt);
        });
    }
    static bool ConfigurationError(Exception error) => error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException;
    static IResult Failure(HttpContext context, StoreFailure error) => Results.Json(new ApiError(error.Code,
        "Store request could not be completed.", context.TraceIdentifier, error.Revision), statusCode: error.Status);
    static IResult Unavailable(HttpContext context, Exception error)
    {
        context.RequestServices.GetRequiredService<ILogger<StoreConfigStore>>().LogError(error, "Store configuration unavailable.");
        return Failure(context, new StoreFailure("store_config_unavailable", 503));
    }
}
