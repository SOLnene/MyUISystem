public readonly struct AchievementCountInfo
{
    public int ClaimedCount { get; }
    public int TotalCount { get; }

    public AchievementCountInfo(int claimedCount, int totalCount)
    {
        ClaimedCount = claimedCount;
        TotalCount = totalCount;
    }
}
