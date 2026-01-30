using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;
using UniRx;

public class BackpackEntry : MonoBehaviour
{
    [Header("子视图")]
    [FormerlySerializedAs("tophubView")]
    [SerializeField]
    BackpackTopView topView;
    [SerializeField]
    BackpackMiddleView middleView;
    [FormerlySerializedAs("rightView")]
    [SerializeField]
    InfoPanelView infoView;
    [SerializeField]
    BottomView bottomView;
    // Start is called before the first frame update
    void Start()
    {
        Init();

    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public async UniTask Init()
    {
        await GameContext.Instance.Init();
        InitTop();
       
        var backpackVM = GameContext.Instance.BackpackVM;
        
        topView.Bind(backpackVM.topVM);
        // 4. Back 按钮
        topView.OnBackClicked += () => Debug.Log("关闭背包");
        topView.OnBackClicked += OnBackClicked;
        middleView.Bind(backpackVM.middleVM);
        infoView.Bind(backpackVM.infoVM);
        bottomView.Bind(backpackVM);
        
        
    }
    

    void InitTop()
    {
        
    }
    
    private void OnCategoryChanged(int index)
    {
        Debug.Log($"选择了分类: {index}");
        // TODO: 通知 MiddleHub 显示对应道具
    }

    private void OnBackClicked()
    {
        Debug.Log("点击返回按钮");
        // TODO: 通知 UIManager 关闭背包
    }
}
