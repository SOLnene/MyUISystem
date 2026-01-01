using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IEventBinding<T>
{
    public Action<T> OnEvent { get; set; }
    public Action OnEventNoArgs { get; set; }
}

/// <summary>
/// 表示一个事件绑定，将特定事件类型 <typeparamref name="T"/> 与回调函数关联。
/// </summary>
/// <typeparam name="T"></typeparam>
public class EventBinding<T> : IEventBinding<T> where T:IEvent
{
    /*默认赋值（避免空引用）
    如果你不写默认值，初始值是 null。
    当事件触发时，如果没有人注册回调，你调用 onEvent(@event) 就会抛出 NullReferenceException。
    现在即使没人注册，也能安全调用，只是什么都不发生。
    */
    Action<T> OnEvent = _ => { };
    Action OnEventNoArgs = () => { };

    /*
     * 这里用的是 显式接口实现。意思是：
    这两个属性 不是公开的，你不能直接通过 EventBinding<T> 访问它们。
    只有当你把对象当作接口 IEventBinding<T> 来用时，才会看到它们。
     */
    Action<T> IEventBinding<T>.OnEvent
    {
        get => OnEvent;
        set => OnEvent = value;
    }
    
    Action IEventBinding<T>.OnEventNoArgs
    {
        get => OnEventNoArgs;
        set => OnEventNoArgs = value;
    }
    
    //构造函数
    public EventBinding(Action<T> onEvent) => OnEvent = onEvent;
    public EventBinding(Action onEventNoArgs) => OnEventNoArgs = onEventNoArgs;

    public void Add(Action onEvent) => OnEventNoArgs += onEvent;
    public void Remove(Action onEvent) => OnEventNoArgs -= onEvent;
    
    public void Add(Action<T> onEvent) => OnEvent += onEvent;
    public void Remove(Action<T> onEvent) => OnEvent -= onEvent;
}
