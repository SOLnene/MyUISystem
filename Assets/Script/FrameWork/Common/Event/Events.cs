using Unity.VisualScripting;/// <summary>
/// 事件所需参数
/// </summary>
public interface IEvent { }

public struct TestEvent : IEvent { }

public struct FinishLoadingEvent : IEvent { }

public struct EnterGameEvent : IEvent { }

public struct PlayerEvent : IEvent {
    public int health;
    public int mana;
}

public struct OpenUIEvent : IEvent
{
    public UIType UIType { get; }
    public OpenUIEvent(UIType uiType)
    {
        UIType = uiType;
    }
}

public struct CloseUIEvent : IEvent
{
    public UIType UIType { get; }
    public CloseUIEvent(UIType uiType)
    {
        UIType = uiType;
    }
}

public struct LoadingProgressEvent : IEvent
{
    public float Progress { get; }
    public string Description { get; }

    public LoadingProgressEvent(float progress, string description)
    {
        Progress = progress;
        Description = description;
    }
}

/// <summary>
/// 与人物数据相关的事件
/// TODO:之后换用专有事件
/// </summary>
public struct PlayerStateEvent : IEvent
{
    public int characterId;
    public int hp;
    public int maxHp;
    public int level;
    public int charge;

    public PlayerStateEvent(PlayerStateEvent data)
    {
        characterId = data.characterId;
        hp = data.hp;
        maxHp = data.maxHp;
        level = data.level;
        charge = data.charge;
    }
}

/// <summary>
/// 主界面队伍面板选择角色事件
/// </summary>
public struct SelectCharacterEvent : IEvent
{
    public int index;
    public SelectCharacterEvent(int id)
    {
        index = id;
    }
}

public struct UpdateBottomHubEvent : IEvent
{
    /// <summary>需要显示的角色数据</summary>
    public PlayerStateEvent player;

    public UpdateBottomHubEvent(PlayerStateEvent playerData)
    {
        player = playerData;
    }
}