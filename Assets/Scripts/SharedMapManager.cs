using System.Collections.Generic;
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
    [SerializeField] private float dragSmoothing = 25f;
    [SerializeField] private bool enablePlacement;
    [SerializeField] private bool enableDiscard;

    [Header("Draw Data")]
    [SerializeField] private GameObject drawDot;
    [SerializeField] private float maxAllowedDots = 500f;
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

    /*public void OnMapToggle()
    {
        isMapActive = !isMapActive;
        mapObject.SetActive(!mapObject.activeSelf);
    }*/

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
        //SetDrawDotRaycastState(false);
    }

    public void OnStopElementDrag(GameObject targetElement)
    {
        // destroy element if its dropped over a discard allowed area
        if (enableDiscard)
        {
            if (!NetworkManager.IsHost && targetElement.GetComponent<NetworkObject>())
            {
                GameManager.Instance.DestroyElementServerRpc(targetElement.GetComponent<NetworkObject>().NetworkObjectId);        
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
                    GameManager.Instance.DestroyElementServerRpc(targetElement.GetComponent<NetworkObject>().NetworkObjectId);        
                }
                else
                {
                    Destroy(targetElement);
                }
                return;
            }

            GameManager.Instance.MoveMapElementServerRpc(targetElement.GetComponent<NetworkObject>().NetworkObjectId, savedElementPosition);
        }

        // reset raycast target state
        targetElement.GetComponent<Image>().raycastTarget = true;

        // conditionally enable drawdot raycast state
        //SetDrawDotRaycastState(activeTool == eraserIcon);

        //NETCODE
        if (savedElementPosition == Vector3.zero)
        {
            GameManager.Instance.SpawnMapElementServerRpc("MapIcon", targetElement.name, targetElement.transform.localPosition);
            Destroy(targetElement);
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
        if (!NetworkManager.IsHost && savedElementPosition != Vector3.zero)
        {
            GameManager.Instance.MoveMapElementServerRpc(targetElement.GetComponent<NetworkObject>().NetworkObjectId, targetPosition);            
        }
        else
        {
            targetElement.transform.position = Vector3.Lerp(targetElement.transform.position, targetPosition, Time.deltaTime * dragSmoothing);            
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
        if (activeTool != pencilIcon || !enablePlacement || InputManager.Instance.lookAction.ReadValue<Vector2>() == Vector2.zero) return;

        // instantiate new dots in world space based on mouse position
        Vector3 worldPosition = InputManager.Instance.mousePosition;
        worldPosition.z = GameManager.Instance.mapCamera.nearClipPlane + 1f;
        Vector3 targetPosition = GameManager.Instance.mapCamera.ScreenToWorldPoint(worldPosition);
        GameObject newDot = Instantiate(drawDot, targetPosition, Quaternion.Euler(0f, 0f, 0f), transform);
        newDot.transform.localEulerAngles = Vector3.zero;
        newDot.SetActive(true);

        GameManager.Instance.SpawnMapElementServerRpc("DrawDot", "DrawDot", newDot.transform.localPosition);
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
        GameManager.Instance.ClearMapServerRpc();
    }

    public void OnEraseLine()
    {
        if (activeTool != eraserIcon || InputManager.Instance.lookAction.ReadValue<Vector2>() == Vector2.zero) return;

        if (!activeHoveredDot && !activeHoveredDot.GetComponent<NetworkObject>()) return;

        if (!NetworkManager.IsHost)
        {
            GameManager.Instance.DestroyElementServerRpc(activeHoveredDot.GetComponent<NetworkObject>().NetworkObjectId);        
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
}
