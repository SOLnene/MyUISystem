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
        TextMeshProUGUI effectText;
        [SerializeField]
        Button activateButton;
        [SerializeField]
        TextMeshProUGUI activateButtonText;

        readonly CompositeDisposable disposables = new();

        public void Bind(CharacterTalentViewModel viewModel)
        {
            disposables.Clear();
            iconRoot.gameObject.SetActive(true);

            viewModel.SelectedPanelText
                .Subscribe(RefreshEffectText)
                .AddTo(disposables);
            viewModel.CanActivateSelected
                .Subscribe(SetActivateButton)
                .AddTo(disposables);
            viewModel.ActivateButtonText
                .Subscribe(RefreshActivateButtonText)
                .AddTo(disposables);

            activateButton.onClick.RemoveAllListeners();
            activateButton.onClick.AddListener(viewModel.ActivateSelectedTalent);
        }

        void RefreshEffectText(string text)
        {
            effectText.text = text;
        }

        void SetActivateButton(bool canActivate)
        {
            activateButton.interactable = canActivate;
        }

        void RefreshActivateButtonText(string text)
        {
            activateButtonText.text = text;
        }

        void OnDestroy()
        {
            disposables.Dispose();
        }
    }
}
