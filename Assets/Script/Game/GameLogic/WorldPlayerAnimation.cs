using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

[DisallowMultipleComponent]
public sealed class WorldPlayerAnimation : MonoBehaviour
{
    [SerializeField] Animator animator;
    [SerializeField] AnimationClip idleClip;
    [SerializeField] AnimationClip walkClip;
    [SerializeField] AnimationClip runClip;
    [SerializeField] AnimationClip jumpClip;
    [SerializeField, Min(.01f)] float blendTime = .12f;

    PlayableGraph graph;
    AnimationMixerPlayable mixer;
    AnimationPlayableOutput output;
    RuntimeAnimatorController originalController;
    AnimatorCullingMode originalCulling;
    bool originalRootMotion;
    readonly AnimationClipPlayable[] clips = new AnimationClipPlayable[4];
    readonly float[] weights = new float[4];
    bool wasGrounded = true;

    internal Animator Animator => animator;

    void OnEnable()
    {
        // Locomotion owns this scene instance's Animator; UI presentation controllers keep their own graphs.
        CaptureAnimator();
        graph = PlayableGraph.Create("WorldPlayerLocomotion");
        mixer = AnimationMixerPlayable.Create(graph, clips.Length);
        AnimationClip[] sources = { idleClip, walkClip, runClip, jumpClip };
        for (int i = 0; i < clips.Length; i++)
        {
            clips[i] = AnimationClipPlayable.Create(graph, sources[i]);
            clips[i].SetApplyFootIK(false);
            graph.Connect(clips[i], 0, mixer, i);
            weights[i] = i == 0 ? 1f : 0f;
            mixer.SetInputWeight(i, weights[i]);
        }
        output = AnimationPlayableOutput.Create(graph, "Body", animator);
        output.SetSourcePlayable(mixer);
        wasGrounded = true;
        graph.Play();
    }

    internal void BindAnimator(Animator target)
    {
        if (target == animator) return;
        if (!graph.IsValid()) { animator = target; return; }
        output.SetTarget(null);
        RestoreAnimator();
        animator = target;
        CaptureAnimator();
        // Retarget the existing graph instead of restarting locomotion or resetting jump progress.
        output.SetTarget(animator);
        graph.Evaluate(0f);
    }

    void CaptureAnimator()
    {
        originalController = animator.runtimeAnimatorController;
        originalCulling = animator.cullingMode;
        originalRootMotion = animator.applyRootMotion;
        animator.runtimeAnimatorController = null;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
    }

    void RestoreAnimator()
    {
        // A pooled presentation actor must return with its original Animator ownership and pose.
        if (animator == null) return;
        animator.runtimeAnimatorController = originalController;
        animator.applyRootMotion = originalRootMotion;
        animator.cullingMode = originalCulling;
        animator.Rebind();
    }

    internal void UpdateLocomotion(bool grounded, float speed, float verticalSpeed, float walkSpeed, float runSpeed)
    {
        if (!grounded && wasGrounded)
        {
            // Walking off a ledge starts at the descending part of the shared jump clip.
            clips[3].SetTime(verticalSpeed > 0f ? 0f : jumpClip.length * .55f);
            clips[3].SetSpeed(1f);
        }
        if (!grounded && clips[3].GetTime() >= jumpClip.length * .9f)
            clips[3].SetSpeed(0f);
        float walk = Mathf.Clamp01(speed / Mathf.Max(.01f, walkSpeed));
        float run = Mathf.InverseLerp(walkSpeed, Mathf.Max(walkSpeed + .01f, runSpeed), speed);
        float step = Time.deltaTime / blendTime;
        float sum = 0f;
        for (int i = 0; i < clips.Length; i++)
        {
            float target = grounded ? (i == 0 ? 1f - walk : i == 1 ? walk * (1f - run) : i == 2 ? run : 0f)
                : (i == 3 ? 1f : 0f);
            weights[i] = Mathf.MoveTowards(weights[i], target, step);
            sum += weights[i];
        }
        for (int i = 0; i < clips.Length; i++) mixer.SetInputWeight(i, weights[i] / Mathf.Max(.001f, sum));
        clips[1].SetSpeed(Mathf.Clamp(speed / Mathf.Max(.01f, walkSpeed), .5f, 1.25f));
        clips[2].SetSpeed(Mathf.Clamp(speed / Mathf.Max(.01f, runSpeed), .6f, 1.25f));
        wasGrounded = grounded;
    }

    void OnDisable()
    {
        if (graph.IsValid()) graph.Destroy();
        RestoreAnimator();
    }
}
