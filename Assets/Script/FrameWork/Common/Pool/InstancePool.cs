using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

//只服务于resourcemanager
public class InstancePool
{
    //常量
    static string poolName = "InstancePool";
    static string recycleName = "RecyclePool";

    /// <summary>
    /// 
    /// </summary>
    Dictionary<string,Stack<GameObject>> instances = new Dictionary<string, Stack<GameObject>>();
    
    Transform poolRoot = null;
    Transform recycleRoot = null;
    
    //pool的根节点在构造函数中创建
    public InstancePool()
    {
        GameObject go = new GameObject(poolName);
        GameObject.DontDestroyOnLoad(go);
        poolRoot = go.transform;
        go.SetActive(true);
        recycleRoot = new GameObject(recycleName).transform;
        recycleRoot.SetParent(poolRoot);
        recycleRoot.gameObject.SetActive(false);
    }

    //只负责从池中取对象,不负责创建新对象
    public GameObject Get(string path)
    {
        Stack<GameObject> gameobjects = null;
        if (!instances.TryGetValue(path, out gameobjects))
        {
            return null;
        }
        else
        {
            if (gameobjects == null || gameobjects.Count == 0)
            {
                return null;
            }
            return gameobjects.Pop();
        }
    }
    
    public void Recycle(string path, GameObject go, bool forceDestroy = false)
    {
        //强制销毁
        if (forceDestroy)
        {
            if (Application.isPlaying)
            {
                GameObject.Destroy(go);
            }
            else
            {
                GameObject.DestroyImmediate(go);
            }
            return;
        }
        Stack<GameObject> gameobjects = null;
        if (!instances.TryGetValue(path, out gameobjects))
        {
            gameobjects = new Stack<GameObject>();
            instances.Add(path, gameobjects);
        }
        AssignParent(go, false);
        gameobjects.Push(go);
    }
    
    //挂在到root下
    public void AssignParent(GameObject go,bool active = true)
    {
        if (go != null)
        {
            if (active)
            {
                go.transform.SetParent(poolRoot,true);
            }
            else
            {
                go.transform.SetParent(recycleRoot,true);
            }
        }
    }

    /// <summary>
    /// 清空某个路径对应的池子
    /// </summary>
    /// <param name="key"></param>
    public void Clear(string key)
    {
        Stack<GameObject> objects = null;
        if (instances.TryGetValue(key, out objects))
        {
            while (objects.Count > 0)
            {
                GameObject objectToDestroy = objects.Pop();
                UnityEngine.AddressableAssets.Addressables.ReleaseInstance(objectToDestroy);
                if (Application.isPlaying)
                {
                    GameObject.Destroy(objectToDestroy);
                }
                else
                {
                    GameObject.DestroyImmediate(objectToDestroy);
                }
            }
        }
    }
}
