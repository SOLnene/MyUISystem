using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.ResourceManagement.AsyncOperations;

public enum UIBlackType
{
    None,       // 无黑边，全适应
    Height,     // 保持高度填满，两边黑边
    Width,      // 保持宽度填满, 上下黑边
    AutoBlack,  // 自动黑边(选中左右或上下黑边最少的一方)
}

/*UIManager → 管理逻辑和界面状态（打开/关闭/常驻）

UIViewController → 控制单个 UI prefab 的生命周期

UIView → 控制界面内部逻辑，可复用组件由这里管理*/
public class UIManager : SingletonMono<UIManager>
{
    public int width = 1920;
    public int height = 1080;
    public UIBlackType uiBlackType = UIBlackType.None;
    
    //当前打开的UI界面
    HashSet<UIType> openedUIs = new HashSet<UIType>();

    Dictionary<UIType, UIViewHandle> viewHandles;
    Dictionary<UILayer, UILayerLogic> layers;
    
    Camera worldCamera;
    Camera uiCamera;
    Transform uiRoot;

    CanvasGroup blackMask;
    CanvasGroup backgrondMask;

    void Awake()
    {
        Init();
    }
    
    void Init()
    {
        if (layers != null && viewHandles != null)
        {
            return;
        }

        layers = new Dictionary<UILayer, UILayerLogic>();
        viewHandles = new Dictionary<UIType, UIViewHandle>();
        
        worldCamera = Camera.main;
        // 把 UI 层对应的位清0（保留其它位）
        worldCamera.cullingMask &= ~(1 << LayerMask.NameToLayer("UI"));
        
        var root = GameObject.Find("UIRoot");
        if (root == null)
        {
            root = new GameObject("UIRoot");
        }
        root.layer = LayerMask.NameToLayer("UI");
        DontDestroyOnLoad(root);
        uiRoot = root.transform;
        
        var camera = GameObject.Find("UICamera");
        if (camera == null)
        {
            camera = new GameObject("UICamera");
        }

        uiCamera = camera.GetOrAddComponent<Camera>();
        uiCamera.cullingMask = 1<<LayerMask.NameToLayer("UI");
        uiCamera.transform.SetParent(uiRoot);
        uiCamera.orthographic = true;
        uiCamera.clearFlags = CameraClearFlags.Depth;
        uiCamera.depth = worldCamera.depth + 1;
        UIBackdropCaptureService.Instance.Configure(uiCamera);
        
        var layersArray = Enum.GetValues(typeof(UILayer));
        foreach (UILayer layer in layersArray)
        {
            bool is3d = layer == UILayer.SceneLayer;
            Canvas layerCanvas = UIHelper.CreateLayerCanvas(
                layer, is3d, uiRoot, is3d?worldCamera:uiCamera, width, height);
            UILayerLogic uiLayerLogic = new UILayerLogic(layer, layerCanvas);
            layers.Add(layer,uiLayerLogic);
        }
        blackMask = UIHelper.CreateBlackMask(layers[UILayer.BlackMaskLayer].canvas.transform);
        backgrondMask = UIHelper.CreateBlackMask(layers[UILayer.BackgroundLayer].canvas.transform);
    }
    
    

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public AsyncOperationHandle InitUIConfig()
    {
        return UIConfig.GetAllConfigs((result) =>
        {
            foreach (var config in result)
            {
                if (viewHandles.ContainsKey(config.uiType))
                {
                    Debug.LogErrorFormat("uiType{0}重复", config.uiType);
                    continue;
                }
                viewHandles.Add(config.uiType, new UIViewHandle
                {
                    address = config.uiAddress,
                    uiType = config.uiType,
                    uiViewType = config.uiViewType,
                    uiLayerLogic = layers[config.uiLayer],
                    isWindow = config.isWindow
                });
            }
        });
    }

    public AsyncOperationHandle PreLoad(UIType type)
    {
        if(!viewHandles.TryGetValue(type,out var handle))
        {
            Debug.LogErrorFormat("UIType {0} 不存在", type);
            return default;
        }
        return handle.Load();
    }

    public void Open(UIType type, object data = null,Action callback = null)
    {
        openedUIs.Add(type);
        viewHandles[type].Show(data,callback);
    }
    /// <summary>
    /// 回调带UIView的重载
    /// </summary>
    /// <param name="type"></param>
    /// <param name="data"></param>
    /// <param name="callback"></param>
    public void OpenWithView(UIType type, object data = null,Action<UIView> callback = null)
    {
        openedUIs.Add(type);
        viewHandles[type].Show(data, () =>
        {
            var view = viewHandles[type].uiView;
            if (view != null)
            {
                callback?.Invoke(view);
            }
            else
            {
                Debug.LogWarning($"UIType {type} 打开成功但 UIView 为空，无法执行回调");
            }
        });
    }
    
    public void Close(UIType uiType, Action callback = null)
    {
        if (!viewHandles.ContainsKey(uiType))
        {
            Debug.LogErrorFormat("尝试关闭未加载的UIType{0}",uiType.ToString());
            return;
        }
        openedUIs.Remove(uiType);
        viewHandles[uiType].Close(callback);
    }

    private void EnsureEventSystem()
    {
        // 优先用全局单例的 EventSystem
        var existing = FindObjectOfType<EventSystem>();
        if (existing != null)
        {
            // 如果这个 EventSystem 不在 DontDestroyOnLoad 里，挂上去
            if (existing.transform.parent == null)
                DontDestroyOnLoad(existing.gameObject);
            return;
        }

        // 否则创建一个新的全局 EventSystem
        GameObject go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        //添加inputsystem
        go.AddComponent<StandaloneInputModule>();
        DontDestroyOnLoad(go);
    }

    /// <summary>
    /// 将安全区扩大黑边区域
    /// </summary>
    /// <returns></returns>
    public Rect GetSafeArea()
    {
        Rect rect = Screen.safeArea;
        if (uiBlackType == UIBlackType.Width)
        {
            var parent = layers[UILayer.BackgroundLayer].canvas.transform as RectTransform;
            float blackArea = Mathf.Abs(height - parent.rect.height) / 2;
            rect.yMin = Mathf.Max(0, rect.yMin - blackArea);
            rect.yMax = Mathf.Min(rect.yMax + blackArea, Screen.height);
        }
        else if (uiBlackType == UIBlackType.Height)
        {
            var parent = layers[UILayer.BackgroundLayer].canvas.transform as RectTransform;
            float blackArea = Mathf.Abs(width - parent.rect.width) / 2;
            rect.xMin = Mathf.Max(0, rect.xMin - blackArea);
            rect.xMax = Mathf.Min(rect.xMax + blackArea, Screen.width);
        }
        return rect;
    }
    
}
