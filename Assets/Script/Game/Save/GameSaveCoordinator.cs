using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class GameSaveCoordinator : SingletonMono<GameSaveCoordinator>
{
    const float SaveDelay = 1f;

    bool dirty;
    float saveAt;
    uint dirtyRevision;
    bool isFlushing;
    bool quitRequested;
    bool allowQuit;
    internal Exception LastCloudError { get; private set; }

    public void MarkDirty()
    {
        if (!GameContext.Instance.CanSave)
        {
            return;
        }

        dirty = true;
        dirtyRevision++;
        saveAt = Time.realtimeSinceStartup + SaveDelay;
    }

    public bool Flush()
    {
        if (CloudGameSaveSession.IsCloudSource(GameContext.Instance.ActiveSaveDirectory))
        {
            if (!dirty && !isFlushing && !(CloudGameSaveSession.Current?.HasPendingSave ?? false))
                return true;
            if (!isFlushing)
                FlushCloudAsync().Forget();
            return false;
        }
        if (!dirty)
        {
            return true;
        }

        if (!GameContext.Instance.CanSave || !GameSaveSystem.TrySaveCurrentGame())
        {
            saveAt = Time.realtimeSinceStartup + SaveDelay;
            return false;
        }

        dirty = false;
        return true;
    }

    internal async UniTask PrepareSessionSwitchAsync(CancellationToken cancellationToken)
    {
        while (isFlushing)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await UniTask.Yield();
        }
        // 已进入游戏的会话先保存；取消进入时的初始候选不阻止选择本地入口。
        if ((dirty || (CloudGameSaveSession.Current?.HasPendingSave ?? false)) && GameContext.Instance.CanSave &&
            (!CloudGameSaveSession.IsCloudSource(GameContext.Instance.ActiveSaveDirectory) ||
                (CloudGameSaveSession.Current?.IsReady ?? false)))
        {
            bool saved = CloudGameSaveSession.IsCloudSource(GameContext.Instance.ActiveSaveDirectory)
                ? await FlushCloudAsync(cancellationToken) : Flush();
            if (!saved)
                throw LastCloudError ?? new InvalidOperationException("当前进度尚未保存，请先完成保存再切换。");
        }
        cancellationToken.ThrowIfCancellationRequested();
        dirty = false;
        dirtyRevision++;
        LastCloudError = null;
    }

    internal async UniTask<bool> FlushCloudAsync(CancellationToken cancellationToken = default)
    {
        // 并发调用等待同一次上传完成；取消等待不改变原请求的保存结果。
        while (isFlushing)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await UniTask.Yield();
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (!dirty && !(CloudGameSaveSession.Current?.HasPendingSave ?? false))
            return true;
        isFlushing = true;
        uint revision = dirtyRevision;
        try
        {
            var session = CloudGameSaveSession.RequireSession(GameContext.Instance.ActiveSaveDirectory);
            if (dirty)
                GameSaveSystem.TrySaveCurrentGame();
            if (!session.HasPendingSave)
                throw new InvalidOperationException("未能生成待上传的云存档。");
            await session.SavePendingAsync(cancellationToken);
            GameSaveSystem.ConfirmCloudSave();
            LastCloudError = null;
            // 确认的可能是失败重试中的旧快照；还有新快照时继续保留 dirty。
            if (revision == dirtyRevision && !session.HasPendingSave)
                dirty = false;
            saveAt = Time.realtimeSinceStartup + SaveDelay;
            return !dirty && !session.HasPendingSave;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            LastCloudError = exception;
            saveAt = Time.realtimeSinceStartup + 10f;
            Debug.LogError($"云存档保存失败，保留待保存状态: {exception.Message}");
            return false;
        }
        finally
        {
            isFlushing = false;
        }
    }

    void OnEnable()
    {
        Application.wantsToQuit += RequestQuit;
    }

    void OnDisable()
    {
        Application.wantsToQuit -= RequestQuit;
    }

    bool RequestQuit()
    {
        // 冲突时不能自动覆盖云端，也不能阻止用户退出后重读云端版本。
        if (CloudGameSaveSession.Current?.HasConflict ?? false)
        {
            Debug.LogWarning("云存档存在冲突，本次未确认的改动不会覆盖云端版本。");
            return true;
        }
        if (allowQuit || !CloudGameSaveSession.IsCloudSource(GameContext.Instance.ActiveSaveDirectory) ||
            (!dirty && !isFlushing && !(CloudGameSaveSession.Current?.HasPendingSave ?? false)))
            return true;
        if (!quitRequested)
            SaveBeforeQuitAsync().Forget();
        return false;
    }

    async UniTaskVoid SaveBeforeQuitAsync()
    {
        quitRequested = true;
        try
        {
            while (dirty || isFlushing || (CloudGameSaveSession.Current?.HasPendingSave ?? false))
            {
                if (!await FlushCloudAsync())
                {
                    if (LastCloudError != null)
                        return;
                }
            }
            // 正常退出要等云端确认，随后才允许认证组件清理临时登录凭据。
            allowQuit = true;
            Application.Quit();
        }
        finally
        {
            quitRequested = false;
        }
    }

    void Update()
    {
        if (dirty && !isFlushing &&
            (!CloudGameSaveSession.IsCloudSource(GameContext.Instance.ActiveSaveDirectory) ||
                (CloudGameSaveSession.Current?.IsReady ?? false)) &&
            !(CloudGameSaveSession.Current?.HasConflict ?? false) &&
            Time.realtimeSinceStartup >= saveAt)
        {
            Flush();
        }
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            Flush();
        }
    }

    void OnApplicationQuit()
    {
        Flush();
    }
}
