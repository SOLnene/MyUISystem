using UniRx;
namespace Game.UI.Components.CharacterDetail
{
    public class CharacterTabController
    {
        ReactiveProperty<CharacterTab> currentTab;
        public IReadOnlyReactiveProperty<CharacterTab> CurrentTab => currentTab;
    
        public CharacterTabController()
        {
            currentTab = new ReactiveProperty<CharacterTab>(CharacterTab.Overview);
        }

        public void SwitchTab(CharacterTab tab)
        {
            currentTab.Value = tab;
        }
    }
}
