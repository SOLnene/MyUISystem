using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Domain.Character;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class WorldPartyController : MonoBehaviour
{
    [SerializeField] WorldPlayerController player;
    [SerializeField] WorldPlayerInput input;
    [SerializeField] WorldPlayerAnimation locomotion;
    [SerializeField] GameObject initialModel;
    [SerializeField] ModelPreviewDatabase modelDatabase;
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
        return character != null && character.IsAlive && modelDatabase.Get(ModelPreviewType.Character, key) != null
            ? character : null;
    }

    async UniTask ReconcileAsync(CancellationToken token)
    {
        // Editing an inactive preset must not change possession. Keep the current member when possible.
        for (int i = 0; i < TeamRepository.MemberCapacity; i++)
            if (GetAvailableMember(i) == ActiveCharacter && ActiveCharacter != null) return;
        for (int i = 0; i < TeamRepository.MemberCapacity; i++)
        {
            var character = GetAvailableMember(i);
            if (character == null) continue;
            await SwitchAsync(i, character, false, token);
            return;
        }
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
            var definition = modelDatabase.Get(ModelPreviewType.Character, character.Definition.key);
            bool useInitial = character.Definition.key == initialCharacterKey;
            loaded = useInitial ? initialModel : await resources.InstantiateItemAsync(
                definition.ModelAddress, initialModel.transform.parent, false, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            if (loaded == null || !isActiveAndEnabled || GetAvailableMember(slot) != character ||
                (requireGameplay && !input.CanAcceptGameplay)) return;
            // Scene-authored actors need only an Animator; UI preview components are not gameplay dependencies.
            var animator = loaded.GetComponent<Animator>();
            if (animator == null || !animator.isHuman || animator.avatar == null || !animator.avatar.isValid)
                throw new InvalidOperationException("Playable character requires a valid Humanoid Avatar: " + character.Definition.key);

            if (!useInitial)
            {
                loaded.transform.localPosition = definition.LocalPosition;
                loaded.transform.localRotation = Quaternion.Euler(definition.LocalEulerAngles);
                loaded.transform.localScale = definition.LocalScale;
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
            locomotion.BindAnimator(animator);
            player.BindCharacter(character);
            activeModel = loaded;
            loaded = null;
            CharacterSwitchEffect.SetFade(outgoing, 1f);
            outgoing = null;
            ReleaseModel(previous, false);
            ActiveCharacterChanged?.Invoke();
            if (incoming != null)
                await CharacterSwitchEffect.FadeAsync(incoming, 0f, 1f, SwitchFadeInTime, cancellation.Token);
            effectFinished = true;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception error)
        {
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
    }

    void OnDisable()
    {
        if (teams != null) teams.Changed -= OnTeamChanged;
        switchCancellation?.Cancel();
        if (switchParticles != null)
            switchParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (activeModel == null || activeModel == initialModel) return;
        locomotion.BindAnimator(initialModel.GetComponent<Animator>());
        // Never reparent world children into the pool while their scene is being destroyed.
        ReleaseModel(activeModel, true);
        activeModel = initialModel;
        initialModel.SetActive(true);
    }
}
