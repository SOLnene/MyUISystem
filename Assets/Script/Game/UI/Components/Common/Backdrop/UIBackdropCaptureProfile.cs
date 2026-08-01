using UnityEngine;

public readonly struct UIBackdropCaptureProfile
{
    // Reward popups use a half-resolution target to reduce blur cost while retaining the backdrop
    // impression behind the foreground reward content.
    public static UIBackdropCaptureProfile RewardPopup =>
        new(0.5f, 2.25f, 0.28f, 0.72f,
            new Color(0.75f, 0.78f, 0.95f, 1f), 0.35f);

    // These values are applied to the shared capture material immediately before recording a
    // request; they describe the visual treatment, not the lifetime of the RenderTexture.
    public float ResolutionScale { get; }
    public float BlurRadius { get; }
    public float Saturation { get; }
    public float Brightness { get; }
    public Color Tint { get; }
    public float TintStrength { get; }

    public UIBackdropCaptureProfile(
        float resolutionScale,
        float blurRadius,
        float saturation,
        float brightness,
        Color tint,
        float tintStrength)
    {
        ResolutionScale = resolutionScale;
        BlurRadius = blurRadius;
        Saturation = saturation;
        Brightness = brightness;
        Tint = tint;
        TintStrength = tintStrength;
    }
}
