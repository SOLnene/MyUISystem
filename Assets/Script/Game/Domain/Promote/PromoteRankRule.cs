using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 单个阶级的突破材料配置，做成 ScriptableObject，便于在不同角色之间复用、排列组合。
/// 建议放在 Assets/GameData/Promote 下，与 CharacterPromoteDefinition 一起使用。
/// </summary>
[CreateAssetMenu(menuName = "Game/Promote/Promote Rank Rule")]
public class PromoteRankRule : ScriptableObject
{
    /// <summary>
    /// 阶级编号（0 起始：0阶→1阶...）
    /// </summary>
    public int rank;

    /// <summary>
    /// 本阶突破所需的材料清单
    /// </summary>
    public List<PromoteMaterialCost> materials = new List<PromoteMaterialCost>();
}

/// <summary>
/// 单项材料的 Key 与数量
/// </summary>
[Serializable]
public class PromoteMaterialCost
{
    /// <summary>
    /// 材料在 ItemDatabase 中的 key，如 "expbook_small"
    /// </summary>
    public string materialKey;

    /// <summary>
    /// 该材料需要的数量
    /// </summary>
    public int count;
}

