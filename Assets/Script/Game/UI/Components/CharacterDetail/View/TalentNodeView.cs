using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI.Components.CharacterDetail
{
    public class TalentNodeView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField]
        Button button;
        [SerializeField]
        Image selectBg;
        [SerializeField]
        TalentNodeIconView iconView;

        [SerializeField]
        Color hoverColor = new Color(0.45f, 0.9f, 1f, 0.55f);
        [SerializeField]
        Color selectPeakColor = new Color(0.78f, 1f, 1f, 0.85f);
        [SerializeField]
        Color selectColor = new Color(0.28f, 0.72f, 0.95f, 0.35f);
        [SerializeField]
        float selectedBgExpandDuration = 0.18f;
        [SerializeField]
        float stateDuration = 0.18f;
        [SerializeField]
        float selectedFadeDuration = 0.42f;

        Vector3 selectBgBaseScale;
        Sequence stateSequence;
        bool isCached;
        bool isSelected;
        bool isHovered;

        public void Bind(int index, bool active, Action<int> clickHandler)
        {
            CacheVisualState();
            iconView.SetActiveState(active);

            button.transition = Selectable.Transition.None;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => clickHandler?.Invoke(index));
        }

        public void SetSelected(bool selected, bool instant)
        {
            bool wasSelected = isSelected;
            isSelected = selected;
            RefreshFocusVisual(instant, selected && !wasSelected);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;
            RefreshFocusVisual(false);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            RefreshFocusVisual(false);
        }

        void RefreshFocusVisual(bool instant, bool playSelectEnter = false)
        {
            CacheVisualState();
            stateSequence?.Kill();

            bool visible = isSelected || isHovered;
            Color targetColor = GetTargetColor();
            Vector3 targetSelectBgScale = visible ? selectBgBaseScale : new Vector3(selectBgBaseScale.x, 0f, selectBgBaseScale.z);

            if (instant)
            {
                selectBg.color = targetColor;
                selectBg.rectTransform.localScale = targetSelectBgScale;
                return;
            }

            stateSequence = DOTween.Sequence();
            if (visible && selectBg.rectTransform.localScale.y <= 0.01f)
            {
                selectBg.color = ClearColor(targetColor);
                selectBg.rectTransform.localScale = new Vector3(selectBgBaseScale.x, 0f, selectBgBaseScale.z);
                stateSequence.Join(selectBg.DOColor(playSelectEnter ? selectPeakColor : targetColor, selectedBgExpandDuration).SetEase(Ease.OutCubic));
                stateSequence.Join(selectBg.rectTransform.DOScale(targetSelectBgScale, selectedBgExpandDuration).SetEase(Ease.OutCubic));
            }
            else
            {
                stateSequence.Join(selectBg.DOColor(playSelectEnter ? selectPeakColor : targetColor, stateDuration).SetEase(Ease.OutCubic));
                stateSequence.Join(selectBg.rectTransform.DOScale(targetSelectBgScale, stateDuration).SetEase(Ease.OutCubic));
            }

            if (playSelectEnter)
            {
                stateSequence.Append(selectBg.DOColor(selectColor, selectedFadeDuration).SetEase(Ease.OutCubic));
            }
        }

        Color GetTargetColor()
        {
            if (isSelected)
            {
                return selectColor;
            }

            return isHovered ? hoverColor : ClearColor(hoverColor);
        }

        void CacheVisualState()
        {
            if (isCached)
            {
                return;
            }

            selectBgBaseScale = selectBg.rectTransform.localScale;
            selectBg.color = ClearColor(hoverColor);

            isCached = true;
        }

        static Color ClearColor(Color color)
        {
            color.a = 0f;
            return color;
        }

        void OnDestroy()
        {
            stateSequence?.Kill();
        }
    }
}
