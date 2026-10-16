using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BottomView : MonoBehaviour,IActionHubView
{
    [SerializeField]
    Button button;
    [SerializeField]
    Button enhanceButton;
    BackpackViewModel vm;

    [SerializeField]
    Transform buttonContainer;
    [SerializeField]
    GameObject buttonPrefab;
    [SerializeField]
    AnimatedPanel anim;
    readonly List<Button> activeButtons = new List<Button>();

    public UniTask Show()
    {
        return anim.Show();
    }

    public async UniTask Hide()
    {
        await anim.Hide();
    }
    
    
    public void Bind(BackpackViewModel backpackViewModel)
    {
        vm = backpackViewModel;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnDetailClick);
        //TODO:换成buttondata生成
        if (enhanceButton != null)
        {
            
        }
    }

    public void SetButton(Action onClick)
    {
        enhanceButton.onClick.RemoveAllListeners();
        enhanceButton.onClick.AddListener(() => onClick?.Invoke());
    }

    public void SetButtons(params HubButtonData[] buttonDatas)
    {
        foreach (var button in activeButtons)
        {
            Destroy(button.gameObject);
        }
        activeButtons.Clear();

        foreach (var data in buttonDatas)
        {
            var btnObj = Instantiate(buttonPrefab);
            var text = btnObj.GetComponentInChildren<TextMeshProUGUI>();
            var btn = btnObj.GetComponent<Button>();

            text.text = data.label;
            button.interactable = data.interactabel;
            button.onClick.AddListener(()=>data.onClick?.Invoke());
            activeButtons.Add(button);
        }
    }
    
    public void OnDetailClick()
    {
        ItemViewModel item = vm.selectedSlot.Value?.ItemViewModel;
        if (item == null)
            return;

        if (item.Model is EquipItem weapon)
            UIManager.Instance.Open(UIType.EquipDetailView,
                new EquipDetailOpenParams(new EquipItemViewModel(weapon), WeaponDetailTab.Info));
        else
            UIManager.Instance.Open(UIType.InfoPanelPopup, item);
    }
}

public interface IActionHubView
{
    void SetButtons(params HubButtonData[] buttonDatas);
}

public struct HubButtonData
{
    public string label;
    public Action onClick;
    public bool interactabel;
    
    public HubButtonData(string label, Action onClick, bool interactabel = true)
    {
        this.label = label;
        this.onClick = onClick;
        this.interactabel = interactabel;
    }
}
