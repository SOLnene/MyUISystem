using System;
using System.Collections.Generic;
using System.IO;

[Serializable]
internal sealed class GachaCost
{
    public int fateItemId;
    public int primogemsPerFate;

    public GachaCost() { }
    internal GachaCost(int fateItemId, int primogemsPerFate)
    {
        this.fateItemId = fateItemId;
        this.primogemsPerFate = primogemsPerFate;
    }

    internal void Validate()
    {
        if ((fateItemId != 223 && fateItemId != 224) || primogemsPerFate < 1 || primogemsPerFate > 1000000)
            throw new InvalidDataException("Invalid gacha payment configuration.");
    }

    internal GachaPaymentQuote Quote(int count, int fates, int primogems)
    {
        Validate();
        if ((count != 1 && count != 10) || fates < 0 || primogems < 0)
            throw new ArgumentOutOfRangeException(nameof(count));
        int spent = Math.Min(count, fates);
        return new GachaPaymentQuote(fates, primogems, spent, checked((count - spent) * primogemsPerFate));
    }

    internal static int ReadBalance(CurrencySaveData currencies, int itemId)
    {
        if (currencies?.items == null) throw new InvalidDataException("Gacha currency snapshot is missing.");
        var ids = new HashSet<int>();
        int result = 0;
        foreach (var item in currencies.items)
        {
            if (item == null || item.itemId <= 0 || item.amount < 0 || !ids.Add(item.itemId))
                throw new InvalidDataException("Invalid gacha currency snapshot.");
            if (item.itemId == itemId) result = item.amount;
        }
        return result;
    }
}

internal readonly struct GachaPaymentQuote
{
    internal readonly int FatesSpent;
    internal readonly int PrimogemCost;
    internal readonly int FatesRemaining;
    internal readonly int PrimogemsRemaining;
    internal bool CanAfford => PrimogemsRemaining >= 0;

    internal GachaPaymentQuote(int fates, int primogems, int fatesSpent, int primogemCost)
    {
        FatesSpent = fatesSpent;
        PrimogemCost = primogemCost;
        FatesRemaining = fates - fatesSpent;
        PrimogemsRemaining = primogems - primogemCost;
    }
}
