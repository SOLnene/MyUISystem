using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace CloudSaveServer;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record GachaDrawRequest(Guid RequestId, string GachaKey, int Count, long ExpectedRevision, long ConfigVersion)
{
    internal bool IsValid => RequestId != Guid.Empty && GachaKey != null &&
        Regex.IsMatch(GachaKey, "\\A[A-Za-z0-9_-]{1,64}\\z") &&
        Count is 1 or 10 && ExpectedRevision is > 0 and < long.MaxValue && ConfigVersion > 0;

    internal string Fingerprint() => Convert.ToHexString(SHA256.HashData(
        JsonSerializer.SerializeToUtf8Bytes(this, GachaJson.Options)));
}

internal sealed record GachaAward(string EntryKey, int EntryType, int Rarity, long InstanceId, bool NewCharacter);
internal sealed record GachaOperation(Guid RequestId, string GachaKey, int Count, long ConfigVersion,
    long CommittedRevision, string CreatedAtUtc, List<GachaAward> Results);
internal sealed record GachaDrawReply(GachaOperation Operation, SaveSnapshot State, bool Replayed);
internal sealed class GachaFailure(string code, int status = 409, long? revision = null) : Exception(code)
{
    internal string Code { get; } = code;
    internal int Status { get; } = status;
    internal long? Revision { get; } = revision;
}

internal static class GachaJson
{
    internal static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { MaxDepth = 64 };
}

internal sealed class GachaPity
{
    public List<PoolPity> PityCounters { get; set; } = [];
    public List<EventPity> EventPityCounters { get; set; } = [];

    internal static GachaPity Parse(string json)
    {
        var state = JsonSerializer.Deserialize<GachaPity>(json, GachaJson.Options)
            ?? throw new GachaFailure("invalid_gacha_state", 400);
        if (state.PityCounters == null || state.EventPityCounters == null ||
            state.PityCounters.Any(p => p == null || string.IsNullOrWhiteSpace(p.GachaKey) || p.Count < 0) ||
            state.EventPityCounters.Any(p => p == null || string.IsNullOrWhiteSpace(p.GroupKey) || p.FiveStarCount < 0 || p.FourStarCount < 0) ||
            state.PityCounters.Select(p => p.GachaKey).Distinct(StringComparer.Ordinal).Count() != state.PityCounters.Count ||
            state.EventPityCounters.Select(p => p.GroupKey).Distinct(StringComparer.Ordinal).Count() != state.EventPityCounters.Count)
            throw new GachaFailure("invalid_gacha_state", 400);
        return state;
    }

    internal string Canonical() => JsonSerializer.Serialize(new GachaPity
    {
        PityCounters = PityCounters.OrderBy(p => p.GachaKey, StringComparer.Ordinal).ToList(),
        EventPityCounters = EventPityCounters.OrderBy(p => p.GroupKey, StringComparer.Ordinal).ToList()
    }, GachaJson.Options);
}
internal sealed class PoolPity
{
    public string GachaKey { get; set; } = "";
    public int Count { get; set; }
}
internal sealed class EventPity
{
    public string GroupKey { get; set; } = "";
    public int FiveStarCount { get; set; }
    public int FourStarCount { get; set; }
    public bool FeaturedFiveStarGuaranteed { get; set; }
}

