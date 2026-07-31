using System;
using DG.Tweening;
using TMPro;
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
        Image selectedBg;
        [SerializeField]
        TalentNodeIconView iconView;
        [SerializeField]
        TextMeshProUGUI nameText;

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
        Vector3 selectedBgBaseScale;
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

        internal void SetName(string name)
        {
            nameText.text = name;
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
            if (isSelected)
            {
                return;
            }

            RefreshFocusVisual(false);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            if (isSelected)
            {
                return;
            }

            RefreshFocusVisual(false);
        }

        void RefreshFocusVisual(bool instant, bool playSelectEnter = false)
        {
            CacheVisualState();
            stateSequence?.Kill();

            bool visible = isSelected || isHovered;
            Color targetColor = GetTargetColor();
            Vector3 targetSelectBgScale = visible ? selectBgBaseScale : new Vector3(selectBgBaseScale.x, 0f, selectBgBaseScale.z);
            Vector3 targetSelectedBgScale = isSelected
                ? selectedBgBaseScale
                : new Vector3(selectedBgBaseScale.x, 0f, selectedBgBaseScale.z);

            if (instant)
            {
                selectedBg.color = isSelected ? selectColor : ClearColor(selectColor);
                selectedBg.rectTransform.localScale = targetSelectedBgScale;
                selectBg.color = isSelected ? ClearColor(selectPeakColor) : targetColor;
                selectBg.rectTransform.localScale = targetSelectBgScale;
                return;
            }

            stateSequence = DOTween.Sequence();
            if (isSelected)
            {
                selectedBg.color = ClearColor(selectColor);
                selectedBg.rectTransform.localScale = new Vector3(selectedBgBaseScale.x, 0f, selectedBgBaseScale.z);
                stateSequence.Join(selectedBg.DOColor(selectColor, selectedBgExpandDuration).SetEase(Ease.OutCubic));
                stateSequence.Join(selectedBg.rectTransform.DOScale(targetSelectedBgScale, selectedBgExpandDuration).SetEase(Ease.OutCubic));

                if (playSelectEnter)
                {
                    selectBg.color = ClearColor(selectPeakColor);
                    selectBg.rectTransform.localScale = new Vector3(selectBgBaseScale.x, 0f, selectBgBaseScale.z);
                    stateSequence.Join(selectBg.DOColor(selectPeakColor, selectedBgExpandDuration).SetEase(Ease.OutCubic));
                    stateSequence.Join(selectBg.rectTransform.DOScale(selectBgBaseScale, selectedBgExpandDuration).SetEase(Ease.OutCubic));
                    stateSequence.Append(selectBg.DOFade(0f, selectedFadeDuration).SetEase(Ease.OutCubic));
                }
                else
                {
                    stateSequence.Join(selectBg.DOFade(0f, stateDuration).SetEase(Ease.OutCubic));
                    stateSequence.Join(selectBg.rectTransform.DOScale(
                        new Vector3(selectBgBaseScale.x, 0f, selectBgBaseScale.z),
                        stateDuration).SetEase(Ease.OutCubic));
                }

                return;
            }

            stateSequence.Join(selectedBg.DOColor(ClearColor(selectColor), stateDuration).SetEase(Ease.OutCubic));
            stateSequence.Join(selectedBg.rectTransform.DOScale(
                new Vector3(selectedBgBaseScale.x, 0f, selectedBgBaseScale.z),
                stateDuration).SetEase(Ease.OutCubic));

            if (visible && selectBg.rectTransform.localScale.y <= 0.01f)
            {
                selectBg.color = ClearColor(targetColor);
                selectBg.rectTransform.localScale = new Vector3(selectBgBaseScale.x, 0f, selectBgBaseScale.z);
                stateSequence.Join(selectBg.DOColor(targetColor, selectedBgExpandDuration).SetEase(Ease.OutCubic));
                stateSequence.Join(selectBg.rectTransform.DOScale(targetSelectBgScale, selectedBgExpandDuration).SetEase(Ease.OutCubic));
            }
            else
            {
                stateSequence.Join(selectBg.DOColor(targetColor, stateDuration).SetEase(Ease.OutCubic));
                stateSequence.Join(selectBg.rectTransform.DOScale(targetSelectBgScale, stateDuration).SetEase(Ease.OutCubic));
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
            selectedBgBaseScale = selectedBg.rectTransform.localScale;
            selectBg.color = ClearColor(hoverColor);
            selectBg.rectTransform.localScale = new Vector3(selectBgBaseScale.x, 0f, selectBgBaseScale.z);
            selectedBg.color = ClearColor(selectColor);
            selectedBg.rectTransform.localScale = new Vector3(selectedBgBaseScale.x, 0f, selectedBgBaseScale.z);

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
