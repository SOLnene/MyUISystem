using Cinemachine;
using UnityEngine;

public sealed class TeamStageCameraController : MonoBehaviour
{
    private const int ActivePriority = 20;
    private const int InactivePriority = 10;

    [SerializeField] private CinemachineVirtualCamera overviewCamera;
    [SerializeField] private CinemachineVirtualCamera memberFocusCamera;
    [SerializeField] private Transform[] memberFocusTargets;

    private void Awake()
    {
        ShowOverview();
    }

    internal void FocusMember(int memberIndex)
    {
        Transform target = memberFocusTargets[memberIndex];
        memberFocusCamera.Follow = target;
        memberFocusCamera.LookAt = target;
        memberFocusCamera.Priority = ActivePriority;
        overviewCamera.Priority = InactivePriority;
    }

    internal void ShowOverview()
    {
        overviewCamera.Priority = ActivePriority;
        memberFocusCamera.Priority = InactivePriority;
    }
}
