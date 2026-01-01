using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Item Definition/Weapon")]
public class EquipDefinition : ItemDefinition
{
    public int baseAttack;
    public float baseCritRate;
    public float baseCritDamage;
    //todo:添加武器种类
    //public WeaponType weaponType;

    //public int baseExpValue;

    // 突破/阶级表（建议从 ScriptableObject / JSON 加载）
    public List<RankInfo> rankInfos = new List<RankInfo>();

    // 获取总 rank 数量（最高 rank index）
    public int MaxRank => rankInfos?.Count - 1 ?? 0;

    public int GetMaxLevelForRank(int rank)
    {
        if (rankInfos == null || rankInfos.Count == 0) return 1;
        var r = rank;
        if (r < 0) r = 0;
        if (r >= rankInfos.Count) r = rankInfos.Count - 1;
        return rankInfos[r].maxLevel;
    }

    public RankInfo GetRankInfo(int rank)
    {
        if (rankInfos == null || rankInfos.Count == 0) return null;
        var r = Mathf.Clamp(rank, 0, rankInfos.Count - 1);
        return rankInfos[r];
    }


}
