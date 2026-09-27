using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 表情控制系统（UI角色预览用）
/// </summary>
public class FaceController : MonoBehaviour
{
    [System.Flags]
    enum BlinkEyes
    {
        None = 0,
        Left = 1,
        Right = 2,
        Both = Left | Right
    }

    const string BlinkLeftExpression = "Eye_WinkB_L";
    const string BlinkRightExpression = "Eye_WinkB_R";
    const float ClosedBlinkShapeWeight = 99f;

    [Header("引用")]
    [SerializeField] private SkinnedMeshRenderer mainFaceRenderer; // 主面部网格（可选，手动拖或自动找）

    [Header("平滑速度")]
    [SerializeField] private float lerpSpeed = 8f;

    // 所有有 BlendShape 的 Renderer
    public  List<SkinnedMeshRenderer> faceRenderers = new();

    // 核心数据结构：表情名 → (Renderer, BlendShapeIndex, 当前值, 目标值)
    public Dictionary<string, List<BlendTarget>> expressionMap = new();

    //是否需要眨眼
    BlinkEyes allowedBlinkEyes;
    //眨眼计时器
    float blinkTimer;
    float nextBlinkInterval;
    Tween blinkTween;
    BlinkEyes proceduralBlinkEyes;
    float blinkLeftRestoreWeight;
    float blinkRightRestoreWeight;
    readonly List<FaceExpressionPreset.CurveData> activeCurves = new();
    bool isPlayingCurvePreset;
    BlinkEyes allowedBlinkEyesAfterCurve;
    float curveElapsedTime;
    float curveDuration;
    float curveIntensity;
    double curveStartedAt;
    double faceBlendStartedAt;
    float faceBlendDuration;
    readonly Dictionary<string, float> activeWeights = new();
    FaceBlendSource blendSource;
    bool presentationFacePlaybackEnabled = true;
    internal bool IsPresentationFacePlaybackEnabled => presentationFacePlaybackEnabled;

    sealed class FaceBlendSource
    {
        internal readonly Dictionary<string, float> Weights = new();
        internal readonly List<FaceExpressionPreset.CurveData> Curves = new();
        internal double StartedAt;
        internal float Duration;
        internal float Intensity;

        internal void Sample(double now)
        {
            float time = Mathf.Clamp((float)(now - StartedAt), 0f, Duration);
            foreach (var curve in Curves)
                Weights[curve.blendShapeName] = EvaluateFaceCurve(curve, time) * Intensity;
        }
    }

    [Header("眨眼参数")]
    [SerializeField, Tooltip("两次眨眼之间的最小间隔（秒）")]
    private float minBlinkInterval = 2.5f;

    [SerializeField, Tooltip("两次眨眼之间的最大间隔（秒）")]
    private float maxBlinkInterval = 7.0f;

    [SerializeField, Tooltip("闭眼保持的最小时间（秒）")]
    private float minCloseDuration = 0.12f;

    [SerializeField, Tooltip("闭眼保持的最大时间（秒）")]
    private float maxCloseDuration = 0.22f;
    
    /// <summary>
    ///  BlendShape 目标数据结构，包含对应 Renderer、BlendShape 索引、当前权重和目标权重
    /// </summary>
    public  class BlendTarget
    {
        public SkinnedMeshRenderer renderer;
        public int index;
        public float currentWeight;
        public float targetWeight;
    }

    void Awake()
    {
        CollectFaceRenderers();
        CacheAllExpressions();
    }

    public void Bind(SkinnedMeshRenderer[] renderers)
    {
        ResetAll();
        StopBlink();
        faceRenderers.Clear();

        foreach (SkinnedMeshRenderer renderer in renderers)
        {
            if (renderer != null
                && renderer.sharedMesh != null
                && renderer.sharedMesh.blendShapeCount > 0
                && !faceRenderers.Contains(renderer))
            {
                faceRenderers.Add(renderer);
            }
        }

        CacheAllExpressions();
        presentationFacePlaybackEnabled = true;
    }

