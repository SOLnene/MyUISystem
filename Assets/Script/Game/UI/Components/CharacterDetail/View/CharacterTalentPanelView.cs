using Game.Domain.Character;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Components.CharacterDetail
{
    public class CharacterTalentPanelView : MonoBehaviour
    {
        [SerializeField]
        TalentNodeView[] talentNodes;
        [SerializeField]
        TextMeshProUGUI talentTokenText;
        [SerializeField]
        Button activateButton;

        readonly CompositeDisposable disposables = new();
        CharacterTalentViewModel vm;
        int selectedIndex = -1;

        public void Bind(CharacterTalentViewModel viewModel)
        {
            disposables.Clear();
            vm = viewModel;
            vm.TalentLevel
                .Subscribe(RefreshTalentLevel)
                .AddTo(disposables);
            vm.TalentTokenCount
                .Subscribe(RefreshTalentToken)
                .AddTo(disposables);
            vm.CanActivate
                .Subscribe(SetActivateButton)
                .AddTo(disposables);

            activateButton.onClick.RemoveAllListeners();
            activateButton.onClick.AddListener(vm.ActivateTalent);
        }

        void RefreshTalentLevel(int talentLevel)
        {
            int activeCount = Mathf.Clamp(talentLevel, 0, CharacterModel.MaxTalentLevel);
            int count = Mathf.Min(talentNodes.Length, CharacterModel.MaxTalentLevel);

            for (int i = 0; i < count; i++)
            {
                talentNodes[i].Bind(i, i < activeCount, SelectNode);
            }

            int defaultSelectedIndex = activeCount > 0 ? activeCount - 1 : 0;
            SelectNode(Mathf.Clamp(defaultSelectedIndex, 0, count - 1), true);
        }

        void RefreshTalentToken(int count)
        {
            talentTokenText.text = count.ToString();
        }

        void SetActivateButton(bool canActivate)
        {
            activateButton.interactable = canActivate;
        }

        void SelectNode(int index)
        {
            SelectNode(index, false);
        }

        void SelectNode(int index, bool instant)
        {
            if (index < 0 || index >= talentNodes.Length)
            {
                return;
            }

            if (selectedIndex == index && !instant)
            {
                return;
            }

            selectedIndex = index;
            for (int i = 0; i < talentNodes.Length; i++)
            {
                talentNodes[i].SetSelected(i == selectedIndex, instant);
            }
        }

        void OnDestroy()
        {
            disposables.Dispose();
        }
    }
}
