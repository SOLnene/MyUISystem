using System.Collections.Generic;
using UnityEngine;

internal sealed class RewardPopupOpenParams
{
    // This object transports reward data and the capture result across the async coordinator/UI
    // boundary. The view does not own or release the RenderTexture; the capture service does.
    public IReadOnlyList<RewardItemData> Rewards { get; }
    public RenderTexture Backdrop { get; }

    public RewardPopupOpenParams(
        IReadOnlyList<RewardItemData> rewards,
        RenderTexture backdrop)
    {
        Rewards = rewards;
        Backdrop = backdrop;
    }
}
