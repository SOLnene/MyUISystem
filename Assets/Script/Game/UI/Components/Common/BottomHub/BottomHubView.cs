using System.Collections;
using System.Collections.Generic;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
public class BottomHubView : MonoBehaviour
{
    [Header("容器")]
    [SerializeField] private Transform buttonContainer;
    [SerializeField] private GameObject buttonPrefab;

    [Header("显示区域")]
    [SerializeField] private TextMeshProUGUI goldText;
    [SerializeField] private TextMeshProUGUI expText;

    private BottomHubViewModel viewModel;
    private List<GameObject> activeButtons = new();

    public void Bind(BottomHubViewModel vm)
    {
        viewModel = vm;

        // 清理旧按钮
        foreach (var b in activeButtons) Destroy(b);
        activeButtons.Clear();

        // 动态生成按钮
        foreach (var cfg in viewModel.Buttons)
        {
            var go = Instantiate(buttonPrefab, buttonContainer);
            var text = go.GetComponentInChildren<TextMeshProUGUI>();
            var btn = go.GetComponent<Button>();
            text.text = cfg.Label;
            cfg.Command.BindTo(btn).AddTo(this);   
            go.SetActive(true);
            activeButtons.Add(go);
        }

        // 绑定数据展示
        viewModel.GoldCost.Where(c=>c!=0).Subscribe(v => goldText.text = $"金币消耗: {v}").AddTo(this);
        viewModel.ExpGain.Where(c=>c!=0).Subscribe(v => expText.text = $"获得经验: {v}").AddTo(this);
    }
}
