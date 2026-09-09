using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PlayerInfoView : MonoBehaviour
{
    [SerializeField] private Image avatarIcon;
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text playerIdText;

    public void Bind(PlayerSummaryInfo playerSummary)
    {
        playerNameText.text = playerSummary.DisplayName;
        playerIdText.text = $"UID {playerSummary.PlayerId}";

        // 本地档案暂未保存头像，未提供覆盖值时保留 Prefab 中配置的默认头像。
        if (playerSummary.Avatar != null)
        {
            avatarIcon.sprite = playerSummary.Avatar;
        }
    }
}
