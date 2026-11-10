using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Domain.Character;
using UniRx;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

internal enum WorldMotion
{
    None = -1,
    ClimbIdle,
    ClimbUp,
    ClimbDown,
    ClimbLeft,
    ClimbRight,
    Mantle,
    SwimIdle,
    SwimForward,
    PlungeStart,
    PlungeLoop,
    PlungeImpact,
    PlungeRecovery,
    SkillStart,
    SkillStrike,
    SkillRecovery,
    Burst,
    BurstRecovery,
    Count
}

[DisallowMultipleComponent]
public sealed class WorldPlayerAnimation : MonoBehaviour
{
    [SerializeField, Min(.01f)] float blendTime = .12f;

    [Serializable]
    internal struct NormalAttackStage
    {
        public AnimationClip clip;
        [Min(0f)] public float nextActionTime;
        [SerializeField] internal Vector2 swordTrailWindow;
    }

    PlayableGraph graph;
    AnimationMixerPlayable mixer, auxiliaryMixer, outputMixer, attackMixer;
    AnimationLayerMixerPlayable locomotionLayers;
    AnimationMixerPlayable specialMixer;
    AnimationClipPlayable[] specialClips;
    float[] specialWeights;
    float specialWeight;
    AnimationClipPlayable[] attackClips;
    WorldCharacterMotionSet motionSet;
    AnimationClip[] movementSources;
    AnimationClip[] specialSources;
    NormalAttackStage[] attackStages;
    Animator animator;
    CharacterModel character;
    IDisposable equippedWeaponSubscription;
    CancellationTokenSource weaponLoadCancellation;
    GameObject equippedWeapon;
    bool weaponVisible;
    WorldSwordTrail swordTrail;
    int attackIndex = -1;
    int previousAttackIndex = -1;
    float attackWeight, attackStageBlend;
    bool nextAttackQueued, endingAttack;
    AnimationPlayableOutput output;
    RuntimeAnimatorController originalController;
    AnimatorCullingMode originalCulling;
    bool originalRootMotion;
    WorldPlayerRootMotion rootMotion;
    Vector3 pendingAttackDisplacement;
    readonly AnimationClipPlayable[] clips = new AnimationClipPlayable[4];
    readonly AnimationClipPlayable[] auxiliaryClips = new AnimationClipPlayable[4];
    readonly float[] weights = new float[4];
    const float JumpApexTime = .25f;
    const float FallEntryTime = .5f;
    const float FallHoldTime = .7f;
    const float RunContactPhaseOffset = .25f;
    const float NormalAttackComboStartRatio = .2f;

    float gaitPhase, walkStrideDistance, runStrideDistance;
    float jumpPoseTime, jumpLaunchSpeed;
    bool wasGrounded = true;
    bool wasRising;

    internal Animator Animator => animator;
    internal bool SupportsSpecialMotion => specialMixer.IsValid();
    internal WorldMotion SpecialMotion { get; private set; } = WorldMotion.None;
    internal float SpecialMotionProgress => SpecialMotion == WorldMotion.None ? 0f : Mathf.Clamp01(
        (float)specialClips[(int)SpecialMotion].GetTime() / specialSources[(int)SpecialMotion].length);

    void OnEnable()
    {
        // Locomotion owns this scene instance's Animator; UI presentation controllers keep their own graphs.
        gaitPhase = jumpPoseTime = 0f;
        wasGrounded = true;
        wasRising = false;
        Array.Clear(weights, 0, weights.Length);
        weights[0] = 1f;
        if (animator != null) CreateGraph();
        BindEquippedWeapon();
    }

