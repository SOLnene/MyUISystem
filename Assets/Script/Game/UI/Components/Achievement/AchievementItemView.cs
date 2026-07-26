using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
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

    readonly VersionedAssetLoader<Sprite> iconLoader = new();

    public void Bind(AchievementItemViewModel viewModel)
    {
        titleText.text = viewModel.Title;
        descriptionText.text = viewModel.Description;
        progressText.text = viewModel.ProgressText;
        buttonText.text = viewModel.ButtonText;
        claimButton.interactable = viewModel.CanClaim;
        rewardSlot.Bind(viewModel.RewardSlot);

        icon.sprite = null;
        LoadIconAsync(viewModel.IconAddress, this.GetCancellationTokenOnDestroy()).Forget();
    }

    public void Unbind()
    {
        iconLoader.Cancel();
        icon.sprite = null;
        rewardSlot.ResetState();
        claimButton.onClick.RemoveAllListeners();
    }

    async UniTask LoadIconAsync(string iconAddress, CancellationToken cancellationToken)
    {
        VersionedAssetLoadResult<Sprite> result =
            await iconLoader.LoadAsync(iconAddress, cancellationToken);
        if (result.IsCurrent)
        {
            icon.sprite = result.Asset;
        }
    }

    void OnDestroy()
    {
        iconLoader.Dispose();
        claimButton.onClick.RemoveAllListeners();
    }
}
