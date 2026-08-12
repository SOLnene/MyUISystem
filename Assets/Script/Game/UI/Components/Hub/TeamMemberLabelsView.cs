using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

public sealed class TeamMemberLabelsView : MonoBehaviour
{
    [SerializeField] private AnimatedPanel animatedPanel;
    [SerializeField] private TMP_Text[] memberLabels;

    internal void Refresh(TeamStageView teamStage)
    {
        int memberCount = Mathf.Min(teamStage.MemberCount, memberLabels.Length);
        for (int memberIndex = 0; memberIndex < memberLabels.Length; memberIndex++)
        {
            TMP_Text memberLabel = memberLabels[memberIndex];
            if (memberIndex >= memberCount
                || !teamStage.TryGetMemberCharacterKey(
                    memberIndex,
                    out string characterKey))
            {
                memberLabel.gameObject.SetActive(false);
                continue;
            }

            var character = GameContext.Instance.CharacterRepository.GetByKey(
                characterKey);
            memberLabel.gameObject.SetActive(character != null);
            if (character != null)
            {
                memberLabel.text = $"{character.Name.Value}\nLv.{character.LevelRP.Value}";
            }
        }
    }

    internal void UpdatePositions(TeamStageView teamStage, Camera eventCamera)
    {
        int memberCount = Mathf.Min(teamStage.MemberCount, memberLabels.Length);
        for (int memberIndex = 0; memberIndex < memberCount; memberIndex++)
        {
            TMP_Text memberLabel = memberLabels[memberIndex];
            if (!memberLabel.gameObject.activeSelf)
            {
                continue;
            }

            Vector3 screenPosition = teamStage.DisplayCamera.WorldToScreenPoint(
                teamStage.GetMemberInfoPosition(memberIndex));
            if (screenPosition.z <= 0f)
            {
                continue;
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    memberLabel.rectTransform.parent as RectTransform,
                    screenPosition,
                    eventCamera,
                    out Vector2 anchoredPosition))
            {
                memberLabel.rectTransform.anchoredPosition = anchoredPosition;
            }
        }
    }

    internal UniTask ShowLabels(bool instant = false)
    {
        return animatedPanel.Show(instant);
    }

    internal async UniTask HideLabels(bool instant = false)
    {
        await animatedPanel.Hide(instant);
    }

    internal void HideLabelsImmediate()
    {
        animatedPanel.HideImmediate();
    }
}