    void CreateGraph()
    {
        movementSources = motionSet.locomotion;
        attackStages = motionSet.attacks;
        specialSources = motionSet.special;
        CaptureAnimator();
        graph = PlayableGraph.Create("WorldPlayerLocomotion");
        mixer = AnimationMixerPlayable.Create(graph, clips.Length);
        locomotionLayers = default;
        specialMixer = default;
        bool hasAuxiliary = motionSet.locomotionAuxiliary.Length != 0;
        for (int i = 0; i < clips.Length; i++)
        {
            clips[i] = AnimationClipPlayable.Create(graph, movementSources[i]);
            clips[i].SetApplyFootIK(false);
            if (i != 0 || hasAuxiliary) clips[i].SetSpeed(0f);
            graph.Connect(clips[i], 0, mixer, i);
            mixer.SetInputWeight(i, weights[i]);
        }
        outputMixer = AnimationMixerPlayable.Create(graph, 3);
        if (!hasAuxiliary)
            graph.Connect(mixer, 0, outputMixer, 0);
        else
            ConnectLocomotionAuxiliary();
        outputMixer.SetInputWeight(0, 1f);
        attackMixer = AnimationMixerPlayable.Create(graph, attackStages.Length);
        graph.Connect(attackMixer, 0, outputMixer, 1);
        attackClips = new AnimationClipPlayable[attackStages.Length];
        for (int i = 0; i < attackClips.Length; i++)
        {
            attackClips[i] = AnimationClipPlayable.Create(graph, attackStages[i].clip);
            attackClips[i].SetSpeed(0f);
            graph.Connect(attackClips[i], 0, attackMixer, i);
        }
        ConnectSpecialMotions();
        output = AnimationPlayableOutput.Create(graph, "Body", animator);
        output.SetSourcePlayable(outputMixer);
        SampleLocomotionPose();
        graph.Play();
    }

    void ConnectLocomotionAuxiliary()
    {
        var locomotionAuxiliaryClips = motionSet.locomotionAuxiliary;
        if (locomotionAuxiliaryClips.Length != clips.Length)
            throw new InvalidOperationException("Locomotion auxiliary clips must match idle, walk, run and jump.");

        auxiliaryMixer = AnimationMixerPlayable.Create(graph, auxiliaryClips.Length);
        for (int i = 0; i < auxiliaryClips.Length; i++)
        {
            auxiliaryClips[i] = AnimationClipPlayable.Create(graph, locomotionAuxiliaryClips[i]);
            // Match the body's IK policy so the accessory layer cannot change the leg pose.
            auxiliaryClips[i].SetApplyFootIK(false);
            auxiliaryClips[i].SetSpeed(0f);
            graph.Connect(auxiliaryClips[i], 0, auxiliaryMixer, i);
            auxiliaryMixer.SetInputWeight(i, weights[i]);
        }

        locomotionLayers = AnimationLayerMixerPlayable.Create(graph, 2);
        graph.Connect(mixer, 0, locomotionLayers, 0);
        graph.Connect(auxiliaryMixer, 0, locomotionLayers, 1);
        locomotionLayers.SetInputWeight(0, 1f);
        // The exported auxiliary tracks contain absolute local transforms.
        locomotionLayers.SetLayerAdditive(1, false);
        locomotionLayers.SetInputWeight(1, 1f);
        graph.Connect(locomotionLayers, 0, outputMixer, 0);
    }

    internal void BindAnimator(Animator target, WorldCharacterMotionSet nextSet)
    {
        if (target == animator && motionSet == nextSet) return;
        UnbindEquippedWeapon();
        weaponVisible = false;
        if (graph.IsValid())
        {
            ResetNormalAttack();
            ResetSpecialMotion();
            output.SetTarget(null);
            RestoreAnimator();
            graph.Destroy();
        }
        animator = target;
        motionSet = nextSet;
        if (target == null || !isActiveAndEnabled)
        {
            movementSources = specialSources = null;
            attackStages = null;
            return;
        }
        // Keep the locomotion phase while replacing the outgoing character's action clips.
        CreateGraph();
        graph.Evaluate(0f);
        BindEquippedWeapon();
    }

    internal void BindCharacter(CharacterModel nextCharacter)
    {
        if (character == nextCharacter) return;
        UnbindEquippedWeapon();
        character = nextCharacter;
        BindEquippedWeapon();
    }

