using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
public sealed class WorldPlayerInput : MonoBehaviour
{
    [SerializeField] PlayerInput playerInput;

    FlowManager flow;
    Transform uiRoot;
    InputAction move, jump, sprint, look, zoom, orbit;
    readonly InputAction[] switchSlots = new InputAction[TeamRepository.MemberCapacity];

    internal bool GameplayEnabled { get; private set; }
    internal int SwitchSlotPressed { get; private set; } = -1;
    internal bool CanAcceptGameplay => isActiveAndEnabled && playerInput.isActiveAndEnabled &&
        Application.isFocused && (flow == null || flow.IsInWorld) && !HasBlockingView();
    internal Vector2 Move { get; private set; }
    internal Vector2 Look { get; private set; }
    internal float Zoom { get; private set; }
    internal bool SprintHeld { get; private set; }
    internal bool JumpPressed { get; private set; }
    internal bool OrbitHeld { get; private set; }
    internal bool CanControlCamera => GameplayEnabled &&
        (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject());

    void Awake()
    {
        // Standalone map testing has no application flow or UI root.
        flow = FindObjectOfType<FlowManager>();
        var root = GameObject.Find("UIRoot");
        uiRoot = root != null ? root.transform : null;
    }

    void Start()
    {
        // PlayerInput finishes device pairing in OnEnable; read its owned action instance afterwards.
        var actions = playerInput.actions;
        move = actions.FindAction("Gameplay/Move", true);
        jump = actions.FindAction("Gameplay/Jump", true);
        sprint = actions.FindAction("Gameplay/Sprint", true);
        look = actions.FindAction("Gameplay/Look", true);
        zoom = actions.FindAction("Gameplay/Zoom", true);
        orbit = actions.FindAction("Gameplay/Orbit", true);
        for (int i = 0; i < switchSlots.Length; i++)
            switchSlots[i] = actions.FindAction($"Gameplay/SwitchSlot{i + 1}", true);
    }

    void Update()
    {
        bool allowed = CanAcceptGameplay;
        bool reactivating = allowed && !playerInput.inputIsActive;
        if (allowed != playerInput.inputIsActive)
        {
            if (allowed) playerInput.ActivateInput();
            else playerInput.DeactivateInput();
        }
        GameplayEnabled = allowed;
        SwitchSlotPressed = -1;
        if (!allowed)
        {
            ClearInputFrame();
            return;
        }

        Move = move.ReadValue<Vector2>();
        SprintHeld = sprint.IsPressed();
        JumpPressed = jump.WasPressedThisFrame();
        Look = look.ReadValue<Vector2>();
        bool gamepadLook = look.activeControl?.device is Gamepad;
        // Mouse delta is already a per-frame distance; a stick represents an angular rate instead.
        if (gamepadLook) Look *= 60f * Time.deltaTime;
        OrbitHeld = gamepadLook || orbit.IsPressed();
        Zoom = zoom.ReadValue<float>();
        // A key held while closing a menu must not become a new switch request on reactivation.
        if (!reactivating)
            for (int i = 0; i < switchSlots.Length; i++)
                if (switchSlots[i].WasPressedThisFrame()) { SwitchSlotPressed = i; break; }
    }

    bool HasBlockingView()
    {
        if (uiRoot == null) return false;
        // Keep gameplay blocked until the page root closes, including its exit animation.
        for (int i = 0; i < uiRoot.childCount; i++)
        {
            Transform layer = uiRoot.GetChild(i);
            for (int j = 0; j < layer.childCount; j++)
            {
                GameObject page = layer.GetChild(j).gameObject;
                if (page.activeInHierarchy && page.TryGetComponent<UIView>(out var view) &&
                    view.Handle != null && view.Handle.uiType != UIType.HubRoot)
                    return true;
            }
        }
        return false;
    }

    void ClearInputFrame()
    {
        Move = Look = Vector2.zero;
        Zoom = 0f;
        SwitchSlotPressed = -1;
        SprintHeld = JumpPressed = OrbitHeld = false;
    }

    void OnDisable()
    {
        GameplayEnabled = false;
        ClearInputFrame();
        playerInput.DeactivateInput();
    }
}
