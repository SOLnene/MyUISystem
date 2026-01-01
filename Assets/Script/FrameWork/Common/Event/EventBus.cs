using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 事件总线，负责分发和监听事件
/// </summary>
public static class EventBus<T> where T:IEvent
{
    //这里的 readonly 并不是让不可修改，而是让字段本身的引用不可被重新赋值。
    static readonly HashSet<IEventBinding<T>> bindings = new HashSet<IEventBinding<T>>();

    public static void Register(EventBinding<T> binding) => bindings.Add(binding);
    public static void Deregister(EventBinding<T> binding) => bindings.Remove(binding);
    
    //@ 前缀：把关键字当成普通标识符
    /// <summary>
    /// 触发某个事件
    /// </summary>
    /// <param name="event"></param>
    public static void Raise(T @event)
    {
        var snapshot = new HashSet<IEventBinding<T>>(bindings);
        foreach (var binding in snapshot)
        {
            if (bindings.Contains(binding))
            {
                binding.OnEvent.Invoke(@event);
                binding.OnEventNoArgs.Invoke();
            }
        }
    }
    
    static void Clear()
    {
        Debug.Log($"Clearing {typeof(T).Name} bindings");
        bindings.Clear();
    }
    #region 弃用代码




    /*//这里的 readonly 并不是让字典不可修改，而是让 字段本身的引用不可被重新赋值。
    static readonly Dictionary<string, Action<object>> eventTable = new Dictionary<string, Action<object>>();

    public static void Subscribe(string eventName, Action<object> callback)
    {
        if(string.IsNullOrEmpty(eventName) || callback == null)
        {
            Debug.LogError("Subscribe: eventName or callback is null");
            return;
        }
        if (eventTable.ContainsKey(eventName))
        {
            eventTable[eventName] += callback;
        }
        else
        {
            eventTable[eventName] = callback;
        }
    }

    public static void UnSubscribe(string eventName, Action<object> callback)
    {
        if(string.IsNullOrEmpty(eventName) || callback == null)
        {
            Debug.LogError("UnSubscribe: eventName or callback is null");
            return;
        }
        if (eventTable.ContainsKey(eventName))
        {
            eventTable[eventName] -= callback;
            if (eventTable[eventName] == null)
            {
                eventTable.Remove(eventName);
            }
        }
    }

    public static void Publish(string eventName, object param = null)
    {
        if(string.IsNullOrEmpty(eventName))
        {
            Debug.LogError("Publish: eventName is null");
            return;
        }
        if (eventTable.ContainsKey(eventName))
        {
            eventTable[eventName]?.Invoke(param);
        }
    }

    /// <summary>
    /// 清空事件
    /// </summary>
    public static void Clear()
    {
        eventTable.Clear();
    }*/

     #endregion
}
