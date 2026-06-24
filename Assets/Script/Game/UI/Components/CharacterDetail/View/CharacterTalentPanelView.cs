using Game.Domain.Character;
using UniRx;
using UnityEngine;

namespace Game.UI.Components.CharacterDetail
{
    public class CharacterTalentPanelView : MonoBehaviour
    {
        [SerializeField]
        TalentNodeView[] talentNodes;

        readonly CompositeDisposable disposables = new();
        int selectedIndex = -1;

        public void Bind(CharacterModel character)
        {
            disposables.Clear();
            character.TalentLevelRP
                .Subscribe(RefreshTalentLevel)
                .AddTo(disposables);
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
