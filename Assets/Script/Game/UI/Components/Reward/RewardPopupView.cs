using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class RewardPopupView : UIView
{
    // The backdrop RawImage consumes the persistent RT supplied by RewardPopupCoordinator.
    [SerializeField]
    RawImage backdropImage;
    [SerializeField]
    Material backdropBlurMaterial;
    [SerializeField]
    Button closeHandle;
    [SerializeField]
    RewardListView rewardListView;
    [SerializeField]
    AnimatedPanel pageRootPanel;

    internal Material BackdropBlurMaterial => backdropBlurMaterial;

    public override void OnAddListener()
    {
        closeHandle.onClick.RemoveListener(HandleClose);
        closeHandle.onClick.AddListener(HandleClose);
    }

    public override void OnRemoveListener()
    {
        closeHandle.onClick.RemoveListener(HandleClose);
    }

    public override void OnOpen(object data)
    {
        base.OnOpen(data);
        pageRootPanel.Show().Forget();

        IReadOnlyList<RewardItemData> rewards;
        if (data is RewardPopupOpenParams openParams)
        {
            // Normal path: bind both the captured backdrop and the reward payload.
            ApplyBackdrop(openParams.Backdrop);
            rewards = openParams.Rewards;
        }
        else if (data is IReadOnlyList<RewardItemData> rewardItems)
        {
            // Compatibility path for callers that provide rewards without a capture.
            ApplyBackdrop(null);
            rewards = rewardItems;
        }
        else
        {
            // Invalid payload leaves the view in a safe empty state.
            ApplyBackdrop(null);
            rewardListView.Clear();
            return;
        }

        rewardListView.Bind(rewards, GameDatabase.ItemDatabase);
    }

    public override void OnRelease()
    {
        // Unbind the image before the view returns to the pool; the service still owns the RT.
        ReleaseBackdrop();
        rewardListView.Clear();
        base.OnRelease();
    }

    public override void OnClose()
    {
        // Closing only removes the RawImage reference. It must not release the shared capture RT.
        ReleaseBackdrop();
        rewardListView.Clear();
        base.OnClose();
        EventBus<RewardPopupClosedEvent>.Raise(default);
    }

    void HandleClose()
    {
        CloseAfterAnimationAsync().Forget();
    }

    async UniTaskVoid CloseAfterAnimationAsync()
    {
        if (await pageRootPanel.Hide())
        {
            OnCancel();
        }
    }

    void ApplyBackdrop(RenderTexture texture)
    {
        // Replace the previous binding before applying the new capture or fallback color.
        ReleaseBackdrop();
        backdropImage.texture = texture;
        backdropImage.color = texture == null
            ? new Color(0.082f, 0.09f, 0.137f, 1f)
            : Color.white;
    }

    void ReleaseBackdrop()
    {
        // Clearing RawImage.texture prevents a released/reused view from displaying stale content.
        backdropImage.texture = null;
    }
}