    void CaptureAnimator()
    {
        // The controller supplies movement, so match the gait cadence to the clip's authored stride.
        float modelScale = animator.humanScale * Mathf.Abs(animator.transform.lossyScale.y);
        walkStrideDistance = Vector3.ProjectOnPlane(movementSources[1].averageSpeed, Vector3.up).magnitude * movementSources[1].length * modelScale;
        runStrideDistance = Vector3.ProjectOnPlane(movementSources[2].averageSpeed, Vector3.up).magnitude * movementSources[2].length * modelScale;
        originalController = animator.runtimeAnimatorController;
        originalCulling = animator.cullingMode;
        originalRootMotion = animator.applyRootMotion;
        animator.runtimeAnimatorController = null;
        if (attackStages.Length != 0 && motionSet.attackRootMotion)
        {
            if (!animator.TryGetComponent(out rootMotion))
                rootMotion = animator.gameObject.AddComponent<WorldPlayerRootMotion>();
            rootMotion.Owner = this;
        }
        animator.applyRootMotion = rootMotion != null;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
    }

    void RestoreAnimator()
    {
        // A pooled presentation actor must return with its original Animator ownership and pose.
        if (rootMotion != null) rootMotion.Owner = null;
        rootMotion = null;
        pendingAttackDisplacement = Vector3.zero;
        if (animator == null) return;
        animator.runtimeAnimatorController = originalController;
        animator.applyRootMotion = originalRootMotion;
        animator.cullingMode = originalCulling;
        animator.Rebind();
    }

    internal void CaptureAttackDisplacement(Vector3 delta)
    {
        if (attackIndex < 0 || endingAttack) return;
        // The graph evaluates after Update. Keep its world-space delta for the next movement sweep.
        pendingAttackDisplacement += new Vector3(delta.x, 0f, delta.z);
    }

    internal Vector3 ConsumeAttackDisplacement()
    {
        Vector3 displacement = pendingAttackDisplacement;
        pendingAttackDisplacement = Vector3.zero;
        return displacement;
    }

    internal void UpdateLocomotion(bool grounded, float speed, float verticalSpeed, float walkSpeed, float runSpeed)
    {
        if (!graph.IsValid()) return;
        float deltaTime = Time.deltaTime;
        bool rising = !grounded && verticalSpeed > 0f;
        if (!grounded)
        {
            if (rising)
            {
                if (!wasRising) jumpLaunchSpeed = verticalSpeed;
                jumpPoseTime = JumpApexTime * Mathf.InverseLerp(jumpLaunchSpeed, 0f, verticalSpeed);
            }
            else
            {
                // Ledges enter the falling pose directly. Long falls hold it until physical contact.
                jumpPoseTime = wasGrounded ? FallEntryTime : Mathf.MoveTowards(jumpPoseTime, FallHoldTime, deltaTime);
            }
        }

        float walk = Mathf.Clamp01(speed / Mathf.Max(.01f, walkSpeed));
        float run = Mathf.InverseLerp(walkSpeed, Mathf.Max(walkSpeed + .01f, runSpeed), speed);
        float strideDistance = Mathf.Lerp(walkStrideDistance, runStrideDistance, run);
        gaitPhase = Mathf.Repeat(gaitPhase + speed * deltaTime / Mathf.Max(.01f, strideDistance), 1f);
        SampleLocomotionPose();
        float step = deltaTime / blendTime;
        float sum = 0f;
        for (int i = 0; i < clips.Length; i++)
        {
            float target = grounded ? (i == 0 ? 1f - walk : i == 1 ? walk * (1f - run) : i == 2 ? run : 0f)
                : (i == 3 ? 1f : 0f);
            weights[i] = Mathf.MoveTowards(weights[i], target, step);
            sum += weights[i];
        }
        bool hasAuxiliary = locomotionLayers.IsValid();
        // Sample both channels from the same clock, including the idle loop boundary.
        if (hasAuxiliary)
            clips[0].SetTime(Mathf.Repeat((float)clips[0].GetTime() + deltaTime, movementSources[0].length));
        for (int i = 0; i < clips.Length; i++)
        {
            float weight = weights[i] / Mathf.Max(.001f, sum);
            mixer.SetInputWeight(i, weight);
            if (hasAuxiliary)
            {
                auxiliaryClips[i].SetTime(clips[i].GetTime());
                auxiliaryMixer.SetInputWeight(i, weight);
            }
        }
        wasGrounded = grounded;
        wasRising = rising;
        UpdateSpecialBlend(deltaTime);
    }

