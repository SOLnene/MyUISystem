using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public sealed class AchievementItemView : MonoBehaviour
{
    [SerializeField]
    Image icon;
    [SerializeField]
    TextMeshProUGUI titleText;
    [SerializeField]
    TextMeshProUGUI descriptionText;
    [SerializeField]
    ItemSlotView rewardSlot;
    [SerializeField]
    TextMeshProUGUI progressText;
    [SerializeField]
    Button claimButton;
    [SerializeField]
    TextMeshProUGUI buttonText;
    [SerializeField]
    AnimatedPanel anim;

    readonly VersionedAssetLoader<Sprite> iconLoader = new();
    readonly CompositeDisposable bindDisposables = new();

    internal AnimatedPanel Anim => anim;

    public void Bind(AchievementItemViewModel viewModel)
    {
        // Item View 只绑定当前 VM；列表排序或分类切换时可复用同一个 GameObject。
        bindDisposables.Clear();
        titleText.text = viewModel.Title;
        descriptionText.text = viewModel.Description;
        buttonText.text = viewModel.ButtonText;
        viewModel.ClaimCommand
            .BindTo(claimButton)
            .AddTo(bindDisposables);
        viewModel.ProgressText
            .Subscribe(progress => progressText.text = progress)
            .AddTo(bindDisposables);
        viewModel.IsCompleted
            .Subscribe(isCompleted =>
            {
                progressText.gameObject.SetActive(!isCompleted);
                claimButton.gameObject.SetActive(isCompleted);
            })
            .AddTo(bindDisposables);
        viewModel.IsClaimed
            .Subscribe(isClaimed =>
                buttonText.text = isClaimed ? "已领取" : viewModel.ButtonText)
            .AddTo(bindDisposables);
        rewardSlot.Bind(viewModel.RewardSlot);

        icon.sprite = null;
        LoadIconAsync(viewModel.IconAddress, this.GetCancellationTokenOnDestroy()).Forget();
    }

    public void Unbind()
    {
        bindDisposables.Clear();
        iconLoader.Cancel();
        icon.sprite = null;
        rewardSlot.ResetState();
        claimButton.onClick.RemoveAllListeners();
    }

    async UniTask LoadIconAsync(string iconAddress, CancellationToken cancellationToken)
    {
        // 图标加载使用版本化 Loader，避免快速切换列表时旧请求覆盖新绑定。
        VersionedAssetLoadResult<Sprite> result =
            await iconLoader.LoadAsync(iconAddress, cancellationToken);
        if (result.IsCurrent)
        {
            icon.sprite = result.Asset;
        }
    }

    void OnDestroy()
    {
        bindDisposables.Dispose();
        iconLoader.Dispose();
        claimButton.onClick.RemoveAllListeners();
    }
}
