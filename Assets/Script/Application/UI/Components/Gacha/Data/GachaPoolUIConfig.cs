using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "Game/Gacha Pool UI Config")]
public class GachaPoolUIConfig : ScriptableObject
{
    public GachaPoolType poolType;
    // 入口（必定存在）
    public Sprite tabIcon;

    // 内容（可失败）
    [FormerlySerializedAs("poolVisualKey")] public string poolVisualPath;
}