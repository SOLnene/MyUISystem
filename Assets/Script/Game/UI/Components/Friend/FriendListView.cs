using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class FriendListView : MonoBehaviour
{
    [SerializeField]
    RectTransform content;
    [SerializeField]
    FriendItemView itemPrefab;
    [SerializeField] AnimatedPanelGroup motions;
    internal Action<string> RejectRequested;

    readonly List<FriendItemView> itemViews = new();
    readonly List<AnimatedPanel> activePanels = new();

    public void Bind(
        IReadOnlyList<FriendInfo> friends,
        Action<string> onChatRequested)
    {
        motions.HideAllImmediate();
        activePanels.Clear();
        // 实例池只按历史最大数量增长，页面关闭或数据减少时保留 Item 供下次复用。
        while (itemViews.Count < friends.Count)
        {
            itemViews.Add(Instantiate(itemPrefab, content));
        }

        for (int i = 0; i < itemViews.Count; i++)
        {
            bool active = i < friends.Count;
            FriendItemView itemView = itemViews[i];
            itemView.gameObject.SetActive(active);

            if (active)
            {
                itemView.BindActions(friends[i], onChatRequested, RejectRequested);
                motions.HideImmediate(itemView.Panel);
                activePanels.Add(itemView.Panel);
            }
            else
            {
                itemView.Unbind();
            }
        }
    }

    // Binding prepares hidden rows; the page owns when their shared stagger begins.
    internal UniTask ShowItems() => motions.Show(activePanels);

    public void Clear()
    {
        motions.HideAllImmediate();
        activePanels.Clear();
        foreach (FriendItemView itemView in itemViews)
        {
            itemView.Unbind();
            itemView.gameObject.SetActive(false);
        }
    }
}
