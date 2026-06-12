using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class ResourceManager : Singleton<ResourceManager>
{
    //加载缓存
    //Dictionary<string,AsyncOperationHandle> handleCaches = new Dictionary<string, AsyncOperationHandle>();
    
    /// <summary>
    ///正在加载的资源数
    /// </summary>
    int loadingAssetsCount = 0;
    /// <summary>
    /// 资源引用计数
    /// </summary>
    Dictionary<string,int> assetRefCounts = new Dictionary<string, int>();
    /// <summary>
    /// 缓存每个资源的加载句柄，无论该资源已经完成加载还是仍在加载中
    /// </summary>
    Dictionary<string, AsyncOperationHandle> assetHandles = new Dictionary<string, AsyncOperationHandle>();
    /// <summary>
    /// 维护加载canceltoken
    /// </summary>
    private Dictionary<string, CancellationTokenSource> loadingCTS = new Dictionary<string, CancellationTokenSource>();
    
    /// <summary>
    /// 常驻资源路径集合
    /// </summary>
    HashSet<string> residentAssets = new HashSet<string>();

    HashSet<string> resourcesToRelease = new HashSet<string>();
    /// <summary>
    /// 是否正在加载资源
    /// </summary>
    public bool IsProcessLoading
    {
        get => loadingAssetsCount > 0;
    }
    
    /// <summary>
    /// 这个类实例化的对象ID到路径的映射,来源于resourcemanager
    /// </summary>
    Dictionary<int, string> instanceIdToPath = new Dictionary<int, string>();
    
    InstancePool instancePool;

    public void Init()
    {
        instancePool = new InstancePool();
    }

    /// <summary>
    /// 等待Addressables初始化完成
    /// </summary>
    /// <returns></returns>
    public IEnumerator InitAsync()
    {
        yield return Addressables.InitializeAsync();
    }

    /// <summary>
    /// 等待所有正在加载的资源加载完成后清除所有常驻资源之外的资源
    /// </summary>
    /// <returns></returns>
    public IEnumerable CleanupAsync()
    {
        yield return new WaitUntil(() =>
        {
            return !IsProcessLoading;
        });
        Cleanup();
    }

    /// <summary>
    /// 清除所有常驻资源之外的资源
    /// </summary>
    public void Cleanup()
    {
        foreach (var handle in assetHandles)
        {
            if (!residentAssets.Contains(handle.Key))
            {
                resourcesToRelease.Add(handle.Key);
                //Addressable中清除引用
                Addressables.Release(handle.Value);
            }
        }
        foreach (var key in resourcesToRelease)
        {
            assetHandles.Remove(key);
            assetRefCounts.Remove(key);
            instancePool.Clear(key);
        }
        resourcesToRelease.Clear();
    }
    
    
    #region UniTask 版本

    /// <summary>
    /// 使用 UniTask 异步加载资源（无回调）
    /// </summary>
    public async UniTask<T> LoadAssetAsync<T>(string path, CancellationToken cancellationToken = default, bool autoUnload = false) where T : class
    {
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError("[ResourceManager] LoadAssetAsync path 为空");
            return null;
        }

        AsyncOperationHandle handle;
        if (assetHandles.TryGetValue(path, out handle))
        {
            if (handle.IsDone)
            {
                // IsDone 只表示加载已经结束，失败的 Handle 同样会进入这里。
                if (handle.Status != AsyncOperationStatus.Succeeded)
                {
                    // 失败 Handle 不能继续留在缓存中，否则后续请求会一直命中同一次失败结果。
                    ReleaseFailedHandle(path, handle);
                    return await LoadAssetAsync<T>(path, cancellationToken, autoUnload);
                }

                return handle.Result as T;
            }
            else
            {
                // 等待完成或取消
                try
                {
                    // 这里只取消当前调用方的等待，不取消可能被其他调用方共享的 Addressables 加载。
                    return await handle.Task
                        .AsUniTask()
                        .AttachExternalCancellation(cancellationToken) as T;
                }
                catch (OperationCanceledException)
                {
                    // 取消属于调用流程的一部分，继续抛给调用方决定如何处理，并保留原始异常堆栈。
                    throw;
                }
                catch (Exception)
                {
                    // Handle 可能由旧的回调接口创建，因此这里也要保证失败缓存能够被清理。
                    ReleaseFailedHandle(path, handle);
                    return null;
                }
            }
        }
        else
        {
            // 新建 CTS
            var cts = new CancellationTokenSource();
            loadingCTS[path] = cts;
            
            loadingAssetsCount++;
            assetRefCounts[path] = 1;

            var newHandle = Addressables.LoadAssetAsync<T>(path);
            assetHandles[path] = newHandle;

            // 全局加载状态必须跟随底层 Handle，而不能跟随某一个可能提前取消等待的调用方。
            newHandle.Completed += result =>
            {
                loadingAssetsCount--;
                if (loadingCTS.TryGetValue(path, out var loadingCts))
                {
                    loadingCts.Dispose();
                    loadingCTS.Remove(path);
                }

                if (result.Status != AsyncOperationStatus.Succeeded)
                {
                    Debug.LogError($"[ResourceManager.LoadAssetAsync] 加载失败: {path}");
                    ReleaseFailedHandle(path, result);
                }
            };

            try
            {
                // 调用方取消后底层加载仍会继续，完成结果可供其他调用方或后续请求复用。
                return await newHandle.Task
                    .AsUniTask()
                    .AttachExternalCancellation(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // throw; 会原样重新抛出当前异常，不会重置异常的调用堆栈。
                throw;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    void ReleaseFailedHandle(string path, AsyncOperationHandle failedHandle)
    {
        // 旧请求的失败回调可能晚于新请求，只允许失败 Handle 清理它自己对应的缓存项。
        if (!assetHandles.TryGetValue(path, out var cachedHandle) ||
            !cachedHandle.Equals(failedHandle))
        {
            return;
        }

        assetHandles.Remove(path);
        assetRefCounts.Remove(path);
        if (failedHandle.IsValid())
        {
            Addressables.Release(failedHandle);
        }
    }

    /// <summary>
    /// 取消某条路径加载
    /// </summary>
    public void CancelLoad(string path)
    {
        if (loadingCTS.TryGetValue(path, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
            loadingCTS.Remove(path);
        }
    }

    /// <summary>
    /// 取消所有正在加载的资源（如关闭页面）
    /// </summary>
    public void CancelAll()
    {
        foreach (var cts in loadingCTS.Values)
        {
            cts.Cancel();
            cts.Dispose();
        }
        loadingCTS.Clear();
    }
    
    /// <summary>
    /// 兼容
    /// </summary>
    /// <param name="path"></param>
    /// <param name="callback"></param>
    /// <param name="parent"></param>
    /// <param name="active"></param>
    public void InstantiateItem(string path, Action<GameObject> callback = null, 
        Transform parent = null, 
        bool active = true,
        CancellationToken cancellationToken = default)
    {
        InternalInstantiateItemAsync(path,parent,active,cancellationToken).ContinueWith(go =>
        {
            callback?.Invoke(go);
        }).Forget(Debug.LogException);
    }
    
    public UniTask<GameObject> InstantiateItemAsync(string path, Transform parent = null, bool active = true,
        CancellationToken cancellationToken = default)
    {
        return InternalInstantiateItemAsync(path, parent, active,cancellationToken);
    }
    
    /// <summary>
    /// 使用 UniTask 异步实例化对象
    /// 仅不需要复杂管理的item
    /// </summary>
    public async UniTask<GameObject> InternalInstantiateItemAsync(string path
        , Transform parent = null
        , bool active = true,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // 1. 确保资源已加载
        var prefab = await LoadAssetAsync<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogError($"[ResourceManager.InstantiateAsync] 无法实例化: {path}");
            return null;
        }
        cancellationToken.ThrowIfCancellationRequested();
        // 2. 先尝试对象池
        var pooled = instancePool.Get(path);
        GameObject result;

        if (pooled == null)
        {
            result = GameObject.Instantiate(prefab, parent);
        }
        else
        {
            result = pooled;
            result.transform.SetParent(parent, false);
        }
        result.SetActive(active);
        instanceIdToPath[result.GetInstanceID()] = path;
        assetRefCounts[path]++;

        return result;
    }

    
    
#endregion
    //todo:不再暴露AsyncOperationHandle
    public AsyncOperationHandle InstantiateAsync(string path, Action<UnityEngine.GameObject> callback, bool isActive = true)
    {
        AsyncOperationHandle operationHandle = default;
        //第一次加载
        if (!assetHandles.ContainsKey(path))
        {
            operationHandle = LoadAssetAsync<GameObject>(path, (go) =>
            {
                if (go != null)
                {
                    //实例化
                    InternalInstantiate(path, callback, isActive);
                }
                else
                {
                    callback?.Invoke(null);
                }
            });
        }
        else
        {
            operationHandle = assetHandles[path];
            if (operationHandle.IsDone)
            {
                //实例化
                InternalInstantiate(path, callback, isActive);
            }
            else
            {
                operationHandle.Completed += (result) =>
                {
                    //实例化
                    InternalInstantiate(path, callback, isActive);
                };
            }
        }
        return operationHandle;
    }

    //加载资源
    public AsyncOperationHandle LoadAssetAsync<T>(string path, Action<T> onComplete, bool autoUnload = false) where T : class
    {
        if (string.IsNullOrEmpty(path))
        {
            onComplete?.Invoke(null);
            return default;
        }
        AsyncOperationHandle handle;
        if (assetHandles.TryGetValue(path, out handle))
        {
            if (handle.IsDone)
            {
                //防止字典值被更新
                onComplete?.Invoke(assetHandles[path].Result as T);
            }
            else
            {
                handle.Completed += (result) =>
                {
                    if (result.Status == AsyncOperationStatus.Succeeded)
                    {
                        onComplete?.Invoke(result.Result as T);
                        if (autoUnload)
                        {
                            //卸载资源
                        }
                    }
                    else
                    {
                        Debug.LogErrorFormat("[ResourceManager.LoadAssetAsync]{0}加载失败", path);
                        onComplete?.Invoke(null);
                    }
                };
            } 
            return handle;
        }
        else
        {
            loadingAssetsCount++;
            assetRefCounts.Add(path,1);
            handle = Addressables.LoadAssetAsync<T>(path);
            handle.Completed += (result) =>
            {
                loadingAssetsCount--;
                if (result.Status == AsyncOperationStatus.Succeeded)
                {
                    onComplete?.Invoke(result.Result as T);
                    if (autoUnload)
                    {
                        //卸载资源
                    }
                }
                else
                {
                    Debug.LogErrorFormat("[ResourceManager.LoadAssetAsync]{0}初次加载失败", path);
                    onComplete?.Invoke(null);
                }
            };
            assetHandles.Add(path,handle);
            return handle;
        }
        
    }

    //实例化
    void InternalInstantiate(string path,Action<GameObject> callback,bool active = true)
    {
        GameObject go = instancePool.Get(path);
        GameObject result = null;
        if (go == null)
        {
            var res = assetHandles[path].Result;
            if (res != null)
            {
                //result = res as GameObject;
                result = GameObject.Instantiate(res as GameObject);
            }
        }
        else
        {
            result = go;
        }

        if (result != null)
        {
            //instancePool.AssignParent(result,active);
             instanceIdToPath[result.GetInstanceID()] = path;
            assetRefCounts[path]++;
        }
        callback.Invoke(result);
    }
    
    /// <summary>
    /// 回收由ResourceManager实例化的对象
    /// </summary>
    /// <param name="go"></param> 
    /// <param name="forceDestroy"></param> 
    public void Recycle(GameObject go, bool forceDestroy = false)
    {
        if (go == null)
        {
            return;
        }
        int id = go.GetInstanceID();
        if (instanceIdToPath.ContainsKey(id))
        {
            instancePool.Recycle(instanceIdToPath[id], go, forceDestroy);
            assetRefCounts[instanceIdToPath[id]]--;
            instanceIdToPath.Remove(id);
        }
        else
        {
            Debug.LogErrorFormat("{0}不是通过[ResourceManager]实例化", go.name);
            GameObject.Destroy(go);
        }
    }
}
