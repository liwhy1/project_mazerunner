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
    public GameObject mapComponenets;
    public GameObject mapObjectP0;
    public GameObject mapObjectP1;
    public GameObject mapObjectP2;
    [SerializeField] private GameObject ownViewButton;
    public GameObject individualViewButton;
    [SerializeField] private GameObject sharedViewButton;
    [SerializeField] private GameObject ownViewPage;
    [SerializeField] private GameObject individualViewPage;
    [SerializeField] private GameObject sharedViewPage;
    public GameObject activeMapPage;

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
    [SerializeField] private GameObject toolBar;
    [SerializeField] private GameObject activeTool;
    [SerializeField] private GameObject pencilIcon;
    [SerializeField] private GameObject eraserIcon;
    [SerializeField] private GameObject trashIcon;
    [SerializeField] private GameObject saveIcon;

    public void OnSetup()
    {
        Debug.Log("MapManager: Setting up");
        Instance = this;

        // set active map tool
        SetActiveTool(pencilIcon);

        // set active page
        SetMapPage(ownViewPage);

        // generate icon objects
        GenerateIcons();

        // disable inactive buttons
        sharedViewButton.GetComponent<UIElement>().OnElementDisable();
        individualViewButton.GetComponent<UIElement>().OnElementDisable();

        // sync map state
        if (!GameManager.Instance.isOffline)
        {
            GameManager.Instance.SyncPlayerMapStateServerRpc();            
        }
    }

    private void GenerateIcons()
    {
        // load icons from resources folder
        var mapIcons = Resources.LoadAll<Sprite>("MapIconsNew");

        // setup icons
        foreach (var icon in mapIcons)
        {
            InstantiateNewIcon(icon.name, true, -1);
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

    private void InstantiateNewIcon(string targetSprite, bool enableTitle, int siblingIndex)
    {
        GameObject newIcon = Instantiate(Resources.Load<GameObject>("MapIcon"), iconPile.transform);
        newIcon.name = targetSprite;
        newIcon.transform.localPosition = Vector3.zero;
        newIcon.transform.localEulerAngles = Vector3.zero;
        newIcon.transform.Find("Sprite").GetComponent<Image>().sprite = Resources.LoadAll<Sprite>("MapIconsNew").FirstOrDefault(s => s.name.Contains(targetSprite));
        newIcon.transform.Find("Sprite").GetComponent<Image>().preserveAspect = true;
        newIcon.transform.Find("Name").gameObject.SetActive(enableTitle);
        newIcon.transform.Find("Name").GetComponent<TMP_Text>().text = targetSprite.Remove(targetSprite.Length - 2, 2);
        if (siblingIndex != -1) newIcon.transform.SetSiblingIndex(siblingIndex);
        SetupElementTriggers(newIcon);
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
            InstantiateNewIcon(targetElement.name, true, targetElement.transform.GetSiblingIndex());

            // disable target element text
            targetElement.transform.Find("Name").gameObject.SetActive(false);

            // pile position shouldn't be saved, this will be used to destroy instead
            savedElementPosition = Vector3.zero;
        }

        // set target element parent to the map
        targetElement.transform.SetParent(transform);

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

    public void OnElementDrag(GameObject targetElement)
    {
        if (activeTool == null) return;

        // prevent dragging while the map isn't active
        if (!gameObject.activeSelf) return;

        // force object to appear on top
        targetElement.transform.SetAsLastSibling();

        // follow mouse position with object
        Vector3 targetPosition = InputManager.Instance.pointerPosition;
        targetElement.transform.position = Vector3.Lerp(targetElement.transform.position, targetPosition, Time.deltaTime * dragSmoothing);
    }

    public void OnStopElementDrag(GameObject targetElement)
    {
        if (activeTool == null) return;

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
        targetElement.transform.SetParent(mapComponenets.transform);

        // conditionally enable drawdot raycast state
        SetDrawDotRaycastState(activeTool == eraserIcon);
    }

    public void OnDrawLine()
    {
        if (activeTool != pencilIcon || !enablePlacement || enableDiscard || InputManager.Instance.lookAction.ReadValue<Vector2>() == Vector2.zero) return;

        // instantiate new dots in world space based on mouse position
        Vector3 targetPosition = InputManager.Instance.pointerPosition;
        GameObject newDot = Instantiate(drawDot, targetPosition, Quaternion.Euler(0f, 0f, 0f), mapComponenets.transform);
        newDot.transform.localEulerAngles = Vector3.zero;
        newDot.SetActive(true);

        // add pointer enter event to allow detecting existance
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

    public void OnMapToggleReady()
    {
        bool isMapready = iconPile.activeSelf;
        toolBar.SetActive(!isMapready);
        iconPile.SetActive(!isMapready);
        saveIcon.transform.GetChild(0).gameObject.SetActive(isMapready);
        saveIcon.transform.GetChild(1).gameObject.SetActive(!isMapready);
        SetActiveTool(!isMapready ? pencilIcon : null);

        if (!GameManager.Instance.isOffline)
        {
            GameManager.Instance.SetPlayerMapStateServerRpc(isMapready);            
        }
        else
        {
            OnSendMapData();
        }
    }

    public void OnSendMapData()
    {
        sharedViewButton.GetComponent<UIElement>().OnElementEnable();
        InventoryManager.Instance.OnJournalDisable();

        List<MapElementData> mapElements = new List<MapElementData>();
        foreach (var icon in activeIcons)
        {
            mapElements.Add(new MapElementData{iconPrefab = "MapIcon", iconSprite = icon.name, iconPosition = icon.transform.localPosition});
        }
        foreach (var icon in activeDrawDots)
        {
            mapElements.Add(new MapElementData{iconPrefab = "DrawDot", iconSprite = "DrawDot", iconPosition = icon.transform.localPosition});
        }

        if (!GameManager.Instance.isOffline)
        {
            saveIcon.transform.GetChild(0).gameObject.SetActive(true);
            saveIcon.transform.GetChild(1).gameObject.SetActive(false);
            SetActiveTool(null);
            saveIcon.SetActive(false);
            toolBar.SetActive(false);
            iconPile.SetActive(false);

            // prevent sending empty data
            if (mapElements.Count == 0) return;
            SharedMapManager.Instance.SpawnMapInstanceServerRpc(mapElements.ToArray());            
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
        pencilIcon.GetComponent<UIElement>().OnElementDeSelect();
        eraserIcon.GetComponent<UIElement>().OnElementDeSelect();
        trashIcon.GetComponent<UIElement>().OnElementDeSelect();

        if (targetTool == null) return;
        targetTool.GetComponent<UIElement>().OnElementSelect();
    }

    public void SetHoveredDot(GameObject targetDot) 
    {
        activeHoveredDot = targetDot;
        if (InputManager.Instance.primaryAction.ReadValue<float>() != 0)
        {
            OnEraseLine();
        }
    }

    public void SetMapPage(GameObject pageObject)
    {
        activeMapPage = pageObject;
        transform.parent.GetComponent<Image>().enabled = true;
        GameManager.Instance.mapCamera.gameObject.SetActive(false);

        ownViewPage.SetActive(false);
        individualViewPage.SetActive(false);
        sharedViewPage.SetActive(false);
        pageObject.SetActive(true);

        ownViewButton.GetComponent<UIElement>().OnElementDeSelect();
        individualViewButton.GetComponent<UIElement>().OnElementDeSelect();
        sharedViewButton.GetComponent<UIElement>().OnElementDeSelect();
        if (pageObject == ownViewPage)
        {
            ownViewButton.GetComponent<UIElement>().OnElementSelect();
            bool isEditable = !saveIcon.transform.GetChild(0).gameObject.activeSelf;
            iconPile.SetActive(isEditable);
            toolBar.gameObject.SetActive(isEditable);
        }
        else if (pageObject == individualViewPage)
        {
            individualViewButton.GetComponent<UIElement>().OnElementSelect();
            mapObjectP0.SetActive(true);
            mapObjectP1.SetActive(true);
            mapObjectP2.SetActive(true);
        }
        else if (pageObject == sharedViewPage)
        {
            sharedViewButton.GetComponent<UIElement>().OnElementSelect();
            transform.parent.GetComponent<Image>().enabled = false;
            GameManager.Instance.mapCamera.gameObject.SetActive(true);
        }
    }

    public void OnEnablePlacement() => enablePlacement = true;
    public void OnDisablePlacement() => enablePlacement = false;
    public void OnEnableDiscard() => enableDiscard = true;
    public void OnDisableDiscard() => enableDiscard = false;
    public bool FetchSharedViewState() => sharedViewButton.GetComponent<UIElement>().isEnabled;
    public bool FetchIndividualViewState() => individualViewButton.GetComponent<UIElement>().isEnabled;
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