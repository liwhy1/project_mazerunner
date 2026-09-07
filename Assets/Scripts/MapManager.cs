using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MapManager : MonoBehaviour
{
    public static MapManager Instance;
    public bool isMapActive;
    [SerializeField] private GameObject mapObject;
    [SerializeField] private GameObject iconPile;
    private Vector3 savedElementPosition;
    [SerializeField] private float dragSmoothing = 25f;

    [SerializeField] private GameObject drawDot;
    [SerializeField] private float maxAllowedDots = 500f;
    private List<GameObject> activeDrawDots = new List<GameObject>();
    [SerializeField] private bool enablePlacement;
    [SerializeField] private bool enableDiscard;
    
    private void Start()
    {
        Instance = this;
        isMapActive = false;

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
            newIcon.GetComponent<RectTransform>().sizeDelta = new Vector2(0.1f, 0.1f);
            newIcon.transform.SetParent(iconPile.transform);

            SetupElementTriggers(newIcon);
        }
    }

    private void SetupElementTriggers(GameObject targetElement)
    {
        if (targetElement.GetComponent<EventTrigger>() == null)
        {
            targetElement.AddComponent<EventTrigger>();
        }

        EventTrigger.Entry beginDragEntry = new EventTrigger.Entry();
        beginDragEntry.eventID = EventTriggerType.BeginDrag;
        beginDragEntry.callback.AddListener((eventData) => { OnStartElementDrag(targetElement); });

        EventTrigger.Entry dragEntry = new EventTrigger.Entry();
        dragEntry.eventID = EventTriggerType.Drag;
        dragEntry.callback.AddListener((eventData) => { OnElementDrag(targetElement); });

        EventTrigger.Entry endDragEntry = new EventTrigger.Entry();
        endDragEntry.eventID = EventTriggerType.EndDrag;
        endDragEntry.callback.AddListener((eventData) => { OnStopElementDrag(targetElement); });

        EventTrigger.Entry enterHoverEntry = new EventTrigger.Entry();
        enterHoverEntry.eventID = EventTriggerType.PointerEnter;
        enterHoverEntry.callback.AddListener((eventData) => { OnEnablePlacement(); });

        EventTrigger.Entry exitHoverEntry = new EventTrigger.Entry();
        exitHoverEntry.eventID = EventTriggerType.PointerExit;
        exitHoverEntry.callback.AddListener((eventData) => { OnDisablePlacement(); });

        targetElement.GetComponent<EventTrigger>().triggers.Add(beginDragEntry);
        targetElement.GetComponent<EventTrigger>().triggers.Add(dragEntry);
        targetElement.GetComponent<EventTrigger>().triggers.Add(endDragEntry);
        targetElement.GetComponent<EventTrigger>().triggers.Add(enterHoverEntry);
        targetElement.GetComponent<EventTrigger>().triggers.Add(exitHoverEntry);
    }

    public void OnMapToggle()
    {
        isMapActive = !isMapActive;
        mapObject.SetActive(!mapObject.activeSelf);
    }

    public void OnStartElementDrag(GameObject targetElement)
    {
        savedElementPosition = targetElement.transform.position;

        // check if the element is dragged out of the pile
        if (targetElement.transform.parent == iconPile.transform)
        {
            var newElement = Instantiate(targetElement, targetElement.transform.position, targetElement.transform.rotation, iconPile.transform);
            int siblingIndex = targetElement.transform.GetSiblingIndex();
            targetElement.transform.SetParent(mapObject.transform);
            newElement.transform.SetSiblingIndex(siblingIndex);
            SetupElementTriggers(newElement);

            // pile position shouldn't be saved, this will be used to destroy instead
            savedElementPosition = Vector3.zero;
        }
        targetElement.GetComponent<Image>().raycastTarget = false;
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
            // this should only be true if the element wasn't place on the map yet, causing a saved position to not exist
            if (savedElementPosition == Vector3.zero)
            {
                Destroy(targetElement);
                return;
            }

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
        if (!enablePlacement && InputManager.Instance.lookAction.ReadValue<Vector2>() != Vector2.zero) return;

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

    public void OnEnablePlacement() => enablePlacement = true;
    public void OnDisablePlacement() => enablePlacement = false;
    public void OnEnableDiscard() => enableDiscard = true;
    public void OnDisableDiscard() => enableDiscard = false;
}
