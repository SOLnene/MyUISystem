using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Domain.Character;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

[DisallowMultipleComponent]
public sealed class WorldPartyController : MonoBehaviour
{
    [SerializeField] WorldPlayerController player;
    [SerializeField] WorldPlayerInput input;
    [SerializeField] WorldPlayerAnimation locomotion;
    [SerializeField] GameObject initialModel;
    [SerializeField] WorldCharacterAnimationLibrary characterResources;
    [SerializeField] ParticleSystem switchParticlePrefab;
    [SerializeField] CharacterSwitchElementPalette switchElementPalette;

    const float SwitchLeadTime = 0.2f;
    const float SwitchFadeOutTime = 0.06f;
    const float SwitchFadeInTime = 0.15f;

    TeamRepository teams;
    CharacterRepository characters;
    ResourceManager resources;
    GameObject activeModel;
    ParticleSystem switchParticles;
    string initialCharacterKey;
    CancellationTokenSource switchCancellation;
    CancellationTokenSource warmupCancellation;
    readonly Dictionary<string, AsyncOperationHandle<WorldCharacterMotionSet>> motionHandles = new();
    bool reconcilePending, quitting;

    internal event Action ActiveCharacterChanged;
    internal CharacterModel ActiveCharacter => player.Character;
    internal bool IsSwitching { get; private set; }

    internal async UniTask InitializeAsync(TeamRepository teamRepository,
        CharacterRepository characterRepository, CancellationToken token)
    {
        teams = teamRepository;
        characters = characterRepository;
        resources = ResourceManager.Instance;
        activeModel = initialModel;
        initialCharacterKey = player.Character.Definition.key;
        teams.Changed += OnTeamChanged;
        await ReconcileAsync(token);
    }

    internal async UniTask PrepareInitialModelAsync(string characterKey, CancellationToken token)
    {
        var resource = characterResources.Find(characterKey);
        var motion = await LoadMotionAsync(resource, token);
        token.ThrowIfCancellationRequested();
        if (!isActiveAndEnabled) return;
        initialCharacterKey = characterKey;
        activeModel = initialModel;
        locomotion.BindAnimator(initialModel.GetComponent<Animator>(), motion);
    }

    void Update()
    {
        if (teams == null || quitting) return;
        if (reconcilePending && !IsSwitching)
        {
            reconcilePending = false;
            ReconcileAsync(this.GetCancellationTokenOnDestroy()).Forget(Debug.LogException);
        }
        if (input.SwitchSlotPressed >= 0) RequestSwitch(input.SwitchSlotPressed);
    }

    internal void RequestSwitch(int slot)
    {
        // HUD clicks and hardware input share the same permission check, including menu exit frames.
        if (teams == null || quitting || IsSwitching || !input.CanAcceptGameplay) return;
        var character = GetAvailableMember(slot);
        if (character == null || character == ActiveCharacter) return;
        SwitchAsync(slot, character, true, this.GetCancellationTokenOnDestroy()).Forget(Debug.LogException);
    }

    CharacterModel GetAvailableMember(int slot)
    {
        if (!teams.TryGetGameMemberKey(slot, out string key)) return null;
        var character = characters.GetByKey(key);
        return character != null && character.IsAlive && characterResources.Find(key) != null
            ? character : null;
    }

    async UniTask ReconcileAsync(CancellationToken token)
    {
        // Editing an inactive preset must not change possession. Keep the current member when possible.
        for (int i = 0; i < TeamRepository.MemberCapacity; i++)
        {
            var current = GetAvailableMember(i);
            if (current == null || current != ActiveCharacter) continue;
            if (locomotion.Animator == null) await SwitchAsync(i, current, false, token);
            await PrepareTeamResourcesAsync(token);
            return;
        }
        for (int i = 0; i < TeamRepository.MemberCapacity; i++)
        {
            var character = GetAvailableMember(i);
            if (character == null) continue;
            await SwitchAsync(i, character, false, token);
            await PrepareTeamResourcesAsync(token);
            return;
        }
        if (locomotion.Animator == null) await PrepareInitialModelAsync(initialCharacterKey, token);
        await PrepareTeamResourcesAsync(token);
    }

