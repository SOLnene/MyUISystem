using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
/// <summary>
/// 通用ui方法
/// </summary>
public static class UIHelper
{
    /// <summary>
    /// 创建一个全屏点击区域，点击后触发回调
    /// </summary>
    /// <param name="parent">父节点（一般是 Canvas 或 UILayer）</param>
    /// <param name="onClick">点击回调</param>
    /// <param name="raycastTarget">是否允许阻挡下层 UI</param>
    /// <param name="name">GameObject 名称</param>
    /// <returns>创建好的 GameObject</returns>
    public static GameObject CreateFullScreenClick(Transform parent, Action onClick, string name = "FullScreenClick")
    {
        var go = new GameObject(name);
        var rect = go.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        // 透明 Image 用于接收 PointerEvent
        var img = go.AddComponent<UnityEngine.UI.Image>();
        img.color = Color.clear;
        img.raycastTarget = true;

        // 添加你的自定义点击处理器
        var clickHandler = go.AddComponent<FullScreenClickHandler>();
        clickHandler.onClick = onClick;
        go.transform.SetAsFirstSibling();
        return go;
    }
    
    /// <summary>
    /// 创建一个 UI 层级专用的 Canvas，并完成常用组件的初始化。
    /// </summary>
    /// <param name="layer">指定的 UI 层（名字会作为 Canvas 的 GameObject 名字，并决定 sortingOrder）</param>
    /// <param name="is3D">是否是 3D Canvas（true 使用 WorldSpace，false 使用 ScreenSpaceCamera）</param>
    /// <param name="parent">父节点 Transform，会自动重置 RectTransform</param>
    /// <param name="camera">当为 ScreenSpaceCamera 或 WorldSpace 时绑定的摄像机</param>
    /// <param name="width">参考分辨率宽度，用于 CanvasScaler</param>
    /// <param name="height">参考分辨率高度，用于 CanvasScaler</param>
    /// <returns>返回新建并初始化好的 Canvas 组件</returns>
    public static Canvas CreateLayerCanvas(UILayer layer, bool is3D, Transform parent, Camera camera, float width, float height)
    {
        GameObject canvasGo = new GameObject(layer.ToString());
        RectTransform rectTransform = canvasGo.AddComponent<RectTransform>();
        rectTransform.SetParentAndReset(parent);
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = Vector2.zero;
        rectTransform.localScale = Vector3.one;
        canvasGo.layer = is3D? LayerMask.NameToLayer("Default") : LayerMask.NameToLayer("UI");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = is3D?RenderMode.WorldSpace:RenderMode.ScreenSpaceCamera;
        canvas.overrideSorting = true;
        canvas.sortingOrder = (int)layer;
        canvas.worldCamera = camera;
        canvas.pixelPerfect = false;
        CanvasScaler canvasScaler = canvasGo.AddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(width, height);
        canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        canvasGo.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    /// <summary>
    /// 创建一个黑色遮罩，常用于弹窗背景
    /// </summary>
    /// <param name="parent"></param>
    /// <param name="alpha"></param>
    /// <param name="name">canvasGroup的name</param>
    /// <returns></returns>
    public static CanvasGroup CreateBlackMask(Transform parent, float alpha = 0, string name = null)
    {
        GameObject maskGo = new GameObject("Black Mask");
        RectTransform rectTransform = maskGo.AddComponent<RectTransform>();
        rectTransform.SetParentAndReset(parent);
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        Image image = maskGo.AddComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;
        CanvasGroup canvasGroup = maskGo.AddComponent<CanvasGroup>();
        canvasGroup.alpha = alpha;
        if(name != null)
            canvasGroup.name = name;
        return canvasGroup;
    }
}
