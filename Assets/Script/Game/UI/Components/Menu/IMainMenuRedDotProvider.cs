using UniRx;

public interface IMainMenuRedDotProvider
{
    IReadOnlyReactiveProperty<bool> Observe(MainMenuRedDotKey key);
}
