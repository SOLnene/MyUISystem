using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;


public class CategoryButtonView : MonoBehaviour
{
    [SerializeField]
    Button btn;
    [SerializeField]
    TextMeshProUGUI text;
    [SerializeField]
    GameObject selectedImage;
    
    public string CategoryName { get; private set; }

    public void Init(string categoryName, Action onClick)
    {
        CategoryName = categoryName;
        text.text = categoryName;
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => onClick?.Invoke());
    }
    
    public void SetSelected(bool selected)
    {
        if (selectedImage != null)
        {
            selectedImage.SetActive(selected);
        }
        
    }
}
