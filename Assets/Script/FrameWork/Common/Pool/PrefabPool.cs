using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// PrefabPool 是一个专门存放某个预制体实例的对象池，每个池只对应一种预制体原型。
/// </summary>
public class PrefabPool
{
    /// <summary>
    /// 名字对应的对象池
    /// </summary>
    public static Dictionary<string, PrefabPool> Pools = new Dictionary<string, PrefabPool>();
    string poolName;
    /// <summary>
    /// 对象池所对应的预制体原型
    /// </summary>
    GameObject prefab;
    List<GameObject> pool;
    List<GameObject> useList;
    
    public GameObject Prefab => prefab;
    
    public List<GameObject> UseList => useList;
    PrefabPool()
    {
        
    }
    
    void Init( GameObject prefab,string poolName)
    {
        pool = ListPool<GameObject>.Get();
        useList = ListPool<GameObject>.Get();
        this.prefab = prefab;
        this.poolName = poolName;
    }
    
    public static PrefabPool Get(string poolName)
    {
        if (!string.IsNullOrEmpty(poolName))
        {
            if(Pools.TryGetValue(poolName,out var prefabPool))
            {
                return prefabPool;
            }
        }
        Debug.LogErrorFormat($"获取对象池{poolName}失败");
        return null;
    }

    /// <summary>
    /// 创建或获取对象池
    /// </summary>
    /// <param name="prefab"></param>
    /// <param name="poolName">
    ///  池子的名称：
    ///  - 如果传入相同名字，则返回已存在的池子（避免重复创建）
    ///  - 如果为空（null 或 ""），则创建匿名池，不会放入全局字典（需要外部自己持有引用）</param>
    /// <returns></returns>
    public static PrefabPool Create(GameObject prefab, string poolName = null)
    {
        if (prefab == null)
        {
            return null;
        }
        if (!string.IsNullOrEmpty(poolName))
        {
            if(Pools.TryGetValue(poolName,out var prefabPool))
            {
                return prefabPool;
            }
        }
        var pool = new PrefabPool();
        pool.Init(prefab,poolName);
        if (!string.IsNullOrEmpty(poolName))
        {
            Pools.Add(poolName, pool);
        }
        return pool;
    }

    public GameObject Get(Transform parent = null)
    {
        if (prefab == null)
        {
            return null;
        }
        GameObject go = null;
        if (pool.Count > 0)
        {
            go = pool[0];
            pool.RemoveAt(0);
        }
        else
        {
            go = GameObject.Instantiate(prefab);
        }
        go.transform.SetParentAndReset(parent);
        go.SetActive(true);
        go.transform.SetAsLastSibling();
        useList.Add(go);
        return go;
    }

    public void Recycle(GameObject go)
    {
        if (go != null)
        {
            go.SetActive(false);
            pool.Add(go);
            useList.Remove(go);
        }
    }

    /// <summary>
    /// 回收所有正在使用的对象
    /// </summary>
    public void RecycleAll()
    {
        foreach (var go in useList)
        {
            if (go != null)
            {
                go.SetActive(false);
                pool.Add(go);
            }
        }
        useList.Clear();
    }

    /// <summary>
    /// 销毁对象池
    /// </summary>
    public void Destroy()
    {
        foreach (var go in pool)
        {
            if (go != null)
            {
                GameObject.Destroy(go);
            }
        }
        pool.Clear();
        foreach (var go in useList)
        {
            if (go != null)
            {
                GameObject.Destroy(go);
            }
        }
        useList.Clear();
        
        ListPool<GameObject>.Release(pool);
        ListPool<GameObject>.Release(useList);
        Pools.Remove(poolName);

        pool = null;
        useList = null;
        prefab = null;
        poolName = null;
    }
}
