using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Components.CharacterDetail
{
    public class TalentNodeView : UIThreeStateSelectable
    {
        [SerializeField]
        Button button;
        [SerializeField]
        RectTransform iconRoot;
        [SerializeField]
        Image selectBg;
        [SerializeField]
        GameObject lockIcon;
        [SerializeField]
        GameObject magicBg;

        [SerializeField]
        float hoverSelectBgAlpha = 0.5f;
        [SerializeField]
        float selectedSelectBgAlpha = 0.75f;
        [SerializeField]
        float selectedBgExpandDuration = 0.18f;
        [SerializeField]
        float stateDuration = 0.18f;

        Vector3 selectBgBaseScale;
        Sequence stateSequence;
        bool isCached;

        public void Bind(int index, bool active, Action<int> clickHandler)
        {
            CacheVisualState();
            SetActiveState(active);

            button.transition = Selectable.Transition.None;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => clickHandler?.Invoke(index));

            SetSelected(IsSelected, true);
        }

        public void SetActiveState(bool active)
        {
            lockIcon.SetActive(!active);
            magicBg.SetActive(active);
        }

        protected override void ApplyVisualState(VisualState state, bool instant, bool stateChanged)
        {
            CacheVisualState();
            stateSequence?.Kill();

            bool selected = state == VisualState.Selected;
            bool hover = state == VisualState.Hover;
            float selectBgAlpha = selected ? selectedSelectBgAlpha : hover ? hoverSelectBgAlpha : 0f;
            Vector3 targetSelectBgScale = selectBgBaseScale;

            if (instant || !stateChanged)
            {
                SetImageAlpha(selectBg, selectBgAlpha);
                selectBg.rectTransform.localScale = targetSelectBgScale;
                return;
            }

            stateSequence = DOTween.Sequence();
            if (selected)
            {
                SetImageAlpha(selectBg, 0f);
                selectBg.rectTransform.localScale = new Vector3(selectBgBaseScale.x, 0f, selectBgBaseScale.z);
                stateSequence.Join(selectBg.DOFade(selectBgAlpha, selectedBgExpandDuration).SetEase(Ease.OutCubic));
                stateSequence.Join(selectBg.rectTransform.DOScale(targetSelectBgScale, selectedBgExpandDuration).SetEase(Ease.OutCubic));
                return;
            }

            stateSequence.Join(selectBg.DOFade(selectBgAlpha, stateDuration).SetEase(Ease.OutCubic));
            stateSequence.Join(selectBg.rectTransform.DOScale(targetSelectBgScale, stateDuration).SetEase(Ease.OutCubic));
        }

        void CacheVisualState()
        {
            if (isCached)
            {
                return;
            }

            selectBgBaseScale = selectBg.rectTransform.localScale;
            SetImageAlpha(selectBg, 0f);
            isCached = true;
        }

        static void SetImageAlpha(Image image, float alpha)
        {
            Color color = image.color;
            color.a = alpha;
            image.color = color;
        }

        void OnDestroy()
        {
            stateSequence?.Kill();
        }
    }
}
