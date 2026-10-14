using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class MapManager : NetworkBehaviour
{
    public static MapManager Instance;
    public static MapManager SharedInstance;
    public GameObject mapComponents;
    [NonSerialized] public Dictionary<ulong, GameObject> pendingPlacements = new();
    [SerializeField] private bool isNetworked;
    private ulong nextPlacementRequestId = 0;

    [Header("Icon Data")]
    private List<GameObject> activeIcons = new List<GameObject>();
    public List<Sprite> mapSprites = new List<Sprite>();
    public GameObject iconPile;
    [SerializeField] private Vector3 savedElementPosition;
    [SerializeField] private bool enablePlacement;
    [SerializeField] private bool enableDiscard;
    private bool isPileHovered;

    [Header("Draw Data")]
    private List<GameObject> activeDrawDots = new List<GameObject>();
    public GameObject activeHoveredDot;

    [Header("UI Data")]
    public GameObject mapBackground;
    public GameObject toolBar;
    [SerializeField] private GameObject activeTool;
    [SerializeField] private GameObject pencilIcon;
    [SerializeField] private GameObject eraserIcon;
    [SerializeField] private GameObject trashIcon;
    [SerializeField] private Slider pencilSizeSlider;
    public GameObject saveIcon;
    public bool isLoaded;

    public override void OnNetworkSpawn() => OnSetup();

    public void OnSetup()
    {
        if (isNetworked)
        {
            Debug.Log("SharedMapManager: Settings up");
            SharedInstance = this;
            if (IsHost)
            {
                mapComponents = Instantiate(Resources.Load<GameObject>("MapPrefabs/NewElements"));
                mapComponents.GetComponent<NetworkObject>()?.Spawn();
                mapComponents.transform.SetParent(transform);
                mapComponents.transform.SetSiblingIndex(iconPile.transform.GetSiblingIndex());
            }
            gameObject.SetActive(false);
            transform.Find("InnerLayer").localPosition = new Vector3(307f, 0f, 0f);
        }
        else
        {
            Debug.Log("MapManager: Settings up");
            Instance = this;
            if (MapManager.SharedInstance) MapNetworkManager.Instance.OnMapStateChanged(MapState.Null, MapNetworkManager.Instance.mapState.Value);
        }

        // load map icons
        mapSprites = Resources.LoadAll<Sprite>("MapIcons").ToList();

        // generate icons
        mapSprites.ForEach(i => InstantiateNewIcon("MapIcon", i.name, true));

        // set active map tool
        SetActiveTool(pencilIcon);

        // conditionally disable save icon
        if (isNetworked && !IsHost)
        {
            saveIcon.SetActive(false);
        }

        // setup triggers
        SetupMapTriggers();
    }

    private void Update() => PileHoverHandler();

    private void SetupMapTriggers()
    {
        mapBackground = transform.Find("Background").gameObject;
        UIManager.Instance.AddEventTrigger(mapBackground.GetComponent<EventTrigger>(), EventTriggerType.PointerEnter, OnEnablePlacement);
        UIManager.Instance.AddEventTrigger(mapBackground.GetComponent<EventTrigger>(), EventTriggerType.PointerExit, OnDisablePlacement);
        UIManager.Instance.AddEventTrigger(mapBackground.GetComponent<EventTrigger>(), EventTriggerType.Drag, OnDrawLine);
        UIManager.Instance.AddEventTrigger(iconPile.GetComponent<EventTrigger>(), EventTriggerType.PointerEnter, OnEnableDiscard);
        UIManager.Instance.AddEventTrigger(iconPile.GetComponent<EventTrigger>(), EventTriggerType.PointerExit, OnDisableDiscard);
        UIManager.Instance.AddEventTrigger(iconPile.GetComponent<EventTrigger>(), EventTriggerType.PointerEnter, SetPileHoverState, true);
        UIManager.Instance.AddEventTrigger(iconPile.GetComponent<EventTrigger>(), EventTriggerType.PointerExit, SetPileHoverState, false);
        UIManager.Instance.AddEventTrigger(pencilIcon.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, SetActiveTool, pencilIcon);
        UIManager.Instance.AddEventTrigger(eraserIcon.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, SetActiveTool, eraserIcon);
        UIManager.Instance.AddEventTrigger(trashIcon.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, OnMapClearRequest);
        UIManager.Instance.AddEventTrigger(saveIcon.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, () => OnMapToggleReady(true, true));
    }

    private void InstantiateNewIcon(string targetPrefab, string targetSprite, bool enableTitle, int siblingIndex = -1)
    {
        GameObject objectPrefab = Resources.Load<GameObject>("MapPrefabs/" + targetPrefab);
        Sprite objectSprite = mapSprites.FirstOrDefault(s => s.name.Contains(targetSprite));
        GameObject newIcon = Instantiate(objectPrefab, iconPile.transform);
        Image newIconSprite = newIcon.transform.Find("Sprite").GetComponent<Image>();
        TMP_Text newIconTitle = newIcon.transform.Find("Name").GetComponent<TMP_Text>();

        // setup icon
        newIcon.name = targetSprite;
        newIcon.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        // conditionally apply sibling index
        if (siblingIndex != -1) newIcon.transform.SetSiblingIndex(siblingIndex);

        // setup sprite
        newIconSprite.sprite = objectSprite;
        newIconSprite.preserveAspect = true;

        // setup title
        newIconTitle.gameObject.SetActive(enableTitle);
        newIconTitle.text = targetSprite.Remove(targetSprite.Length - 2, 2);

        // setup triggers
        SetupElementTriggers(newIcon);
    }

    public void SetupElementTriggers(GameObject targetElement)
    {
        // make sure the element has an eventtrigger
        if (!targetElement.GetComponent<EventTrigger>()) targetElement.AddComponent<EventTrigger>();
        EventTrigger elementTrigger = targetElement.GetComponent<EventTrigger>();

        // add triggers
        UIManager.Instance.AddEventTrigger(elementTrigger, EventTriggerType.BeginDrag, OnStartElementDrag, targetElement);
        UIManager.Instance.AddEventTrigger(elementTrigger, EventTriggerType.Drag, OnElementDrag, targetElement);
        UIManager.Instance.AddEventTrigger(elementTrigger, EventTriggerType.EndDrag, OnStopElementDrag, targetElement);
        UIManager.Instance.AddEventTrigger(elementTrigger, EventTriggerType.PointerEnter, OnEnablePlacement);
        UIManager.Instance.AddEventTrigger(elementTrigger, EventTriggerType.PointerExit, OnDisablePlacement);
    }

    private void OnStartElementDrag(GameObject targetElement)
    {
        if (activeTool == null || !gameObject.activeSelf) return;

        // TODO: not this
        if (!mapComponents) 
        {
            mapComponents = isNetworked ? transform.Find("NewElements(Clone)").gameObject : transform.Find("NewElements").gameObject;
            mapComponents.transform.SetSiblingIndex(iconPile.transform.GetSiblingIndex());
        }

        // save element position
        savedElementPosition = targetElement.transform.position;

        // request ownership
        if (targetElement.GetComponent<NetworkObject>())
        {
            MapNetworkManager.Instance.SetElementOwnershipServerRpc(targetElement.GetComponent<NetworkObject>().NetworkObjectId);
        }

        // check if the element is dragged out of the pile
        if (targetElement.transform.parent == iconPile.transform)
        {
            // duplicate and replace original element
            InstantiateNewIcon("MapIcon", targetElement.name, true, targetElement.transform.GetSiblingIndex());

            // disable target element text
            targetElement.transform.Find("Name").gameObject.SetActive(false);

            // preset saved position to prevent returning to the pile, this will be used to destroy instead
            savedElementPosition = Vector3.zero;
        }

        // set target element to the last child of the object to appear on top of the pile
        if (!targetElement.GetComponent<NetworkObject>() || IsHost) targetElement.transform.SetParent(transform);
        else MapNetworkManager.Instance.SetMapElementParentServerRpc(targetElement.GetComponent<NetworkObject>().NetworkObjectId, transform.GetComponent<NetworkObject>().NetworkObjectId);

        // disable raycast target to allow detecting hover states under the element
        targetElement.GetComponent<Image>().raycastTarget = false;

        // disable drawdot raycast targets
        SetDrawDotRaycastState(false);

        // enable color highlight
        ToggleColorHighlight(true);
    }

    private void OnElementDrag(GameObject targetElement)
    {
        if (activeTool == null || !gameObject.activeSelf) return;

        // force object to appear on top
        targetElement.transform.SetAsLastSibling();

        // follow mouse position with object
        Vector3 targetPosition = Pointer.current.position.ReadValue();
        targetElement.transform.position = Vector3.Lerp(targetElement.transform.position, targetPosition, Time.deltaTime * 45f);
    }

    private void OnStopElementDrag(GameObject targetElement)
    {
        if (activeTool == null || !gameObject.activeSelf) return;

        // disable color highlight
        ToggleColorHighlight(false);

        // play place sfx
        AudioManager.Instance.OnElementPlace();

        // set target element parent to the components object
        if (!targetElement.GetComponent<NetworkObject>() || IsHost) targetElement.transform.SetParent(mapComponents.transform);
        else MapNetworkManager.Instance.SetMapElementParentServerRpc(targetElement.GetComponent<NetworkObject>().NetworkObjectId, mapComponents.transform.GetComponent<NetworkObject>().NetworkObjectId);

        // destroy element if its dropped over a discard allowed area
        if (enableDiscard)
        {
            if (targetElement.GetComponent<NetworkObject>()) MapNetworkManager.Instance.DestroyMapElementServerRpc(targetElement.GetComponent<NetworkObject>().NetworkObjectId);
            else Destroy(targetElement);
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
            // return element to the saved postion
            else
            {
                targetElement.transform.position = savedElementPosition;
            }
        }

        // reset raycast target state
        targetElement.GetComponent<Image>().raycastTarget = true;

        // conditionally enable drawdot raycast state
        SetDrawDotRaycastState(activeTool == eraserIcon);

        // conditionally store element
        if (!activeIcons.Contains(targetElement)) activeIcons.Add(targetElement);

        // spawn networkobject if the element was pulled from the pile
        if (savedElementPosition == Vector3.zero && isNetworked)
        {
            ulong requestId = nextPlacementRequestId++;
            pendingPlacements.Add(requestId, targetElement);
            MapNetworkManager.Instance.SpawnMapElementServerRpc(requestId, "SharedMapIcon", targetElement.name, targetElement.transform.localPosition, targetElement.transform.localScale);
        }
    }

    private void ToggleColorHighlight(bool targetState)
    {
        Color greenHighlight = new Color(0.8478955f, 1f, 0.7528302f);
        Color redHighlight = new Color(1f, 0.8207547f, 0.8207547f);
        mapBackground.GetComponent<Image>().color = targetState ? greenHighlight : Color.white;
        iconPile.GetComponent<Image>().color = targetState ? redHighlight : Color.white;
    }

    private void SetPileHoverState(bool targetState) => isPileHovered = targetState;

    private void PileHoverHandler()
    {
        float targetY = !isPileHovered ? -520f : -395f;
        iconPile.transform.localPosition = Vector3.Lerp(iconPile.transform.localPosition, new Vector3(iconPile.transform.localPosition.x, targetY, iconPile.transform.localPosition.z), Time.deltaTime * 10f);
    }

    public IEnumerator MapMoveHandler(bool shouldCenter)
    {
        StartCoroutine(InventoryManager.Instance.JournalMoveHandler(shouldCenter));
        yield return new WaitForSeconds(shouldCenter ? .1f : 0f);
        InventoryManager.Instance.MapTabMoveHandler(shouldCenter);

        Vector2 targetPosition = new Vector2(shouldCenter ? -307f : 0f, transform.GetComponent<RectTransform>().anchoredPosition.y);
        StartCoroutine(UIManager.Instance.UIAnimator(transform.GetComponent<RectTransform>(), targetPosition));

        targetPosition = new Vector2(shouldCenter ? -307f : 0f, transform.Find("InnerLayer").GetComponent<RectTransform>().anchoredPosition.y);
        StartCoroutine(UIManager.Instance.UIAnimator(transform.Find("InnerLayer").GetComponent<RectTransform>(), targetPosition));
    }

    public IEnumerator MapLoadAnimator()
    {
        yield return new WaitForSeconds(1.5f);
        MapManager.Instance.OnMapToggleReady(false);
        isLoaded = true;
    }

    public void OnDrawLine()
    {
        if (activeTool != pencilIcon || !gameObject.activeSelf || !enablePlacement || enableDiscard || InputManager.Instance.lookAction.ReadValue<Vector2>() == Vector2.zero) return;

        // TODO: not this
        if (!mapComponents) 
        {
            mapComponents = isNetworked ? transform.Find("NewElements(Clone)").gameObject : transform.Find("NewElements").gameObject;
            mapComponents.transform.SetSiblingIndex(iconPile.transform.GetSiblingIndex());
        }

        // conditionally play draw sfx
        if (!AudioManager.Instance.IsPlaying()) AudioManager.Instance.OnDraw();

        // instantiate new dot based on mouse position
        Vector3 targetPosition = Pointer.current.position.ReadValue();
        GameObject objectPrefab = Resources.Load<GameObject>("MapPrefabs/" + (isNetworked ? "SharedDrawDot" : "DrawDot"));
        GameObject newDot = Instantiate(objectPrefab, targetPosition, Quaternion.identity, mapComponents.transform);
        newDot.transform.localScale = new Vector3(pencilSizeSlider.value, pencilSizeSlider.value, pencilSizeSlider.value);
        newDot.transform.SetSiblingIndex(0);
        newDot.SetActive(true);

        // add event trigger
        SetupDrawDotEventTriggers(newDot);
        newDot.GetComponent<Image>().raycastTarget = false;

        // store active dots in a list
        activeDrawDots.Add(newDot);

        // conditionally spawn network object
        if (isNetworked)
        {
            ulong requestId = nextPlacementRequestId++;
            pendingPlacements.Add(requestId, newDot);
            MapNetworkManager.Instance.SpawnMapElementServerRpc(requestId, "SharedDrawDot", "DrawDot", newDot.transform.localPosition, newDot.transform.localScale);
        }
    }

    public IEnumerator ElementReplaceRoutine(GameObject oldElement, GameObject newElement)
    {
        float timeCounter = 0f;
        while (newElement.transform.localPosition.z != 1) 
        {
            timeCounter += .1f;
            yield return new WaitForSeconds(.1f);
            if (timeCounter > 2f || !newElement) break;
        }
        if (!newElement || !oldElement) yield break;
        newElement.transform.Find("Sprite")?.gameObject.SetActive(true);
        if (newElement.GetComponent<Image>()) newElement.GetComponent<Image>().enabled = true;
        pendingPlacements.Remove(pendingPlacements.FirstOrDefault(p => p.Value == oldElement).Key);
        Destroy(oldElement);
    }

    public void OnEraseLine()
    {
        if (activeTool != eraserIcon || !activeHoveredDot || !gameObject.activeSelf || InputManager.Instance.lookAction.ReadValue<Vector2>() == Vector2.zero) return;

        if (isNetworked && !IsHost)
        {
            MapNetworkManager.Instance.DestroyMapElementServerRpc(activeHoveredDot.GetComponent<NetworkObject>().NetworkObjectId);        
        }
        else
        {
            activeDrawDots.Remove(activeHoveredDot);
            Destroy(activeHoveredDot);
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
        pencilSizeSlider.gameObject.SetActive(false);

        if (targetTool == null) return;
        targetTool.GetComponent<UIElement>().OnElementSelect();
        pencilSizeSlider.gameObject.SetActive(pencilIcon.GetComponent<UIElement>().isSelected);
    }

    private void SetDrawDotRaycastState(bool targetState)
    {
        if (!mapComponents) return;
        foreach (Transform child in mapComponents.transform)
        {
            if (child.name.Contains("Dot"))
            {
                child.GetComponent<Image>().raycastTarget = targetState;                
            }
        }
    }

    public void SetupDrawDotEventTriggers(GameObject targetDot)
    {
        // add pointer enter event to allowed detecting existance
        targetDot.AddComponent<EventTrigger>();
        UIManager.Instance.AddEventTrigger(targetDot.GetComponent<EventTrigger>(), EventTriggerType.PointerEnter, SetHoveredDrawDot, targetDot);
    }

    private void SetHoveredDrawDot(GameObject targetDot) 
    {
        activeHoveredDot = targetDot;
        if (InputManager.Instance.primaryAction.ReadValue<float>() != 0)
        {
            OnEraseLine();
        }
    }

    public void OnMapClearRequest()
    {
        UIManager.Instance.OnOpenDialog(isNetworked ? DialogId.SharedMapClear : DialogId.MapClear);
    }

    public void OnClearMap()
    {
        if (isNetworked)
        {
            MapNetworkManager.Instance.ClearMapServerRpc();
        }
        else
        {
            // cleanup dots
            activeDrawDots.ForEach(d => Destroy(d));
            activeDrawDots.Clear();

            // cleanup icons
            activeIcons.ForEach(i => Destroy(i));
            activeIcons.Clear();
        }
    }

    public void OnMapToggleReady(bool sendToServer = true, bool shouldAnimate = true)
    {
        bool isMapready = iconPile.activeSelf;
        iconPile.SetActive(!isMapready);
        pencilIcon.GetComponent<UIElement>().OnElementToggleEnable(!isMapready);
        eraserIcon.GetComponent<UIElement>().OnElementToggleEnable(!isMapready);
        trashIcon.GetComponent<UIElement>().OnElementToggleEnable(!isMapready);
        SetActiveTool(!isMapready ? pencilIcon : null);
        if (shouldAnimate) StartCoroutine(MapMoveHandler(isMapready));

        // handle behaviour based on map type
        if (isNetworked)
        {
            saveIcon.SetActive(!isMapready);

            // send ready state
            if (!GameManager.Instance.isMaster && sendToServer) MapNetworkManager.Instance.SetMapStateServerRpc(MapState.Explore);
        }
        else 
        {
            saveIcon.transform.GetChild(0).gameObject.SetActive(isMapready);
            saveIcon.transform.GetChild(1).gameObject.SetActive(!isMapready);

            // send ready state
            if (!GameManager.Instance.isMaster && sendToServer) MapNetworkManager.Instance.SetPlayerMapStateServerRpc(isMapready);
        }
    }

    public void OnDisableMapUI()
    {
        SetActiveTool(null);
        iconPile.SetActive(false);
        saveIcon.SetActive(false);
        pencilIcon.GetComponent<UIElement>().OnElementDisable();
        eraserIcon.GetComponent<UIElement>().OnElementDisable();
        trashIcon.GetComponent<UIElement>().OnElementDisable();
        if (GameManager.Instance.isMaster) toolBar.SetActive(false);
        ToggleColorHighlight(false);
    }

    public void OnSendMapData()
    {
        List<MapElementData> mapElements = new List<MapElementData>();
        foreach (var icon in activeDrawDots)
        {
            if (icon == null) continue; // NOTE: These checks should prevent failed map data sending
            mapElements.Add(new MapElementData{iconPrefab = "DrawDot", iconSprite = "DrawDot", iconPosition = icon.transform.localPosition, iconScale = icon.transform.localScale});
        }
        foreach (var icon in activeIcons)
        {
            if (icon == null) continue;
            mapElements.Add(new MapElementData{iconPrefab = "MapIcon", iconSprite = icon.name, iconPosition = icon.transform.localPosition, iconScale = icon.transform.localScale});
        }

        // prevent sending empty data
        if (mapElements.Count == 0) return;
        MapNetworkManager.Instance.SpawnMapInstanceServerRpc(mapElements.ToArray());            
    }

    public void UpdateMapReadyState()
    {
        string targetText = GameManager.Instance.playerList.Count(p => p.IsMapReady.Value == true) + "/" + GameManager.Instance.playerList.Count;
        transform.Find("SaveIcon").Find("ReadyText").GetComponent<TMP_Text>().text = targetText;
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
    public Vector3 iconScale;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref iconPrefab);
        serializer.SerializeValue(ref iconSprite);
        serializer.SerializeValue(ref iconPosition);
        serializer.SerializeValue(ref iconScale);
    }
}
