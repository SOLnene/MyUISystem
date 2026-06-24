using System.Collections.Generic;
public interface IGachaSchedule
{
    IReadOnlyList<string> GetActiveGachaKeys();
}


//负责“哪一个池是当前活跃池”
public class LocalGachaSchedule : IGachaSchedule
{
    readonly string[] activeGachaKeys =
    {
        "CharPool_Hutao",
        "CharPool_Eula",
        "WeaponPool_01"
    };

    public IReadOnlyList<string> GetActiveGachaKeys()
    {
        return activeGachaKeys;
    }
}
