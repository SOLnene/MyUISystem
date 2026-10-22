using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class GachaResultItemFx : MonoBehaviour
{
    [SerializeField] Canvas cardCanvas;
    [SerializeField] CanvasGroup frameGlowGroup;
    [SerializeField] Image frameOverlay;
    [SerializeField] Sprite threeStarFrame;
    [SerializeField] Sprite fourStarFrame;
    [SerializeField] Sprite fiveStarFrame;
    [SerializeField] ParticleSystem[] motes;
    [SerializeField] ParticleSystem[] accents;
    [SerializeField, ColorUsage(false)] Color fourStarGlowColor = new Color(0.32f, 0.24f, 1f);

    [Serializable]
    sealed class EffectSettings
    {
        [Range(0f, 1f)] public float frameOpacity = 1f;
        [Min(0f)] public float moteScale = 1f;
        [Min(0f)] public float moteRateScale = 1f;
        [Min(0f)] public float accentScale = 1f;
        public Vector2 accentSpread = Vector2.one;
        [Min(0f)] public float accentRateScale = 1f;
        [Min(0)] public int accentBurst = 2;
        [Min(0f)] public float sparkleIntensity = 1f;
    }

    struct ParticleDefaults
    {
        public ParticleSystem.MinMaxCurve sizeX;
        public ParticleSystem.MinMaxCurve sizeY;
        public float rate;
        public Vector3 area;

        public ParticleDefaults(ParticleSystem system)
        {
            var main = system.main;
            sizeX = main.startSizeX;
            sizeY = main.startSizeY;
            rate = system.emission.rateOverTimeMultiplier;
            area = system.shape.scale;
        }
    }

    [SerializeField] EffectSettings threeStarEffect = new EffectSettings
    {
        frameOpacity = 0.6f,
        moteScale = 0.75f, moteRateScale = 0.6f, accentScale = 0.4f,
        accentSpread = new Vector2(0.75f, 0.6f), accentRateScale = 0f,
        accentBurst = 0, sparkleIntensity = 1f
    };
    [SerializeField] EffectSettings fourStarEffect = new EffectSettings
    {
        frameOpacity = 0.9f,
        moteScale = 0.85f, moteRateScale = 0.8f, accentScale = 0.55f,
        accentSpread = new Vector2(0.85f, 0.65f), accentRateScale = 0.65f,
        accentBurst = 1, sparkleIntensity = 0.95f
    };
    [SerializeField] EffectSettings fiveStarEffect = new EffectSettings
    {
        frameOpacity = 0.95f,
        moteScale = 1f, moteRateScale = 1.2f, accentScale = 0.65f,
        accentSpread = new Vector2(0.9f, 0.7f), accentRateScale = 0.9f,
        accentBurst = 1, sparkleIntensity = 1.05f
    };

    static readonly int ColorId = Shader.PropertyToID("_Color");
    ParticleSystem[] particles;
    ParticleSystemRenderer[] foregroundRenderers;
    CanvasGroup[] visibilityGroups;
    Canvas ownerCanvas;
    MaterialPropertyBlock properties;
    bool playing;
    int rarity;
    float pulsePhase;
    EffectSettings effect;
    ParticleDefaults[] moteDefaults;
    ParticleDefaults[] accentDefaults;

    void Awake()
    {
        particles = GetComponentsInChildren<ParticleSystem>(true);
        var foreground = new ParticleSystem[motes.Length + accents.Length];
        motes.CopyTo(foreground, 0);
        accents.CopyTo(foreground, motes.Length);
        foregroundRenderers = Array.ConvertAll(foreground, system => system.GetComponent<ParticleSystemRenderer>());
        properties = new MaterialPropertyBlock();
        // Always apply a tier against the authored baseline, never against the previous tier.
        moteDefaults = Array.ConvertAll(motes, system => new ParticleDefaults(system));
        accentDefaults = Array.ConvertAll(accents, system => new ParticleDefaults(system));
    }

    internal Color GlowColor { get; private set; }

    internal void BindRarity(int value, Color color)
    {
        StopAndClear();
        rarity = value;
        effect = rarity >= 5 ? fiveStarEffect : rarity == 4 ? fourStarEffect : threeStarEffect;
        // Calibrate the four-star glow without changing the shared inventory and reward UI palette.
        GlowColor = rarity == 4 ? fourStarGlowColor : color;
        // Every sprite includes the frame and both apex lights, registered to the same card contour.
        // Keep vertex tint white so the baked white core and colored halo retain their separate colors.
        frameOverlay.sprite = rarity >= 5 ? fiveStarFrame : rarity == 4 ? fourStarFrame : threeStarFrame;
        frameOverlay.color = Color.white;
        for (int i = 0; i < motes.Length; i++)
        {
            var main = motes[i].main;
            main.startColor = Color.Lerp(GlowColor, Color.white, 0.3f);
            main.startSizeX = ScaleCurve(moteDefaults[i].sizeX, effect.moteScale);
            main.startSizeY = ScaleCurve(moteDefaults[i].sizeY, effect.moteScale);
            var emission = motes[i].emission;
            emission.rateOverTimeMultiplier = moteDefaults[i].rate * effect.moteRateScale;
        }
        for (int i = 0; i < accents.Length; i++)
        {
            var main = accents[i].main;
            main.startColor = new ParticleSystem.MinMaxGradient(
                Color.Lerp(GlowColor, Color.white, 0.85f), new Color(1f, 1f, 0.82f));
            main.startSizeX = ScaleCurve(accentDefaults[i].sizeX, effect.accentScale);
            main.startSizeY = ScaleCurve(accentDefaults[i].sizeY, effect.accentScale);
            var shape = accents[i].shape;
            shape.scale = Vector3.Scale(accentDefaults[i].area, new Vector3(effect.accentSpread.x, effect.accentSpread.y, 1f));
            var emission = accents[i].emission;
            emission.rateOverTimeMultiplier = accentDefaults[i].rate * effect.accentRateScale;
            // Accents serialize one star/streak pair per tip; streak bursts stay sparser than stars.
            int count = i % 2 == 0 ? effect.accentBurst : (effect.accentBurst + 1) / 2;
            emission.SetBursts(count > 0
                ? new[] { new ParticleSystem.Burst(0f, new ParticleSystem.MinMaxCurve(1f, count)) }
                : Array.Empty<ParticleSystem.Burst>());
        }
    }

    static ParticleSystem.MinMaxCurve ScaleCurve(ParticleSystem.MinMaxCurve curve, float scale)
    {
        switch (curve.mode)
        {
            case ParticleSystemCurveMode.Constant:
                curve.constant *= scale;
                break;
            case ParticleSystemCurveMode.TwoConstants:
                curve.constantMin *= scale;
                curve.constantMax *= scale;
                break;
            default:
                curve.curveMultiplier *= scale;
                break;
        }
        return curve;
    }

    internal void Play()
    {
        // Skipping the entrance may call Play again; keep the existing sparkle layout alive.
        if (playing || !isActiveAndEnabled)
            return;

        ownerCanvas = GetComponentInParent<Canvas>();
        visibilityGroups = GetComponentsInParent<CanvasGroup>();
        frameOverlay.enabled = true;
        foreach (var system in motes)
            system.Play(false);
        if (effect.accentRateScale > 0f || effect.accentBurst > 0)
        {
            foreach (var system in accents)
                system.Play(false);
        }
        pulsePhase = motes[0].randomSeed / (float)uint.MaxValue * Mathf.PI * 2f;
        playing = true;
        SynchronizeRendering();
    }

    internal void StopAndClear()
    {
        playing = false;
        frameOverlay.enabled = false;
        foreach (var system in particles)
            system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    void LateUpdate()
    {
        if (playing)
            SynchronizeRendering();
    }

    void SynchronizeRendering()
    {
        float alpha = 1f;
        foreach (var group in visibilityGroups)
        {
            if (!group.enabled)
                continue;
            alpha *= group.alpha;
            if (group.ignoreParentGroups)
                break;
        }

        // Windows reserve ten sorting orders. All ten cards share these three local layers.
        int order = ownerCanvas.sortingOrder;
        int layer = ownerCanvas.sortingLayerID;
        cardCanvas.overrideSorting = true;
        cardCanvas.sortingLayerID = layer;
        cardCanvas.sortingOrder = order + 2;
        cardCanvas.worldCamera = ownerCanvas.worldCamera;

        // The unified UI frame inherits parent fades; native moving particles need the alpha explicitly.
        float wave = Mathf.Sin(Time.unscaledTime * 2f + pulsePhase);
        frameGlowGroup.alpha = effect.frameOpacity * (0.95f + 0.05f * wave);
        float sparkle = effect.sparkleIntensity;
        properties.SetColor(ColorId, new Color(sparkle, sparkle, sparkle, alpha));
        foreach (var renderer in foregroundRenderers)
        {
            renderer.sortingLayerID = layer;
            renderer.sortingOrder = order + 3;
            renderer.SetPropertyBlock(properties);
        }
    }

    void OnDisable()
    {
        StopAndClear();
    }
}
