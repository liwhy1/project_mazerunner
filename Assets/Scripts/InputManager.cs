using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance;

    [Header("Input Data")]
    private InputSystem inputSystem;
    public Vector3 pointerPosition;
    private InputAction pauseAction;
    private InputAction mapAction;
    public InputAction moveAction;
    public InputAction lookAction;
    public InputAction sprintAction;
    public InputAction jumpAction;
    public InputAction primaryAction;
    public InputAction interactAction;

    private void Awake()
    {
        Instance = this;

        // set cursor state
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        //setup input
        inputSystem = new InputSystem();
        pauseAction = inputSystem.Player.Pause;
        mapAction = inputSystem.Player.Map;
        moveAction = inputSystem.Player.Move;
        lookAction = inputSystem.Player.Look;
        sprintAction = inputSystem.Player.Sprint;
        jumpAction = inputSystem.Player.Jump;
        primaryAction = inputSystem.Player.Primary;
        interactAction = inputSystem.Player.Interact;

        // subscribe to input events
        pauseAction.performed += context => GameManager.Instance.OnPauseToggle();
        mapAction.performed += context => GameManager.Instance.OnInventoryToggle();
        jumpAction.performed += context => PlayerController.Instance.OnJump();
        primaryAction.performed += context => GameManager.Instance.OnPrimaryAction();
    }

    private void OnEnable() => inputSystem.Enable();
    private void OnDisable() => inputSystem.Disable();

    private void Update()
    {
        // track pointer position
        pointerPosition = Pointer.current.position.ReadValue();
    }

    public bool IsPointerOverUI()
    {
        PointerEventData pointerData = new PointerEventData(EventSystem.current) { position = pointerPosition };

        List<RaycastResult> castResults = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, castResults);

        // ignore shared map objects
        foreach (RaycastResult result in castResults)
        {
            if (result.module.eventCamera == GameManager.Instance.mapCamera) continue;
            return true;
        }

        return false;
    }
}