internal static class GachaRoller
{
    internal static List<GachaEntryConfig> Draw(GachaPoolConfig pool, int count, GachaPity state,
        Func<int, int>? next = null)
    {
        next ??= RandomNumberGenerator.GetInt32;
        var output = new List<GachaEntryConfig>(count);
        var rules = pool.EventRules;
        var ordinary = state.PityCounters.SingleOrDefault(p => p.GachaKey == pool.GachaKey);
        var shared = rules == null ? null : state.EventPityCounters.SingleOrDefault(p => p.GroupKey == rules.PityGroup);
        if (rules == null && ordinary == null)
        {
            ordinary = new PoolPity { GachaKey = pool.GachaKey };
            state.PityCounters.Add(ordinary);
        }
        if (rules != null && shared == null)
        {
            int inherited = rules.PityGroup == "CharacterEvent"
                ? state.PityCounters.Where(p => p.GachaKey.StartsWith("CharPool_", StringComparison.Ordinal))
                    .Select(p => p.Count).DefaultIfEmpty(0).Max()
                : 0;
            // Old independent counters cannot be summed; an existing shared state always takes precedence.
            shared = new EventPity { GroupKey = rules.PityGroup,
                FiveStarCount = Math.Min(inherited, pool.PityCount - 1) };
            state.EventPityCounters.Add(shared);
        }
        decimal Roll() => next(int.MaxValue) / (decimal)int.MaxValue;
        GachaEntryConfig Pick(IEnumerable<GachaEntryConfig> candidates)
        {
            var entries = candidates.ToArray();
            int total = entries.Aggregate(0, (sum, entry) => checked(sum + entry.Weight));
            if (total <= 0) throw new InvalidDataException("Gacha tier is empty.");
            int roll = next(total);
            foreach (var entry in entries)
            {
                if (roll < entry.Weight) return entry;
                roll -= entry.Weight;
            }
            throw new InvalidDataException("Gacha random source is outside its range.");
        }
        for (int i = 0; i < count; i++)
        {
            if (rules == null)
            {
                // Keep the existing ordinary-pool rule; migration does not introduce a new pity formula.
                ordinary!.Count = Math.Min(ordinary.Count, pool.PityCount - 1) + 1;
                if (ordinary.Count >= pool.PityCount)
                {
                    ordinary.Count = 0;
                    output.Add(pool.Entries.OrderByDescending(e => e.Rarity).First());
                }
                else output.Add(Pick(pool.Entries));
                continue;
            }
            shared!.FiveStarCount = Math.Min(shared.FiveStarCount, pool.PityCount - 1) + 1;
            shared.FourStarCount = Math.Min(shared.FourStarCount, rules.FourStarPityCount - 1) + 1;
            decimal rarityRoll = Roll();
            if (shared.FiveStarCount >= pool.PityCount || rarityRoll < rules.FiveStarProbability)
            {
                bool featured = shared.FeaturedFiveStarGuaranteed || Roll() < rules.FeaturedFiveStarProbability;
                output.Add(featured
                    ? pool.Entries.Single(e => e.Rarity == 5 && e.EntryKey == rules.FeaturedFiveStarKey)
                    : Pick(pool.Entries.Where(e => e.Rarity == 5 && e.EntryKey != rules.FeaturedFiveStarKey)));
                shared.FiveStarCount = 0;
                shared.FourStarCount = 0;
                shared.FeaturedFiveStarGuaranteed = !featured;
            }
            else if (shared.FourStarCount >= rules.FourStarPityCount ||
                     rarityRoll < rules.FiveStarProbability + rules.FourStarProbability)
            {
                output.Add(Pick(pool.Entries.Where(e => e.Rarity == 4)));
                shared.FourStarCount = 0;
            }
            else output.Add(Pick(pool.Entries.Where(e => e.Rarity == 3)));
        }
        return output;
    }
}

internal static class GachaEndpoints
{
    internal static void Map(WebApplication app)
    {
        var group = app.MapGroup("/v1/gacha").RequireAuthorization().RequireRateLimiting("saves");
        group.MapPost("/draw", async (GachaDrawRequest request, HttpContext context, SaveStore saves,
            GachaConfigStore configs, GachaCatalog catalog, TimeProvider clock) =>
        {
            if (!request.IsValid) return Results.Json(new ApiError("invalid_gacha_request",
                "Use a UUID, one or ten draws, and the loaded save/config versions.", context.TraceIdentifier), statusCode: 400);
            string owner = context.User.FindFirst("sub")!.Value;
            try
            {
                // A committed retry must work even after a pool closes or its configuration becomes unavailable.
                var replay = saves.FindGacha(owner, request.RequestId, request.Fingerprint());
                if (replay != null) return Results.Ok(replay);
                var config = await configs.ReadCurrentAsync(clock.GetUtcNow(), context.RequestAborted);
                if (config.Version != request.ConfigVersion) throw new GachaFailure("gacha_config_changed");
                var pool = config.Pools.SingleOrDefault(p => p.GachaKey == request.GachaKey)
                    ?? throw new GachaFailure("gacha_pool_closed");
                catalog.Validate(pool);
                return Results.Ok(saves.DrawGacha(owner, request, pool, catalog));
            }
            catch (GachaFailure error)
            {
                return Results.Json(new ApiError(error.Code, "Gacha request could not be completed.",
                    context.TraceIdentifier, error.Revision), statusCode: error.Status);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
            {
                context.RequestServices.GetRequiredService<ILogger<GachaConfigStore>>().LogError(error, "Gacha settlement configuration is unavailable.");
                return Results.Json(new ApiError("gacha_config_unavailable", "Retry the same request later.",
                    context.TraceIdentifier), statusCode: 503);
            }
        });
        group.MapGet("/requests/{requestId:guid}", (Guid requestId, HttpContext context, SaveStore saves) =>
        {
            var reply = saves.FindGacha(context.User.FindFirst("sub")!.Value, requestId);
            return reply == null ? Results.Json(new ApiError("gacha_request_not_found",
                "No committed result is visible; retry the original request.", context.TraceIdentifier), statusCode: 404)
                : Results.Ok(reply);
        });
    }
}
