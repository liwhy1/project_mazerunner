using System.Linq;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SharedMapManager : NetworkBehaviour
{
    public static SharedMapManager Instance;

    [Header("Map Data")]
    public bool isMapActive;

    [Header("Icon Data")]
    [SerializeField] private GameObject iconPile;
    private Vector3 savedElementPosition;
    [SerializeField] private bool enablePlacement;
    [SerializeField] private bool enableDiscard;

    [Header("Draw Data")]
    //[SerializeField] private float maxAllowedDots = 500f;
    public GameObject activeHoveredDot;
    [SerializeField] private GameObject toolBar;
    [SerializeField] private GameObject activeTool;
    [SerializeField] private GameObject pencilIcon;
    [SerializeField] private GameObject eraserIcon;
    [SerializeField] private GameObject trashIcon;
    [SerializeField] private GameObject saveIcon;

    void Start()
    {
        Debug.Log("SMM: Setting up");
        Instance = this;

        // set active map tool
        SetActiveTool(pencilIcon);

        // generate icon objects
        GenerateIcons();

        if (!NetworkManager.IsHost && !GameManager.Instance.isOffline)
        {
            saveIcon.SetActive(false);
        }
    }

   private void GenerateIcons()
    {
        // load icons from resources folder
        var mapIcons = Resources.LoadAll<Sprite>("MapIcons");

        // setup icons
        foreach (var icon in mapIcons)
        {
            // we load the regual map icon here since the icon pile is not synced
            GameObject newIcon = Instantiate(Resources.Load<GameObject>("MapIcon"));
            newIcon.transform.SetParent(iconPile.transform);
            newIcon.name = icon.name;
            newIcon.GetComponent<Image>().sprite = icon;
            newIcon.transform.localPosition = Vector3.zero;
            newIcon.transform.localEulerAngles = Vector3.zero;
            // apply shared prefab size
            newIcon.transform.localScale = new Vector3(1f, 1f, 1f);
            GameObject newText = newIcon.transform.GetChild(0).gameObject;
            newText.transform.localScale = new Vector3(.1f, .1f, .1f);
            newText.transform.localPosition = new Vector3(0f, -7f, 0f);
            newText.GetComponent<TMP_Text>().text = icon.name.Remove(icon.name.Length - 6, 6);

            // setup event triggers
            SetupElementTriggers(newIcon);
        }
    }

    public void SetupElementTriggers(GameObject targetElement)
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

    public void OnStartElementDrag(GameObject targetElement)
    {
        if (activeTool == null || PlayerController.Instance.playerCamera.activeSelf) return;

        // get ownership
        if (targetElement.GetComponent<NetworkObject>())
        {
            SetElementOwnershipServerRpc(targetElement.GetComponent<NetworkObject>().NetworkObjectId);
        }

        // save element position
        savedElementPosition = targetElement.transform.position;

        // check if the element is dragged out of the pile
        if (targetElement.transform.parent == iconPile.transform)
        {
            // duplicate and replace original element
            var newElement = Instantiate(targetElement, targetElement.transform.position, targetElement.transform.rotation, iconPile.transform);
            newElement.name = targetElement.GetComponent<Image>().name;
            int siblingIndex = targetElement.transform.GetSiblingIndex();
            targetElement.transform.SetParent(transform);
            targetElement.transform.GetChild(0).gameObject.SetActive(false);
            newElement.transform.SetSiblingIndex(siblingIndex);
            SetupElementTriggers(newElement);

            // pile position shouldn't be saved, this will be used to destroy instead
            savedElementPosition = Vector3.zero;
        }

        // disable raycast target to allow detecting hover states under the element
        targetElement.GetComponent<Image>().raycastTarget = false;

        // disable drawdot raycast target
        SetDrawDotRaycastState(false);
    }

    public void OnElementDrag(GameObject targetElement)
    {
        if (activeTool == null || PlayerController.Instance.playerCamera.activeSelf) return;

        // force object to appear on top
        targetElement.transform.SetAsLastSibling();

        // follow mouse position with object
        Vector3 worldPosition = InputManager.Instance.pointerPosition;
        worldPosition.z = GameManager.Instance.mapCamera.nearClipPlane + 1f;
        Vector3 targetPosition = GameManager.Instance.mapCamera.ScreenToWorldPoint(worldPosition);
        targetElement.transform.position = Vector3.Lerp(targetElement.transform.position, targetPosition, Time.deltaTime * 45f);
    }

    public void OnStopElementDrag(GameObject targetElement)
    {
        if (activeTool == null || PlayerController.Instance.playerCamera.activeSelf) return;

        // destroy element if its dropped over a discard allowed area
        if (enableDiscard)
        {
            if (targetElement.GetComponent<NetworkObject>())
            {
                DestroyMapElementServerRpc(targetElement.GetComponent<NetworkObject>().NetworkObjectId);                
            }
            else
            {
                Destroy(targetElement);
            }
            return;
        }

        // return element to previous position if the current drop target is invalid
        if (!enablePlacement)
        {
            // this should only be true if the element wasn't place on the map yet, causing a saved position to "not exist"
            if (savedElementPosition == Vector3.zero)
            {
                if (targetElement.GetComponent<NetworkObject>())
                {
                    DestroyMapElementServerRpc(targetElement.GetComponent<NetworkObject>().NetworkObjectId);                
                }
                else
                {
                    Destroy(targetElement);
                }
                return;
            }

            MoveMapElementServerRpc(targetElement.GetComponent<NetworkObject>().NetworkObjectId, savedElementPosition);
        }

        // reset raycast target state
        targetElement.GetComponent<Image>().raycastTarget = true;

        // conditionally enable drawdot raycast state
        SetDrawDotRaycastState(activeTool == eraserIcon);

        // spawn networkobject if element was pulled from the pile
        if (savedElementPosition == Vector3.zero)
        {
            SpawnMapElementServerRpc("SharedMapIcon", targetElement.name, targetElement.transform.localPosition);
            Destroy(targetElement);
        }
    }

    public void OnDrawLine()
    {
        if (activeTool != pencilIcon || PlayerController.Instance.playerCamera.activeSelf || !enablePlacement || enableDiscard || InputManager.Instance.lookAction.ReadValue<Vector2>() == Vector2.zero) return;

        // instantiate new dots in world space based on mouse position
        Vector3 worldPosition = InputManager.Instance.pointerPosition;
        worldPosition.z = GameManager.Instance.mapCamera.nearClipPlane + 1f;
        Vector3 targetPosition = GameManager.Instance.mapCamera.ScreenToWorldPoint(worldPosition);
        GameObject newDot = Instantiate(Resources.Load<GameObject>("SharedDrawDot"), targetPosition, Quaternion.identity, transform);
        newDot.GetComponent<RectTransform>().sizeDelta = new Vector3(.02f, 0.02f);
        newDot.SetActive(true);

        SpawnMapElementServerRpc("SharedDrawDot", "DrawDot", newDot.transform.localPosition);
        Destroy(newDot);
    }

    public void SetupDotEventTriggers(GameObject targetDot)
    {
        // add pointer enter event to allowed detecting existance
        targetDot.AddComponent<EventTrigger>();
        EventTrigger.Entry enterHoverEntry = new EventTrigger.Entry() {eventID = EventTriggerType.PointerEnter};
        enterHoverEntry.callback.AddListener((eventData) => { SetHoveredDot(targetDot); });
        targetDot.GetComponent<EventTrigger>().triggers.Add(enterHoverEntry);
        targetDot.GetComponent<Image>().raycastTarget = false;
    }

    public void OnClearMap()
    {
        if (GameManager.Instance.isOffline) return;
        ClearMapServerRpc();
    }

    public void OnEraseLine()
    {
        if (activeTool != eraserIcon || InputManager.Instance.lookAction.ReadValue<Vector2>() == Vector2.zero) return;

        if (!activeHoveredDot && !activeHoveredDot.GetComponent<NetworkObject>()) return;

        if (!NetworkManager.IsHost)
        {
            DestroyMapElementServerRpc(activeHoveredDot.GetComponent<NetworkObject>().NetworkObjectId);        
        }
        else
        {
            Destroy(activeHoveredDot);
        }
    }

    private void SetDrawDotRaycastState(bool targetState)
    {
        foreach (Transform child in transform)
        {
            if (child.name.Contains("Dot"))
            {
                child.GetComponent<Image>().raycastTarget = targetState;                
            }
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

    public void OnMapToggleReady()
    {
        toolBar.SetActive(false);
        iconPile.SetActive(false);
        saveIcon.SetActive(false);
        SetActiveTool(null);

        if (!GameManager.Instance.isOffline)
        {
            OnSharedMapReadyServerRpc();
        }
        else
        {
            OnMapFinished();
        }
    }

    public void OnMapFinished()
    {
        toolBar.SetActive(false);
        iconPile.SetActive(false);
        saveIcon.SetActive(false);
        SetActiveTool(null);
        MapManager.Instance.individualViewButton.GetComponent<UIElement>().OnElementEnable();
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

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SpawnMapElementServerRpc(string iconPrefab, string iconSprite, Vector3 iconPosition)
    {
        Debug.Log("SMM: Spawning new object with type: " + iconPrefab);
        GameObject newObject = Instantiate(Resources.Load<GameObject>(iconPrefab));
        newObject.GetComponent<NetworkObject>().Spawn();
        newObject.transform.SetParent(transform, false);
        newObject.transform.localPosition = new Vector3(iconPosition.x, iconPosition.y, 1f);

        ulong objectId = newObject.GetComponent<NetworkObject>().NetworkObjectId;
        SetMapElementDataClientRpc(objectId, iconSprite);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void MoveMapElementServerRpc(ulong targetElement, Vector3 targetPosition)
    {
        if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetElement, out NetworkObject targetObject))
        {
            targetObject.transform.position = new Vector3(targetPosition.x, targetPosition.y, 1f);
        }
    }

    [ClientRpc]
    public void SetMapElementDataClientRpc(ulong targetElement, string targetSprite)
    {
        if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetElement, out NetworkObject targetObject))
        {
            Debug.Log("SMM: Updating element data for: " + targetObject);
            targetObject.name = targetSprite;
            if (targetSprite == "DrawDot")
            {
                SetupDotEventTriggers(targetObject.gameObject);
            }
            else
            {
                targetObject.GetComponent<Image>().sprite = Resources.LoadAll<Sprite>("MapIcons").FirstOrDefault(i => i.name.Contains(targetSprite));
                targetObject.transform.GetChild(0).GetComponent<TMP_Text>().text = "";
                SetupElementTriggers(targetObject.gameObject);
            }
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void DestroyMapElementServerRpc(ulong targetElement)
    {
        Debug.Log("SMM: Destroying element: " + targetElement);
        if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetElement, out NetworkObject targetObject))
        {
            targetObject.Despawn();
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void ClearMapServerRpc()
    {
        Debug.Log("SMM: Clearing map");
        foreach (Transform element in transform)
        {
            if (element.name.Contains("DrawDot") || element.name.Contains("Icon"))
            {
                element.gameObject.GetComponent<NetworkObject>().Despawn();
            }
        }
    }

    [ClientRpc]
    public void SpawnMapInstanceClientRpc(int persistentId, string playerName, MapElementData[] mapElements)
    {
        // TODO: this might not sync correctly on player reconnects
        GameObject targetMap = persistentId == 0 ? MapManager.Instance.mapObjectP0 : persistentId == 1 ? MapManager.Instance.mapObjectP1 : MapManager.Instance.mapObjectP2;
        targetMap.transform.Find("Title").GetComponent<TMP_Text>().text = playerName;

        Debug.Log("SMM: Spawning map objects for player: " + playerName);
        foreach (var element in mapElements)
        {
            GameObject newObject = Instantiate(Resources.Load<GameObject>(element.iconPrefab));
            newObject.name = element.iconSprite;
            if (element.iconPrefab == "MapIcon")
            {
                newObject.GetComponent<Image>().sprite = Resources.LoadAll<Sprite>("MapIcons").FirstOrDefault(i => i.name.Contains(element.iconSprite));                
                newObject.transform.GetChild(0).GetComponent<TMP_Text>().text = "";
            }
            newObject.transform.SetParent(targetMap.transform);
            newObject.transform.localPosition = element.iconPosition;
            newObject.transform.localScale = new Vector3(1f, 1f, 1f);
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SpawnMapInstanceServerRpc(MapElementData[] mapElements, RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        int persistentId = GameManager.Instance.playerList.FirstOrDefault(p => p.OwnerClientId == clientId).persistentPlayerId.Value;
        string playerName = GameManager.Instance.playerList.FirstOrDefault(p => p.OwnerClientId == clientId).PlayerName.Value.ToString();
        SpawnMapInstanceClientRpc(persistentId, playerName, mapElements);
    }

    [ClientRpc]
    public void OnSendMapInsanceClientRpc()
    {
        MapManager.Instance.OnSendMapData();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SetElementOwnershipServerRpc(ulong targetElement, bool resetOwnership = false, RpcParams rpcParams = default)
    {
        if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetElement, out NetworkObject targetObject))
        {
            ulong clientId = resetOwnership ? NetworkManager.ServerClientId : rpcParams.Receive.SenderClientId;
            targetObject.ChangeOwnership(clientId);    
        }
    }

    [ClientRpc]
    public void OnSharedMapReadyClientRpc()
    {
        OnMapFinished();
    }

    [ServerRpc]
    public void OnSharedMapReadyServerRpc()
    {
        OnSharedMapReadyClientRpc();
    }
}
