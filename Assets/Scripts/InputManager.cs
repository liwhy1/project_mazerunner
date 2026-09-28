using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System.Collections;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance;

    [Header("Input Data")]
    private InputSystem inputSystem;
    public Vector3 pointerPosition;
    private InputAction pauseAction;
    private InputAction inventoryAction;
    public InputAction moveAction;
    public InputAction lookAction;
    public InputAction sprintAction;
    public InputAction jumpAction;
    public InputAction primaryAction;
    public InputAction interactAction;
    public InputAction scrollAction;
    public InputAction submitNavAction;
    private Coroutine scrollRoutine;

    private void Awake()
    {
        Instance = this;

        // set cursor state
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        //setup input
        inputSystem = new InputSystem();
        pauseAction = inputSystem.Player.Pause;
        inventoryAction = inputSystem.Player.Inventory;
        moveAction = inputSystem.Player.Move;
        lookAction = inputSystem.Player.Look;
        sprintAction = inputSystem.Player.Sprint;
        jumpAction = inputSystem.Player.Jump;
        primaryAction = inputSystem.Player.Primary;
        interactAction = inputSystem.Player.Interact;
        scrollAction = inputSystem.Player.Scroll;
        submitNavAction = inputSystem.UI.SubmitNav;

        // subscribe to input events
        pauseAction.performed += context => OnPauseAction();
        inventoryAction.performed += context => OnInventoryAction();
        jumpAction.performed += context => OnJumpAction();
        primaryAction.performed += context => OnPrimaryAction();
        scrollAction.performed += context => OnScrollAction(scrollAction.ReadValue<float>());
        submitNavAction.performed += context => UIManager.Instance.OnNavigationDown();
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
        PointerEventData pointerData = new PointerEventData(EventSystem.current) { position = Pointer.current.position.ReadValue() };

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

    public void OnPauseAction()
    {
        if (!GameManager.Instance.FetchGameStartState()) return;

        GameManager.Instance.isPaused = !GameManager.Instance.isPaused;
        UIManager.Instance.OnPauseToggle();
    }

    public void OnInventoryAction()
    {
        if (GameManager.Instance.isPaused) return;

        InventoryManager.Instance.OnToggleInventory();
    }

    public void OnJumpAction()
    {
        if (GameManager.Instance.isPaused) return;

        if (PlayerController.Instance) PlayerController.Instance.OnJump();
    }

    public void OnPrimaryAction()
    {
        if (GameManager.Instance.isPaused) return;

        // prevent clicking through ui elements
        if (IsPointerOverUI()) return;

        if (PlayerController.Instance && !InventoryManager.Instance.isInventoryActive)
        {
            Ray ray = PlayerController.Instance.playerCamera.GetComponent<Camera>().ScreenPointToRay(Pointer.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                PlayerController.Instance.OnMove(hit.point);
            }
        }
        else if (EditorManager.Instance && InventoryManager.Instance.isInventoryActive)
        {
            EditorManager.Instance.OnPrimaryAction();
        }
    }

    public void OnScrollStart(float scrollValue)
    {
        if (GameManager.Instance.isPaused) return;

        if (scrollRoutine != null) return;
        scrollRoutine = StartCoroutine(OnAutoScroll(scrollValue));
    }

    private IEnumerator OnAutoScroll(float scrollValue)
    {
        if (GameManager.Instance.isPaused) yield break;

        while(true)
        {
            OnScrollAction(scrollValue);
            yield return new WaitForSeconds(0.1f);            
        }
    } 

    public void OnScrollStop()
    {
        if (GameManager.Instance.isPaused) return;

        StopCoroutine(scrollRoutine);
        scrollRoutine = null;
    }

    public void OnScrollAction(float scrollValue)
    {
        if (GameManager.Instance.isPaused) return;

        float targetValue = scrollValue > 0 ? .25f : scrollValue < 0 ? -.25f : 0;

        if (PlayerController.Instance && !InventoryManager.Instance.isInventoryActive)
        {
            PlayerController.Instance.OnUpdateCameraHeight(targetValue);
        }
        else if (EditorManager.Instance && InventoryManager.Instance.isInventoryActive)
        {
            EditorManager.Instance.OnScrollAction(targetValue);
        }
    }

}
