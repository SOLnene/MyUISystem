using System;
using Game.Domain.Character;
using UniRx;
namespace Game.UI.Components.CharacterDetail
{
    public class CharacterDetailInfoViewModel : IDisposable
    {

        public CharacterModel model;
        public ReadOnlyReactiveProperty<string> ExpText { get; }
        public ReadOnlyReactiveProperty<float> ExpProgress { get; }

        CompositeDisposable disposable = new CompositeDisposable();
        public CharacterDetailInfoViewModel(CharacterModel model)
        {
            this.model = model;
            disposable.Clear();
            ExpText = model.LevelRP.CombineLatest(model.ExpRP,
                (level, exp) =>
                {
                    int maxExp = model.LevelSystem.GetExpRequired(level);
                    return $"{exp}/{maxExp}";
                }).ToReadOnlyReactiveProperty();
            ExpProgress = model.LevelRP.CombineLatest(model.ExpRP,
                (level, exp) =>
                {
                    int maxExp = model.LevelSystem.GetExpRequired(level);
                    return (float)exp/maxExp;
                }).ToReadOnlyReactiveProperty();
        }

        public void Dispose()
        {
            disposable.Dispose();
        }
    }
}
