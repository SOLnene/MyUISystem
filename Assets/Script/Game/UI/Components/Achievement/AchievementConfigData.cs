using System;
using System.Collections.Generic;
using Newtonsoft.Json;

[Serializable]
public sealed class AchievementConfigData
{
    public int version;
    public List<AchievementCategoryConfigData> categories = new();
}

[Serializable]
public sealed class AchievementCategoryConfigData
{
    // id 是运行时选中状态和进度归属的稳定标识，order 只负责显示顺序。
    public string id;
    public string name;
    public string iconAddress;
    public int order;
    public List<AchievementDefinition> achievements = new();
}

[Serializable]
public sealed class AchievementDefinition
{
    // progressKey 连接统一进度服务；target 表示该成就自己的完成阈值。
    public string id;
    public string title;
    public string description;
    public string iconAddress;
    [JsonProperty]
    internal string progressKey;
    public int target;
    public AchievementRewardConfigData reward;
}

[Serializable]
public sealed class AchievementRewardConfigData
{
    public int itemId;
    public int amount;
}
