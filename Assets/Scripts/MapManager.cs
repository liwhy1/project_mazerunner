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

public class MapManager : MonoBehaviour
{
    public static MapManager Instance;
    public GameObject mapObject;
    [NonSerialized] public Dictionary<ulong, GameObject> pendingPlacements = new();
    private ulong nextPlacementRequestId = 0;

    [Header("Icon Data")]
    private List<GameObject> activeIcons = new List<GameObject>();
    public List<Sprite> mapSprites = new List<Sprite>();
    public GameObject iconPile;
    [SerializeField] private Vector2 savedElementPosition;

    [Header("Draw Data")]
    private List<GameObject> activeDrawDots = new List<GameObject>();
    [SerializeField] private GameObject pointerCircle;

    [Header("UI Data")]
    public bool isLoaded;
    [SerializeField] private GameObject mapUI;
    public GameObject activeCanvas;
    public GameObject individualCanvas;
    public GameObject toolBar;
    public bool isPileHovered;
    public bool isMapHovered;
    public bool isElementActive;
    [SerializeField] private GameObject activeTool;
    [SerializeField] private GameObject placeIcon;
    [SerializeField] private GameObject pencilIcon;
    [SerializeField] private GameObject eraserIcon;
    [SerializeField] private GameObject trashIcon;
    [SerializeField] private Slider pencilSizeSlider;
    [SerializeField] private Slider eraserlSizeSlider;
    [SerializeField] private Sprite pointerEraserSprite;
    [SerializeField] private Sprite pointerPencilSprite;
    public GameObject saveIcon;
    private float nextPositionSyncTime;
    private const float positionSyncInterval = 0.1f;

    public void OnSetup()
    {
        Debug.Log("MapManager: Settings up");
        Instance = this;

        // reset the map transforms before setup
        mapObject.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
        individualCanvas.GetComponent<RectTransform>().anchoredPosition = new Vector2(307f, 0);
        mapObject.GetComponent<RectTransform>().localScale = Vector2.one;
        pointerCircle.SetActive(false);

        // load map icons
        mapSprites = Resources.LoadAll<Sprite>("MapIcons").ToList();

        // generate icons
        mapSprites.ForEach(i => InstantiateNewIcon("MapIcon", i.name, true));

        // set active map tool
        SetActiveTool(placeIcon);

        // setup map triggers
        SetupMapTriggers();

        // apply mapstate values
        if (MapNetworkManager.Instance) OnMapStateChanged(MapState.Null, MapNetworkManager.Instance.mapState.Value);
    }

    public void SetActiveCanvas(GameObject targetCanvas)
    {
        // only one map can be visible and editable at a time
        bool isSharedMapEnabled = targetCanvas == MapNetworkManager.Instance.sharedCanvas;
        activeCanvas = targetCanvas;

        // toggle states
        individualCanvas.SetActive(!isSharedMapEnabled);
        InventoryManager.Instance.ToggleBackground(!isSharedMapEnabled);
        MapNetworkManager.Instance.ToggleSharedCanvas(isSharedMapEnabled);
        MapNetworkManager.Instance.ToggleMapBackground(isSharedMapEnabled && !GameManager.Instance.isMaster);
        MapNetworkManager.Instance.ResetRectTransforms();

        // determine editability
        bool isEditable = (targetCanvas == individualCanvas && MapNetworkManager.Instance.mapState.Value == MapState.Own && isLoaded) || (targetCanvas == MapNetworkManager.Instance.sharedCanvas && MapNetworkManager.Instance.mapState.Value == MapState.Shared);
        saveIcon.transform.Find("ReadyText").gameObject.SetActive(activeCanvas == individualCanvas && MapNetworkManager.Instance.mapState.Value == MapState.Own);
        OnToggleMapEditability(isEditable);
    }

    private void Update()
    {
        // only handle map input while the inventory is open
        if (InventoryManager.Instance.isInventoryActive)
        {
            PileAnimationHandler();
            HoverHandler();
            PointerHandler();
        }
    }

