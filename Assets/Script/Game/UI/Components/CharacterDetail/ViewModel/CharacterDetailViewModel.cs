using System;
using System.Collections.Generic;
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
        readonly Subject<Unit> requestRebindCharacter = new Subject<Unit>();

      
        
        CompositeDisposable disposable = new CompositeDisposable();
        public CharacterModel model;
        IReadOnlyList<CharacterModel> ownedCharacters;
        internal IReadOnlyList<CharacterModel> OwnedCharacters => ownedCharacters;
        internal IObservable<Unit> RequestRebindCharacter => requestRebindCharacter;
        public CharacterDetailViewModel(CharacterModel model)
        {
            ownedCharacters = GameContext.Instance.CharacterRepository.Characters;
            CreateChildViewModels(model);
        }

        void CreateChildViewModels(CharacterModel model)
        {
            this.model = model;
            contentViewModel = new CharacterDetailContentViewModel(model);
            materialInput = new ExpBookMaterialInput();
            enhanceViewmodel = new CharacterEnhanceViewmodel(model, materialInput, RequestBackToMain);
            promoteViewmodel = new CharacterPromoteViewmodel(model, RequestBackToMain);
        }

        internal void SelectCharacter(CharacterModel character)
        {
            if (character == null || character == model)
            {
                return;
            }

            ReleaseChildViewModels();
            CreateChildViewModels(character);
            requestRebindCharacter.OnNext(Unit.Default);
        }

        void RequestBackToMain()
        {
            onBackToMain.Execute(Unit.Default);
            Debug.Log("VM:请求返回主界面");
        }

        void ReleaseChildViewModels()
        {
            enhanceViewmodel?.Dispose();
            enhanceViewmodel = null;
            promoteViewmodel?.Dispose();
            promoteViewmodel = null;
            contentViewModel?.Dispose();
            contentViewModel = null;
        }

        public void Dispose()
        {
            ReleaseChildViewModels();
            requestRebindCharacter.Dispose();

            disposable.Dispose();
        }
    }
}
