using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Transform的拓展方法
/// </summary>
public static class TransformExtension
{
    /// <summary>
    /// set parent 并重置位置、旋转、缩放
    /// </summary>
    /// <param name="trans"></param>
    /// <param name="parent"></param>
    public static void SetParentAndReset(this Transform trans, Transform parent)
    {
        trans.SetParent(parent);
        trans.localPosition=Vector3.zero;
        trans.localRotation = Quaternion.identity;
        trans.localScale = Vector3.one;
    }
}
