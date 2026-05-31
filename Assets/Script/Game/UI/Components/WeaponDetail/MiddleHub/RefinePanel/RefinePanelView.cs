    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using Cysharp.Threading.Tasks;
    using DG.Tweening;
    using TMPro;
    using UniRx;
    using UnityEngine;
    using UnityEngine.Serialization;
    using UnityEngine.UI;

    public class RefinePanelView : MonoBehaviour
    {
        [SerializeField]
        TextMeshProUGUI refineLevelText;
        [SerializeField]
        Button quickAddButton;
        [FormerlySerializedAs("itemContent")]
        [SerializeField]
        GameObject slotContent;
        [SerializeField]
        List<ItemSlotView> slotViews;
        [FormerlySerializedAs("materialArea")]
        [SerializeField]
        GameObject materialNormalRoot;
        [SerializeField]
        CanvasGroup materialAreaGroup;
        [SerializeField]
        RectTransform materialAreaRoot;
        [SerializeField]
        MaterialResultFxView materialResultFxView;
        [FormerlySerializedAs("normalRootPanel")]
        [SerializeField]
        AnimatedPanel perviewNormalRoot;
        [SerializeField]
        RefineRankResultFxView refineRankResultFxView;
        [SerializeField]
        float materialFadeDuration = 0.12f;
        [SerializeField]
        float materialEnterDuration = 0.18f;
        [SerializeField]
        float materialEnterOffsetY = 8f;
        RefinePanelViewModel vm;

        CompositeDisposable disposable = new CompositeDisposable();
        Sequence materialSequence;
        Vector2 materialDefaultPos;
        bool hasMaterialDefaultPos;

        ItemSlotView slotPrefab;
        public void Bind(RefinePanelViewModel viewModel)
        {
            disposable.Clear();
            
            vm = viewModel;
            vm.equipItem.Value.refineLevel.Subscribe(level =>
            {
                refineLevelText.text = $"{level}阶";
            }).AddTo(disposable);

            if(slotViews == null || slotViews.Count == 0)
            {
                slotViews = slotContent.GetComponentsInChildren<ItemSlotView>().ToList();
            }

            quickAddButton.onClick.AsObservable().Subscribe(_ =>
            {
                vm.OnQuickAddClicked();
            }).AddTo(disposable);
            
            //格子绑定
            for(int i=0;i<vm.slotViewModels.Count;i++)
            {
                var slotVM = vm.slotViewModels[i];
                var slotView = slotViews[i];
                slotView.Bind(slotVM);
                slotVM.onClick.Subscribe(_ =>
                {
                    vm.OnSlotClick(slotVM);
                }).AddTo(disposable);
            }

            Refresh();
        }

        public void Refresh()
        {
            if (refineRankResultFxView != null)
                refineRankResultFxView.HideImmediate();

            perviewNormalRoot.Show(true).Forget();
            materialResultFxView.Hide();

            materialNormalRoot.SetActive(true);
            materialAreaGroup.alpha = 1f;
        }

        public async UniTask PlayRefineResult(RefineResultData result, Action onResultAccentComplete = null)
        {
            await perviewNormalRoot.Hide();
            try
            {
                await refineRankResultFxView.Play(result.oldRefineLevel, result.newRefineLevel, onResultAccentComplete);
            }
            finally
            {
                refineRankResultFxView.HideImmediate();
                await perviewNormalRoot.Show();
            }
        }

        public void ShowRefineProcessing()
        {
            PrepareMaterialFx();
            materialNormalRoot.SetActive(true);
            materialResultFxView.Hide();

            materialSequence = DOTween.Sequence().SetUpdate(true);
            materialSequence.Join(materialAreaGroup.DOFade(0f, materialFadeDuration).SetEase(Ease.OutQuad));
            materialSequence.OnComplete(materialResultFxView.ShowLoading);
        }

        public void ShowRefineNormal(bool playMaterialEnter)
        {
            PrepareMaterialFx();
            materialNormalRoot.SetActive(true);
            materialResultFxView.Hide();

            materialSequence = DOTween.Sequence().SetUpdate(true);

            if (playMaterialEnter)
                AppendMaterialEnter(materialSequence);
            else
                materialAreaGroup.alpha = 1f;
        }

        public void ShowRefineMaxText(string text)
        {
            PrepareMaterialFx();
            materialAreaGroup.alpha = 0f;
            materialNormalRoot.SetActive(false);
            materialResultFxView.ShowMaxText(text);
        }

        void AppendMaterialEnter(Sequence sequence)
        {
            materialAreaGroup.alpha = 0f;
            materialAreaRoot.anchoredPosition = materialDefaultPos + new Vector2(0f, materialEnterOffsetY);

            sequence.Join(materialAreaGroup.DOFade(1f, materialEnterDuration).SetEase(Ease.OutQuad));
            sequence.Join(materialAreaRoot.DOAnchorPos(materialDefaultPos, materialEnterDuration).SetEase(Ease.OutCubic));
        }

        void PrepareMaterialFx()
        {
            CacheMaterialDefaultPosition();
            KillMaterialSequence();
        }

        void CacheMaterialDefaultPosition()
        {
            if (hasMaterialDefaultPos)
                return;

            materialDefaultPos = materialAreaRoot.anchoredPosition;
            hasMaterialDefaultPos = true;
        }

        void KillMaterialSequence()
        {
            if (materialSequence == null)
                return;

            materialSequence.Kill();
            materialSequence = null;
        }

        void OnDestroy()
        {
            KillMaterialSequence();
        }
        
    }
