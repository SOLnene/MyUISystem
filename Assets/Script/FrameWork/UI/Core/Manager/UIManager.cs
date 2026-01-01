using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
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
    
    //uicreate场景中使用，用于关闭场景中原本的ui
    [SerializeField]
    GameObject testCanvas;
    
    /// <summary>
    /// 预加载的itemslotview,先放这里
    /// </summary>
    public  ItemSlotView slotPrefab;
    void Awake()
    {
        Init();
    }

    async void OnEnable()
    {
        //注册事件
        //打开，关闭ui界面不适合事件总线
        /*openUIBinding = new EventBinding<OpenUIEvent>(e => Open(e.UIType));
        closeUIBinding = new EventBinding<CloseUIEvent>(e => Close(e.UIType));
        EventBus<OpenUIEvent>.Register(openUIBinding);
        EventBus<CloseUIEvent>.Register(closeUIBinding);*/
        //在ui创建界面测试
        if (testCanvas != null)
        {
            testCanvas.SetActive(false);
        }
        await StartAsync();
    }

    async UniTask StartAsync()
    {
        await ResourceManager.Instance.InitAsync();
        await InitUIConfig();
        await GameDatabase.Init();
        //preload
        await EnsureSlotPrefabLoaded();
        await EnsureSpritesLoaded("Assets/AssetsPackage/UI/Sprite/TouchIcon/UI_TouchIcon_Plus.png");
        await PreLoad(UIType.GachaView);
        
        //创建item并添加到背包中
        var items = ItemFactory.CreateTestItems();
        foreach (var item in items)
        {
            GameContext.Instance.BackpackVM.AddItem(item);
        }
        Open(UIType.GachaView);
    }
    
    void Init()
    {
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
                    uiPath = config.uiPath,
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
    
    public async UniTask<ItemSlotView> EnsureSlotPrefabLoaded()
    {
        if (slotPrefab == null)
        {
            var prefab = await ResourceManager.Instance.LoadAssetAsync<GameObject>(
                "Assets/AssetsPackage/UI/Prefab/ItemSlot.prefab");
            slotPrefab = prefab.GetComponent<ItemSlotView>();
        }
        return slotPrefab;
    }
    
    public async UniTask<Sprite> EnsureSpritesLoaded(string path)
    {
        var sprite = await ResourceManager.Instance.LoadAssetAsync<Sprite>(path);
        return sprite;
    }
    
    void OnDestroy()
    {
        /*EventBus<OpenUIEvent>.Deregister(openUIBinding);
        EventBus<CloseUIEvent>.Deregister(closeUIBinding);*/
    }

  #region 测试用
    public void ShowTest()
    {
        
    }
  #endregion
  

    
  
}
