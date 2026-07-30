using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RewardPopupView : UIView
{
    [SerializeField]
    Button closeHandle;
    [SerializeField]
    RewardListView rewardListView;

    public override void OnAddListener()
    {
        closeHandle.onClick.RemoveListener(HandleClose);
        closeHandle.onClick.AddListener(HandleClose);
    }

    public override void OnRemoveListener()
    {
        closeHandle.onClick.RemoveListener(HandleClose);
    }

    public override void OnOpen(object data)
    {
        base.OnOpen(data);

        if (data is not IReadOnlyList<RewardItemData> rewards)
        {
            rewardListView.Clear();
            return;
        }

        rewardListView.Bind(rewards, GameDatabase.ItemDatabase);
    }

    public override void OnRelease()
    {
        rewardListView.Clear();
        base.OnRelease();
    }

    public override void OnClose()
    {
        rewardListView.Clear();
        base.OnClose();
        EventBus<RewardPopupClosedEvent>.Raise(default);
    }

    void HandleClose()
    {
        OnCancel();
    }
}
