public class FriendInfo
{
    public PlayerSummaryInfo Player { get; }
    public int Level { get; }
    public string PresenceText { get; }
    public string Signature { get; }

    public FriendInfo(
        PlayerSummaryInfo player,
        int level,
        string presenceText,
        string signature)
    {
        Player = player;
        Level = level;
        PresenceText = presenceText;
        Signature = signature;
    }
}
