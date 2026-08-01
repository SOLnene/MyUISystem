using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

internal static class RewardPopupCoordinator
{
    // This coordinator serializes reward notifications so only one popup and one backdrop capture
    // are active at a time.
    const string RewardPopupAddress = "ui/view/rewardpopupview";

    static readonly Queue<IReadOnlyList<RewardItemData>> pendingRewards = new();
    static readonly EventBinding<RewardGrantedEvent> rewardGrantedBinding =
        new(OnRewardGranted);
    static readonly EventBinding<RewardPopupClosedEvent> popupClosedBinding =
        new(OnPopupClosed);

    static bool isPopupOpen;
    static int initializationVersion;
    static Material blurMaterial;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Initialize()
    {
        initializationVersion++;
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

        // Queue the data first; ShowNext decides whether it can start a new capture/open sequence.
        pendingRewards.Enqueue(rewardGrantedEvent.Rewards);
        ShowNext();
    }

    static void OnPopupClosed()
    {
        isPopupOpen = false;
        // Yield once so UIManager finishes closing the current view before the next capture starts.
        ShowNextAfterCloseAsync().Forget();
    }

    static void ShowNext()
    {
        if (isPopupOpen || pendingRewards.Count == 0)
        {
            return;
        }

        // Mark open before starting the async operation so another event cannot start a second one.
        isPopupOpen = true;
        ShowNextAsync(
            pendingRewards.Dequeue(),
            initializationVersion).Forget();
    }

    static async UniTask ShowNextAsync(
        IReadOnlyList<RewardItemData> rewards,
        int requestVersion)
    {
        RenderTexture backdrop = null;

        try
        {
            // The material is read from the prefab once and then reused for subsequent captures.
            if (blurMaterial == null)
            {
                blurMaterial = await LoadBlurMaterialAsync();
            }

            if (blurMaterial != null)
            {
                // Capture must happen before opening the popup, otherwise the popup itself would
                // be included in its backdrop.
                backdrop = await UIBackdropCaptureService.Instance
                    .CaptureCompositeAsync(
                        blurMaterial,
                        UIBackdropCaptureProfile.RewardPopup);
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }

        // Ignore a result produced by an earlier scene/application initialization.
        if (requestVersion != initializationVersion)
        {
            return;
        }

        try
        {
            // The view receives the already-rendered persistent RT; it only binds the texture.
            UIManager.Instance.Open(
                UIType.RewardPopupView,
                new RewardPopupOpenParams(rewards, backdrop));
        }
        catch (Exception exception)
        {
            isPopupOpen = false;
            Debug.LogException(exception);
            ShowNextAfterCloseAsync().Forget();
        }
    }

    static async UniTask<Material> LoadBlurMaterialAsync()
    {
        // Loading the prefab also gives the coordinator the exact material configured for this UI.
        var popupPrefab =
            await ResourceManager.Instance.LoadAssetAsync<GameObject>(
                RewardPopupAddress);
        if (popupPrefab == null)
        {
            return null;
        }

        var popupView = popupPrefab.GetComponent<RewardPopupView>();
        return popupView == null ? null : popupView.BackdropBlurMaterial;
    }

    static async UniTask ShowNextAfterCloseAsync()
    {
        // Let the close lifecycle complete before dequeuing and capturing the next reward batch.
        await UniTask.Yield();
        ShowNext();
    }
}
