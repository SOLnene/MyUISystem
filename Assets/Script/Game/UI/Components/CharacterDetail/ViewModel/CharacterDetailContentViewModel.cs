using System;
using Game.Domain.Character;
using UniRx;
namespace Game.UI.Components.CharacterDetail
{
    public class CharacterDetailContentViewModel : IDisposable
    {
        public CharacterDetailTabViewModel TabViewModel { get; private set; }
        public CharacterDetailPreviewViewModel PreviewViewModel { get; private set; }
        public CharacterDetailInfoViewModel InfoViewModel { get; private set; }

        public readonly ReactiveProperty<CharacterModel> CurrentCharacter = new ReactiveProperty<CharacterModel>();
    
        CompositeDisposable disposable;
    
        public CharacterDetailContentViewModel(CharacterModel model)
        {
            CurrentCharacter.Value = model;
        
            TabViewModel = new CharacterDetailTabViewModel(model);
            PreviewViewModel = new CharacterDetailPreviewViewModel(model);
            InfoViewModel = new CharacterDetailInfoViewModel(model);
        }

        public void Dispose()
        {
       
        }
    }
}
