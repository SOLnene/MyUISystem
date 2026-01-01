using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// 通用对象池
/// </summary>

public static class Pool
{
    /// <summary>
    /// 所有拥有对象池的对象
    /// </summary>
    public readonly static List<IPoolBase> AllPoll = new List<IPoolBase>();
    
    /// <summary>
    /// 释放所有对象池对象
    /// </summary>
    public static void ReleaseAll()
    {
        foreach (var pool in AllPoll)
        {
            pool.Dispose();
        }
        AllPoll.Clear();
    }
}

public interface IObject
{
    void OnRelease();
}

public interface IPoolBase
{
    void Dispose();
}

public class CustomObjectPool<T> : IPoolBase where T:new()
{
    static CustomObjectPool<T> instance;
    Stack<T> pool;

    /// <summary>
    /// 控制访问权限  默认生成的构造函数是 public
    /// </summary>
    CustomObjectPool()
    {
        
    }

    static void Init()
    {
        if (instance == null)
        {
            instance = new CustomObjectPool<T>();
            instance.pool = new Stack<T>();
            Pool.AllPoll.Add(instance);
        }
    }

    public static T Get()
    {
        Init();
        if (instance.pool.Count > 0)
        {
            return instance.pool.Pop();
        }
        else
        {
            return new T();
        }
    }

    public static void Release(T obj)
    {
        if (obj == null || instance == null)
        {
            return;
        }
        //C#7 之后的语法糖，在判断类型的同时，直接定义一个变量并完成转换。
        if (obj is IObject interfac)
        {
            interfac.OnRelease();
        }
        instance.pool.Push(obj);
    }
    
    
    public void Dispose()
    {
        if (instance != null)
        {
            if (instance.pool != null)
            {
                instance.pool.Clear();
                instance.pool = null;
            }
            instance = null;
        }
    }
}

public class ListPool<T> : IPoolBase
{
    static ListPool<T> instance;

    Stack<List<T>> pool;

    ListPool()
    {
        
    }

    static void Init()
    {
        if (instance == null)
        {
            instance = new ListPool<T>();
            instance.pool = new Stack<List<T>>();
            Pool.AllPoll.Add(instance);
        }
    }

    public static List<T> Get()
    {
        Init();
        if (instance.pool.Count > 0)
        {
            return instance.pool.Pop();
        }
        else
        {
            return new List<T>();
        }
    }

    public static void Release(List<T> list)
    {
        if (list == null || instance == null)
        {
            return;
        }
        list.Clear();
        instance.pool.Push(list);
    }
    
    public void Dispose()
    {
        if (instance != null)
        {
            if (instance.pool != null)
            {
                instance.pool.Clear();
                instance.pool = null;
            }
        }
        instance = null;
    }
}

public class DictionaryPool<TKey,TValue> : IPoolBase
{
    static DictionaryPool<TKey,TValue> instance;

    Stack<Dictionary<TKey,TValue>> pool;

    DictionaryPool()
    {
        
    }

    static void Init()
    {
        if (instance == null)
        {
            instance = new DictionaryPool<TKey,TValue>();
            instance.pool = new Stack<Dictionary<TKey,TValue>>();
            Pool.AllPoll.Add(instance);
        }
    }

    public static Dictionary<TKey,TValue> Get()
    {
        Init();
        if (instance.pool.Count > 0)
        {
            return instance.pool.Pop();
        }
        else
        {
            return new Dictionary<TKey, TValue>();
        }
    }

    public static void Release(Dictionary<TKey,TValue> dic)
    {
        if (dic == null || instance == null)
        {
            return;
        }
        dic.Clear();
        instance.pool.Push(dic);
    }
    
    public void Dispose()
    {
        if (instance != null)
        {
            if (instance.pool != null)
            {
                instance.pool.Clear();
                instance.pool = null;
            }
        }
        instance = null;
    }
}   

