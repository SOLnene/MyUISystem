using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GachaResultItemFx : MonoBehaviour
{
    [SerializeField] Canvas cardCanvas;
    [SerializeField] ParticleSystem[] beams;
    [SerializeField] ParticleSystem[] motes;
    [SerializeField] ParticleSystem[] accents;

    static readonly int ColorId = Shader.PropertyToID("_Color");
    ParticleSystem[] particles;
    ParticleSystemRenderer[] beamRenderers;
    ParticleSystemRenderer[] foregroundRenderers;
    CanvasGroup[] visibilityGroups;
    Canvas ownerCanvas;
    MaterialPropertyBlock properties;
    bool playing;
    int rarity;
    float pulsePhase;

    void Awake()
    {
        particles = GetComponentsInChildren<ParticleSystem>(true);
        beamRenderers = Array.ConvertAll(beams, system => system.GetComponent<ParticleSystemRenderer>());
        var foreground = new ParticleSystem[motes.Length + accents.Length];
        motes.CopyTo(foreground, 0);
        accents.CopyTo(foreground, motes.Length);
        foregroundRenderers = Array.ConvertAll(foreground, system => system.GetComponent<ParticleSystemRenderer>());
        properties = new MaterialPropertyBlock();
    }

    internal void BindRarity(int value, Color color)
    {
        StopAndClear();
        rarity = value;
        bool highlighted = rarity >= 4;
        for (int i = 0; i < beams.Length; i++)
        {
            // Each tip serializes its halo before its bright core.
            bool core = i % 2 == 1;
            Color tint = core ? Color.Lerp(color, Color.white, 0.8f) : color;
            tint.a = highlighted ? (core ? 0.85f : 0.75f) : (core ? 0.035f : 0.12f);
            var main = beams[i].main;
            main.startColor = tint;
        }
        foreach (var system in motes)
        {
            var main = system.main;
            main.startColor = Color.Lerp(color, Color.white, 0.3f);
        }
        foreach (var system in accents)
        {
            var main = system.main;
            main.startColor = Color.Lerp(color, Color.white, 0.75f);
        }
    }

    internal void Play()
    {
        // Skipping the entrance may call Play again; keep the existing sparkle layout alive.
        if (playing || !isActiveAndEnabled)
            return;

        ownerCanvas = GetComponentInParent<Canvas>();
        visibilityGroups = GetComponentsInParent<CanvasGroup>();
        foreach (var system in beams)
            system.Play(false);
        foreach (var system in motes)
            system.Play(false);
        if (rarity >= 4)
        {
            foreach (var system in accents)
                system.Play(false);
        }
        pulsePhase = beams[0].randomSeed / (float)uint.MaxValue * Mathf.PI * 2f;
        playing = true;
        SynchronizeRendering();
    }

    internal void StopAndClear()
    {
        playing = false;
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

        // Native particle renderers do not inherit CanvasGroup alpha.
        float pulse = 1f + 0.1f * Mathf.Sin(Time.unscaledTime * 2f + pulsePhase);
        properties.SetColor(ColorId, new Color(pulse, pulse, pulse, alpha));
        foreach (var renderer in beamRenderers)
        {
            renderer.sortingLayerID = layer;
            renderer.sortingOrder = order + 1;
            renderer.SetPropertyBlock(properties);
        }
        properties.SetColor(ColorId, new Color(1, 1, 1, alpha));
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
