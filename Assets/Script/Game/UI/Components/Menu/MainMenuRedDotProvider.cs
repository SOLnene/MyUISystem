using System;
using System.Collections.Generic;
using UniRx;

public class MainMenuRedDotProvider : IMainMenuRedDotProvider, IDisposable
{
    private readonly Dictionary<MainMenuRedDotKey, IReadOnlyReactiveProperty<bool>> states =
        new Dictionary<MainMenuRedDotKey, IReadOnlyReactiveProperty<bool>>();

    private readonly ReactiveProperty<bool> noneState = new ReactiveProperty<bool>(false);

    public IReadOnlyReactiveProperty<bool> Observe(MainMenuRedDotKey key)
    {
        if (key == MainMenuRedDotKey.None)
        {
            return noneState;
        }

        return states.TryGetValue(key, out var state)
            ? state
            : noneState;
    }

    internal void Bind(
        MainMenuRedDotKey key,
        IReadOnlyReactiveProperty<bool> state)
    {
        states[key] = state;
    }

    public void Dispose()
    {
        states.Clear();
        noneState.Dispose();
    }
}
