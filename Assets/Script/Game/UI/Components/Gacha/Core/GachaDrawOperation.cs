using System;
using Cysharp.Threading.Tasks;

internal static class GachaDrawOperation
{
    internal static string Feedback(Exception exception)
    {
        if (exception is CloudSaveServerException error)
        {
            if (error.Code == "gacha_config_changed" || error.Code == "gacha_pool_closed")
                return "卡池已更新或关闭，请刷新卡池信息后再抽卡。";
            if (error.Code == "gacha_payment_confirmation_required")
                return "所需原石已变化，请重新确认本次消耗。";
            if (error.Code == "gacha_insufficient_currency")
                return "原石和祈愿道具不足，未进行扣费。";
            if (error.Code == "gacha_client_upgrade_required")
                return "抽卡消耗协议已更新，请重新发起抽卡。";
            if (error.StatusCode == 404)
                return "服务器尚未提供抽卡接口，请更新服务器后重试；原请求已保留。";
        }
        if (CloudGameSaveSession.Current?.HasPendingGacha ?? false)
            return "抽卡结果尚未确认，请在原卡池重试相同次数；也可退出后重新登录恢复。";
        return exception.Message;
    }

    internal static async UniTask<GachaResult> ExecuteAsync(IGachaService service, int count, string key,
        int maxPrimogemCost = 0)
    {
        var context = GameContext.Instance;
        if (!CloudGameSaveSession.IsCloudSource(context.ActiveSaveDirectory) || !CloudGameSaveSession.UsesServerSettlement)
        {
            var cost = context.GachaService.GetCost(key);
            var economy = GameEconomy.Instance;
            var quote = cost.Quote(count, economy.GetCurrency(cost.fateItemId), economy.GetCurrency(201));
            if (!quote.CanAfford) throw new InvalidOperationException("原石和祈愿道具不足。");
            if (quote.PrimogemCost > maxPrimogemCost)
                throw new InvalidOperationException("请先确认使用原石补足祈愿道具。");
            var result = service.Draw(count, key);
            if (result.Entries.Count != count) throw new InvalidOperationException("抽卡结果数量无效。");
            economy.ApplyGachaBalances(cost.fateItemId, quote.FatesRemaining, quote.PrimogemsRemaining);
            return result;
        }
        if (!ReferenceEquals(service, context.GachaService))
            throw new InvalidOperationException("抽卡页面不属于当前游戏会话，请重新打开。");
        // Cloud failures must never fall back to local RNG or local reward granting.
        return await GameSaveCoordinator.Instance.DrawGachaAsync(context.GachaService, key, count,
            maxPrimogemCost: maxPrimogemCost);
    }
}
