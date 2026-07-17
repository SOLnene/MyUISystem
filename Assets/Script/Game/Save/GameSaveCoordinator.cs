using UnityEngine;

public sealed class GameSaveCoordinator : SingletonMono<GameSaveCoordinator>
{
    const float SaveDelay = 1f;

    bool dirty;
    float saveAt;

    public void MarkDirty()
    {
        if (!GameContext.Instance.CanSave)
        {
            return;
        }

        dirty = true;
        saveAt = Time.realtimeSinceStartup + SaveDelay;
    }

    public bool Flush()
    {
        if (!dirty)
        {
            return true;
        }

        if (!GameContext.Instance.CanSave || !GameSaveSystem.TrySaveCurrentGame())
        {
            saveAt = Time.realtimeSinceStartup + SaveDelay;
            return false;
        }

        dirty = false;
        return true;
    }

    void Update()
    {
        if (dirty && Time.realtimeSinceStartup >= saveAt)
        {
            Flush();
        }
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            Flush();
        }
    }

    void OnApplicationQuit()
    {
        Flush();
    }
}
