using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class StarDisplay : MonoBehaviour
{
    [SerializeField]
    Transform starParent;
    List<GameObject> stars = new List<GameObject>();
    
    public void SetStarLevel(int level,int size = 40,int space = 20)
    {
        string path = "Assets/Sprite/Backpack/UI_IconStar.png";
        UpdateLayout(size, space);
        foreach (var star in stars)
        {
            Destroy(star);
        }
        ResourceManager.Instance.LoadAssetAsync<Sprite>(path, sprite =>
        {
            if (sprite == null)
            {
                Debug.LogError($"加载星级图片失败: {path}");
                return;
            }
            for (int i = 0; i < level; i++)
            {
                CreateStarImage(sprite,size);
            }
        });
    }
    
    /// <summary>
    /// 创建带有星星image的gameobject
    /// </summary>
    /// <param name="sprite"></param>
    /// <param name="size"></param>
    private void CreateStarImage(Sprite sprite,int size = 40)
    {
        GameObject starGO = new GameObject("StarIcon", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        var rect = starGO.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(size, size);  
        var image = starGO.GetComponent<UnityEngine.UI.Image>();
        image.sprite = sprite;
        image.color = new Color(1.0f, 0.8f, 0.2f);
        //image.SetNativeSize();

        // 放到目标容器下，例如 Slot/Stars
        starGO.transform.SetParent(starParent, false);
        stars.Add(starGO);
    }
    
    private void UpdateLayout(int starSize = 40,int spacing = 20)
    {
        // 可选：让父节点自适应宽度
        var parentRect = GetComponent<RectTransform>();
        float totalWidth = stars.Count * (starSize+ spacing) - spacing;
        parentRect.sizeDelta = new Vector2(parentRect.sizeDelta.x, MathF.Max(starSize,parentRect.sizeDelta.y));
    }
}
