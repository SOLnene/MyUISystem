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
    public string id;
    public string name;
    public string iconAddress;
    public int order;
    public List<AchievementDefinition> achievements = new();
}

[Serializable]
public sealed class AchievementDefinition
{
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
