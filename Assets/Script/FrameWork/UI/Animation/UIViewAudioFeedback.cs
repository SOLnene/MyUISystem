using UnityEngine;

// UIViewHandle consumes this component, so it must belong to the framework assembly.
[DisallowMultipleComponent]
public sealed class UIViewAudioFeedback : MonoBehaviour
{
    enum PlaybackPolicy { Default, Silent, Custom }

    [SerializeField] PlaybackPolicy openPolicy;
    [SerializeField] PlaybackPolicy closePolicy;
    [SerializeField] AudioClip openClip;
    [SerializeField] AudioClip closeClip;
    [SerializeField, Range(0f, 1f)] float openVolume = 1f;
    [SerializeField, Range(0f, 1f)] float closeVolume = 1f;

    internal void PlayOpen()
    {
        PlayLifecycle(openPolicy, openClip, openVolume, UISound.PanelOpen);
    }

    internal void PlayClose()
    {
        PlayLifecycle(closePolicy, closeClip, closeVolume, UISound.PanelClose);
    }

    static void PlayLifecycle(PlaybackPolicy policy, AudioClip clip, float volume, UISound fallback)
    {
        // A page owns either its dedicated lifecycle sound or the default, never both.
        if (policy == PlaybackPolicy.Default)
            AudioManager.Instance.PlayUI(fallback);
        else if (policy == PlaybackPolicy.Custom)
            AudioManager.Instance.PlayUI(clip, volume);
    }
}