    async UniTask SwitchAsync(int slot, CharacterModel character, bool requireGameplay, CancellationToken token)
    {
        IsSwitching = true;
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        switchCancellation = cancellation;
        GameObject loaded = null;
        Renderer[] outgoing = null, incoming = null;
        ParticleSystem playingEffect = null;
        bool effectFinished = false;
        try
        {
            var definition = characterResources.Find(character.Definition.key);
            bool useInitial = character.Definition.key == initialCharacterKey;
            var prefabLoad = useInitial ? UniTask.FromResult(initialModel) :
                resources.LoadAssetAsync<GameObject>(definition.modelAddress, cancellation.Token);
            var (prefab, motion) = await UniTask.WhenAll(prefabLoad, LoadMotionAsync(definition, cancellation.Token));
            cancellation.Token.ThrowIfCancellationRequested();
            if (prefab == null)
                throw new InvalidOperationException("Playable model could not be loaded: " + definition.modelAddress);
            loaded = useInitial ? initialModel : await resources.InstantiateItemAsync(
                definition.modelAddress, initialModel.transform.parent, false, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            if (loaded == null || !isActiveAndEnabled || GetAvailableMember(slot) != character ||
                (requireGameplay && !input.CanAcceptGameplay)) return;
            // Scene-authored actors need only an Animator; UI preview components are not gameplay dependencies.
            var animator = loaded.GetComponent<Animator>();
            if (animator == null || !animator.isHuman || animator.avatar == null || !animator.avatar.isValid)
                throw new InvalidOperationException("Playable character requires a valid Humanoid Avatar: " + character.Definition.key);

            if (!useInitial)
            {
                loaded.transform.localPosition = definition.localPosition;
                loaded.transform.localRotation = Quaternion.Euler(definition.localEulerAngles);
                loaded.transform.localScale = definition.localScale;
                foreach (Transform child in loaded.GetComponentsInChildren<Transform>(true))
                    child.gameObject.layer = gameObject.layer;
            }
            var previous = activeModel;
            if (requireGameplay)
            {
                if (switchParticles == null)
                {
                    // The effect follows the player root and can outlive either visual model.
                    switchParticles = Instantiate(switchParticlePrefab, transform);
                    switchParticles.transform.localPosition = Vector3.up * 0.9f;
                    foreach (Transform child in switchParticles.GetComponentsInChildren<Transform>(true))
                        child.gameObject.layer = gameObject.layer;
                }
                playingEffect = switchParticles;
                CharacterSwitchParticlePlayback.Play(playingEffect,
                    switchElementPalette.Resolve(character.Definition.element));
                // Keep the old character visible through the lead-in, then fade just before the handoff.
                await UniTask.Delay(TimeSpan.FromSeconds(SwitchLeadTime - SwitchFadeOutTime),
                    cancellationToken: cancellation.Token);
                if (!input.CanAcceptGameplay) return;
                outgoing = previous.GetComponentsInChildren<Renderer>(true);
                await CharacterSwitchEffect.FadeAsync(outgoing, 1f, 0f, SwitchFadeOutTime, cancellation.Token);
                if (!input.CanAcceptGameplay || GetAvailableMember(slot) != character) return;
                incoming = loaded.GetComponentsInChildren<Renderer>(true);
                CharacterSwitchEffect.SetFade(incoming, 0f);
            }
            // Commit data and visuals together only after loading and validation have both succeeded.
            loaded.SetActive(true);
            locomotion.BindAnimator(animator, motion);
            player.BindCharacter(character);
            activeModel = loaded;
            loaded = null;
            CharacterSwitchEffect.SetFade(outgoing, 1f);
            outgoing = null;
            if (previous != activeModel) ReleaseModel(previous, false);
            ActiveCharacterChanged?.Invoke();
            if (incoming != null)
                await CharacterSwitchEffect.FadeAsync(incoming, 0f, 1f, SwitchFadeInTime, cancellation.Token);
            effectFinished = true;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception error)
        {
            if (!requireGameplay && locomotion.Animator == null) throw;
            Debug.LogException(error, this);
        }
        finally
        {
            // Cancellation before the handoff restores the old model. After it, finish on the new model.
            CharacterSwitchEffect.SetFade(outgoing, 1f);
            CharacterSwitchEffect.SetFade(incoming, 1f);
            if (!effectFinished && playingEffect != null)
                playingEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (loaded != null && loaded != activeModel)
                ReleaseModel(loaded, this == null || !isActiveAndEnabled || quitting);
            if (switchCancellation == cancellation) switchCancellation = null;
            cancellation.Dispose();
            IsSwitching = false;
        }
    }

    async UniTask<WorldCharacterMotionSet> LoadMotionAsync(WorldCharacterResource resource, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (resource == null) throw new InvalidOperationException("Playable character motion is missing from the index.");
        string address = resource.motionAddress;
        // Party ownership lets team edits release one motion bundle without clearing shared UI assets.
        if (!motionHandles.TryGetValue(address, out var handle))
        {
            handle = Addressables.LoadAssetAsync<WorldCharacterMotionSet>(address);
            motionHandles.Add(address, handle);
        }
        try
        {
            var motion = await handle.Task.AsUniTask().AttachExternalCancellation(token);
            if (motion == null || motion.characterKey != resource.characterKey ||
                motion.locomotion.Length != 4 || Array.Exists(motion.locomotion, clip => clip == null) ||
                (motion.locomotionAuxiliary.Length != 0 && (motion.locomotionAuxiliary.Length != 4 ||
                    Array.Exists(motion.locomotionAuxiliary, clip => clip == null))) ||
                Array.Exists(motion.attacks, stage => stage.clip == null) ||
                (motion.special.Length != 0 && motion.special.Length != (int)WorldMotion.Count))
                throw new InvalidOperationException("Invalid playable motion asset: " + address);
            return motion;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch
        {
            if (motionHandles.TryGetValue(address, out var cached) && cached.Equals(handle))
            {
                motionHandles.Remove(address);
                if (handle.IsValid()) Addressables.Release(handle);
            }
            throw;
        }
    }

    async UniTask PrepareTeamResourcesAsync(CancellationToken token)
    {
        if (!isActiveAndEnabled || quitting) return;
        warmupCancellation?.Cancel();
        var members = new List<(WorldCharacterResource resource, string weaponKey)>(TeamRepository.MemberCapacity);
        var retained = new HashSet<string>();
        var active = characterResources.Find(ActiveCharacter?.Definition.key);
        if (active != null)
        {
            retained.Add(active.motionAddress);
            members.Add((active, ActiveCharacter.CurrentEquipRP.Value?.Key));
        }
        for (int i = 0; i < TeamRepository.MemberCapacity; i++)
        {
            if (!teams.TryGetGameMemberKey(i, out string key)) continue;
            var character = characters.GetByKey(key);
            var resource = characterResources.Find(key);
            if (character != null && resource != null && retained.Add(resource.motionAddress))
                members.Add((resource, character.CurrentEquipRP.Value?.Key));
        }
        foreach (var address in new List<string>(motionHandles.Keys))
        {
            if (retained.Contains(address)) continue;
            Addressables.Release(motionHandles[address]);
            motionHandles.Remove(address);
        }
        if (members.Count == 0) return;
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token, this.GetCancellationTokenOnDestroy());
        warmupCancellation = cancellation;
        try
        {
            var loadToken = cancellation.Token;
            // Startup awaits reconciliation before opening the HUD. Team edits reuse the same preparation.
            foreach (var (resource, weaponKey) in members)
            {
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, loadToken);
                var modelLoad = resource.characterKey == initialCharacterKey ? UniTask.FromResult(initialModel) :
                    resources.LoadAssetAsync<GameObject>(resource.modelAddress, loadToken);
                var weaponLoad = string.IsNullOrEmpty(weaponKey) ? UniTask.FromResult<GameObject>(null) :
                    resources.LoadAssetAsync<GameObject>(WorldPlayerAnimation.GetWeaponModelAddress(weaponKey), loadToken);
                var (model, _, _) = await UniTask.WhenAll(modelLoad, LoadMotionAsync(resource, loadToken), weaponLoad);
                if (model == null)
                    throw new InvalidOperationException("Playable model could not be loaded: " + resource.modelAddress);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested && !token.IsCancellationRequested) { }
        finally
        {
            if (warmupCancellation == cancellation) warmupCancellation = null;
            cancellation.Dispose();
        }
    }

    void ReleaseModel(GameObject model, bool forceDestroy)
    {
        if (model == null) return;
        model.SetActive(false);
        if (model != initialModel && resources != null) resources.Recycle(model, forceDestroy);
    }

    void OnTeamChanged()
    {
        // A result loaded for an older formation must never overwrite its replacement.
        switchCancellation?.Cancel();
        warmupCancellation?.Cancel();
        reconcilePending = true;
    }

    void OnEnable()
    {
        if (teams == null) return;
        teams.Changed += OnTeamChanged;
        reconcilePending = true;
    }

    void OnApplicationQuit()
    {
        quitting = true;
        switchCancellation?.Cancel();
        warmupCancellation?.Cancel();
    }

    void OnDisable()
    {
        if (teams != null) teams.Changed -= OnTeamChanged;
        switchCancellation?.Cancel();
        warmupCancellation?.Cancel();
        if (switchParticles != null)
            switchParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        locomotion.BindAnimator(null, null);
        // Never reparent world children into the pool while their scene is being destroyed.
        if (activeModel != initialModel) ReleaseModel(activeModel, true);
        foreach (var handle in motionHandles.Values)
            if (handle.IsValid()) Addressables.Release(handle);
        motionHandles.Clear();
        activeModel = initialModel;
        if (initialModel != null) initialModel.SetActive(true);
    }
}
