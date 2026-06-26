using DG.Tweening;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Components.CharacterDetail
{
    public class TalentEffectPanelView : MonoBehaviour
    {
        [SerializeField]
        RectTransform iconRoot;
        [SerializeField]
        TextMeshProUGUI talentNameText;
        [SerializeField]
        TextMeshProUGUI talentLayerText;
        [SerializeField]
        TextMeshProUGUI talentDescriptionText;
        [SerializeField]
        CostItemView costItemView;
        [SerializeField]
        Button activateButton;
        [SerializeField]
        TextMeshProUGUI activateButtonText;
        [SerializeField]
        TextMeshProUGUI activatedStateText;
        [SerializeField]
        float hiddenOffsetX = -520f;
        [SerializeField]
        float animationDuration = 0.22f;

        const int talentTokenRequireCount = 1;

        readonly CompositeDisposable disposables = new();
        RectTransform panelRoot;
        Vector2 shownPosition;
        Sequence panelSequence;
        bool isMotionCached;

        public void Bind(CharacterTalentViewModel viewModel)
        {
            disposables.Clear();
            iconRoot.gameObject.SetActive(true);

            viewModel.SelectedTalentName
                .Subscribe(RefreshTalentName)
                .AddTo(disposables);
            viewModel.SelectedTalentLayerText
                .Subscribe(RefreshTalentLayer)
                .AddTo(disposables);
            viewModel.SelectedTalentDescription
                .Subscribe(RefreshTalentDescription)
                .AddTo(disposables);
            viewModel.TalentTokenCount
                .Subscribe(RefreshTalentTokenCost)
                .AddTo(disposables);
            viewModel.CanActivateSelected
                .Subscribe(SetActivateButton)
                .AddTo(disposables);
            viewModel.SelectedNodeActivated
                .Subscribe(SetActivatedState)
                .AddTo(disposables);
            viewModel.ActivateButtonText
                .Subscribe(RefreshActivateButtonText)
                .AddTo(disposables);

            activateButton.onClick.RemoveAllListeners();
            activateButton.onClick.AddListener(viewModel.ActivateSelectedTalent);
        }

        public void ShowPanel(bool instant)
        {
            CacheMotionState();
            panelSequence?.Kill();
            gameObject.SetActive(true);

            if (instant)
            {
                panelRoot.anchoredPosition = shownPosition;
                return;
            }

            panelRoot.anchoredPosition = GetHiddenPosition();
            panelSequence = DOTween.Sequence().SetUpdate(true);
            panelSequence.Join(panelRoot.DOAnchorPos(shownPosition, animationDuration).SetEase(Ease.OutCubic));
        }

        public void HidePanel(bool instant)
        {
            CacheMotionState();
            panelSequence?.Kill();

            if (instant)
            {
                panelRoot.anchoredPosition = GetHiddenPosition();
                gameObject.SetActive(false);
                return;
            }

            panelSequence = DOTween.Sequence().SetUpdate(true);
            panelSequence.Join(panelRoot.DOAnchorPos(GetHiddenPosition(), animationDuration).SetEase(Ease.InCubic));
            panelSequence.OnComplete(() => gameObject.SetActive(false));
        }

        void RefreshTalentName(string text)
        {
            talentNameText.text = text;
        }

        void RefreshTalentLayer(string text)
        {
            talentLayerText.text = text;
        }

        void RefreshTalentDescription(string text)
        {
            talentDescriptionText.text = text;
        }

        void RefreshTalentTokenCost(int tokenCount)
        {
            costItemView.SetCount(tokenCount, talentTokenRequireCount);
        }

        void SetActivateButton(bool canActivate)
        {
            activateButton.interactable = canActivate;
        }

        void SetActivatedState(bool activated)
        {
            activateButton.gameObject.SetActive(!activated);
            activatedStateText.gameObject.SetActive(activated);
        }

        void RefreshActivateButtonText(string text)
        {
            activateButtonText.text = text;
            activatedStateText.text = text;
        }

        void CacheMotionState()
        {
            if (isMotionCached)
            {
                return;
            }

            panelRoot = (RectTransform)transform;
            shownPosition = panelRoot.anchoredPosition;
            isMotionCached = true;
        }

        Vector2 GetHiddenPosition()
        {
            return shownPosition + new Vector2(hiddenOffsetX, 0f);
        }

        void OnDestroy()
        {
            panelSequence?.Kill();
            disposables.Dispose();
        }
    }
}
