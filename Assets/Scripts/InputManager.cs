using UnityEngine;
using UnityEngine.InputSystem;

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

        // set cursor state
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        // webgl doesn't like this, drop it in a try catch
        /*try
        {
            if (GameManager.Instance.isPaused || (InventoryManager.Instance && InventoryManager.Instance.isInventoryActive))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
        catch {}*/
    }
}