    void ConnectSpecialMotions()
    {
        if (specialSources.Length == 0) return;
        if (specialSources.Length != (int)WorldMotion.Count)
            throw new InvalidOperationException("Special motion clips must match WorldMotion.");

        specialMixer = AnimationMixerPlayable.Create(graph, specialSources.Length);
        specialClips = new AnimationClipPlayable[specialSources.Length];
        specialWeights = new float[specialClips.Length];
        for (int i = 0; i < specialClips.Length; i++)
        {
            if (specialSources[i] == null) continue;
            specialClips[i] = AnimationClipPlayable.Create(graph, specialSources[i]);
            specialClips[i].SetApplyFootIK(false);
            specialClips[i].SetSpeed(0f);
            graph.Connect(specialClips[i], 0, specialMixer, i);
        }
        graph.Connect(specialMixer, 0, outputMixer, 2);
    }

    void SampleLocomotionPose()
    {
        clips[1].SetTime(gaitPhase * movementSources[1].length);
        // The run clip plants the same foot a quarter cycle later than the walk clip.
        clips[2].SetTime(Mathf.Repeat(gaitPhase + RunContactPhaseOffset, 1f) * movementSources[2].length);
        clips[3].SetTime(jumpPoseTime);
    }

    internal bool HasSpecialMotion(WorldMotion motion) => SupportsSpecialMotion && motion != WorldMotion.None &&
        specialClips[(int)motion].IsValid();

    internal void PlaySpecialMotion(WorldMotion motion)
    {
        if (motion == SpecialMotion || (motion != WorldMotion.None && !HasSpecialMotion(motion))) return;
        SpecialMotion = motion;
        if (motion == WorldMotion.None) return;
        ResetNormalAttack();
        SetWeaponVisible((motion >= WorldMotion.PlungeStart && motion <= WorldMotion.PlungeRecovery) ||
            motion == WorldMotion.Burst || motion == WorldMotion.BurstRecovery);
        var clip = specialClips[(int)motion];
        clip.SetTime(0d);
        clip.SetDone(false);
        clip.SetSpeed(1f);
    }

    void UpdateSpecialBlend(float deltaTime)
    {
        if (!specialMixer.IsValid() || (SpecialMotion == WorldMotion.None && specialWeight == 0f)) return;
        float step = deltaTime / blendTime;
        specialWeight = Mathf.MoveTowards(specialWeight, SpecialMotion == WorldMotion.None ? 0f : 1f, step);
        if (specialWeight == 0f)
        {
            ResetSpecialMotion();
            return;
        }
        if (SpecialMotion != WorldMotion.None)
        {
            float sum = 0f;
            for (int i = 0; i < specialClips.Length; i++)
            {
                if (!specialClips[i].IsValid()) continue;
                specialWeights[i] = Mathf.MoveTowards(specialWeights[i], i == (int)SpecialMotion ? 1f : 0f, step);
                sum += specialWeights[i];
                if (i != (int)SpecialMotion && specialWeights[i] == 0f) specialClips[i].SetSpeed(0f);
            }
            for (int i = 0; i < specialClips.Length; i++)
                specialMixer.SetInputWeight(i, specialWeights[i] / Mathf.Max(.001f, sum));
        }
        ApplyOutputWeights();
    }

