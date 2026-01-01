using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gameobject的拓展方法
/// </summary>
public static class GameObjectExtension 
{
    public static T GetOrAddComponent<T>(this GameObject gameObject) where T : Component
    {
        T t = gameObject.GetComponent<T>();
        if (t == null)
            t = gameObject.AddComponent<T>();
        return t;
    }

    public static Component GetOrAddComponent(this GameObject gameObject, Type type)
    {
        if (gameObject == null) return null;

        Component component = gameObject.GetComponent(type);
        if (component == null)
            component = gameObject.AddComponent(type);
        return component;
    }
}
