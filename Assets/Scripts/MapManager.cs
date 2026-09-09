using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MapManager : MonoBehaviour
{
    public static MapManager Instance;

    [Header("Map Data")]
    public GameObject mapObject;
    public bool isMapActive;

    [Header("Icon Data")]
    [SerializeField] private GameObject iconPile;
    private Vector3 savedElementPosition;
    [SerializeField] private float dragSmoothing = 25f;
    private List<GameObject> activeIcons = new List<GameObject>();
    [SerializeField] private bool enablePlacement;
    [SerializeField] private bool enableDiscard;

    [Header("Draw Data")]
    [SerializeField] private GameObject drawDot;
    [SerializeField] private float maxAllowedDots = 500f;
    private List<GameObject> activeDrawDots = new List<GameObject>();
    private GameObject activeHoveredDot;
    [SerializeField] private GameObject activeTool;
    [SerializeField] private GameObject pencilIcon;
    [SerializeField] private GameObject eraserIcon;
    [SerializeField] private GameObject trashIcon;
    [SerializeField] private GameObject saveIcon;

    private void Start()
    {
        Instance = this;

        // setup vars
        isMapActive = false;
        mapObject.SetActive(false);

        // set active map tool
        SetActiveTool(pencilIcon);

        // generate icon objects
        GenerateIcons();
    }

    private void GenerateIcons()
    {
        // load icons from resources folder
        var mapIcons = Resources.LoadAll<Sprite>("MapIcons");

        // setup icons
        foreach (var icon in mapIcons)
        {
            GameObject newIcon = Instantiate(Resources.Load<GameObject>("MapIcon"), iconPile.transform);
            newIcon.name = icon.name;
            newIcon.transform.localPosition = Vector3.zero;
            newIcon.transform.localEulerAngles = Vector3.zero;
            newIcon.GetComponent<RectTransform>().sizeDelta = new Vector2(0.1f, 0.1f);
            newIcon.GetComponent<RectTransform>().localScale = new Vector3(100f, 100f, 100f);
            newIcon.GetComponent<Image>().sprite = icon;
            newIcon.transform.GetChild(0).GetComponent<TMP_Text>().text = icon.name.Remove(icon.name.Length - 6, 6);

            // setup event triggers
            SetupElementTriggers(newIcon);
        }
    }

    private void SetupElementTriggers(GameObject targetElement)
    {
        if (targetElement.GetComponent<EventTrigger>() == null)
        {
            targetElement.AddComponent<EventTrigger>();
        }

        // begin drag trigger
        EventTrigger.Entry beginDragEntry = new EventTrigger.Entry() {eventID = EventTriggerType.BeginDrag};
        beginDragEntry.callback.AddListener((eventData) => { OnStartElementDrag(targetElement); });
        targetElement.GetComponent<EventTrigger>().triggers.Add(beginDragEntry);

        // on drag trigger
        EventTrigger.Entry dragEntry = new EventTrigger.Entry() {eventID = EventTriggerType.Drag};
        dragEntry.callback.AddListener((eventData) => { OnElementDrag(targetElement); });
        targetElement.GetComponent<EventTrigger>().triggers.Add(dragEntry);

        // end drag trigger
        EventTrigger.Entry endDragEntry = new EventTrigger.Entry() {eventID = EventTriggerType.EndDrag};
        endDragEntry.callback.AddListener((eventData) => { OnStopElementDrag(targetElement); });
        targetElement.GetComponent<EventTrigger>().triggers.Add(endDragEntry);

        // pointer enter trigger
        EventTrigger.Entry enterHoverEntry = new EventTrigger.Entry() {eventID = EventTriggerType.PointerEnter};
        enterHoverEntry.callback.AddListener((eventData) => { OnEnablePlacement(); });
        targetElement.GetComponent<EventTrigger>().triggers.Add(enterHoverEntry);

        // pointer exit trigger
        EventTrigger.Entry exitHoverEntry = new EventTrigger.Entry() {eventID = EventTriggerType.PointerExit};
        exitHoverEntry.callback.AddListener((eventData) => { OnDisablePlacement(); });
        targetElement.GetComponent<EventTrigger>().triggers.Add(exitHoverEntry);
    }

    public void OnMapToggle()
    {
        isMapActive = !isMapActive;
        mapObject.SetActive(!mapObject.activeSelf);
    }

    public void OnSendMapData()
    {
        pencilIcon.gameObject.SetActive(false);
        eraserIcon.gameObject.SetActive(false);
        trashIcon.gameObject.SetActive(false);
        saveIcon.gameObject.SetActive(false);
        iconPile.gameObject.SetActive(false);
        activeTool = null;

        if (GameManager.Instance.isOffline) return;

        List<MapElementData> mapElements = new List<MapElementData>();
        foreach (var icon in activeIcons)
        {
            mapElements.Add(new MapElementData{iconPrefab = "MapIcon", iconSprite = icon.name, iconPosition = icon.transform.localPosition});
        }
        foreach (var icon in activeDrawDots)
        {
            mapElements.Add(new MapElementData{iconPrefab = "DrawDot", iconSprite = "DrawDot", iconPosition = icon.transform.localPosition});
        }

        GameManager.Instance.SpawnMapElementsServerRpc(mapElements.ToArray());
    }

    public void OnStartElementDrag(GameObject targetElement)
    {
        if (activeTool == null) return;

        // save element position
        savedElementPosition = targetElement.transform.position;

        // check if the element is dragged out of the pile
        if (targetElement.transform.parent == iconPile.transform)
        {
            // duplicate and replace original element
            var newElement = Instantiate(targetElement, targetElement.transform.position, targetElement.transform.rotation, iconPile.transform);
            int siblingIndex = targetElement.transform.GetSiblingIndex();
            targetElement.transform.SetParent(mapObject.transform);
            targetElement.transform.GetChild(0).gameObject.SetActive(false);
            newElement.transform.SetSiblingIndex(siblingIndex);
            SetupElementTriggers(newElement);


            // pile position shouldn't be saved, this will be used to destroy instead
            savedElementPosition = Vector3.zero;
        }

        // store active icons
        if (!activeIcons.Contains(targetElement))
        {
            activeIcons.Add(targetElement);
        }

        // disable raycast target to allow detecting hover states under the element
        targetElement.GetComponent<Image>().raycastTarget = false;

        // disable drawdot raycast target
        SetDrawDotRaycastState(false);
    }

    public void OnStopElementDrag(GameObject targetElement)
    {
        // destroy element if its dropped over a discard allowed area
        if (enableDiscard)
        {
            Destroy(targetElement);
            return;
        }

        // return element to previous position if the current drop target is invalid
        if (!enablePlacement)
        {
            // this should only be true if the element wasn't place on the map yet, causing a saved position to "not exist"
            if (savedElementPosition == Vector3.zero)
            {
                Destroy(targetElement);
                return;
            }

            targetElement.transform.position = savedElementPosition;            
        }

        // reset raycast target state
        targetElement.GetComponent<Image>().raycastTarget = true;

        // conditionally enable drawdot raycast state
        SetDrawDotRaycastState(activeTool == eraserIcon);
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

    public void OnDrawableDrag()
    {
        // determine target action based on active tool
        if (activeTool == pencilIcon)
        {
            OnDrawLine();
        }
    }

    public void OnDrawLine()
    {
        if (activeTool != pencilIcon || !enablePlacement || enableDiscard || InputManager.Instance.lookAction.ReadValue<Vector2>() == Vector2.zero) return;

        // instantiate new dots in world space based on mouse position
        Vector3 worldPosition = InputManager.Instance.mousePosition;
        worldPosition.z = PlayerController.Instance.cameraObject.nearClipPlane + 1f;
        Vector3 targetPosition = PlayerController.Instance.cameraObject.ScreenToWorldPoint(worldPosition);
        GameObject newDot = Instantiate(drawDot, targetPosition, Quaternion.Euler(0f, 0f, 0f), mapObject.transform);
        newDot.transform.localEulerAngles = Vector3.zero;
        newDot.SetActive(true);

        // add pointer enter event to allowed detecting existance
        newDot.AddComponent<EventTrigger>();
        EventTrigger.Entry enterHoverEntry = new EventTrigger.Entry() {eventID = EventTriggerType.PointerEnter};
        enterHoverEntry.callback.AddListener((eventData) => { SetHoveredDot(newDot); });
        newDot.GetComponent<EventTrigger>().triggers.Add(enterHoverEntry);
        newDot.GetComponent<Image>().raycastTarget = false;

        // store active dots in a list
        activeDrawDots.Add(newDot);

        // cleanup old dots based on limit
        if (activeDrawDots.Count > maxAllowedDots)
        {
            var targetDot = activeDrawDots.FirstOrDefault(d => d != null);
            activeDrawDots.Remove(targetDot);
            Destroy(targetDot);
        }
    }

    public void OnClearMap()
    {
        // cleanup dots
        foreach (var dot in activeDrawDots)
        {
            Destroy(dot);
        }

        // cleanup icons
        foreach (var icon in activeIcons)
        {
            Destroy(icon);
        }

        activeDrawDots.Clear();
        activeIcons.Clear();
    }

    public void OnEraseLine()
    {
        if (activeTool != eraserIcon || InputManager.Instance.lookAction.ReadValue<Vector2>() == Vector2.zero) return;

        if (!activeHoveredDot) return;

        activeDrawDots.Remove(activeHoveredDot);
        Destroy(activeHoveredDot);
    }

    private void SetDrawDotRaycastState(bool targetState)
    {
        foreach (var dot in activeDrawDots)
        {
            dot.GetComponent<Image>().raycastTarget = targetState;
        }
    }

    public void SetActiveTool(GameObject targetTool)
    {
        activeTool = targetTool;

        // reset raycast state for draw dots
        SetDrawDotRaycastState(activeTool == eraserIcon);

        // reset icon states
        pencilIcon.GetComponent<Image>().color = Color.white;
        eraserIcon.GetComponent<Image>().color = Color.white;
        trashIcon.GetComponent<Image>().color = Color.white;
        saveIcon.GetComponent<Image>().color = Color.white;

        // highlight target tool
        targetTool.GetComponent<Image>().color = Color.gray;
    }

    public void SetHoveredDot(GameObject targetDot) 
    {
        activeHoveredDot = targetDot;
        if (InputManager.Instance.primaryAction.ReadValue<float>() != 0)
        {
            OnEraseLine();
        }
    }

    public void OnEnablePlacement() => enablePlacement = true;
    public void OnDisablePlacement() => enablePlacement = false;
    public void OnEnableDiscard() => enableDiscard = true;
    public void OnDisableDiscard() => enableDiscard = false;
}

public struct MapElementData : INetworkSerializable
{
    public string iconPrefab;
    public string iconSprite;
    public Vector3 iconPosition;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref iconPrefab);
        serializer.SerializeValue(ref iconSprite);
        serializer.SerializeValue(ref iconPosition);
    }
}