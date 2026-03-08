using System;
using Game.Domain.Character;
using UniRx;
namespace Game.UI.Components.CharacterDetail
{
    public class CharacterDetailViewModel : IDisposable
    {
        public CharacterDetailContentViewModel contentViewModel;
    
        CompositeDisposable disposable = new CompositeDisposable();
        public CharacterModel model;
        public CharacterDetailViewModel(CharacterModel model)
        {
            this.model = model;

            contentViewModel = new CharacterDetailContentViewModel(model);
        }
    

        public void Dispose()
        {
        

            disposable.Dispose();
        }
    }
}
