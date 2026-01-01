using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum UIAdaptType
{
    All,
    LeftOrTop,
    RightOrBottom
}

public class UIAdapter : MonoBehaviour
{
    public UIAdaptType uiAdaptType = UIAdaptType.All;

    float cd;

    void Update()
    {
        //TODO:可以只在 OnRectTransformDimensionsChange()、屏幕方向事件或 safeArea 变化时触发。
        // 为避免旋转屏幕，华为分屏机等导致分辨率变化，且安全区变化的问题，需要持续检测
        if (Time.time > cd)
        {
            InitAdapter();
            cd = Time.time + 1;
        }
    }


    void InitAdapter()
    {
        var safeArea = Screen.safeArea;
        if (UIManager.Instance != null)
        {
            safeArea = UIManager.Instance.GetSafeArea();
        }
        var orientation = Screen.orientation;
        RectTransform rectTransform = transform as RectTransform;
        /*sizeDelta 是在 锚点约束下的“附加宽高差值”。
        anchors 固定 → sizeDelta = 宽高。
        anchors 拉伸 → sizeDelta = 在父容器大小基础上的差值（边距）。*/
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.offsetMin = rectTransform.offsetMax = Vector2.zero;
        rectTransform.sizeDelta = Vector2.zero;
        if (orientation == ScreenOrientation.LandscapeLeft || orientation == ScreenOrientation.LandscapeRight)
        {
            switch (uiAdaptType)
            {
                case UIAdaptType.All:
                    rectTransform.anchorMin = new Vector2(safeArea.xMin / Screen.width, 0);
                    rectTransform.anchorMax = new Vector2(safeArea.xMax/Screen.width, 1);
                    break;
                case UIAdaptType.LeftOrTop:
                    if (orientation == ScreenOrientation.LandscapeLeft)
                    {
                        rectTransform.anchorMin = new Vector2(safeArea.xMin / Screen.width, 0);
                        rectTransform.anchorMax = new Vector2(1, 1);
                    }
                    else
                    {
                        rectTransform.anchorMin = new Vector2(0, 0);
                        rectTransform.anchorMax = new Vector2(safeArea.xMax/Screen.width, 1);
                    }
                    break;
                case UIAdaptType.RightOrBottom:
                    if (orientation == ScreenOrientation.LandscapeLeft)
                    {
                        rectTransform.anchorMin = new Vector2(0,0);
                        rectTransform.anchorMax = new Vector2(safeArea.xMax/Screen.width, 1);
                    }
                    else
                    {
                        rectTransform.anchorMin = new Vector2(safeArea.xMin/Screen.width, 0);
                        rectTransform.anchorMax = new Vector2(1,1);
                    }
                    break;
            }
        }
        else if (orientation == ScreenOrientation.Portrait || orientation == ScreenOrientation.PortraitUpsideDown)
        {
            switch (uiAdaptType)
            {
                case UIAdaptType.All:
                    rectTransform.anchorMin = new Vector2(0, safeArea.yMin / Screen.height);
                    rectTransform.anchorMax = new Vector2(1,safeArea.yMax / Screen.height);
                    break;
                case UIAdaptType.LeftOrTop:
                    if (orientation == ScreenOrientation.Portrait)
                    {
                        rectTransform.anchorMin = new Vector2(0, 0);
                        rectTransform.anchorMax = new Vector2(1,safeArea.yMax/Screen.height);
                    }
                    else
                    {
                        rectTransform.anchorMin = new Vector2(0,safeArea.yMin/Screen.height);
                        rectTransform.anchorMax = new Vector2(1, 1);
                    }
                    break;
                case UIAdaptType.RightOrBottom:
                    if (orientation == ScreenOrientation.Portrait)
                    {
                        rectTransform.anchorMin = new Vector2(0, safeArea.yMin / Screen.height);
                        rectTransform.anchorMax = new Vector2(1, 1);
                    }
                    else
                    {
                        rectTransform.anchorMin = new Vector2(0, 0);
                        rectTransform.anchorMax = new Vector2(1, safeArea.yMax / Screen.height);
                    }
                    break;
            }
        }
    }
}
