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
    public GameObject playerCamera;

    [Header("Movement Data")]
    public bool enableMovement;
    public Vector2 moveDirection;
    public float jumpStrength = 5f;
    public float movementSpeed;
    public float walkSpeed = 20f;
    public float sprintSpeed = 30f;
    public bool isGrounded;

    [Header("Camera Data")]
    public bool enableCamera;
    public bool rotateCamera;
    public float mouseSensitivity = 2f;
    public float verticalLimit = 90f;

    [Header("Interaction Data")]
    public bool enableInteraction;
    public GameObject rayHitObject;
    public float rayLength = 3f;

    private void Start()
    {
        // only run this in offline mode
        if (GameManager.Instance.networkState == NetworkState.Online) return;
        OnSetup();
    }

    public override void OnNetworkSpawn()
    {
        // setup client player
        if (!IsOwner)
        {
            playerCamera.gameObject.SetActive(false);
            GetComponent<Renderer>().enabled = false;
            transform.Find("NameCanvas").gameObject.SetActive(false);
            GetComponent<NavMeshAgent>().enabled = false;
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
        playerRigidbody.isKinematic = true;
        playerAgent = GetComponent<NavMeshAgent>();
        enableInteraction = true;
        enableMovement = true;
        enableCamera = true;
        gameObject.name = "Player";

        // disable name indicator on own player
        transform.Find("NameCanvas").gameObject.SetActive(false);

        playerCamera = Instantiate(Resources.Load<GameObject>("PlayerCamera"));
        GameManager.Instance.playerViewCamera.transform.SetParent(transform);
        GameManager.Instance.playerViewCamera.transform.localPosition = Vector3.zero + -Vector3.forward;
        GameManager.Instance.playerViewCamera.transform.LookAt(transform);
    }

    private void FixedUpdate()
    {
        // handle movement
        MovementHandler();

        // check for ground
        GroundCheckHandler();
    }

    private void LateUpdate()
    {
        // handle camera
        CameraHandler();
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
        // toggle camera based on shared map view activity
        playerCamera.gameObject.SetActive(!(GameManager.Instance.mapCamera.gameObject.activeSelf && InventoryManager.Instance.isInventoryActive));
        UIManager.Instance.pageBackground.SetActive(playerCamera.activeSelf); // TODO: this shouldn't be here

        if (!enableCamera || playerCamera == null) return;

        // follow player
        Vector3 forward = transform.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 targetPosition = transform.position + Vector3.up * 8f - forward * .5f;
        float t = 1f - Mathf.Exp(-5f * Time.deltaTime);
        playerCamera.transform.position = Vector3.Lerp(playerCamera.transform.position, targetPosition, t);

        if (rotateCamera)
        {
            // look at player
            Vector3 direction = transform.position - playerCamera.transform.position;
            if (direction.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
                playerCamera.transform.rotation = Quaternion.Slerp(playerCamera.transform.rotation, targetRotation, t);
            }   
        }
        else
        {
            playerCamera.transform.localEulerAngles = new Vector3(80f, 0f, 0f);
        }
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
        if (moveDirection.sqrMagnitude < 0.001f) return;

        // reset agent
        if (playerRigidbody.isKinematic)
        {
            playerRigidbody.isKinematic = false;
            playerRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            playerAgent.updatePosition = false;
            playerAgent.updateRotation = false;
            playerAgent.isStopped = true;
            playerAgent.ResetPath();
        }

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
        Vector3 targetVelocity = movementDirection * movementSpeed * 0.2f;
        playerRigidbody.linearVelocity = new Vector3(targetVelocity.x, playerRigidbody.linearVelocity.y, targetVelocity.z);

        Vector3 lookDirection = cameraForward;
        if (lookDirection.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
            playerRigidbody.MoveRotation(Quaternion.RotateTowards(playerRigidbody.rotation,targetRotation, 720f * Time.fixedDeltaTime));
        }
    }

    public void OnMove(Vector3 targetPosition)
    {
        if (GameManager.Instance.isPaused || InventoryManager.Instance.isInventoryActive || UIManager.Instance.activeDialog) return;

        //GameObject targetMarker = Instantiate(Resources.Load<GameObject>("TargetMarker"));
        //targetMarker.transform.position = targetPosition;

        // reset agent conditionally
        if (!playerRigidbody.isKinematic) playerAgent.Warp(playerRigidbody.position);
        playerRigidbody.interpolation = RigidbodyInterpolation.None;
        playerRigidbody.isKinematic = true;
        playerAgent.updatePosition = true;
        playerAgent.updateRotation = true;
        playerAgent.isStopped = false;

        // move agent
        playerAgent.SetDestination(targetPosition);
    }

    public void SetPlayerPosition(Vector3 targetPosition)
    {
        playerRigidbody.interpolation = RigidbodyInterpolation.None;
        playerRigidbody.isKinematic = false;
        playerAgent.updatePosition = false;
        playerAgent.updateRotation = false;
        playerAgent.isStopped = true;
        playerAgent.ResetPath();
        gameObject.transform.position = targetPosition;
    }

    public void OnJump()
    {
        if (GameManager.Instance.isPaused || !isGrounded || !enableMovement || playerRigidbody.isKinematic || InventoryManager.Instance.isInventoryActive) return;

        playerRigidbody.linearVelocity = gameObject.transform.up * jumpStrength;
    }
}