    void ResetSpecialMotion()
    {
        if (!specialMixer.IsValid()) return;
        SpecialMotion = WorldMotion.None;
        specialWeight = 0f;
        if (attackIndex < 0) SetWeaponVisible(false);
        for (int i = 0; i < specialClips.Length; i++)
        {
            if (specialClips[i].IsValid()) specialClips[i].SetSpeed(0f);
            specialWeights[i] = 0f;
            specialMixer.SetInputWeight(i, 0f);
        }
        ApplyOutputWeights();
    }

    void ApplyOutputWeights()
    {
        float remaining = 1f - specialWeight;
        outputMixer.SetInputWeight(0, remaining * (1f - attackWeight));
        outputMixer.SetInputWeight(1, remaining * attackWeight);
        outputMixer.SetInputWeight(2, specialWeight);
    }

    internal bool UpdateNormalAttack(bool requested, bool wantsMovement, bool grounded, bool gameplayEnabled)
    {
        if (!graph.IsValid() || attackClips.Length == 0) return false;
        if (attackIndex < 0)
        {
            if (!requested || !grounded || !gameplayEnabled) return false;
            PlayAttackStage(0);
        }
        else if (!endingAttack)
        {
            if (!grounded || !gameplayEnabled)
            {
                endingAttack = true;
            }
            else
            {
                // Buffer one click for the shared combo window. Movement uses the stage's cancel timing.
                nextAttackQueued |= requested;
                double time = attackClips[attackIndex].GetTime();
                var stage = attackStages[attackIndex];
                // A buffered restart begins a new combo after the finisher plays out.
                float comboTime = attackIndex == attackClips.Length - 1 ? stage.clip.length :
                    stage.clip.length * NormalAttackComboStartRatio;
                if (nextAttackQueued && time >= comboTime)
                    PlayAttackStage((attackIndex + 1) % attackClips.Length);
                else if (time >= stage.clip.length || (wantsMovement && time >= stage.nextActionTime))
                    endingAttack = true;
            }
        }

        float step = Time.deltaTime / blendTime;
        attackWeight = Mathf.MoveTowards(attackWeight, endingAttack ? 0f : 1f, step);
        ApplyOutputWeights();
        if (previousAttackIndex >= 0)
        {
            attackStageBlend = Mathf.MoveTowards(attackStageBlend, 1f, step);
            attackMixer.SetInputWeight(attackIndex, attackStageBlend);
            attackMixer.SetInputWeight(previousAttackIndex, 1f - attackStageBlend);
            if (attackStageBlend >= 1f)
            {
                attackClips[previousAttackIndex].SetSpeed(0f);
                previousAttackIndex = -1;
            }
        }
        if (endingAttack && attackWeight <= 0f) ResetNormalAttack();
        return attackIndex >= 0 && !endingAttack;
    }

    void PlayAttackStage(int index)
    {
        if (attackIndex != index)
        {
            previousAttackIndex = attackIndex;
            if (previousAttackIndex >= 0) attackStageBlend = 0f;
            attackIndex = index;
        }
        attackClips[index].SetTime(0d);
        attackClips[index].SetDone(false);
        attackClips[index].SetSpeed(1f);
        attackMixer.SetInputWeight(index, previousAttackIndex >= 0 ? 0f : 1f);
        nextAttackQueued = false;
        SetWeaponVisible(true);
        Vector2 trailWindow = attackStages[index].swordTrailWindow;
        if (trailWindow.y > trailWindow.x) swordTrail?.Clear();
    }

    void LateUpdate()
    {
        float now = Time.time;
        if (attackIndex >= 0 && !endingAttack && equippedWeapon != null && motionSet.swordTrailMaterial != null)
        {
            Vector2 window = attackStages[attackIndex].swordTrailWindow;
            double clipTime = attackClips[attackIndex].GetTime();
            // Sample the evaluated sword pose only during the strike, excluding windup and recovery.
            if (clipTime >= window.x && clipTime < window.y)
            {
                swordTrail ??= new WorldSwordTrail(equippedWeapon.transform, motionSet.swordTrailMaterial);
                swordTrail.SampleBlade(now);
            }
        }
        swordTrail?.Draw(now);
    }