    public void Unbind()
    {
        ResetAll();
        StopBlink();
        faceRenderers.Clear();
        expressionMap.Clear();
        presentationFacePlaybackEnabled = true;
    }

    internal void SetPresentationFacePlayback(bool enabled)
    {
        if (presentationFacePlaybackEnabled == enabled)
        {
            if (!enabled) ResetAll();
            return;
        }
        StopBlink();
        ResetAll();
        allowedBlinkEyes = BlinkEyes.None;
        presentationFacePlaybackEnabled = enabled;
    }

    void LateUpdate()
    {
        if (!presentationFacePlaybackEnabled) return;
        // Animator may also target facial renderers during a body crossfade; presentation ownership wins before rendering.
        if (isPlayingCurvePreset)
        {
            UpdateCurvePreset();
        }
        else
        {
            UpdateAllBlendShapes();
        }

        if (!isPlayingCurvePreset)
        {
            SetBlink();
        }
    }

    // 1. 收集所有可能有 BlendShape 的面部 Renderer
    private void CollectFaceRenderers()
    {
        faceRenderers.Clear();

        // 如果手动指定了主网格，优先用它
        if (mainFaceRenderer != null && mainFaceRenderer.sharedMesh.blendShapeCount > 0)
        {
            faceRenderers.Add(mainFaceRenderer);
        }

        // 自动收集子物体中所有带 BlendShape 的
        var all = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (var r in all)
        {
            if (r.sharedMesh != null && r.sharedMesh.blendShapeCount > 0)
            {
                if (!faceRenderers.Contains(r))
                    faceRenderers.Add(r);
            }
        }
    }

