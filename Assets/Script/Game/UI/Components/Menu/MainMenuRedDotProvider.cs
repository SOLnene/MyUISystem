using System;
using System.Collections.Generic;
using UniRx;

public class MainMenuRedDotProvider : IMainMenuRedDotProvider, IDisposable
{
    private readonly Dictionary<MainMenuRedDotKey, ReactiveProperty<bool>> states =
        new Dictionary<MainMenuRedDotKey, ReactiveProperty<bool>>();

    private readonly ReactiveProperty<bool> noneState = new ReactiveProperty<bool>(false);

    public IReadOnlyReactiveProperty<bool> Observe(MainMenuRedDotKey key)
    {
        if (key == MainMenuRedDotKey.None)
        {
            return noneState;
        }

        if (!states.TryGetValue(key, out var state))
        {
            state = new ReactiveProperty<bool>(false);
            states.Add(key, state);
        }

        return state;
    }

    public void Set(MainMenuRedDotKey key, bool visible)
    {
        if (key == MainMenuRedDotKey.None)
        {
            return;
        }

        if (!states.TryGetValue(key, out var state))
        {
            state = new ReactiveProperty<bool>(visible);
            states.Add(key, state);
            return;
        }

        state.Value = visible;
    }

    public void Dispose()
    {
        foreach (var state in states.Values)
        {
            state.Dispose();
        }

        states.Clear();
        noneState.Dispose();
    }
}
