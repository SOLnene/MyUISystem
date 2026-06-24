using System;
using Game.Domain.Character;
using UniRx;

namespace Game.UI.Components.CharacterDetail
{
    public class CharacterTalentViewModel : IDisposable
    {
        public IReadOnlyReactiveProperty<int> TalentLevel => model.TalentLevelRP;
        public IReadOnlyReactiveProperty<int> TalentTokenCount => model.TalentTokenCountRP;
        public IReadOnlyReactiveProperty<bool> CanActivate => canActivate;

        readonly CharacterModel model;
        readonly ReactiveProperty<bool> canActivate = new ReactiveProperty<bool>();
        readonly CompositeDisposable disposable = new CompositeDisposable();

        public CharacterTalentViewModel(CharacterModel model)
        {
            this.model = model;
            Observable.CombineLatest(
                    model.TalentLevelRP,
                    model.TalentTokenCountRP,
                    (level, tokenCount) => level < CharacterModel.MaxTalentLevel && tokenCount > 0)
                .Subscribe(value => canActivate.Value = value)
                .AddTo(disposable);
        }

        public void ActivateTalent()
        {
            model.TryActivateTalent();
        }

        public void Dispose()
        {
            disposable.Dispose();
            canActivate.Dispose();
        }
    }
}
