using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance;
    private InputSystem inputSystem;
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
        mapAction.performed += context => UIManager.Instance.OnMapToggle();
        jumpAction.performed += context => PlayerController.Instance.OnJump();
    }

    private void OnEnable() => inputSystem.Enable();
    private void OnDisable() => inputSystem.Enable();

    public void ToggleCursor()
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

    void Update()
    {
        
    }
}
