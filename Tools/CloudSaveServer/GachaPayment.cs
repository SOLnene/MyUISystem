using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace CloudSaveServer;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record GachaPaymentConfig([property: JsonRequired] int FateItemId,
    [property: JsonRequired] int PrimogemsPerFate)
{
    internal void Validate()
    {
        if (FateItemId is not (223 or 224) || PrimogemsPerFate is < 1 or > 1000000)
            throw new InvalidDataException("Invalid gacha payment configuration.");
    }
}

internal sealed record GachaPayment(int FateItemId, int FatesSpent, int PrimogemsSpent,
    int FatesRemaining, int PrimogemsRemaining, int PrimogemsPerFate)
{
    internal static GachaPayment Settle(JsonObject save, GachaDrawRequest request, GachaPaymentConfig cost)
    {
        cost.Validate();
        if (request.PaymentVersion != 1) throw new GachaFailure("gacha_client_upgrade_required");
        var items = GachaCatalog.Array(GachaCatalog.Section(save, "currencies"), "items");
        var balances = new Dictionary<int, JsonObject>();
        foreach (var node in items)
        {
            if (node is not JsonObject item || item["itemId"] is not JsonValue idValue ||
                !idValue.TryGetValue<int>(out int id) || id <= 0 ||
                item["amount"] is not JsonValue amountValue || !amountValue.TryGetValue<int>(out int amount) ||
                amount < 0 || !balances.TryAdd(id, item))
                throw new GachaFailure("invalid_gacha_currency", 400);
        }
        int Balance(int id) => balances.TryGetValue(id, out var item) ? item["amount"]!.GetValue<int>() : 0;
        int fates = Balance(cost.FateItemId), primogems = Balance(201);
        int fatesSpent = Math.Min(fates, request.Count);
        int primogemsSpent = checked((request.Count - fatesSpent) * cost.PrimogemsPerFate);
        // The ceiling belongs to the durable request, so retries can never increase the player's consent.
        if (primogemsSpent > request.MaxPrimogemCost)
            throw new GachaFailure("gacha_payment_confirmation_required");
        if (primogemsSpent > primogems)
            throw new GachaFailure("gacha_insufficient_currency");
        void SetBalance(int id, int amount)
        {
            if (balances.TryGetValue(id, out var item)) item["amount"] = amount;
            else items.Add(new JsonObject { ["itemId"] = id, ["amount"] = amount });
        }
        SetBalance(cost.FateItemId, fates - fatesSpent);
        SetBalance(201, primogems - primogemsSpent);
        return new(cost.FateItemId, fatesSpent, primogemsSpent, fates - fatesSpent,
            primogems - primogemsSpent, cost.PrimogemsPerFate);
    }
}
