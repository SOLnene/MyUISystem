using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "Game/Gacha Pool UI Config")]
public class GachaPoolUIConfig : ScriptableObject
{
    public string gachaKey;
    public GachaPoolType poolType;
    // 入口（必定存在）
    public Sprite tabIcon;

    // 内容（可失败）
    public Sprite poolBackground;
    [FormerlySerializedAs("poolVisual")]
    public Sprite primaryRateUpIcon;
    public Sprite secondaryRateUpIcon;
    public string primaryRateUpName;

    public Sprite poolVisual => primaryRateUpIcon;
}
