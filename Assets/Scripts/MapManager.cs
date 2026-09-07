using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.LightTransport;
using UnityEngine.UI;

public class MapManager : MonoBehaviour
{
    public static MapManager Instance;
    [SerializeField] private GameObject mapObject;
    private Vector3 savedElementPosition;
    [SerializeField] private float dragSmoothing = 25f;

    [SerializeField] private GameObject drawDot;
    [SerializeField] private float maxAllowedDots = 300f;
    private List<GameObject> activeDrawDots = new List<GameObject>();
    [SerializeField] private bool enableDraw;
    
    private void Start()
    {
        /*if (!IsOwner)
        {
            Debug.Log("Destorying map manager");
            Destroy(this);
            return;
        }*/
        Instance = this;
        GetComponent<Canvas>().worldCamera = PlayerController.Instance.cameraObject;
        mapObject.SetActive(false);
        GenerateIcons();
    }

    private void GenerateIcons()
    {
        var mapIcons = Resources.LoadAll<Sprite>("MapIcons");
        foreach (var icon in mapIcons)
        {
            var newIcon = new GameObject();
            newIcon.name = icon.name;
            newIcon.transform.SetParent(mapObject.transform);
            newIcon.transform.localPosition = Vector3.zero;
            newIcon.transform.localEulerAngles = Vector3.zero;
            newIcon.AddComponent<Image>();
            newIcon.GetComponent<Image>().sprite = icon;
            newIcon.AddComponent<EventTrigger>();
            newIcon.GetComponent<RectTransform>().sizeDelta = new Vector2(0.1f, 0.1f);

            EventTrigger.Entry beginDragEntry = new EventTrigger.Entry();
            beginDragEntry.eventID = EventTriggerType.BeginDrag;
            beginDragEntry.callback.AddListener((eventData) => { OnStartElementDrag(newIcon); });

            EventTrigger.Entry dragEntry = new EventTrigger.Entry();
            dragEntry.eventID = EventTriggerType.Drag;
            dragEntry.callback.AddListener((eventData) => { OnElementDrag(newIcon); });

            EventTrigger.Entry endDragEntry = new EventTrigger.Entry();
            endDragEntry.eventID = EventTriggerType.EndDrag;
            endDragEntry.callback.AddListener((eventData) => { OnStopElementDrag(newIcon); });

            newIcon.GetComponent<EventTrigger>().triggers.Add(beginDragEntry);
            newIcon.GetComponent<EventTrigger>().triggers.Add(dragEntry);
            newIcon.GetComponent<EventTrigger>().triggers.Add(endDragEntry);
        }
    }

    public void OnMapToggle()
    {
        mapObject.SetActive(!mapObject.activeSelf);
        InputManager.Instance.ToggleCursor();
        GameManager.Instance.isPaused = !GameManager.Instance.isPaused;
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
        Vector3 worldPosition = InputManager.Instance.mousePosition;
        worldPosition.z = PlayerController.Instance.cameraObject.nearClipPlane + 1f;
        Vector3 targetPosition = PlayerController.Instance.cameraObject.ScreenToWorldPoint(worldPosition);
        targetElement.transform.position = Vector3.Lerp(targetElement.transform.position, targetPosition, Time.deltaTime * dragSmoothing);
    }

    public void OnDrawLine()
    {
        if (!enableDraw && InputManager.Instance.lookAction.ReadValue<Vector2>() != Vector2.zero) return;

        Vector3 worldPosition = InputManager.Instance.mousePosition;
        worldPosition.z = PlayerController.Instance.cameraObject.nearClipPlane + 1f;
        Vector3 targetPosition = PlayerController.Instance.cameraObject.ScreenToWorldPoint(worldPosition);
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
