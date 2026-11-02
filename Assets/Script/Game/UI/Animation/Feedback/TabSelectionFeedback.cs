using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TabSelectionFeedback : MonoBehaviour
{
    [SerializeField] Canvas contentCanvas;
    [SerializeField] ParticleSystem glow;
    [SerializeField] ParticleSystem streak;
    [SerializeField] ParticleSystem sparkles;
    [SerializeField] ParticleSystem driftingMotes;
    [SerializeField] ParticleSystem flowingStreaks;

    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int FlowTimeId = Shader.PropertyToID("_FlowTime");
    readonly ParticleSystem.Particle[] flowParticle = new ParticleSystem.Particle[1];
    ParticleSystem[] systems;
    ParticleSystemRenderer[] renderers;
    CanvasGroup[] visibilityGroups;
    RectTransform tabRect;
    MaterialPropertyBlock properties;
    float previousWidth = -1f;
    [NonSerialized] bool initialized;
    bool selected;

    void Awake() => InitializeFeedback();

    void InitializeFeedback()
    {
        // A reload can retain serializable fields while losing the native property-block cache.
        if (initialized && properties != null)
            return;

        systems = new[] { glow, streak, sparkles, driftingMotes, flowingStreaks };
        renderers = Array.ConvertAll(systems, system => system.GetComponent<ParticleSystemRenderer>());
        tabRect = (RectTransform)transform;
        properties = new MaterialPropertyBlock();
        visibilityGroups = GetComponentsInParent<CanvasGroup>(true);
        initialized = true;
    }

    internal void SetSelected(bool value, bool instant)
    {
        // The parent view may bind the tab before this child's Awake has run.
        InitializeFeedback();
        if (selected == value)
            return;

        selected = value;
        if (!selected)
        {
            StopAndClear();
            return;
        }

        visibilityGroups = GetComponentsInParent<CanvasGroup>(true);
        FitToTabWidth();
        SynchronizeRendering();
        glow.Play(false);
        sparkles.Play(false);
        driftingMotes.Play(false);
        // Seed a sparse field immediately instead of waiting for the continuous emission interval.
        driftingMotes.Emit(4);
        sparkles.Emit(instant ? 1 : 3);
        flowingStreaks.Play(false);
        flowingStreaks.Emit(1);
        if (!instant)
        {
            streak.Play(false);
        }
    }

    void LateUpdate()
    {
        InitializeFeedback();
        // Both foreground canvases must follow the window even when their tab is not selected.
        SynchronizeRendering();
        if (selected)
            FitToTabWidth();
    }

    void FitToTabWidth()
    {
        float width = tabRect.rect.width;
        if (width <= 0f || Mathf.Approximately(width, previousWidth))
            return;

        previousWidth = width;
        float effectWidth = Mathf.Clamp(width - 40f, 80f, 260f);
        float center = 40f + effectWidth * 0.5f;
        glow.transform.localPosition = new Vector3(center, 0f, 0f);
        streak.transform.localPosition = new Vector3(center, 0f, 0f);
        sparkles.transform.localPosition = new Vector3(center, 0f, 0f);
        driftingMotes.transform.localPosition = new Vector3(center, 0f, 0f);
        flowingStreaks.transform.localPosition = new Vector3(center, 0f, 0f);
        var glowMain = glow.main;
        glowMain.startSizeX = effectWidth;
        var streakMain = streak.main;
        streakMain.startSizeX = new ParticleSystem.MinMaxCurve(effectWidth * 0.85f, effectWidth * 1.05f);
        var shape = sparkles.shape;
        shape.scale = new Vector3(effectWidth * 0.88f, 40f, 0f);
        var moteShape = driftingMotes.shape;
        moteShape.scale = new Vector3(effectWidth * 0.9f, 42f, 0f);
        var flowMain = flowingStreaks.main;
        flowMain.startSizeX = effectWidth;
        // Start size only affects new particles; resize the persistent drawing surface as well.
        if (flowingStreaks.GetParticles(flowParticle) > 0)
        {
            flowParticle[0].startSize3D = new Vector3(effectWidth, flowMain.startSizeY.constant, 1f);
            flowingStreaks.SetParticles(flowParticle, 1);
        }
    }

    void SynchronizeRendering()
    {
        // UIView can add its canvas after OnEnable; resolve the current owner instead of caching it early.
        var ownerCanvas = GetComponentInParent<Canvas>();
        if (ownerCanvas == null)
            return;

        contentCanvas.overrideSorting = true;
        contentCanvas.sortingLayerID = ownerCanvas.sortingLayerID;
        contentCanvas.sortingOrder = ownerCanvas.sortingOrder + 2;
        contentCanvas.worldCamera = ownerCanvas.worldCamera;

        float alpha = 1f;
        foreach (var group in visibilityGroups)
        {
            if (!group.enabled)
                continue;
            alpha *= group.alpha;
            if (group.ignoreParentGroups)
                break;
        }

        // Native particles do not inherit CanvasGroup alpha or UI sibling order.
        properties.SetColor(ColorId, new Color(1f, 1f, 1f, alpha));
        properties.SetFloat(FlowTimeId, Time.unscaledTime);
        foreach (var renderer in renderers)
        {
            renderer.sortingLayerID = ownerCanvas.sortingLayerID;
            renderer.sortingOrder = ownerCanvas.sortingOrder + 1;
            renderer.SetPropertyBlock(properties);
        }
    }

    void StopAndClear()
    {
        foreach (var system in systems)
            system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    void OnDisable()
    {
        if (!initialized)
            return;
        selected = false;
        StopAndClear();
        contentCanvas.overrideSorting = false;
    }
}
