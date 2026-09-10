using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
    [SerializeField] private GameObject drawDot;
    //[SerializeField] private float maxAllowedDots = 500f;
    public GameObject activeHoveredDot;
    [SerializeField] private GameObject activeTool;
    [SerializeField] private GameObject pencilIcon;
    [SerializeField] private GameObject eraserIcon;
    [SerializeField] private GameObject trashIcon;
    [SerializeField] private GameObject saveIcon;
    void Start()
    {
        Instance = this;

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
            GameObject newIcon = Instantiate(Resources.Load<GameObject>("MapIconOld"), iconPile.transform);
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

    public void SetupElementTriggers(GameObject targetElement)
    {
        if (targetElement.GetComponent<EventTrigger>() == null)
        {
            targetElement.AddComponent<EventTrigger>();
        }

        // pointer down trigger
        EventTrigger.Entry pointerDownEntry = new EventTrigger.Entry() {eventID = EventTriggerType.PointerDown};
        pointerDownEntry.callback.AddListener((eventData) => { OnElementPointerDown(targetElement); });
        targetElement.GetComponent<EventTrigger>().triggers.Add(pointerDownEntry);

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
        if (activeTool == null) return;

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

    public void OnElementPointerDown(GameObject targetElement)
    {
        if (activeTool == null) return;

        // get ownership
        if (targetElement.GetComponent<NetworkObject>())
        {
            SetElementOwnershipServerRpc(targetElement.GetComponent<NetworkObject>().NetworkObjectId);
        }
    }

    public void OnElementDrag(GameObject targetElement)
    {
        // force object to appear on top
        targetElement.transform.SetAsLastSibling();

        // follow mouse position with object
        Vector3 worldPosition = InputManager.Instance.mousePosition;
        worldPosition.z = GameManager.Instance.mapCamera.nearClipPlane + 1f;
        Vector3 targetPosition = GameManager.Instance.mapCamera.ScreenToWorldPoint(worldPosition);
        targetElement.transform.position = Vector3.Lerp(targetElement.transform.position, targetPosition, Time.deltaTime * 45f);
    }

    public void OnStopElementDrag(GameObject targetElement)
    {
        // destroy element if its dropped over a discard allowed area
        if (enableDiscard)
        {
            if (!NetworkManager.IsHost && targetElement.GetComponent<NetworkObject>())
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
                if (!NetworkManager.IsHost && targetElement.GetComponent<NetworkObject>())
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
            SpawnMapElementServerRpc("MapIcon", targetElement.name, targetElement.transform.localPosition);
            Destroy(targetElement);
        }
        else
        {
            // reset ownership
            SetElementOwnershipServerRpc(targetElement.GetComponent<NetworkObject>().NetworkObjectId, true);
        }
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
        worldPosition.z = GameManager.Instance.mapCamera.nearClipPlane + 1f;
        Vector3 targetPosition = GameManager.Instance.mapCamera.ScreenToWorldPoint(worldPosition);
        GameObject newDot = Instantiate(drawDot, targetPosition, Quaternion.identity, transform);
        newDot.GetComponent<RectTransform>().sizeDelta = new Vector3(.02f, 0.02f);
        newDot.SetActive(true);

        SpawnMapElementServerRpc("DrawDot", "DrawDot", newDot.transform.localPosition);
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

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SpawnMapElementServerRpc(string iconPrefab, string iconSprite, Vector3 iconPosition)
    {
        Debug.Log("SMM: Spawning new object with type: " + iconPrefab);
        GameObject newObject = Instantiate(Resources.Load<GameObject>(iconPrefab + "2"));
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
    public void SpawnMapElementsClientRpc(ulong clientId, MapElementData[] mapElements)
    {
        if (clientId == NetworkManager.LocalClientId) return;

        GameObject targetMap = MapManager.Instance.mapObjectP1.transform.childCount > 1 ? MapManager.Instance.mapObjectP2 : MapManager.Instance.mapObjectP1;
        string targetName = GameManager.Instance.playerList.FirstOrDefault(p => p.OwnerClientId == clientId).PlayerName.Value.ToString();
        targetMap.transform.Find("Title").GetComponent<TMP_Text>().text = targetName;

        Debug.Log("SMM: Spawning map objects for player: " + targetName);
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
            newObject.transform.localEulerAngles = Vector3.zero;
            newObject.transform.localScale = new Vector3(1f, 1f, 1f);
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SpawnMapElementsServerRpc(MapElementData[] mapElements, RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        SpawnMapElementsClientRpc(clientId, mapElements);
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
}
