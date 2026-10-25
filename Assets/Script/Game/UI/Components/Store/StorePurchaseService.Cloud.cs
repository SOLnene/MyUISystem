using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;

[Serializable]
internal sealed class CloudStoreSnapshot
{
    public string configVersion;
    public string serverTime;
    public string nextRefreshAt;
    public string periodKey;
    public List<CloudStoreListing> items;
    internal double SecondsUntilRefresh;

    internal void Validate()
    {
        if (string.IsNullOrEmpty(configVersion) || configVersion.Length != 64 || string.IsNullOrEmpty(periodKey) ||
            !DateTimeOffset.TryParse(serverTime, CultureInfo.InvariantCulture, DateTimeStyles.None, out var now) ||
            !DateTimeOffset.TryParse(nextRefreshAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var next) ||
            next <= now || items == null || items.Count > 1000 ||
            items.Any(i => i?.offer == null || i.remainingLimit < 0 || i.remainingLimit > i.offer.DailyPurchaseLimit) ||
            items.Select(i => i.offer.StoreItemId).Distinct().Count() != items.Count ||
            (items.Count > 0 && !StoreConfigValidator.TryValidate(
                new StoreConfigData { items = items.Select(i => i.offer).ToList() }, GameDatabase.ItemDatabase)))
            throw new InvalidDataException("商店配置无效，请稍后重试。");
        SecondsUntilRefresh = (next - now).TotalSeconds;
    }
}
[Serializable]
internal sealed class CloudStoreListing
{
    public StoreConfigItemData offer;
    public int remainingLimit;
}

partial class StorePurchaseService
{
    CloudStoreSnapshot remoteStore;
    readonly Stopwatch remoteClock = new();
    readonly Dictionary<int, int> remainingLimits = new();
    internal bool UsesCloud => CloudGameSaveSession.IsCloudSource(GameContext.Instance.ActiveSaveDirectory);
    internal string ConfigurationToken => remoteStore?.configVersion;
    internal string CurrentPeriod => remoteStore?.periodKey;
    internal double RefreshDelay => remoteStore == null ? 0 : Math.Max(0, remoteStore.SecondsUntilRefresh - remoteClock.Elapsed.TotalSeconds);

    StorePurchasePreview CreateCloudPreview(StoreConfigItemData offer)
    {
        int remaining = RefreshDelay > 0 && remainingLimits.TryGetValue(offer.StoreItemId, out int limit) ? limit : 0;
        int affordable = offer.Price == 0 ? remaining : GameEconomy.Instance.GetCurrency(offer.CostItemId) / offer.Price;
        return new StorePurchasePreview(Math.Min(remaining, affordable), remaining, affordable);
    }

    internal async UniTask<StoreConfigDatabase> LoadCloudStoreAsync(CancellationToken cancellationToken)
    {
        var session = CloudGameSaveSession.RequireSession(GameContext.Instance.ActiveSaveDirectory);
        if (session.HasPendingStore)
        {
            var request = session.PendingStore;
            await GameSaveCoordinator.Instance.PurchaseStoreAsync(this, request.storeItemId, request.count,
                request.configVersion, cancellationToken);
        }
        var snapshot = await session.LoadStoreAsync(cancellationToken);
        await UniTask.SwitchToMainThread(cancellationToken);
        if (!ReferenceEquals(this, GameContext.Instance.StorePurchaseService))
            throw new OperationCanceledException(cancellationToken);
        remoteStore = snapshot; remoteClock.Restart(); remainingLimits.Clear();
        foreach (var listing in snapshot.items) remainingLimits.Add(listing.offer.StoreItemId, listing.remainingLimit);
        return new StoreConfigDatabase(snapshot.items.Select(i => i.offer).ToList());
    }

    internal UniTask PurchaseCloudAsync(int id, int count, CancellationToken cancellationToken)
    {
        if (RefreshDelay <= 0) throw new InvalidOperationException("商店正在刷新，请稍后重新选择商品。");
        return GameSaveCoordinator.Instance.PurchaseStoreAsync(this, id, count, ConfigurationToken, cancellationToken);
    }
}

internal static class StoreFeedback
{
    internal static string Message(Exception error)
    {
        if (error is CloudSaveServerException request)
            return request.Code switch
            {
                "store_config_changed" => "商店内容已更新，请重新选择商品。",
                "store_item_unavailable" => "该商品已下架，请重新选择商品。",
                "store_limit_exceeded" => "该商品的剩余购买次数不足，正在刷新商店。",
                "store_insufficient_currency" => "货币不足，无法购买该商品。",
                "save_conflict" => "云端进度已更新，请重新登录后购买。",
                "store_config_unavailable" => "商店暂时不可用，请稍后重试。",
                _ => "购买结果尚未确认，返回商城将继续确认，也可重新登录恢复。"
            };
        return error is InvalidOperationException || error is InvalidDataException
            ? error.Message : "商店请求未完成，请稍后重试。";
    }
}
