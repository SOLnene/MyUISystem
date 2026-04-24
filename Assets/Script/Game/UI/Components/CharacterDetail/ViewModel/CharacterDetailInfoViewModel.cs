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
        
        //升级按钮显示文本
        public ReadOnlyReactiveProperty<string> EntryBtnText { get; }
        public AttributePageViewModel AttributeViewModel { get; }
        
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
                }).ToReadOnlyReactiveProperty()
                .AddTo(disposable);
            ExpProgress = model.LevelRP.CombineLatest(model.ExpRP,
                (level, exp) =>
                {
                    int maxExp = model.LevelSystem.GetExpRequired(level);
                    return (float)exp/maxExp;
                }).ToReadOnlyReactiveProperty()
                .AddTo(disposable);
            
            EntryBtnText = model.LevelRP
                .Select(level => level < model.GetMaxLevel() ? "升级" : "突破")
                .ToReadOnlyReactiveProperty()
                .AddTo(disposable);
            
            AttributeViewModel = new AttributePageViewModel(model);
        }

        public void Dispose()
        {
            AttributeViewModel?.Dispose();
            disposable.Dispose();
        }
    }
}
