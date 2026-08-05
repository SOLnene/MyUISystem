using UnityEngine;

public sealed class TeamStageView : MonoBehaviour
{
    [SerializeField] private Camera displayCamera;
    [SerializeField] private Transform[] memberInfoAnchors;
    [SerializeField] private string[] memberCharacterKeys;

    public Camera DisplayCamera => displayCamera;
    public int MemberCount => Mathf.Min(memberInfoAnchors.Length, memberCharacterKeys.Length);

    public Vector3 GetMemberInfoPosition(int index)
    {
        return memberInfoAnchors[index].position;
    }

    public string GetMemberCharacterKey(int index)
    {
        return memberCharacterKeys[index];
    }
}