    private void SetupMapTriggers()
    {
        UIManager.Instance.AddEventTrigger(placeIcon.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, SetActiveTool, placeIcon);
        UIManager.Instance.AddEventTrigger(pencilIcon.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, SetActiveTool, pencilIcon);
        UIManager.Instance.AddEventTrigger(eraserIcon.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, SetActiveTool, eraserIcon);
        UIManager.Instance.AddEventTrigger(trashIcon.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, OnMapClearRequest);
        UIManager.Instance.AddEventTrigger(saveIcon.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, () => OnMapToggleReady(iconPile.activeSelf));
    }

    private void HoverHandler()
    {
        PointerEventData pointerData = new PointerEventData(EventSystem.current) { position = Pointer.current.position.ReadValue() };

        List<RaycastResult> castResults = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, castResults);

        // handle hover states
        bool isSliderHovered = castResults.Any(r => r.gameObject.GetComponentInParent<Slider>());
        isMapHovered = activeCanvas && castResults.Any(r => r.gameObject == activeCanvas && !isSliderHovered);
        isPileHovered = iconPile && castResults.Any(r => r.gameObject == iconPile && !isSliderHovered);

        // handle pointer circle
        pointerCircle.SetActive(!isPileHovered && isMapHovered && (activeTool == pencilIcon || activeTool == eraserIcon));
        pointerCircle.GetComponent<Image>().sprite = activeTool == pencilIcon ? pointerPencilSprite : pointerEraserSprite;
        pointerCircle.GetComponent<RectTransform>().localScale =  Vector2.one * (activeTool == pencilIcon ? pencilSizeSlider.value : eraserlSizeSlider.value);
        pointerCircle.transform.position = Pointer.current.position.ReadValue();
    }

    private void PointerHandler()
    {
        if (InputManager.Instance.primaryAction.ReadValue<float>() == 1 && !isPileHovered && isMapHovered && !isElementActive)
        {
            if (activeTool == pencilIcon)
            {
                OnDrawLine();
            }
            else if (activeTool == eraserIcon)
            {
                OnEraseLine();
            }
        }
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
        newIcon.transform.SetLocalPositionAndRotation(Vector2.zero, Quaternion.identity);
        // conditionally apply sibling index
        if (siblingIndex != -1) newIcon.transform.SetSiblingIndex(siblingIndex);

        // setup sprite
        newIconSprite.sprite = objectSprite;
        newIconSprite.preserveAspect = true;

        // setup title
        newIconTitle.gameObject.SetActive(enableTitle);
        newIconTitle.text = UIManager.Instance.GetLocalLandmarkString("LANDMARK_" + targetSprite);

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
    }

    private void OnStartElementDrag(GameObject targetElement)
    {
        if (activeTool == null) return;

        // network objects need ownership before they can be dragged
        if (targetElement.GetComponent<NetworkObject>()) MapNetworkManager.Instance.SetElementOwnershipServerRpc(targetElement.GetComponent<NetworkObject>().NetworkObjectId);

        // save element position
        savedElementPosition = targetElement.transform.position;

        // check if the element is dragged out of the pile
        if (targetElement.transform.parent == iconPile.transform)
        {
            // duplicate and replace original element
            InstantiateNewIcon("MapIcon", targetElement.name, true, targetElement.transform.GetSiblingIndex());

            // disable target element text
            targetElement.transform.Find("Name").gameObject.SetActive(false);

            // preset saved position to prevent returning to the pile, this will be used to destroy instead
            savedElementPosition = Vector2.zero;
        }
        else if (activeTool != placeIcon) return;

        isElementActive = true;

        // enable color highlight
        ToggleColorHighlight(true);
    }

    private void OnElementDrag(GameObject targetElement)
    {
        if (activeTool == null || (savedElementPosition != Vector2.zero && activeTool != placeIcon)) return;

        // follow mouse position with object
        Vector2 targetPosition = Pointer.current.position.ReadValue();
        targetElement.transform.position = Vector2.Lerp(targetElement.transform.position, targetPosition, Time.deltaTime * 45f);

        // set target element to the last child of the object to appear on top of the pile
        if (targetElement.GetComponent<NetworkObject>()) targetElement.GetComponent<NetworkObject>().TrySetParent(mapObject.transform, true);
        else targetElement.transform.SetParent(mapObject.transform, true);

        // networked elements sync their position at a fixed rate
        if (Time.unscaledTime >= nextPositionSyncTime)
        {
            nextPositionSyncTime = Time.unscaledTime + positionSyncInterval;
            SetElementPosition(targetElement, targetPosition, true);
        }
    }

    private void OnStopElementDrag(GameObject targetElement)
    {
        if (activeTool == null || (savedElementPosition != Vector2.zero && activeTool != placeIcon)) return;

        // disable color highlight
        ToggleColorHighlight(false);

        isElementActive = false;

        // play place sfx
        AudioManager.Instance.OnElementPlace();

        // set target element parent to the components object
        targetElement.transform.SetParent(activeCanvas.transform, true);
        if (targetElement.GetComponent<NetworkObject>()) SetElementPosition(targetElement, Pointer.current.position.ReadValue(), true);

        // drop over the pile to delete, outside the map to cancel
        if (isPileHovered)
        {
            if (targetElement.GetComponent<NetworkObject>()) MapNetworkManager.Instance.DestroyMapElementServerRpc(targetElement.GetComponent<NetworkObject>().NetworkObjectId);
            else Destroy(targetElement);
            return;
        }
        else if (!isMapHovered)
        {
            // this should only be true if the element wasn't place on the map yet, causing a saved position to "not exist"
            if (savedElementPosition == Vector2.zero)
            {
                Destroy(targetElement);
                return;
            }
            // return element to the saved postion
            else
            {
                SetElementPosition(targetElement, savedElementPosition, false);
            }
        }

        // conditionally store element
        if (!activeIcons.Contains(targetElement)) activeIcons.Add(targetElement);

        // spawn networkobject if the element was pulled from the pile
        if (savedElementPosition == Vector2.zero && activeCanvas == MapNetworkManager.Instance.sharedCanvas)
        {
            // prevent interaction until the element is replaced with a networked one
            targetElement.GetComponent<EventTrigger>().enabled = false;
            targetElement.transform.Find("Sprite").GetComponent<Image>().color = Color.darkGray;

            // request to spawn a networked variant of the element
            ulong requestId = nextPlacementRequestId++;
            pendingPlacements.Add(requestId, targetElement);
            Vector2 targetPosition = UIManager.Instance.GetNormalizedPosition(targetElement.transform.parent.GetComponent<RectTransform>(), targetElement.transform.position);
            MapNetworkManager.Instance.SpawnMapElementServerRpc(requestId, "SharedMapIcon", targetElement.name, targetPosition, targetElement.transform.localScale);
        }
    }

    private void SetElementPosition(GameObject targetElement, Vector2 targetPosition, bool ignoreOwner = false)
    {
        // networked positions are stored normalized so every screen agrees
        if (targetElement.GetComponent<NetworkObject>())
        {
            ulong objectId = targetElement.GetComponent<NetworkObject>().NetworkObjectId;
            Vector2 normalisedPosition = UIManager.Instance.GetNormalizedPosition(targetElement.transform.parent.GetComponent<RectTransform>(), targetPosition);
            MapNetworkManager.Instance.SetMapElementPositionRpc(objectId, normalisedPosition, ignoreOwner);
        }
        else targetElement.transform.position = targetPosition;
    }

    private void ToggleColorHighlight(bool targetState)
    {
        Color greenHighlight = new Color(0.8478955f, 1f, 0.7528302f);
        Color redHighlight = new Color(1f, 0.8207547f, 0.8207547f);
        activeCanvas.GetComponent<Image>().color = targetState ? greenHighlight : Color.white;
        iconPile.GetComponent<Image>().color = targetState ? redHighlight : Color.white;
    }

    private void PileAnimationHandler()
    {
        if (!iconPile) return;
        float targetY = !isPileHovered ? -495f : -335f;
        iconPile.transform.localPosition = Vector2.Lerp(iconPile.transform.localPosition, new Vector2(iconPile.transform.localPosition.x, targetY), Time.deltaTime * 10f);
    }

    public void MapMoveHandler(bool shouldCenter)
    {
        if (!mapUI.activeInHierarchy) return;

        // slide the map aside to make room for the journal
        InventoryManager.Instance.MapTabMoveHandler(shouldCenter);

        // set canvas transforms
        Vector2 targetPosition = new Vector2(shouldCenter ? 0f : 307f, 0f);
        StartCoroutine(UIManager.Instance.UIAnimator(individualCanvas.GetComponent<RectTransform>(), targetPosition));
        StartCoroutine(UIManager.Instance.UIAnimator(MapNetworkManager.Instance.sharedCanvas.GetComponent<RectTransform>(), targetPosition));

        targetPosition = new Vector2(shouldCenter ? 0f : 307f, 0f);
        StartCoroutine(UIManager.Instance.UIAnimator(mapUI.GetComponent<RectTransform>(), targetPosition));
    }

    public IEnumerator MapLoadAnimation()
    {
        Debug.Log("MapManager: Running load animation");

        // reset ui
        // NOTE: yeah, I know how this looks
        InventoryManager.Instance.mapPageButtons.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
        InventoryManager.Instance.journalObject.GetComponent<RectTransform>().anchoredPosition = new Vector2(-700f, InventoryManager.Instance.journalObject.GetComponent<RectTransform>().anchoredPosition.y);
        mapUI.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
        individualCanvas.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;

        // move to the drawing phase and show the tutorial
        yield return new WaitForSeconds(1f);
        isLoaded = true;
        OnToggleMapEditability(true);
        InventoryManager.Instance.OnOwnJournalToggle(true);

        // prompt tutorial dialog
        if (MapNetworkManager.Instance.mapState.Value == MapState.Own) UIDialog.Instance.OnOpenDialog(DialogId.MapTutorial);
    }

    public void OnDrawLine()
    {
        // conditionally play draw sfx
        if (!AudioManager.Instance.IsPlaying()) AudioManager.Instance.OnDraw();

        // instantiate new dot based on mouse position
        Vector2 targetPosition = Pointer.current.position.ReadValue();
        GameObject objectPrefab = Resources.Load<GameObject>("MapPrefabs/" + (activeCanvas == MapNetworkManager.Instance.sharedCanvas ? "SharedDrawDot" : "DrawDot"));
        GameObject newDot = Instantiate(objectPrefab, targetPosition, Quaternion.identity, activeCanvas.transform);
        newDot.transform.localScale = Vector2.one * pencilSizeSlider.value;
        newDot.transform.SetSiblingIndex(0);
        newDot.SetActive(true);

        // store active dots in a list
        activeDrawDots.Add(newDot);

        // conditionally spawn network object
        if (activeCanvas == MapNetworkManager.Instance.sharedCanvas)
        {
            ulong requestId = nextPlacementRequestId++;
            pendingPlacements.Add(requestId, newDot);
            Vector2 normalisedPosition = UIManager.Instance.GetNormalizedPosition(newDot.transform.parent.GetComponent<RectTransform>(), newDot.transform.position);
            MapNetworkManager.Instance.SpawnMapElementServerRpc(requestId, "SharedDrawDot", "DrawDot", normalisedPosition, newDot.transform.localScale);
        }
    }

    public void OnEraseLine()
    {
        if (!isMapHovered) return;

        // check for drawdots under pointercircle
        foreach (Transform element in activeCanvas.transform)
        {
            if (!element.name.Contains("Dot")) continue;

            Vector3[] cornersA = new Vector3[4];
            Vector3[] cornersB = new Vector3[4];
            pointerCircle.GetComponent<RectTransform>().GetWorldCorners(cornersA);
            element.GetComponent<RectTransform>().GetWorldCorners(cornersB);
            Bounds boundsA = new Bounds(cornersA[0], Vector3.zero);
            Bounds boundsB = new Bounds(cornersB[0], Vector3.zero);

            for (int i = 1; i < 4; i++)
            {
                boundsA.Encapsulate(cornersA[i]);
                boundsB.Encapsulate(cornersB[i]);
            }

            // destroy element on intersect
            if (boundsA.Intersects(boundsB))
            {
                if (activeCanvas == MapNetworkManager.Instance.sharedCanvas)
                {
                    MapNetworkManager.Instance.DestroyMapElementServerRpc(element.GetComponent<NetworkObject>().NetworkObjectId);        
                }
                else
                {
                    activeDrawDots.Remove(element.gameObject);
                    Destroy(element.gameObject);
                }
            }
        }
    }

    public void SetActiveTool(GameObject targetTool)
    {
        activeTool = targetTool;

        // reset icon states
        placeIcon.GetComponent<UIElement>().OnElementDeSelect();
        pencilIcon.GetComponent<UIElement>().OnElementDeSelect();
        eraserIcon.GetComponent<UIElement>().OnElementDeSelect();
        trashIcon.GetComponent<UIElement>().OnElementDeSelect();
        pencilSizeSlider.gameObject.SetActive(false);
        eraserlSizeSlider.gameObject.SetActive(false);

        if (targetTool == null) return;
        targetTool.GetComponent<UIElement>().OnElementSelect();
        pencilSizeSlider.gameObject.SetActive(pencilIcon.GetComponent<UIElement>().isSelected);
        eraserlSizeSlider.gameObject.SetActive(eraserIcon.GetComponent<UIElement>().isSelected);
    }

    public IEnumerator ElementReplaceRoutine(GameObject oldElement, GameObject newElement)
    {
        // wait for the networked replacement to become visible
        float timeCounter = 0f;
        while (newElement.transform.localPosition.z != 0)
        {
            timeCounter += .1f;
            yield return new WaitForSeconds(.1f);
            if (timeCounter > 2f || !newElement) break;
        }
        if (!newElement || !oldElement) 
        {
            if (!newElement) Destroy(oldElement);
            else if (!oldElement) Destroy(newElement);
            yield break;
        }
        newElement.transform.Find("Sprite")?.gameObject.SetActive(true);
        if (newElement.GetComponent<Image>()) newElement.GetComponent<Image>().enabled = true;
        pendingPlacements.Remove(pendingPlacements.FirstOrDefault(p => p.Value == oldElement).Key);
        Destroy(oldElement);
    }

    public void OnMapClearRequest()
    {
        UIDialog.Instance.OnOpenDialog(activeCanvas == MapNetworkManager.Instance.sharedCanvas ? DialogId.SharedMapClear : DialogId.MapClear);
    }

    public void OnClearMap()
    {
        if (activeCanvas == MapNetworkManager.Instance.sharedCanvas)
        {
            MapNetworkManager.Instance.ClearMapServerRpc();
        }
        else
        {
            // cleanup dots
            activeDrawDots.Where(d => d != null).ToList().ForEach(d => Destroy(d));
            activeDrawDots.Clear();

            // cleanup icons
            activeIcons.Where(i => i != null).ToList().ForEach(i => Destroy(i));
            activeIcons.Clear();
        }
    }

    public void OnMapToggleReady(bool isReady, bool sendToServer = true)
    {
        OnToggleMapEditability(!isReady);
        InventoryManager.Instance.OnJournalToggle(!isReady); // NOTE: This isn't really ideal, since it can enable the journal when it is availible, but the current gameplay progress shouldn't allow that

        // handle behaviour based on map type
        if (activeCanvas == MapNetworkManager.Instance.sharedCanvas)
        {
            // send ready state
            if (!GameManager.Instance.isMaster && sendToServer) MapNetworkManager.Instance.SetMapStateServerRpc(MapState.Explore);
        }
        else
        {
            // send ready state
            if (!GameManager.Instance.isMaster && sendToServer) MapNetworkManager.Instance.SetPlayerMapStateServerRpc(isReady);
        }
    }

    public void OnToggleMapEditability(bool targetState)
    {
        SetActiveTool(targetState ? placeIcon : null);
        iconPile.SetActive(targetState);
        saveIcon.GetComponent<UIElement>().OnElementToggleEnable((activeCanvas == individualCanvas && targetState) || (activeCanvas == MapNetworkManager.Instance.sharedCanvas && MapNetworkManager.Instance.mapState.Value == MapState.Shared && GameManager.Instance.IsHost));
        placeIcon.GetComponent<UIElement>().OnElementToggleEnable(targetState);
        pencilIcon.GetComponent<UIElement>().OnElementToggleEnable(targetState);
        eraserIcon.GetComponent<UIElement>().OnElementToggleEnable(targetState);
        trashIcon.GetComponent<UIElement>().OnElementToggleEnable(targetState);
        saveIcon.transform.GetChild(0).gameObject.SetActive(targetState);
        saveIcon.transform.GetChild(1).gameObject.SetActive(!targetState);
        if (GameManager.Instance.isMaster) toolBar.SetActive(false);
        if (activeCanvas) ToggleColorHighlight(false);
    }

    public void OnSendMapData()
    {
        // serialize the local map so the server can share it
        List<MapElementData> mapElements = new List<MapElementData>();
        foreach (var icon in activeDrawDots)
        {
            if (icon == null) continue; // NOTE: These checks should prevent failed map data sending
            Vector2 normalisedPosition = UIManager.Instance.GetNormalizedPosition(icon.transform.parent.GetComponent<RectTransform>(), icon.transform.position);
            mapElements.Add(new MapElementData{iconPrefab = "DrawDot", iconSprite = "DrawDot", iconPosition = normalisedPosition, iconScale = icon.transform.localScale});
        }
        foreach (var icon in activeIcons)
        {
            if (icon == null) continue;
            Vector2 normalisedPosition = UIManager.Instance.GetNormalizedPosition(icon.transform.parent.GetComponent<RectTransform>(), icon.transform.position);
            mapElements.Add(new MapElementData{iconPrefab = "MapIcon", iconSprite = icon.name, iconPosition = normalisedPosition, iconScale = icon.transform.localScale});
        }

        // prevent sending empty data
        if (mapElements.Count == 0) return;
        Debug.Log("MapManager: Sending map data from: " + GameManager.Instance.FetchPersistentPlayerId());
        MapNetworkManager.Instance.SpawnMapInstanceServerRpc(mapElements.ToArray());            
    }

    public void UpdateMapReadyState()
    {
        string targetText = GameManager.Instance.playerList.Count(p => p.IsMapReady.Value == true) + "/" + GameManager.Instance.playerList.Count;
        saveIcon.transform.Find("ReadyText").GetComponent<TMP_Text>().text = targetText;
    }

    public void OnMapStateChanged(MapState oldValue, MapState newState)
    {
        if (Instance == null) return;
        switch (newState)
        {
            case MapState.Shared:
                // upload the local map before the shared view opens
                if (oldValue != MapState.Null) OnSendMapData();
                if (GameManager.Instance.isMaster) OnToggleMapEditability(false);
                break;
            case MapState.Explore:
                // editing is over once the maze starts
                OnToggleMapEditability(false);
                break;
            case MapState.Individual:
                OnToggleMapEditability(false);
                break;
        }
    }

    public void ToggleMapObject(bool targetState) => mapObject.SetActive(targetState);
    public void ToggleMapUI(bool targetState) => mapUI.SetActive(targetState);
}

public struct MapElementData : INetworkSerializable
{
    public string iconPrefab;
    public string iconSprite;
    public Vector2 iconPosition;
    public Vector2 iconScale;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref iconPrefab);
        serializer.SerializeValue(ref iconSprite);
        serializer.SerializeValue(ref iconPosition);
        serializer.SerializeValue(ref iconScale);
    }
}
