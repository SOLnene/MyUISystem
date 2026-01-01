using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
///事件系统的全局管理工具
/// </summary>
public static class EventBusUtil
{
    public static IReadOnlyList<Type> EventTypes { get; set; }
    public static IReadOnlyList<Type> EventBusTypes { get; set; }
    
    #if UNITY_EDITOR
        public static PlayModeStateChange PlayModeState { get; set; }

        //[InitializeOnLoadMethod] 属性使得此方法在每次脚本加载时或在编辑器中游戏进入播放模式时都会被调用。
        //这有助于初始化类中在编辑状态下必需且在游戏进入播放模式时也适用的字段或状态。
        [InitializeOnLoadMethod]
        public static void InitializeEditor()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            PlayModeState = state;
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                ClearAllBuses();
            }
        }
    #endif

    //[RuntimeInitializeOnLoadMethod] 属性指示 Unity 在游戏加载完成但任何场景尚未加载时
    //（无论是在播放模式还是在构建运行之后）执行此方法
    //这确保了与消息总线相关的类型和事件的必要初始化在任何游戏对象、脚本或组件开始之前完成。
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Initialize()
    {
        EventTypes = PredefinedAssemblyUtil.GetTypes(typeof(IEvent));
        EventBusTypes = InitializeAllBuses();
    }

    /// <summary>
    /// 从所有继承自 IEvent 的类型中，创建对应的 EventBus<> 泛型类，并返回其 Type 列表
    /// </summary>
    /// <returns></returns>
    static List<Type> InitializeAllBuses()
    {
        List<Type> eventBusTypes = new List<Type>();
        var typedef = typeof(EventBus<>);
        foreach (var eventType in EventTypes)
        {
            var busType = typedef.MakeGenericType(eventType);
            eventBusTypes.Add(busType);
            Debug.Log($"初始化EventBus<{eventType.Name}>");
        }
        return eventBusTypes;
    }
    
    /// <summary>
    /// 调用所有EventBus的私有静态Clear方法，清空所有注册的事件
    /// </summary>
    public static void ClearAllBuses()
    {
        Debug.Log("Clearing all buses...");
        for (int i = 0; i < EventBusTypes.Count; i++)
        {
            var busType = EventBusTypes[i];
            var clearMethod = busType.GetMethod("Clear",BindingFlags.Static | BindingFlags.NonPublic);
            clearMethod?.Invoke(null, null);
        }
    }
}
