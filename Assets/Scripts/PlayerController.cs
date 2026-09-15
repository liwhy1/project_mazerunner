using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public class PlayerController : NetworkBehaviour
{
    public static PlayerController Instance;

    [Header("Player Data")]
    public Rigidbody playerRigidbody;
    public NavMeshAgent playerAgent;
    public Camera cameraObject;
    public GameObject playerCamera;

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
    public GameObject rayHitObject;
    public float rayLength = 3f;

    private void Start()
    {
        // only run this in offline mode
        if (!GameManager.Instance.isOffline) return;
        OnSetup();
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
        OnSetup();
    }

    private void OnSetup()
    {
        Instance = this;
        playerRigidbody = GetComponent<Rigidbody>();
        playerRigidbody.isKinematic = false;
        playerAgent = GetComponent<NavMeshAgent>();
        enableInteraction = true;
        enableMovement = true;
        enableCamera = false;

        // disable name indicator on own player
        transform.Find("NameCanvas").gameObject.SetActive(false);

        playerCamera = Instantiate(Resources.Load<GameObject>("PlayerCamera"));
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
            rayHitObject = null;                
            return;
        }

        // this is the ray that is created from the cameras center
        Ray ray = cameraObject.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));

        // fire ray
        RaycastHit rayHit;
        if (Physics.Raycast(ray, out rayHit, rayLength, Physics.AllLayers, QueryTriggerInteraction.Ignore))
        {
            rayHitObject = rayHit.collider ? rayHit.collider.gameObject : null;
        }
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
        if (playerCamera != null)
        {
            // follow player
            Vector3 forward = transform.forward;
            forward.y = 0f;

            if (forward.sqrMagnitude > 0.001f)
            {
                forward.Normalize();
            }

            Vector3 targetPosition = transform.position + Vector3.up * 4f - forward * 1.5f;
            playerCamera.transform.position = Vector3.Lerp(playerCamera.transform.position, targetPosition, 5f * Time.deltaTime);


            // look at player
            Vector3 direction = transform.position - playerCamera.transform.position;
            if (direction.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
                playerCamera.transform.rotation = Quaternion.Slerp(playerCamera.transform.rotation, targetRotation, 3f * Time.deltaTime);
            }
        }

        // toggle camera based on shared map view activity
        playerCamera.gameObject.SetActive(!(GameManager.Instance.mapCamera.gameObject.activeSelf && InventoryManager.Instance.isInventoryActive));

        if (!enableCamera || GameManager.Instance.isPaused || InventoryManager.Instance.isInventoryActive) 
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
        if (!enableMovement || GameManager.Instance.isPaused || UIManager.Instance.activeDialog || InventoryManager.Instance.isInventoryActive) 
        {
            if (playerRigidbody.isKinematic) return;
            playerRigidbody.linearVelocity = Vector3.zero;
            playerRigidbody.angularVelocity = Vector3.zero;
            return;
        }

        // store move vector
        moveDirection = InputManager.Instance.moveAction.ReadValue<Vector2>();

        // reset agent
        if (moveDirection == Vector3.zero) return;
        playerRigidbody.isKinematic = false;
        playerAgent.updatePosition = false;
        playerAgent.updateRotation = false;
        playerAgent.isStopped = true;
        playerAgent.ResetPath();

        // apply speed based on sprint state
        movementSpeed = InputManager.Instance.sprintAction.ReadValue<float>() == 1 ? sprintSpeed : walkSpeed;

        // get camera directions
        Vector3 cameraForward = playerCamera.transform.forward;
        Vector3 cameraRight = playerCamera.transform.right;
        cameraForward.y = 0f;
        cameraRight.y = 0f;
        cameraForward.Normalize();
        cameraRight.Normalize();

        // calculate movement direction
        Vector3 movementDirection = cameraForward * moveDirection.y + cameraRight * moveDirection.x;

        // prevent diagonal movement from being faster
        if (movementDirection.sqrMagnitude > 1f)
        {
            movementDirection.Normalize();            
        }

        // apply movement
        Vector3 targetVelocity = movementDirection * movementSpeed / 5f;
        playerRigidbody.linearVelocity = new Vector3(targetVelocity.x, playerRigidbody.linearVelocity.y, targetVelocity.z);
    }

    public void OnMove(Vector3 targetPosition)
    {
        if (GameManager.Instance.isPaused || InventoryManager.Instance.isInventoryActive || UIManager.Instance.activeDialog) return;

        GameObject targetMarker = Instantiate(Resources.Load<GameObject>("TargetMarker"));
        targetMarker.transform.position = targetPosition;

        // reset agent conditionally
        if (!playerRigidbody.isKinematic) playerAgent.Warp(playerRigidbody.position);
        playerRigidbody.isKinematic = true;
        playerAgent.updatePosition = true;
        playerAgent.updateRotation = false;
        playerAgent.isStopped = false;
        playerAgent.SetDestination(targetPosition);
    }

    public void OnJump()
    {
        if (GameManager.Instance.isPaused || !isGrounded || !enableMovement || playerRigidbody.isKinematic || InventoryManager.Instance.isInventoryActive) return;

        playerRigidbody.linearVelocity = gameObject.transform.up * jumpStrength;
    }

    public void OnInteract()
    {
        if (GameManager.Instance.isPaused || rayHitObject == null) return;
        //MapManager.Instance.isMapActive = true;
    }
}
