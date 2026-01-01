using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Unity.VisualScripting;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// ui配置json数据
/// </summary>
[Serializable]
public class UIConfigData
{
    public string uiType;
    public string uiPath;
    public string uiLayer;
    public bool isWindow;
}

public class UIConfig
{
    public string uiPath;
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
                            Debug.LogErrorFormat("{0}uiLayer解析异常{1}", config.uiPath, config.uiLayer);
                        }
                        if (!Enum.TryParse(config.uiType, out UIType uiType))
                        {
                            Debug.LogErrorFormat("{0}uiType解析异常{1}", config.uiPath, config.uiType);
                        }
                       
                        Type viewType = GetType(config.uiType);
                        if (viewType == null)
                        {
                            viewType = GetType($"{typeof(UIConfig).Namespace}.{config.uiType}");
                        }
                        list.Add(new UIConfig
                        {
                            uiPath = config.uiPath,
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
            type = Type.GetType(string.Format("{0}, {1}", typeName, assembly.FullName));
            if (type != null)
            {
                return type;
            }
        }
        Debug.LogErrorFormat("找不到类型{0}",typeName);
        return null;
    }
}
