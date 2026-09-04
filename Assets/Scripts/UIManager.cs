using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;
    [SerializeField] private float dragSmoothing = 25f;
    [SerializeField] private GameObject pauseObject;
    [SerializeField] private GameObject mapObject;
    private Vector3 savedElementPosition;

    [SerializeField] private GameObject drawDot;
    [SerializeField] private float maxAllowedDots = 300f;
    private List<GameObject> activeDrawDots = new List<GameObject>();
    [SerializeField] private bool enableDraw;

    [SerializeField] private Button startHost;
    [SerializeField] private Button startClient;
    [SerializeField] private Button stopClient;


    private void Awake()
    {
        Instance = this;
        pauseObject.SetActive(false);
        mapObject.SetActive(false);
        drawDot.SetActive(false);

        startHost.onClick.AddListener(delegate { GameManager.Instance.OnStartHost(); });
        startClient.onClick.AddListener(delegate { GameManager.Instance.OnStartClient(); });
        stopClient.onClick.AddListener(delegate { GameManager.Instance.OnDisconnectClient(); });
    }

    public void OnMapToggle()
    {
        mapObject.SetActive(!mapObject.activeSelf);
        InputManager.Instance.ToggleCursor();
        GameManager.Instance.isPaused = !GameManager.Instance.isPaused;
    }

    public void OnPauseToggle()
    {
        pauseObject.SetActive(!pauseObject.activeSelf);
    }

    public void OnStartElementDrag(GameObject targetElement)
    {
        savedElementPosition = targetElement.transform.position;
        targetElement.GetComponent<Image>().raycastTarget = false;
    }

    public void OnStopElementDrag(GameObject targetElement)
    {
        if (!enableDraw)
        {
            targetElement.transform.position = savedElementPosition;            
        }
        targetElement.GetComponent<Image>().raycastTarget = true;
    }

    public void OnElementDrag(GameObject targetElement)
    {
        // prevent dragging while the map isn't active
        if (!mapObject.activeSelf) return;

        // force object to appear on top
        targetElement.transform.SetAsLastSibling();

        // follow mouse position with object
        Vector3 targetPosition = PlayerController.Instance.cameraObject.ScreenToWorldPoint(new Vector3(Mouse.current.position.ReadValue().x, Mouse.current.position.ReadValue().y, PlayerController.Instance.cameraObject.nearClipPlane + 1f));
        targetElement.transform.position = Vector3.Lerp(targetElement.transform.position, targetPosition, Time.deltaTime * dragSmoothing);
    }

    public void OnDrawLine()
    {
        if (!enableDraw && InputManager.Instance.lookAction.ReadValue<Vector2>() != Vector2.zero) return;

        Vector3 targetPosition = PlayerController.Instance.cameraObject.ScreenToWorldPoint(new Vector3(Mouse.current.position.ReadValue().x, Mouse.current.position.ReadValue().y, PlayerController.Instance.cameraObject.nearClipPlane + 1f));
        GameObject newDot = Instantiate(drawDot, targetPosition, Quaternion.Euler(0f, 0f, 0f), mapObject.transform);
        newDot.SetActive(true);

        activeDrawDots.Add(newDot);
        if (activeDrawDots.Count > maxAllowedDots)
        {
            Destroy(activeDrawDots.FirstOrDefault(d => d != null));
        }
    }

    public void OnEnableDraw() => enableDraw = true;
    public void OnDisableDraw() => enableDraw = false;
}
