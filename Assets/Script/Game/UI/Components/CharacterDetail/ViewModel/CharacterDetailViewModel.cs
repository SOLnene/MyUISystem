using System;
using Game.Domain.Character;
using UniRx;
using UnityEngine;
namespace Game.UI.Components.CharacterDetail
{
    public class CharacterDetailViewModel : IDisposable
    {
        public CharacterDetailContentViewModel contentViewModel;
        public CharacterEnhanceViewmodel enhanceViewmodel;
        public CharacterPromoteViewmodel promoteViewmodel;
        public ExpBookMaterialInput materialInput;
        public ReactiveCommand onBackToMain = new ReactiveCommand();

      
        
        CompositeDisposable disposable = new CompositeDisposable();
        public CharacterModel model;
        public CharacterDetailViewModel(CharacterModel model)
        {
            this.model = model;

            contentViewModel = new CharacterDetailContentViewModel(model);
            materialInput = new ExpBookMaterialInput();
            enhanceViewmodel = new CharacterEnhanceViewmodel(model, materialInput, RequestBackToMain);
            promoteViewmodel = new CharacterPromoteViewmodel(model, RequestBackToMain);
        }

        void RequestBackToMain()
        {
            onBackToMain.Execute(Unit.Default);
            Debug.Log("VM:请求返回主界面");
        }
    

        public void Dispose()
        {
            enhanceViewmodel?.Dispose();
            enhanceViewmodel = null;
            promoteViewmodel?.Dispose();
            promoteViewmodel = null;
            contentViewModel?.Dispose();
            contentViewModel = null;

            disposable.Dispose();
        }
    }
}