    void BindEquippedWeapon()
    {
        // The Animator and the session character can bind in either order during a handoff.
        if (!isActiveAndEnabled || animator == null || character == null ||
            character.Definition.key != motionSet.characterKey) return;

        Transform socket = animator.transform.Find(motionSet.weaponSocketPath);
        if (socket == null) return;
        // All gameplay catalysts share the prefab's neutral hover pose, even after a pooled rig was animated.
        bool catalyst = character.Definition.weaponType == "Catalyst";
        Transform parent = catalyst ? socket.parent : socket;
        var grip = catalyst ? new Pose(new Vector3(-.5f, .9f, 0f), Quaternion.AngleAxis(90f, Vector3.forward)) :
            new Pose(Vector3.zero, Quaternion.identity);
        equippedWeaponSubscription = character.CurrentEquipRP
            .Select(item => item?.Key)
            .DistinctUntilChanged()
            .Subscribe(key =>
            {
                ReleaseEquippedWeapon();
                if (!string.IsNullOrEmpty(key)) LoadEquippedWeaponAsync(key, parent, grip).Forget(Debug.LogException);
            });
    }

    internal static string GetWeaponModelAddress(string key) => "model/equip/" + key.ToLowerInvariant();

    async UniTask LoadEquippedWeaponAsync(string key, Transform parent, Pose grip)
    {
        using var cancellation = new CancellationTokenSource();
        weaponLoadCancellation = cancellation;
        var resources = ResourceManager.Instance;
        GameObject loaded = null;
        try
        {
            resources.Init();
            loaded = await resources.InstantiateItemAsync(GetWeaponModelAddress(key), parent,
                false, cancellation.Token);
            if (loaded == null || cancellation.IsCancellationRequested || !isActiveAndEnabled) return;

            // UI and gameplay share the pool. Reapply the grip transform and layer for this socket.
            loaded.transform.SetLocalPositionAndRotation(grip.position, grip.rotation);
            loaded.transform.localScale = Vector3.one;
            foreach (Transform child in loaded.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = parent.gameObject.layer;
            equippedWeapon = loaded;
            loaded = null;
            SetWeaponVisible(weaponVisible);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        finally
        {
            if (loaded != null) resources.Recycle(loaded, !isActiveAndEnabled);
            if (weaponLoadCancellation == cancellation) weaponLoadCancellation = null;
        }
    }

    void UnbindEquippedWeapon()
    {
        equippedWeaponSubscription?.Dispose();
        equippedWeaponSubscription = null;
        ReleaseEquippedWeapon();
    }

    void ReleaseEquippedWeapon()
    {
        weaponLoadCancellation?.Cancel();
        swordTrail?.Dispose();
        swordTrail = null;
        if (equippedWeapon == null) return;
        equippedWeapon.SetActive(false);
        ResourceManager.Instance.Recycle(equippedWeapon, !isActiveAndEnabled);
        equippedWeapon = null;
    }

    void SetWeaponVisible(bool visible)
    {
        weaponVisible = visible;
        if (equippedWeapon != null) equippedWeapon.SetActive(visible);
    }

    void ResetNormalAttack()
    {
        if (attackIndex < 0) return;
        SetWeaponVisible(false);
        attackClips[attackIndex].SetSpeed(0f);
        attackMixer.SetInputWeight(attackIndex, 0f);
        if (previousAttackIndex >= 0)
        {
            attackClips[previousAttackIndex].SetSpeed(0f);
            attackMixer.SetInputWeight(previousAttackIndex, 0f);
        }
        attackIndex = previousAttackIndex = -1;
        nextAttackQueued = endingAttack = false;
        attackWeight = 0f;
        ApplyOutputWeights();
    }

    void OnDisable()
    {
        UnbindEquippedWeapon();
        weaponVisible = false;
        if (graph.IsValid())
        {
            ResetNormalAttack();
            ResetSpecialMotion();
            graph.Destroy();
            RestoreAnimator();
        }
    }
}
