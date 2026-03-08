using System;
using System.Collections.Generic;
using Game.Domain.Character;
using UniRx;
using UnityEngine;
using UnityEngine.TextCore.Text;
namespace Game.UI.Components.CharacterDetail
//todo:做成通用
{
    public class CharacterEnhanceViewmodel : IDisposable
    {
        public IEnhanceable model;
        public CharacterLevelPreviewViewmodel previewVm;
        public IMaterialInput materialInput;
        
        public List<StatItemViewModel> statItemViewModels;

        /// <summary>
        /// itemSlots的vm
        /// </summary>
        public List<ItemSlotViewModel> itemViewModels = new List<ItemSlotViewModel>();
        
        //专用 todo:可以不用rp
        public ReactiveProperty<ExpBookType> selectedBook = new ReactiveProperty<ExpBookType>(ExpBookType.None);
        
        CompositeDisposable disposable = new CompositeDisposable();
        public CharacterEnhanceViewmodel(IEnhanceable model,IMaterialInput materialInput)
        {
            this.model = model;
            this.materialInput = materialInput;
            previewVm = new CharacterLevelPreviewViewmodel(model,materialInput.TotalExpRp);
            
            var statItemList = new List<StatItemViewModel>();
            var previews = model.GetStatPreview(0);
            foreach (var preview in previews)
            {
                var statItem = new StatItemViewModel(
                    icon: null, label: preview.label);
                statItem.SetValue(preview.currentValue, preview.nextValue);
                statItemList.Add(statItem);
            }
            statItemViewModels = statItemList;
            
            //材料部分
            CreateItemViewmodel("expbook_small");
            CreateItemViewmodel("expbook_medium");
            CreateItemViewmodel( "expbook_large");
            
            materialInput.TotalExpRp.Subscribe(
                value =>
                {
                    UpdatePreview(value);
                }).AddTo(disposable);
           
        }

        public void UpdatePreview(int exp)
        {
            var previews = model.GetStatPreview(exp);

            for (int i = 0; i < previews.Count; i++)
            {
                statItemViewModels[i].SetValue(previews[i].currentValue, previews[i].nextValue);
                Debug.Log($"{previews[i].label} current: {previews[i].currentValue} next: {previews[i].nextValue}");
            }
        }

        //与materialinput做转接
        public string ChangeToKey(ExpBookType type)
        {
            switch (type)
            {
                case ExpBookType.Small:
                    return "expbook_small";
                case ExpBookType.Medium:
                    return "expbook_medium";
                case ExpBookType.Large:
                    return "expbook_large";
                default:
                    Debug.Log("invalid book type");
                    break;
            }
            throw new ArgumentException($"Invalid book type: {type}");
        }

        public ExpBookType ChangeToBook(string key)
        {
            switch (key)
            {
                case  "expbook_small":
                    return ExpBookType.Small;
                case "expbook_medium":
                    return ExpBookType.Medium;
                case "expbook_large":
                    return ExpBookType.Large;
                default:
                    Debug.Log("invalid book definition");
                    break;
            }
            return ExpBookType.None;
        }
        
        public void AddBook(ExpBookType type, int count = 1)
        {
            if(type!= ExpBookType.None)
            materialInput.Add(ChangeToKey(type),count);
        }
        
        public void RemoveBook(ExpBookType type, int count = 1)
        {
            if(type!= ExpBookType.None)
            materialInput.Remove(ChangeToKey(type),count);
        }
        
        public int GetCurrentBookCount()
        {
            if(selectedBook.Value == ExpBookType.None) return 0;
            var key = ChangeToKey(selectedBook.Value);
            
            return materialInput.Counts[key].Value;
        }
        
        public void ConfirmEnhance()
        {
            int exp = materialInput.GetTotalExp();
            model.LevelSystem.AddExp(exp);
        }

        public void CreateItemViewmodel(string key)
        {
            var item = new InventoryItem(GameDatabase.ItemDatabase.GetItemByKey(key));
            var viewModel = new ItemSlotViewModel(item);
            viewModel.onClick.Subscribe(
                _ =>
                {
                    SetSelectedBook(ChangeToBook(key));
                }).AddTo(disposable);
            selectedBook.Subscribe(
                value =>
                {
                    viewModel.isSelected.Value = value == ChangeToBook(key);
                }).AddTo(disposable);
            materialInput.Counts[key]
                .Subscribe(count =>
                {
                    viewModel.selectedCount.Value = count;
                })
                .AddTo(disposable);
            itemViewModels.Add(viewModel);
            
        }
        
        public void SetSelectedBook(ExpBookType type)
        {
            selectedBook.Value = type;
        }
        
        public void Dispose()
        {
            disposable.Dispose();
        }
    }
}
