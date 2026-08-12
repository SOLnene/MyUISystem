using UnityEditor;

internal static class EventBusEditorLifecycle
{
    //[InitializeOnLoadMethod] 属性使得此方法在每次脚本加载时或在编辑器中游戏进入播放模式时都会被调用。
    //这有助于初始化类中在编辑状态下必需且在游戏进入播放模式时也适用的字段或状态。
    [InitializeOnLoadMethod]
    static void Initialize()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            EventBusUtil.ClearAllBuses();
        }
    }
}
