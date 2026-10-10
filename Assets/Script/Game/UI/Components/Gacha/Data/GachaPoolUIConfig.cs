using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Gacha Pool UI Config")]
public class GachaPoolUIConfig : ScriptableObject
{
    public string gachaKey;
    public GachaPoolType poolType;
    // 入口（必定存在）
    public string tabIconAddress;

    // 内容（可失败）
    // 只保存 Addressable key，加载数据库时不连带加载所有卡池图片。
    public string poolBackgroundAddress;
    public string primaryRateUpIconAddress;
    public string secondaryRateUpIconAddress;
    public string primaryRateUpName;

}
