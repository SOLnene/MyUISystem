using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public class PromoteLevelPreviewView : MonoBehaviour
{
    [SerializeField]
    TextMeshProUGUI currentLevelText;
    [SerializeField]
    TextMeshProUGUI nextLevelText;
    [SerializeField]
    GameObject currentStarContent;
    [SerializeField]
    GameObject nextStarContent;
    [SerializeField]
    AnimatedPanel animatedPanel;
    [SerializeField]
    PromoteLevelResultFxView resultFxView;

    readonly CompositeDisposable disposable = new();
    PromoteLevelPreviewViewModel vm;

    public void Bind(PromoteLevelPreviewViewModel viewModel)
    {
        disposable.Clear();
        vm = viewModel;

        if (vm == null)
        {
            gameObject.SetActive(false);
            return;
        }

        vm.currentLevelText
            .Subscribe(text =>
            {
                if (currentLevelText != null)
                    currentLevelText.text = text;
            })
            .AddTo(disposable);

        vm.nextLevelText
            .Subscribe(text =>
            {
                if (nextLevelText != null)
                    nextLevelText.text = text;
            })
            .AddTo(disposable);

        vm.maxRanked
            .Subscribe(maxRanked =>
            {
                if (nextLevelText != null)
                    nextLevelText.gameObject.SetActive(!maxRanked);

                if (nextStarContent != null)
                    nextStarContent.SetActive(!maxRanked);
            })
            .AddTo(disposable);

        vm.currentStarCount
            .Subscribe(count => SetStars(currentStarContent, count))
            .AddTo(disposable);

        vm.nextStarCount
            .Subscribe(count => SetStars(nextStarContent, count))
            .AddTo(disposable);
    }

    public async UniTask Show()
    {
        gameObject.SetActive(true);

        if (animatedPanel != null)
            await animatedPanel.Show();
    }

    public async UniTask Hide()
    {
        if (animatedPanel != null)
        {
            await animatedPanel.Hide();
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    public async UniTask PlayResult(PromoteLevelResultData result, Action onNewStateShown = null)
    {
        if (resultFxView != null)
            await resultFxView.Play(result, onNewStateShown);
    }
    
    void SetStars(GameObject root, int count)
    {
        if (root == null)
            return;

        var icons = root.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < icons.Length; i++)
        {
            icons[i].color = i < count ? Color.white : Color.grey;
        }
    }

    void OnDestroy()
    {
        disposable.Dispose();
    }
}
