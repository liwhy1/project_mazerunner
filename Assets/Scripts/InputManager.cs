using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance;
    private InputSystem inputSystem;
    public Vector3 mousePosition;
    private InputAction pauseAction;
    private InputAction mapAction;
    public InputAction moveAction;
    public InputAction lookAction;
    public InputAction sprintAction;
    public InputAction jumpAction;

    private void Awake()
    {
        Instance = this;

        inputSystem = new InputSystem();
        pauseAction = inputSystem.Player.Pause;
        mapAction = inputSystem.Player.Map;
        moveAction = inputSystem.Player.Move;
        lookAction = inputSystem.Player.Look;
        sprintAction = inputSystem.Player.Sprint;
        jumpAction = inputSystem.Player.Jump;

        pauseAction.performed += context => GameManager.Instance.OnPauseToggle();
        mapAction.performed += context => GameManager.Instance.OnMapToggle();
        jumpAction.performed += context => PlayerController.Instance.OnJump();
    }

    private void OnEnable() => inputSystem.Enable();
    private void OnDisable() => inputSystem.Disable();

    public void ToggleCursor()
    {
        try
        {
            if (Cursor.visible)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;            
            }
        }
        catch {}
    }

    private void Update()
    {
        mousePosition = Mouse.current.position.ReadValue();
    }
}
