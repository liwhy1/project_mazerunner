using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance;

    [Header("Input Data")]
    private InputSystem inputSystem;
    public Vector3 mousePosition;
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
        mapAction.performed += context => GameManager.Instance.OnMapToggle();
        jumpAction.performed += context => PlayerController.Instance.OnJump();
    }

    private void OnEnable() => inputSystem.Enable();
    private void OnDisable() => inputSystem.Disable();

    private void Update()
    {
        // track mouse position
        mousePosition = Mouse.current.position.ReadValue();

        // set cursor state
        // webgl doesn't like this, drop it in a try catch
        try
        {
            if (GameManager.Instance.isPaused || (MapManager.Instance && MapManager.Instance.isMapActive))
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
        catch {}
    }
}
