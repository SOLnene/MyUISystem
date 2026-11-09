using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

public partial class FlowManager : SingletonMono<FlowManager>
{

    bool isEnteringProfile;

    public IEnumerator StartGameFlow()
    {
        yield return BootAsync(Camera.main, null, string.Empty).ToCoroutine();
    }

    public void ActivateLoadedScene()
    {
        Debug.LogWarning("World activation is now owned by the profile entry flow.");
    }

    async UniTask<bool> PrepareLocalSessionAsync(string profileId, CancellationToken viewCancellation = default)
    {
        // 避免快速重复点击并发初始化同一个全局 GameContext。
        if (isEnteringProfile ||
            !SaveProfileManager.Instance.TryGetProfile(
                profileId,
                out SaveProfileInfo profile,
                out string profileDirectory))
        {
            return false;
        }

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            viewCancellation, this.GetCancellationTokenOnDestroy());
        var cancellationToken = linkedCancellation.Token;
        isEnteringProfile = true;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            // 本地入口不初始化认证服务，也不请求云端；旧会话保存完成后再切换。
            await GameSaveCoordinator.Instance.PrepareSessionSwitchAsync(cancellationToken);
            bool profileStarted = await GameContext.Instance.TryStartProfileAsync(profileDirectory, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!profileStarted)
            {
                Debug.LogError($"进入游戏失败，存档读取结果: {GameContext.Instance.LastSaveLoadResult}");
                return false;
            }

            // Session 已提交后再激活档案，失败的候选不会改变后续保存目标。
            SaveProfileManager.Instance.ActivateProfile(profile);
            CloudGameSaveSession.Current = null;
            GameStartupData.Clear();
            if (GameContext.Instance.TryRequestInitialTestItems())
                foreach (InventoryItem item in ItemFactory.CreateTestItems())
                    GameContext.Instance.BackpackVM.AddItem(item);
            if (GameContext.Instance.LastSaveLoadResult == SaveLoadResult.NotFound ||
                GameSaveSystem.NeedsResave)
            {
                GameSaveCoordinator.Instance.MarkDirty();
            }

            if (!GameSaveCoordinator.Instance.Flush())
                throw new IOException("本地存档未能写入，暂不进入游戏。");

            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 对象销毁属于正常终止，不作为网络或存档错误记录。
            return false;
        }
        catch (RequestFailedException exception)
        {
            // 登录失败不能提交存档会话；保留错误码和调用栈供网络问题排查。
            Debug.LogError($"UGS 登录失败，错误码: {exception.ErrorCode}\n{exception}");
            return false;
        }
        catch (Exception exception)
        {
            Debug.LogError($"进入游戏失败: {exception.Message}");
            return false;
        }
        finally
        {
            isEnteringProfile = false;
        }
    }

    async UniTask PrepareCloudSessionAsync(string username, CancellationToken viewCancellation)
    {
        if (isEnteringProfile)
            throw new InvalidOperationException("已有进入游戏请求正在执行。");
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            viewCancellation, this.GetCancellationTokenOnDestroy());
        var cancellationToken = linkedCancellation.Token;
        isEnteringProfile = true;
        var previousCloudSession = CloudGameSaveSession.Current;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            string playerId = AccountSession.Current.PlayerId;
            await GameSaveCoordinator.Instance.PrepareSessionSwitchAsync(cancellationToken);
            var cloud = previousCloudSession;
            if (cloud == null || cloud.HasConflict || cloud.PlayerId != playerId ||
                GameContext.Instance.ActiveSaveDirectory != cloud.Source)
            {
                cloud = await CloudGameSaveSession.LoadAsync(username, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                CloudGameSaveSession.Current = cloud;
            }
            bool started = await GameContext.Instance.TryStartProfileAsync(cloud.Source, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!started)
                throw new InvalidDataException(
                    $"云存档无法加载: {GameContext.Instance.LastSaveLoadResult}，未创建替代存档。");
            // 初始物品属于新账号会话，先加入快照，再确认首次云端保存。
            if (GameContext.Instance.TryRequestInitialTestItems())
                foreach (InventoryItem item in ItemFactory.CreateTestItems())
                    GameContext.Instance.BackpackVM.AddItem(item);
            if (!cloud.IsReady && (GameContext.Instance.LastSaveLoadResult == SaveLoadResult.NotFound ||
                GameSaveSystem.NeedsResave))
                GameSaveCoordinator.Instance.MarkDirty();
            // 新账号的初始存档也要先得到云端确认，失败时留在登录界面重试。
            while (!await GameSaveCoordinator.Instance.FlushCloudAsync(cancellationToken))
            {
                if (GameSaveCoordinator.Instance.LastCloudError != null)
                    throw GameSaveCoordinator.Instance.LastCloudError;
                cancellationToken.ThrowIfCancellationRequested();
            }
            cancellationToken.ThrowIfCancellationRequested();
            cloud.IsReady = true;
            GameStartupData.StartForCloud(cloud, this.GetCancellationTokenOnDestroy());
        }
        catch
        {
            // 候选未提交时恢复原云会话；已提交但未上传的候选保留以便原账号重试。
            if (GameContext.Instance.ActiveSaveDirectory != CloudGameSaveSession.Current?.Source)
                CloudGameSaveSession.Current = previousCloudSession;
            throw;
        }
        finally
        {
            isEnteringProfile = false;
        }
    }

}