    // 2. 为每个 Renderer 独立缓存所有 BlendShape 名字 → 索引
    private void CacheAllExpressions()
    {
        expressionMap.Clear();

        foreach (var renderer in faceRenderers)
        {
            var mesh = renderer.sharedMesh;
            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                renderer.SetBlendShapeWeight(i, 0f); // 初始化为0
                string name = mesh.GetBlendShapeName(i);
                if (!expressionMap.TryGetValue(name, out var list))
                {
                    list = new List<BlendTarget>();
                    expressionMap[name] = list;
                }

                list.Add(new BlendTarget
                {
                    renderer = renderer,
                    index = i,
                    currentWeight = 0f,
                    targetWeight = 0f
                });
            }
        }
        
    }

    // 3. 每帧平滑更新所有目标权重
    private void UpdateAllBlendShapes()
    {
        foreach (var kv in expressionMap)
        {
            var targets = kv.Value;
            foreach (var target in targets)
            {
                // 平滑插值
                float newWeight = Mathf.Lerp(target.currentWeight, target.targetWeight, Time.deltaTime * lerpSpeed);

                // 更新当前值
                target.currentWeight = newWeight;

                // 应用到对应 Renderer
                target.renderer.SetBlendShapeWeight(target.index, newWeight);
            }
        }
    }

    // 4. 对外接口：设置某个表情的强度（支持多 Renderer 同名）
    public void SetExpression(string expressionName, float weight = 1)
    {
        if (!expressionMap.TryGetValue(expressionName, out var targets))
        {
            Debug.LogWarning($"未找到表情: {expressionName}");
            return;
        }

        foreach (var t in targets)
        {
            t.targetWeight = weight * 100f;
        }
    }

    // 5. 清空所有表情
    public void ResetAll()
    {
        StopCurvePreset();

        foreach (var targets in expressionMap.Values)
        {
            foreach (var t in targets)
            {
                t.currentWeight = 0f;
                t.targetWeight = 0f;
                // 应用到对应 Renderer
                t.renderer.SetBlendShapeWeight(t.index, 0);
            }
        }
    }
    
    /// <summary>
    /// 应用一个表情预设（ScriptableObject）
    /// </summary>
    public void ApplyFacePreset(FaceExpressionPreset preset, float intensity = 1f)
    {
        if (preset == null)
        {
            Debug.LogWarning("表情预设为空或无 Blend 数据");
            return;
        }

        switch (preset.playbackMode)
        {
            case FacePresetPlaybackMode.CurveAnimation:
                ApplyCurvePreset(preset, intensity);
                break;
            default:
                ApplyStaticPreset(preset, intensity);
                break;
        }
    }

    void ApplyStaticPreset(FaceExpressionPreset preset, float intensity)
    {
        if (preset.blends == null || preset.blends.Count == 0)
        {
            Debug.LogWarning("表情预设为空或无 Blend 数据");
            return;
        }

        StopBlink();
        ResetAll();
        allowedBlinkEyes = GetAllowedBlinkEyes(preset, null);

        foreach (var blend in preset.blends)
        {
            SetExpression(blend.blendShapeName, blend.weight * intensity);
        }
    }

    void ApplyCurvePreset(FaceExpressionPreset preset, float intensity)
    {
        if (preset.curves == null || preset.curves.Count == 0)
        {
            Debug.LogWarning("曲线表情预设没有曲线数据");
            return;
        }

        ApplyCurvePresets(preset, null, intensity);
    }

    public void ApplyFacePresets(
        FaceExpressionPreset firstPreset,
        FaceExpressionPreset secondPreset,
        float intensity = 1f)
    {
        if (firstPreset == null || secondPreset == null)
        {
            Debug.LogWarning("组合表情需要两个有效的表情预设");
            return;
        }

        if (firstPreset.playbackMode != FacePresetPlaybackMode.CurveAnimation
            || secondPreset.playbackMode != FacePresetPlaybackMode.CurveAnimation)
        {
            Debug.LogWarning("组合表情当前只支持曲线表情预设");
            return;
        }

        if (firstPreset.curves == null || firstPreset.curves.Count == 0
            || secondPreset.curves == null || secondPreset.curves.Count == 0)
        {
            Debug.LogWarning("组合表情包含没有曲线数据的预设");
            return;
        }

        if ((firstPreset.regions & secondPreset.regions) != FaceRegion.None)
        {
            Debug.LogWarning("组合表情的区域重叠，无法确定同一 BlendShape 的写入顺序");
            return;
        }

        ApplyCurvePresets(firstPreset, secondPreset, intensity);
    }

    void ApplyCurvePresets(
        FaceExpressionPreset firstPreset,
        FaceExpressionPreset secondPreset,
        float intensity,
        float startTime = 0f,
        float blendDuration = 0f)
    {
        BeginFaceTransition(blendDuration);

        int unresolvedCount = 0;
        int missingBlendShapeCount = 0;
        // 组合播放必须只重置一次，再收集两个区域的曲线，否则后一个预设会清掉前一个。
        if (firstPreset != null)
            AddPresetCurves(firstPreset, ref unresolvedCount, ref missingBlendShapeCount);
        if (secondPreset != null)
        {
            AddPresetCurves(secondPreset, ref unresolvedCount, ref missingBlendShapeCount);
        }

        if (unresolvedCount > 0 || missingBlendShapeCount > 0)
        {
            Debug.LogWarning(
                $"表情曲线存在未应用通道：未解析 {unresolvedCount}，模型缺少 BlendShape {missingBlendShapeCount}");
        }

        if (activeCurves.Count == 0 && firstPreset != null
            && firstPreset.playbackMode == FacePresetPlaybackMode.CurveAnimation
            && (secondPreset == null || secondPreset.playbackMode == FacePresetPlaybackMode.CurveAnimation))
        {
            Debug.LogWarning("曲线表情预设没有可应用到当前模型的通道");
        }

        allowedBlinkEyesAfterCurve = GetAllowedBlinkEyes(firstPreset, secondPreset);
        curveDuration = Mathf.Max(
            firstPreset != null && firstPreset.playbackMode == FacePresetPlaybackMode.CurveAnimation ? firstPreset.duration : 0f,
            secondPreset != null && secondPreset.playbackMode == FacePresetPlaybackMode.CurveAnimation ? secondPreset.duration : 0f);
        if (curveDuration <= 0f)
        {
            curveDuration = GetCurveDuration();
        }

        curveIntensity = intensity;
        curveElapsedTime = Mathf.Clamp(startTime, 0f, curveDuration);
        curveStartedAt = Time.timeAsDouble - curveElapsedTime;
        isPlayingCurvePreset = true;
        ApplyCurveFrame(curveElapsedTime);

        if (curveElapsedTime >= curveDuration && blendSource == null)
        {
            CompleteCurvePreset();
        }
    }

    // 展示阶段共用身体的起播时间与混合时长；保留旧 public 接口供单项测试使用。
    internal void PlayPresentationFace(
        FaceExpressionPreset firstPreset,
        FaceExpressionPreset secondPreset,
        float startTime,
        float blendDuration)
    {
        if (firstPreset != null && secondPreset != null
            && (firstPreset.regions & secondPreset.regions) != FaceRegion.None)
        {
            Debug.LogWarning("Overlapping presentation face regions; transition was not applied.");
            return;
        }

        ApplyCurvePresets(firstPreset, secondPreset, 1f, startTime, blendDuration);
    }

    void BeginFaceTransition(float duration)
    {
        // 身体的交接回调可能早于本组件 Update；先结算已结束的混合，再保留来源曲线。
        if (blendSource != null && Time.timeAsDouble - faceBlendStartedAt >= faceBlendDuration)
            UpdateCurvePreset();

        FaceBlendSource source = null;
        if (duration > 0f)
        {
            source = new FaceBlendSource();
            foreach (var pair in expressionMap)
                source.Weights[pair.Key] = pair.Value[0].currentWeight;

            // 普通交接继续采样旧曲线；混合被强制打断时保留当前合成结果，避免回跳。
            if (blendSource == null && isPlayingCurvePreset)
            {
                source.Curves.AddRange(activeCurves);
                source.StartedAt = curveStartedAt;
                source.Duration = curveDuration;
                source.Intensity = curveIntensity;
            }
        }

        StopBlink();
        StopCurvePreset();
        foreach (string name in expressionMap.Keys)
            activeWeights[name] = 0f;
        blendSource = source;
        faceBlendDuration = Mathf.Max(0f, duration);
        faceBlendStartedAt = Time.timeAsDouble;
    }

    void AddPresetCurves(
        FaceExpressionPreset preset,
        ref int unresolvedCount,
        ref int missingBlendShapeCount)
    {
        if (preset.playbackMode == FacePresetPlaybackMode.StaticBlend)
        {
            if (preset.blends != null)
            {
                foreach (var blend in preset.blends)
                {
                    if (string.IsNullOrEmpty(blend.blendShapeName) || !activeWeights.ContainsKey(blend.blendShapeName))
                        missingBlendShapeCount++;
                    else
                        activeWeights[blend.blendShapeName] = blend.weight * 100f;
                }
            }
            return;
        }

        if (preset.curves == null) return;
        foreach (FaceExpressionPreset.CurveData curve in preset.curves)
        {
            if (curve.bindingType == FaceCurveBindingType.Unresolved)
            {
                unresolvedCount++;
                continue;
            }

            if (curve.bindingType == FaceCurveBindingType.Ignored)
            {
                continue;
            }

            if (string.IsNullOrEmpty(curve.blendShapeName)
                || !expressionMap.ContainsKey(curve.blendShapeName))
            {
                missingBlendShapeCount++;
                continue;
            }

            if (curve.curve == null || curve.curve.length == 0)
            {
                unresolvedCount++;
                continue;
            }
            activeCurves.Add(curve);
        }
    }

    static BlinkEyes GetAllowedBlinkEyes(
        FaceExpressionPreset firstPreset,
        FaceExpressionPreset secondPreset)
    {
        BlinkEyes firstEyes = firstPreset == null ? BlinkEyes.Both : ResolveBlinkEyes(firstPreset);
        BlinkEyes secondEyes = secondPreset == null
            ? BlinkEyes.Both
            : ResolveBlinkEyes(secondPreset);
        return firstEyes & secondEyes;
    }

    static BlinkEyes ResolveBlinkEyes(FaceExpressionPreset preset)
    {
        switch (preset.blinkPolicy)
        {
            case FaceBlinkPolicy.Allow:
                return BlinkEyes.Both;
            case FaceBlinkPolicy.Suppress:
                return BlinkEyes.None;
        }

        BlinkEyes result = BlinkEyes.Both;
        if (GetBlinkShapeWeight(preset, BlinkLeftExpression) >= ClosedBlinkShapeWeight)
        {
            result &= ~BlinkEyes.Left;
        }

        if (GetBlinkShapeWeight(preset, BlinkRightExpression) >= ClosedBlinkShapeWeight)
        {
            result &= ~BlinkEyes.Right;
        }

        return result;
    }

    static float GetBlinkShapeWeight(FaceExpressionPreset preset, string blendShapeName)
    {
        if (preset.playbackMode == FacePresetPlaybackMode.StaticBlend)
        {
            FaceExpressionPreset.BlendData blend = preset.blends?.Find(data =>
                data.blendShapeName == blendShapeName);
            return blend == null ? 0f : blend.weight * 100f;
        }

        if (preset.curves == null)
        {
            return 0f;
        }

        foreach (FaceExpressionPreset.CurveData curve in preset.curves)
        {
            if (curve.blendShapeName != blendShapeName
                || curve.curve == null
                || curve.curve.length == 0)
            {
                continue;
            }

            float finalTime = preset.duration > 0f
                ? preset.duration
                : curve.curve.keys[^1].time;
            return EvaluateFaceCurve(curve, finalTime);
        }

        return 0f;
    }

    void UpdateCurvePreset()
    {
        curveElapsedTime = Mathf.Clamp((float)(Time.timeAsDouble - curveStartedAt), 0f, curveDuration);
        ApplyCurveFrame(curveElapsedTime);

        if (curveElapsedTime >= curveDuration && blendSource == null)
        {
            CompleteCurvePreset();
        }
    }

    void ApplyCurveFrame(float time)
    {
        // DAT 曲线已经描述完整时间变化，直接写入才能避免再次平滑造成相位和幅度偏差。
        foreach (FaceExpressionPreset.CurveData curve in activeCurves)
        {
            activeWeights[curve.blendShapeName] = EvaluateFaceCurve(curve, time) * curveIntensity;
        }

        double now = Time.timeAsDouble;
        float progress = blendSource == null ? 1f
            : Mathf.Clamp01((float)(now - faceBlendStartedAt) / faceBlendDuration);
        blendSource?.Sample(now);
        // 缺失通道的目标为零；只在合成完成后写入，切换中不先清空整张脸。
        foreach (var pair in activeWeights)
        {
            float weight = blendSource == null ? pair.Value
                : Mathf.Lerp(blendSource.Weights[pair.Key], pair.Value, progress);
            SetExpressionImmediate(pair.Key, weight);
        }
        if (progress >= 1f) blendSource = null;
    }

    static float EvaluateFaceCurve(FaceExpressionPreset.CurveData data, float time)
    {
        // 短曲线保持末帧，不能因原曲线的循环外推设置在阶段结束前重新播放。
        Keyframe lastKey = data.curve[data.curve.length - 1];
        return time >= lastKey.time ? lastKey.value : data.curve.Evaluate(time);
    }

    void SetExpressionImmediate(string expressionName, float weight)
    {
        foreach (BlendTarget target in expressionMap[expressionName])
        {
            target.currentWeight = weight;
            target.targetWeight = weight;
            target.renderer.SetBlendShapeWeight(target.index, weight);
        }
    }

    float GetCurveDuration()
    {
        float duration = 0f;
        foreach (FaceExpressionPreset.CurveData curve in activeCurves)
        {
            if (curve.curve.length > 0)
            {
                duration = Mathf.Max(duration, curve.curve.keys[^1].time);
            }
        }

        return duration;
    }

    void CompleteCurvePreset()
    {
        BlinkEyes blinkEyes = allowedBlinkEyesAfterCurve;
        StopCurvePreset();
        allowedBlinkEyes = blinkEyes;
    }

    void StopCurvePreset()
    {
        activeCurves.Clear();
        activeWeights.Clear();
        blendSource = null;
        faceBlendDuration = 0f;
        isPlayingCurvePreset = false;
        allowedBlinkEyesAfterCurve = BlinkEyes.None;
        curveElapsedTime = 0f;
        curveDuration = 0f;
        curveIntensity = 1f;
    }

    public void SetBlink()
    {
        if (expressionMap.Count == 0)
        {
            return;
        }

        if (allowedBlinkEyes == BlinkEyes.None)
        {
            return;
        }

        if (blinkTween != null)
        {
            return;
        }

        BlinkEyes availableEyes = allowedBlinkEyes;
        if (!expressionMap.ContainsKey(BlinkLeftExpression))
        {
            availableEyes &= ~BlinkEyes.Left;
        }

        if (!expressionMap.ContainsKey(BlinkRightExpression))
        {
            availableEyes &= ~BlinkEyes.Right;
        }

        if (availableEyes == BlinkEyes.None)
        {
            return;
        }

        if (nextBlinkInterval <= 0f)
        {
            nextBlinkInterval = Random.Range(minBlinkInterval, maxBlinkInterval);
        }

        blinkTimer += Time.deltaTime;

        if (blinkTimer >= nextBlinkInterval)
        {
            proceduralBlinkEyes = availableEyes;
            if ((proceduralBlinkEyes & BlinkEyes.Left) != 0)
            {
                blinkLeftRestoreWeight = GetExpressionTargetWeight(BlinkLeftExpression);
                SetExpression(BlinkLeftExpression, 1);
            }

            if ((proceduralBlinkEyes & BlinkEyes.Right) != 0)
            {
                blinkRightRestoreWeight = GetExpressionTargetWeight(BlinkRightExpression);
                SetExpression(BlinkRightExpression, 1);
            }

            // 延迟一小段时间后复位（闭眼时间 ≈ 0.08~0.15 秒）
            blinkTween = DOVirtual.DelayedCall(Random.Range(minCloseDuration, maxCloseDuration), () =>
            {
                ResetProceduralBlink();
                blinkTween = null;
                blinkTimer = 0f;
                nextBlinkInterval = Random.Range(minBlinkInterval, maxBlinkInterval);
            });
        }
    }

    void StopBlink()
    {
        blinkTween?.Kill();
        blinkTween = null;
        ResetProceduralBlink();
        blinkTimer = 0f;
        nextBlinkInterval = 0f;
        allowedBlinkEyes = BlinkEyes.None;
    }

    void ResetProceduralBlink()
    {
        if ((proceduralBlinkEyes & BlinkEyes.Left) != 0)
        {
            SetExpression(BlinkLeftExpression, blinkLeftRestoreWeight);
        }

        if ((proceduralBlinkEyes & BlinkEyes.Right) != 0)
        {
            SetExpression(BlinkRightExpression, blinkRightRestoreWeight);
        }

        proceduralBlinkEyes = BlinkEyes.None;
        blinkLeftRestoreWeight = 0f;
        blinkRightRestoreWeight = 0f;
    }

    float GetExpressionTargetWeight(string expressionName)
    {
        return expressionMap[expressionName][0].targetWeight / 100f;
    }

    void OnDestroy()
    {
        StopCurvePreset();
        blinkTween?.Kill();
    }
}
