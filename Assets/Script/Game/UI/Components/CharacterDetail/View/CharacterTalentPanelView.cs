using System;
using Game.Domain.Character;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Components.CharacterDetail
{
    public class CharacterTalentPanelView : MonoBehaviour
    {
        public event Action TalentDetailOpened;
        public event Action TalentDetailClosed;

        [SerializeField]
        TalentNodeView[] talentNodes;
        [SerializeField]
        Button activateButton;
        [SerializeField]
        CharacterTalentSetDefinition talentSet;
        [SerializeField]
        TextMeshProUGUI effectText;
        [SerializeField]
        TalentEffectPanelView effectPanel;
        [SerializeField]
        Button closeClickAreaButton;

        readonly CompositeDisposable disposables = new();
        CharacterTalentViewModel vm;
        int selectedIndex = -1;
        bool isEffectPanelOpen;

        public void Bind(CharacterTalentViewModel viewModel)
        {
            disposables.Clear();
            vm = viewModel;
            selectedIndex = -1;
            isEffectPanelOpen = false;
            vm.SetTalentSet(talentSet);
            vm.TalentLevel
                .Subscribe(RefreshTalentLevel)
                .AddTo(disposables);

            if (effectPanel != null)
            {
                effectPanel.Bind(vm);
                effectPanel.HidePanel(true);
                closeClickAreaButton.gameObject.SetActive(false);
                closeClickAreaButton.onClick.RemoveAllListeners();
                closeClickAreaButton.onClick.AddListener(CloseSelectedNode);
            }
            else
            {
                vm.CanActivate
                    .Subscribe(SetActivateButton)
                    .AddTo(disposables);
                vm.SelectedNodeText
                    .Subscribe(RefreshSelectedNodeText)
                    .AddTo(disposables);

                activateButton.onClick.RemoveAllListeners();
                activateButton.onClick.AddListener(vm.ActivateTalent);
            }
        }

        void RefreshTalentLevel(int talentLevel)
        {
            int activeCount = Mathf.Clamp(talentLevel, 0, CharacterModel.MaxTalentLevel);
            int count = Mathf.Min(talentNodes.Length, vm.NodeCount);

            for (int i = 0; i < count; i++)
            {
                var node = talentSet.GetNode(i);
                talentNodes[i].Bind(i, i < activeCount, SelectNode);
                talentNodes[i].SetName(node?.Name ?? string.Empty);
            }

            if (effectPanel == null)
            {
                int defaultSelectedIndex = activeCount > 0 ? activeCount - 1 : 0;
                SelectNode(Mathf.Clamp(defaultSelectedIndex, 0, count - 1), true);
                return;
            }

            if (isEffectPanelOpen && selectedIndex >= 0 && selectedIndex < count)
            {
                SetSelectedNodeVisual(selectedIndex, true);
            }
            else
            {
                ClearSelectedNodeVisual(true);
            }
        }

        void SetActivateButton(bool canActivate)
        {
            activateButton.interactable = canActivate;
        }

        void RefreshSelectedNodeText(string text)
        {
            effectText.text = text;
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

            selectedIndex = index;
            vm.SelectNode(index);
            SetSelectedNodeVisual(index, instant);

            if (effectPanel != null)
            {
                bool wasEffectPanelOpen = isEffectPanelOpen;
                isEffectPanelOpen = true;
                closeClickAreaButton.gameObject.SetActive(true);
                if (!wasEffectPanelOpen)
                {
                    TalentDetailOpened?.Invoke();
                    effectPanel.ShowPanel(instant);
                }
            }
        }

        void CloseSelectedNode()
        {
            selectedIndex = -1;
            isEffectPanelOpen = false;
            ClearSelectedNodeVisual(false);
            closeClickAreaButton.gameObject.SetActive(false);
            TalentDetailClosed?.Invoke();
            effectPanel.HidePanel(false);
        }

        void SetSelectedNodeVisual(int index, bool instant)
        {
            for (int i = 0; i < talentNodes.Length; i++)
            {
                talentNodes[i].SetSelected(i == index, instant);
            }
        }

        void ClearSelectedNodeVisual(bool instant)
        {
            for (int i = 0; i < talentNodes.Length; i++)
            {
                talentNodes[i].SetSelected(false, instant);
            }
        }

        void OnDestroy()
        {
            disposables.Dispose();
        }
    }
}
