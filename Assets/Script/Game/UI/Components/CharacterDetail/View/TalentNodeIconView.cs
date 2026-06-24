using UnityEngine;

namespace Game.UI.Components.CharacterDetail
{
    public class TalentNodeIconView : MonoBehaviour
    {
        [SerializeField]
        RectTransform iconRoot;
        [SerializeField]
        GameObject lockIcon;
        [SerializeField]
        GameObject magicBg;

        public RectTransform IconRoot => iconRoot;

        public void SetActiveState(bool active)
        {
            lockIcon.SetActive(!active);
            magicBg.SetActive(active);
        }
    }
}
