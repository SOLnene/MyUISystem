using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 表情控制系统（UI角色预览用）
/// </summary>
public class FaceController : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private SkinnedMeshRenderer mainFaceRenderer; // 主面部网格（可选，手动拖或自动找）

    [Header("平滑速度")]
    [SerializeField] private float lerpSpeed = 8f;

    // 所有有 BlendShape 的 Renderer
    public  List<SkinnedMeshRenderer> faceRenderers = new();

    // 核心数据结构：表情名 → (Renderer, BlendShapeIndex, 当前值, 目标值)
    public Dictionary<string, List<BlendTarget>> expressionMap = new();

    //是否需要眨眼
    bool canBlink;
    //眨眼计时器
    float blinkTimer;
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

    void Update()
    {
        UpdateAllBlendShapes();
        SetBlink();
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
        if (preset == null || preset.blends == null || preset.blends.Count == 0)
        {
            Debug.LogWarning("表情预设为空或无 Blend 数据");
            return;
        }
        
        ResetAll();
        
        canBlink = preset.canBlink;
        foreach (var blend in preset.blends)
        {
            SetExpression(blend.blendShapeName, blend.weight * intensity);
        }
    }

    public void SetBlink()
    {
        if (!canBlink)
        {
            //假设C只用来做闭眼动画
            SetExpression("Eye_WinkC_L",0);
            SetExpression("Eye_WinkC_R",0);
            return;
        }

        blinkTimer += Time.deltaTime;

        if (blinkTimer >= Random.Range(minBlinkInterval, maxBlinkInterval))
        {
            SetExpression("Eye_WinkC_L",1);
            SetExpression("Eye_WinkC_R",1);
            // 延迟一小段时间后复位（闭眼时间 ≈ 0.08~0.15 秒）
            DOVirtual.DelayedCall(Random.Range(minCloseDuration, maxCloseDuration), () =>
            {
                if (canBlink)
                {
                    SetExpression("Eye_WinkC_L",0);
                    SetExpression("Eye_WinkC_R",0);
                }
            });
            
            blinkTimer = 0f;
        }
    }
}