using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Domain.Character;
using SkierFramework;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Components.CharacterDetail
{
    public class CharacterPromoteView : BindableUI<CharacterPromoteViewmodel>
    {
        #region 控件绑定变量声明，自动生成请勿手改
		#pragma warning disable 0649
        [ControlBinding]
        private Image[] starIcons;
        [ControlBinding]
        private TextMeshProUGUI currentLevelText;
        [ControlBinding]
        private TextMeshProUGUI afterLevelText;
        [ControlBinding]
        private BindableUI[] attributes;
        [ControlBinding]
        private Button promoteBtn;
        [ControlBinding]
        private TextMeshProUGUI costText;
        [ControlBinding]
        private Transform materialParent;

		#pragma warning restore 0649
#endregion

        [SerializeField]
        UITopBar topBar;
        [SerializeField]
        AnimatedPanel topPanel;
        [SerializeField]
        AnimatedPanel infoPanel;
        [SerializeField]
        MaterialAreaFeedbackView feedbackView;
        [SerializeField]
        PromoteLevelResultFxView resultFxView;


        readonly CompositeDisposable disposable = new CompositeDisposable();

        public override void Bind(CharacterPromoteViewmodel viewmodel)
        {
            base.Bind(viewmodel);
            disposable.Clear();
            if (topBar == null)
            {
                topBar = GetComponentInChildren<UITopBar>(true);
            }

            if (topBar != null)
            {
                var characterModel = Vm.model as CharacterModel;
                var title = characterModel != null ? characterModel.Name.Value : string.Empty;
                topBar.Bind(title, new ReactiveProperty<int>(10000), Vm.onBack);
            }

            Vm.rank.Subscribe(value =>
            {
                for (int i = 0; i < starIcons.Length; i++)
                {
                    if (starIcons[i] == null) continue;
                    starIcons[i].color = i < value ? Color.white : Color.grey;
                }
            }).AddTo(disposable);

            Vm.currentLevelText.Subscribe(text =>
            {
                currentLevelText.text = text;
            }).AddTo(disposable);

            Vm.nextLevelCapText.Subscribe(
                text =>
                {
                    afterLevelText.text = text;
                }).AddTo(disposable);
            
            Vm.goldCostText.Subscribe(text =>
            {
                costText.text = text;
            }).AddTo(disposable);
            
            if (attributes == null || Vm.statItemViewModels == null) return;
            int count = Mathf.Min(attributes.Length, Vm.statItemViewModels.Count);
            for (int i = 0; i < count; i++)
            {
                if (attributes[i] == null) continue;
                attributes[i].Bind(Vm.statItemViewModels[i]);
            }
            
            promoteBtn.onClick.RemoveAllListeners();
            promoteBtn.onClick.AddListener(Promote);
        }

        public async UniTask ShowPanel(bool instant)
        {
            gameObject.SetActive(true);
            Vm.RefreshMaterialVMs();
            CreateMaterialViews().Forget();
            ShowPromoteNormal(false);
            await UniTask.WhenAll(
                topPanel.Show(instant),
                infoPanel.Show(instant)
            );
        }

        public async UniTask HidePanel(bool instant)
        {
            await UniTask.WhenAll(
                topPanel.Hide(instant),
                infoPanel.Hide(instant)
            );
            gameObject.SetActive(false);
        }

        public void ShowPromoteProcessing()
        {
            feedbackView.ShowProcessing();
        }

        public void ShowPromoteNormal(bool playMaterialEnter)
        {
            feedbackView.ShowNormal(playMaterialEnter);
        }

        public void ShowPromoteResultText(string text)
        {
            feedbackView.ShowResultText(text);
        }

        public UniTask PlayPromoteResult(PromoteLevelResultData result, Action onNewStateShown = null)
        {
            return resultFxView.Play(result, onNewStateShown);
        }

        async UniTask CreateMaterialViews()
        {
            // 清空旧UI
            foreach (Transform child in materialParent)
            {
                Destroy(child.gameObject);
            }

            if (Vm.itemSlotViewModels == null) return;

            // 只加载一次 prefab
            foreach (var materialVM in Vm.itemSlotViewModels)
            {
                var go =await ResourceManager.Instance.InstantiateItemAsync(
                    "ui/prefab/item_slot_material",materialParent,true);

                var itemSlotView = go.GetComponent<ItemSlotView>();
                if (itemSlotView == null)
                    itemSlotView = go.AddComponent<ItemSlotView>();

                itemSlotView.Bind(materialVM);
            }
        }
        
        void Promote()
        {
            Vm.Promote();
        }
        
        void OnDestroy()
        {
            disposable.Dispose();
        }
    }
}
