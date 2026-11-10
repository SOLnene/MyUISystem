using UnityEngine;
using Game.Domain.Character;
using Cysharp.Threading.Tasks;

public sealed class WorldSceneContext : MonoBehaviour
{
    [SerializeField] Camera worldCamera;
    [SerializeField] AudioListener worldListener;
    [SerializeField] WorldPlayerController player;
    [SerializeField] WorldPartyController party;
    [SerializeField] string characterKey = "lumine";

    internal string CharacterKey => characterKey;
    internal CharacterModel Character => player.Character;
    internal WorldPartyController Party => party;
    internal Camera WorldCamera => worldCamera;

    internal void BindCharacter(CharacterModel character) => player.BindCharacter(character);

    void Start()
    {
        // A standalone map is a movement test entry; additive loading is activated by FlowManager instead.
        if (FindObjectOfType<FlowManager>() != null) return;
        Activate();
        party.PrepareInitialModelAsync(characterKey, this.GetCancellationTokenOnDestroy()).Forget(Debug.LogException);
    }

    internal void Activate()
    {
        player.gameObject.SetActive(true);
        worldCamera.gameObject.SetActive(true);
        // Start retains the UI camera. The world camera renders below it and excludes UI geometry.
        worldCamera.depth = -1f;
        worldCamera.cullingMask &= ~LayerMask.GetMask("UI", "ModelDisplay", "Reflection", "Plane", "UIBackgroud");
        worldCamera.enabled = true;
        worldListener.enabled = true;
    }

    internal void Deactivate()
    {
        worldListener.enabled = false;
        worldCamera.enabled = false;
        worldCamera.gameObject.SetActive(false);
        player.gameObject.SetActive(false);
    }
}
