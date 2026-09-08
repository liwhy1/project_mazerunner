using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class PlayerController : NetworkBehaviour
{
    public static PlayerController Instance;

    [Header("Player Data")]
    public Rigidbody playerRigidbody;
    public Camera cameraObject;

    [Header("Movement Data")]
    public bool enableMovement;
    public Vector3 moveDirection;
    public float jumpStrength = 5f;
    public float movementSpeed;
    public float walkSpeed = 20f;
    public float sprintSpeed = 30f;
    public bool isGrounded;

    [Header("Camera Data")]
    public bool enableCamera;
    private Vector2 lookVector;
    public float mouseSensitivity = 2f;
    public float verticalLimit = 90f;
    private float accumulatedRotationX;
    private float accumulatedRotationY;
    private Vector2 currentMouseDelta;

    [Header("Interaction Data")]
    public bool enableInteraction;
    public RaycastHit rayHit;
    public float rayLength = 3f;

    private void Start()
    {
        // only run this in offline mode
        if (!GameManager.Instance.isOffline) return;

        Instance = this;
        playerRigidbody = GetComponent<Rigidbody>();
        playerRigidbody.isKinematic = false;
        enableInteraction = true;
        enableMovement = true;
        enableCamera = true;
    }

    public override void OnNetworkSpawn()
    {
        // setup client player
        if (!IsOwner)
        {
            cameraObject.gameObject.SetActive(false);
            Destroy(this);
            return;
        }

        // setup local player
        Instance = this;
        playerRigidbody = GetComponent<Rigidbody>();
        enableInteraction = true;
        enableMovement = true;
        enableCamera = true;
        accumulatedRotationX = cameraObject.transform.localEulerAngles.x;
        accumulatedRotationY = transform.localEulerAngles.y;
    }


    void Update()
    {
        // handle raycasting
        RaycastHandler();

        // handle camera
        CameraHandler();
    }

    private void FixedUpdate()
    {
        // handle movement
        MovementHandler();

        // check for ground
        GroundCheckHandler();
    }

    private void RaycastHandler()
    {
        if (!enableInteraction || GameManager.Instance.isPaused) 
        {
            rayHit = new RaycastHit();                
            return;
        }

        // this is the ray that is created from the cameras center
        Ray ray = cameraObject.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));

        // fire ray
        Physics.Raycast(ray, out rayHit, rayLength, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        Debug.DrawRay(ray.origin, ray.direction * rayLength, Color.cyan);
    }

    private void GroundCheckHandler()
    {
        CapsuleCollider playerCollider = GetComponent<CapsuleCollider>();
        Vector3 checkCenter = new Vector3(playerCollider.bounds.center.x, playerCollider.bounds.min.y - 0.02f, playerCollider.bounds.center.z);

        Collider[] hits = Physics.OverlapBox(checkCenter, new Vector3(0.4f, 0.05f, 0.4f), Quaternion.identity, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        isGrounded = hits.Any(h => h.transform != transform);
    }

    private void CameraHandler()
    {
        if (!enableCamera || GameManager.Instance.isPaused || MapManager.Instance.isMapActive) 
        {
            lookVector = Vector2.zero;
            return;
        }

        // horizontal and vertical camera movement
        lookVector = InputManager.Instance.lookAction.ReadValue<Vector2>();
        currentMouseDelta = new Vector2(lookVector.x, lookVector.y) * mouseSensitivity / 10f;
        accumulatedRotationX -= currentMouseDelta.y;
        accumulatedRotationY += currentMouseDelta.x;

        // clamp vertical rotation
        accumulatedRotationX = Mathf.Clamp(accumulatedRotationX, -verticalLimit, verticalLimit);

        // apply transform
        cameraObject.transform.localRotation = Quaternion.Euler(accumulatedRotationX, 0f, 0f);
        transform.localRotation = Quaternion.Euler(0f, accumulatedRotationY, 0f);
    }

    private void MovementHandler()
    {
        if (!enableMovement || GameManager.Instance.isPaused || playerRigidbody.isKinematic || MapManager.Instance.isMapActive) return;

        // checks which direction the player is trying to move reads as a vector2 for x and y;
        moveDirection = InputManager.Instance.moveAction.ReadValue<Vector2>();

        // checks if the player is holding sprint button and changes the speed accordingly
        movementSpeed = InputManager.Instance.sprintAction.ReadValue<float>() == 1 ? sprintSpeed : walkSpeed;

        // apply movement to rigidbody
        Vector3 targetVelocity = (gameObject.transform.forward * moveDirection.y + gameObject.transform.right * moveDirection.x) * movementSpeed * 10f * Time.deltaTime;
        playerRigidbody.linearVelocity = new Vector3(targetVelocity.x, playerRigidbody.linearVelocity.y, targetVelocity.z);
    }

    public void OnJump()
    {
        if (GameManager.Instance.isPaused || !isGrounded || !enableMovement || playerRigidbody.isKinematic || MapManager.Instance.isMapActive) return;

        playerRigidbody.linearVelocity = gameObject.transform.up * jumpStrength;
    }

    public void OnInteract()
    {
        // interact logic here :)
    }
}
