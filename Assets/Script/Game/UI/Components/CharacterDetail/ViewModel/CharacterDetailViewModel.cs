using System;
using Game.Domain.Character;
using UniRx;
namespace Game.UI.Components.CharacterDetail
{
    public class CharacterDetailViewModel : IDisposable
    {
        public CharacterDetailContentViewModel contentViewModel;
        public CharacterEnhanceViewmodel enhanceViewmodel;
        public CharacterPromoteViewmodel promoteViewmodel;
        public ExpBookMaterialInput materialInput;
    
        CompositeDisposable disposable = new CompositeDisposable();
        public CharacterModel model;
        public CharacterDetailViewModel(CharacterModel model)
        {
            this.model = model;

            contentViewModel = new CharacterDetailContentViewModel(model);
            materialInput = new ExpBookMaterialInput();
            enhanceViewmodel = new CharacterEnhanceViewmodel(model, materialInput);
            promoteViewmodel = new CharacterPromoteViewmodel(model);
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
