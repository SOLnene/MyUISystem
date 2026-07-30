using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

internal static class RewardPopupCoordinator
{
    static readonly Queue<IReadOnlyList<RewardItemData>> pendingRewards = new();
    static readonly EventBinding<RewardGrantedEvent> rewardGrantedBinding =
        new(OnRewardGranted);
    static readonly EventBinding<RewardPopupClosedEvent> popupClosedBinding =
        new(OnPopupClosed);

    static bool isPopupOpen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Initialize()
    {
        pendingRewards.Clear();
        isPopupOpen = false;

        EventBus<RewardGrantedEvent>.Deregister(rewardGrantedBinding);
        EventBus<RewardGrantedEvent>.Register(rewardGrantedBinding);
        EventBus<RewardPopupClosedEvent>.Deregister(popupClosedBinding);
        EventBus<RewardPopupClosedEvent>.Register(popupClosedBinding);
    }

    static void OnRewardGranted(RewardGrantedEvent rewardGrantedEvent)
    {
        if (rewardGrantedEvent.Rewards == null ||
            rewardGrantedEvent.Rewards.Count == 0)
        {
            return;
        }

        pendingRewards.Enqueue(rewardGrantedEvent.Rewards);
        ShowNext();
    }

    static void OnPopupClosed()
    {
        isPopupOpen = false;
        ShowNextAfterCloseAsync().Forget();
    }

    static void ShowNext()
    {
        if (isPopupOpen || pendingRewards.Count == 0)
        {
            return;
        }

        isPopupOpen = true;
        UIManager.Instance.Open(
            UIType.RewardPopupView,
            pendingRewards.Dequeue());
    }

    static async UniTask ShowNextAfterCloseAsync()
    {
        await UniTask.Yield();
        ShowNext();
    }
}
