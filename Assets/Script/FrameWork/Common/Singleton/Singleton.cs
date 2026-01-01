using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class Singleton<T> where T:new() 
{
    static T _instance;
    readonly static object lockObj = new object();

    /*// 禁止外部进行实例化
    protected Singleton()
    {
        Init();
    }
    */
    
    public static T Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (lockObj)
                {
                    if (_instance == null)
                    {
                        _instance = new T();
                        //延迟初始化
                        (_instance as Singleton<T>).Init();
                    }
                }
            }
            return _instance;
        }
    }

    public virtual void Init()
    {
        
    }
    
}
