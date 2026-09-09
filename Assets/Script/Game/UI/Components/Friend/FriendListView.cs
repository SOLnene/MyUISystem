using System;
using System.Collections.Generic;
using UnityEngine;

public class FriendListView : MonoBehaviour
{
    [SerializeField]
    RectTransform content;
    [SerializeField]
    FriendItemView itemPrefab;

    readonly List<FriendItemView> itemViews = new();

    public void Bind(
        IReadOnlyList<FriendInfo> friends,
        Action<string> onChatRequested)
    {
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
                itemView.Bind(friends[i], onChatRequested);
            }
            else
            {
                itemView.Unbind();
            }
        }
    }

    public void Clear()
    {
        foreach (FriendItemView itemView in itemViews)
        {
            itemView.Unbind();
            itemView.gameObject.SetActive(false);
        }
    }
}
