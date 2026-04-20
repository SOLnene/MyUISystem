using System;
using System.Collections;
using System.Collections.Generic;
using SkierFramework;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public class CharacterDetailTabItem : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI text;
    [SerializeField]
    private Button btn;
    int index;
    public void Bind(int index,Action onClick)
    {
        this.index = index;
        
        switch (index)
        {
            case 1:
                text.text = "属性";
                break;
            case 2:
                text.text = "装备";
                break;
            case 3:
                text.text = "圣遗物";
                break;
            case 4:
                text.text = "详情";
                break;
        }
        
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => onClick?.Invoke());
    }

    public void SetSelected(bool select)
    {
        text.fontSize = select ? 40 : 32;
    }
}
