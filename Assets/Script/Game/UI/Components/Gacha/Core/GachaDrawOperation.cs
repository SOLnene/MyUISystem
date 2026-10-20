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
            if (error.StatusCode == 404)
                return "服务器尚未提供抽卡接口，请更新服务器后重试；原请求已保留。";
        }
        if (CloudGameSaveSession.Current?.HasPendingGacha ?? false)
            return "抽卡结果尚未确认，请在原卡池重试相同次数；也可退出后重新登录恢复。";
        return exception.Message;
    }

    internal static async UniTask<GachaResult> ExecuteAsync(IGachaService service, int count, string key)
    {
        var context = GameContext.Instance;
        if (!CloudGameSaveSession.IsCloudSource(context.ActiveSaveDirectory))
            return service.Draw(count, key);
        if (!ReferenceEquals(service, context.GachaService))
            throw new InvalidOperationException("抽卡页面不属于当前游戏会话，请重新打开。");
        // Cloud failures must never fall back to local RNG or local reward granting.
        return await GameSaveCoordinator.Instance.DrawGachaAsync(context.GachaService, key, count);
    }
}
