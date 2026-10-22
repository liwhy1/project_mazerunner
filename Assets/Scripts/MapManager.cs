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
    public GameObject mapObject;
    [NonSerialized] public Dictionary<ulong, GameObject> pendingPlacements = new();
    [SerializeField] private bool isNetworked;
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
    public GameObject mapBackground;
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
                mapBackground = Instantiate(Resources.Load<GameObject>("MapPrefabs/Background"));
                mapBackground.GetComponent<NetworkObject>().Spawn();
                mapBackground.GetComponent<NetworkObject>().TrySetParent(mapObject);
            }
            mapObject = gameObject;
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
        SetActiveTool(placeIcon);

        // conditionally disable save icon
        if (isNetworked && !IsHost)
        {
            saveIcon.SetActive(false);
        }

        SetupMapTriggers();
        if (mapBackground)
        {
            SetupLayout();
        }
    }

    public void SetupLayout()
    {
        if (!mapBackground) mapBackground = mapObject.transform.Find("Background(Clone)").gameObject;
        mapBackground.transform.SetSiblingIndex(1);
        mapBackground.GetComponent<RectTransform>().anchoredPosition = new Vector2(307.85f, 0f);
        mapObject.transform.Find("InnerLayer").GetComponent<RectTransform>().anchoredPosition = new Vector2(307.85f, -40f);
        mapObject.GetComponent<RectTransform>().anchoredPosition = new Vector2(-307.85f, mapObject.GetComponent<RectTransform>().anchoredPosition.y);
        mapObject.GetComponent<RectTransform>().localScale = Vector2.one;
        pointerCircle.SetActive(false);
    }

    private void Update()
    {
        bool isMapActive = InventoryManager.Instance.isInventoryActive && InventoryManager.Instance.mapSharedViewPage.activeSelf;
        bool isMasterActive = GameManager.Instance.isMaster && (MapNetworkManager.Instance.mapState.Value == MapState.Shared || MapNetworkManager.Instance.mapState.Value == MapState.Explore);
        if (isNetworked && mapBackground) mapObject.transform.localPosition = new Vector2(-307.85f, isMapActive || isMasterActive ? 40f : -3000f);

        if (!mapBackground && transform.Find("Background(Clone)"))
        {
            SetupLayout();
        }

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
        UIManager.Instance.AddEventTrigger(saveIcon.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, () => OnMapToggleReady(true, GameManager.Instance.lockJournalAfterOwnMap && !isNetworked ? true : false));
    }

    private void HoverHandler()
    {
        PointerEventData pointerData = new PointerEventData(EventSystem.current) { position = Pointer.current.position.ReadValue() };

        List<RaycastResult> castResults = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, castResults);


        // handle hover states
        bool isSliderHovered = castResults.Any(r => r.gameObject.GetComponentInParent<Slider>());
        isMapHovered = mapBackground && castResults.Any(r => r.gameObject == mapBackground && !isSliderHovered);
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
    }

    private void OnStartElementDrag(GameObject targetElement)
    {
        if (activeTool == null) return;

        // request ownership
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

        SetElementPosition(targetElement, targetPosition);
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
        targetElement.transform.SetParent(mapBackground.transform, true);
        if (targetElement.GetComponent<NetworkObject>()) SetElementPosition(targetElement, Pointer.current.position.ReadValue(), true);

        // destroy element if its dropped over a discard allowed area
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
        if (savedElementPosition == Vector2.zero && isNetworked)
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
        if (isNetworked && targetElement.GetComponent<NetworkObject>())
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
        mapBackground.GetComponent<Image>().color = targetState ? greenHighlight : Color.white;
        iconPile.GetComponent<Image>().color = targetState ? redHighlight : Color.white;
    }

    private void PileAnimationHandler()
    {
        if (!iconPile) return;
        float targetY = !isPileHovered ? -520f : -395f;
        iconPile.transform.localPosition = Vector2.Lerp(iconPile.transform.localPosition, new Vector2(iconPile.transform.localPosition.x, targetY), Time.deltaTime * 10f);
    }

    public IEnumerator MapMoveHandler(bool shouldCenter)
    {
        StartCoroutine(InventoryManager.Instance.JournalMoveHandler(shouldCenter));
        yield return new WaitForSeconds(shouldCenter ? .1f : 0f);
        InventoryManager.Instance.MapTabMoveHandler(shouldCenter);

        Vector2 targetPosition = new Vector2(shouldCenter ? -307f : 0f, mapObject.transform.GetComponent<RectTransform>().anchoredPosition.y);
        StartCoroutine(UIManager.Instance.UIAnimator(mapObject.transform.GetComponent<RectTransform>(), targetPosition));

        targetPosition = new Vector2(shouldCenter ? -307f : 0f, mapObject.transform.Find("InnerLayer").GetComponent<RectTransform>().anchoredPosition.y);
        StartCoroutine(UIManager.Instance.UIAnimator(mapObject.transform.Find("InnerLayer").GetComponent<RectTransform>(), targetPosition));
    }

    public void MapLoadAnimator()
    {
        MapManager.Instance.OnMapToggleReady(false);
        isLoaded = true;
    }

    public void OnDrawLine()
    {
        // conditionally play draw sfx
        if (!AudioManager.Instance.IsPlaying()) AudioManager.Instance.OnDraw();

        // instantiate new dot based on mouse position
        Vector2 targetPosition = Pointer.current.position.ReadValue();
        GameObject objectPrefab = Resources.Load<GameObject>("MapPrefabs/" + (isNetworked ? "SharedDrawDot" : "DrawDot"));
        GameObject newDot = Instantiate(objectPrefab, targetPosition, Quaternion.identity, mapBackground.transform);
        newDot.transform.localScale = Vector2.one * pencilSizeSlider.value;
        newDot.transform.SetSiblingIndex(0);
        newDot.SetActive(true);

        // store active dots in a list
        activeDrawDots.Add(newDot);

        // conditionally spawn network object
        if (isNetworked)
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
        foreach (Transform element in mapBackground.transform)
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
                if (isNetworked && !IsHost)
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
        placeIcon.GetComponent<UIElement>().OnElementToggleEnable(!isMapready);
        pencilIcon.GetComponent<UIElement>().OnElementToggleEnable(!isMapready);
        eraserIcon.GetComponent<UIElement>().OnElementToggleEnable(!isMapready);
        trashIcon.GetComponent<UIElement>().OnElementToggleEnable(!isMapready);
        SetActiveTool(!isMapready ? placeIcon : null);
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
        placeIcon.GetComponent<UIElement>().OnElementDisable();
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
        Debug.Log("MapManager: Sending map data from: " + GameManager.Instance.FetchPlayerDataById(NetworkManager.LocalClientId).PlayerName.Value.ToString());
        MapNetworkManager.Instance.SpawnMapInstanceServerRpc(mapElements.ToArray());            
    }

    public void UpdateMapReadyState()
    {
        string targetText = GameManager.Instance.playerList.Count(p => p.IsMapReady.Value == true) + "/" + GameManager.Instance.playerList.Count;
        saveIcon.transform.Find("ReadyText").GetComponent<TMP_Text>().text = targetText;
    }
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
