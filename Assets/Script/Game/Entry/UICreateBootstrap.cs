using Cysharp.Threading.Tasks;
using UnityEngine;

// 仅供 UICreate 场景使用，集中准备该场景运行 UI 所需的游戏状态。
public sealed class UICreateBootstrap : MonoBehaviour
{
    [SerializeField]
    GameObject designCanvas;

    async void Start()
    {
        designCanvas.SetActive(false);

        // Preview progress has its own profile selection and never changes the login entry preferences.
        var preferences = new LoginPreferences("MyUISystem.UIPreview.");
        var profiles = SaveProfileManager.Instance;
        if (!profiles.TryGetProfile(preferences.LocalProfileId, out var profile, out _))
        {
            profile = profiles.CreateLocalProfile();
            preferences.RecordLocalProfile(profile.profileId);
        }
        await FlowManager.Instance.BootPreviewAsync(Camera.main, profile.profileId,
            this.GetCancellationTokenOnDestroy());
    }
}
