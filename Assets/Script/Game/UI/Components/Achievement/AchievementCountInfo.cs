public readonly struct AchievementCountInfo
{
    public int CompletedCount { get; }
    public int TotalCount { get; }

    public AchievementCountInfo(int completedCount, int totalCount)
    {
        CompletedCount = completedCount;
        TotalCount = totalCount;
    }
}
