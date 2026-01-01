using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using DG.Tweening;

//TODO:使其在编辑器模式下也能预览动画
[CustomEditor(typeof(UIAnimator))]
public class UIAnimationEditor : Editor
{

    private UIAnimator animator;
    private bool isPreviewing = false;

    private void OnEnable()
    {
        animator = (UIAnimator)target;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        UIAnimator animator = (UIAnimator)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("🎬 Animation Groups", EditorStyles.boldLabel);

        foreach (var group in animator.groups)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField($"Group: {group.groupName}  ({(group.isParallel ? "Parallel" : "Sequence")})");

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("▶ Play"))
            {
                if (Application.isPlaying)
                {
                    animator.PlayGroup(group.groupName);
                    EditorApplication.QueuePlayerLoopUpdate(); // 关键：刷新编辑器
                    SceneView.RepaintAll();    
                }
                else
                {
                    // 编辑器预览模式
                    /*animator.ResetGroup(group);*/
                    animator.PlayGroup(group.groupName);
                    Debug.Log($"预览{group.groupName}");
                }
            }

            if (GUILayout.Button("⏮ Reset"))
            {
                //animator.ResetGroup(group);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }
    }

    void Preview(string groupName = "")
    {
        animator.PlayGroup(groupName, () =>
        {
            isPreviewing = false;
            Debug.Log("Preview finished");
        });
    }
    
    private void PreviewPlayAll()
    {
        if (animator.sequences.Count == 0) return;

        isPreviewing = true;

        // 重置所有 Step 到初始状态
        foreach (var step in animator.sequences)
        {
            ResetStep(step);
        }

        // 播放动画
        animator.PlayGroup("", () =>
        {
            isPreviewing = false;
            Debug.Log("Preview finished");
        });
    }

    private void PreviewReset()
    {
        foreach (var step in animator.sequences)
        {
            ResetStep(step);
        }
    }

    private void StopPreview()
    {
        animator.Kill();
        isPreviewing = false;
        PreviewReset();
    }

    private void ResetStep(UIAnimStep step)
    {
        if (step.Target == null) return;

        // RectTransform
        var rt = step.Target.GetComponent<RectTransform>();
        if (rt != null)
        {
            if (step.AnimateAnchoredPosition) rt.anchoredPosition = step.AnchoredPositionStart;
            if (step.AnimateLocalScale) rt.localScale = step.LocalScaleStart;
            if (step.AnimateRotation) rt.localEulerAngles = step.RotationStart;
        }

        // Image
        var img = step.Target.GetComponent<UnityEngine.UI.Image>();
        if (img != null && step.AnimateColor)
        {
            img.color = step.ColorStart;
        }

        // CanvasGroup
        var cg = step.Target.GetComponent<CanvasGroup>();
        if (cg != null && step.AnimateAlpha)
        {
            cg.alpha = step.AlphaStart;
        }

        // TMP_Text
        var tmp = step.Target.GetComponent<TMPro.TMP_Text>();
        if (tmp != null && step.AnimateTMPColor)
        {
            tmp.color = step.TMPColorStart;
            if (step.AnimateMaxVisibleCharacters) tmp.maxVisibleCharacters = step.MaxVisibleCharactersStart;
        }

        // SetActive
        if (step.Type == UIAnimStep.SequenceType.SetActive)
        {
            step.Target.SetActive(!step.IsActivating);
        }
    }
}
