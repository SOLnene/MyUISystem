using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public interface IGachaSchedule
{
    string GetActiveGachaKey(GachaPoolType type);
}


//负责“哪一个池是当前活跃池”
public class LocalGachaSchedule : IGachaSchedule
{
    public string GetActiveGachaKey(GachaPoolType type)
    {
        // 暂时返回固定 key，可未来扩展
        switch (type)
        {
            case GachaPoolType.Equip: return "WeaponPool_01";
            case GachaPoolType.Character: return "CharPool_01";
            case GachaPoolType.Mixed: return "MixedPool_01";
        }
        return null;
    }
}
