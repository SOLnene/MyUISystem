using UniRx;

public interface IMainMenuRedDotProvider
{
    IReadOnlyReactiveProperty<bool> Observe(MainMenuRedDotKey key);
    void Set(MainMenuRedDotKey key, bool visible);
}
