using System;
using Game.Domain.Character;
using UniRx;
namespace Game.UI.Components.CharacterDetail
{
    
    public class CharacterDetailContentViewModel : IDisposable
    {
        //todo:弃用 不拆那么细
        public CharacterDetailTabViewModel TabViewModel { get; private set; }
        public CharacterDetailPreviewViewModel PreviewViewModel { get; private set; }
        public CharacterDetailInfoViewModel InfoViewModel { get; private set; }
        public CharacterDetailEquipPageViewModel EquipPageViewModel { get; private set; }
        
        public readonly ReactiveProperty<CharacterModel> currentCharacter = new ReactiveProperty<CharacterModel>();
        
        
        CompositeDisposable disposable  = new CompositeDisposable();
    
        public CharacterDetailContentViewModel(CharacterModel model)
        {
            currentCharacter.Value = model;
        
            TabViewModel = new CharacterDetailTabViewModel(model);
            PreviewViewModel = new CharacterDetailPreviewViewModel(model);
            InfoViewModel = new CharacterDetailInfoViewModel(model);
            EquipPageViewModel = new CharacterDetailEquipPageViewModel(model);
        }

        public void Dispose()
        {
            TabViewModel.Dispose();
            PreviewViewModel.Dispose();
            InfoViewModel.Dispose();
            EquipPageViewModel.Dispose();
            disposable.Dispose();
        }
    }
}
