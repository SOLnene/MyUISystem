using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Reflection;
using Unity.VisualScripting;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.Serialization;

/// <summary>
/// ui配置json数据
/// </summary>
[Serializable]
public class UIConfigData
{
    public string uiType;
    [FormerlySerializedAs("uiPath")] public string assetPath;      //资源路径
    public string uiAddress;  //addressable路径
    public string uiLayer;
    public bool isWindow;
}

public class UIConfig
{
    public string assetPath;
    public string uiAddress;
    public UIType uiType;
    public UILayer uiLayer;
    public Type uiViewType;
    public bool isWindow;

    /// <summary>
    /// json配置文件
    /// </summary>
    const string UIConfigPath = "Assets/AssetsPackage/UI/UIConfig.json";

    /// <summary>
    /// 从配置文件 <c>uiconfig.json</c> 中加载并解析所有 UI 配置。
    /// </summary>
    /// <param name="callback">
    /// 当配置加载完成后回调，返回解析出的 <see cref="UIConfig"/> 列表。
    /// 如果文件不存在或解析失败，回调不会触发。</param>
    /// <returns></returns>
    public static AsyncOperationHandle GetAllConfigs(Action<List<UIConfig>> callback)
    {
        return ResourceManager.Instance.LoadAssetAsync<TextAsset>
            (UIConfigPath, (result) =>
            {
                if (result != null)
                {
                    var list = new List<UIConfig>();
                    var uiConfigs = Newtonsoft.Json.JsonConvert.DeserializeObject<List<UIConfigData>>(result.text);
                    foreach (var config in uiConfigs)
                    {
                        if (!Enum.TryParse(config.uiLayer, out UILayer layer))
                        {
                            layer = UILayer.NormalLayer;
                            Debug.LogErrorFormat("{0}uiLayer解析异常{1}", config.assetPath, config.uiLayer);
                        }
                        if (!Enum.TryParse(config.uiType, out UIType uiType))
                        {
                            Debug.LogErrorFormat("{0}uiType解析异常{1}", config.assetPath, config.uiType);
                        }
                       
                        Type viewType = GetType(config.uiType);
                        if (viewType == null)
                        {
                            viewType = GetType($"{typeof(UIConfig).Namespace}.{config.uiType}");
                        }
                        list.Add(new UIConfig
                        {
                            assetPath = config.assetPath,
                            uiAddress = config.uiAddress,
                            uiType = uiType,
                            uiLayer = layer,
                            uiViewType =  viewType,
                            isWindow = config.isWindow
                        });
                    }
                    callback?.Invoke(list);
                }
                else
                {
                    Debug.LogError("找不到配置文件: " + UIConfigPath);
                }
            }, true);
    }

    /// <summary>
    /// 尝试查找type,如果找不到则遍历所有已加载程序集
    /// </summary>
    /// <param name="typeName"></param>
    /// <returns></returns>
    public static Type GetType(string typeName)
    {
        var type = Type.GetType(typeName);
        if (type != null)
        {
            return type;
        }
        
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach (var assembly in assemblies)
        {
            // 只扫描有意义的程序集（跳过系统程序集，提高效率）
            if (assembly.FullName.StartsWith("System") || 
                assembly.FullName.StartsWith("mscorlib") || 
                assembly.FullName.StartsWith("UnityEngine") || 
                assembly.FullName.StartsWith("UnityEditor"))
            {
                continue;
            }

            try
            {
                var types = assembly.GetTypes();
                foreach (var t in types)
                {
                    // 只匹配类名（t.Name == typeName）
                    if (t.Name == typeName)
                    {
                        // 可选：额外检查是否继承自 UIView，防同名类冲突
                        if (typeof(UIView).IsAssignableFrom(t))
                        {
                            Debug.Log($"[UIConfig] 找到匹配类型: {t.FullName} (类名: {typeName})");
                            return t;
                        }
                    }
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                // 某些程序集加载失败时跳过（常见于插件/第三方包）
                Debug.LogWarning($"程序集 {assembly.FullName} 加载类型失败: {ex.Message}");
            }
        }
        Debug.LogErrorFormat("找不到类型{0}",typeName);
        return null;
    }
}
