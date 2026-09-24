using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class EditorManager : MonoBehaviour
{
    public static EditorManager Instance;

    public Camera editorCamera;
    private Vector3 cameraDefaultPosition;
    private Quaternion cameraDefaultRotation;
    [SerializeField] private GameObject iconPile;
    [SerializeField] private GameObject iconHolder;
    public List<GameObject> structurePrefabs = new List<GameObject>();
    public GameObject editorMapInstance;
    private GameObject selectedIcon;
    private GameObject selectedStructure;
    private Transform structureHolder;

    [Header("Tools")]
    [SerializeField] private GameObject activeTool;
    [SerializeField] private GameObject selectTool;
    [SerializeField] private GameObject pencilTool;
    [SerializeField] private GameObject eraserTool;
    [SerializeField] private GameObject trashIcon;
    [SerializeField] private GameObject readyIcon;

    [Header("Editor")]
    [SerializeField] private GameObject structureEditorPile;
    [SerializeField] private GameObject structureViewIcon;
    [SerializeField] private Slider structureSizeSlider;
    [SerializeField] private Slider structureRotationSlider;

    private void Awake()
    {
        Instance = this;

        structurePrefabs = Resources.LoadAll<GameObject>("TerrainObjects").ToList();
        structureEditorPile.SetActive(false);
        SetActiveTool(pencilTool);
        GenerateIcons();
    }

    public void OnEnableEditor()
    {
        if (editorMapInstance == null)
        {
            editorMapInstance = Instantiate(Resources.Load<GameObject>("TerrainObjects/TerrainTemplateObject"));
            editorCamera = editorMapInstance.transform.Find("TerrainCamera").GetComponent<Camera>();
            cameraDefaultPosition = editorCamera.transform.position;
            cameraDefaultRotation = editorCamera.transform.rotation;
            structureHolder = editorMapInstance.transform.Find("StructureHolder");
        }
        else editorMapInstance.SetActive(true);
    }

    public void OnDisableEditor()
    {
        if (editorMapInstance) editorMapInstance.SetActive(false);
    }
 
    private void GenerateIcons()
    {
        // load icons from resources folder
        var mapIcons = Resources.LoadAll<GameObject>("TerrainObjects");

        // setup icons
        foreach (var icon in mapIcons)
        {
            if (icon.name.Contains("Template")) continue;
            InstantiateNewIcon(icon.name);
        }

        // select first icon
        OnIconSelect(iconPile.transform.GetChild(0).gameObject);
    }

    private void InstantiateNewIcon(string structureName)
    {
        GameObject newIcon = Instantiate(Resources.Load<GameObject>("Elements/SquareIcon"), iconPile.transform);
        newIcon.name = structureName;
        newIcon.transform.localPosition = Vector3.zero;
        newIcon.transform.localEulerAngles = Vector3.zero;
        Sprite targetSprite = Resources.LoadAll<Sprite>("MapIconsNew").FirstOrDefault(s => s.name.Contains(structureName.ToLower().Substring(0, 4)));
        if (targetSprite) newIcon.transform.Find("Sprite").GetComponent<Image>().sprite = targetSprite;
        newIcon.transform.Find("Sprite").GetComponent<Image>().preserveAspect = true;
        newIcon.transform.Find("Text").gameObject.SetActive(true);
        newIcon.transform.Find("Text").GetComponent<TMP_Text>().text = structureName;
        SetupElementTriggers(newIcon);
    }

    private void SetupElementTriggers(GameObject targetElement)
    {
        targetElement.GetComponent<Image>().raycastTarget = true;
        if (targetElement.GetComponent<EventTrigger>() == null) targetElement.AddComponent<EventTrigger>();
        if (targetElement.GetComponent<UIElement>() == null) targetElement.AddComponent<UIElement>();
        targetElement.GetComponent<UIElement>().enableGrow = false;
        targetElement.GetComponent<UIElement>().resetOnClick = false;

        EventTrigger.Entry clickEntry = new EventTrigger.Entry() {eventID = EventTriggerType.PointerClick};
        clickEntry.callback.AddListener((eventData) => { OnIconSelect(targetElement); });
        targetElement.GetComponent<EventTrigger>().triggers.Add(clickEntry);
    }

    private void OnIconSelect(GameObject targetIcon)
    {
        foreach (Transform icon in iconPile.transform)
        {
            if (icon.GetComponent<UIElement>()) icon.GetComponent<UIElement>().OnElementDeSelect();
        }
        targetIcon.GetComponent<UIElement>().OnElementSelect();
        selectedIcon = targetIcon;
    }

    public void SetActiveTool(GameObject targetTool)
    {
        if (targetTool == null) return;
        activeTool = targetTool;

        // reset structure editor
        selectedStructure = null;
        structureEditorPile.SetActive(false);

        // reset icon states
        pencilTool.GetComponent<UIElement>().OnElementDeSelect();
        selectTool.GetComponent<UIElement>().OnElementDeSelect();
        eraserTool.GetComponent<UIElement>().OnElementDeSelect();
        targetTool.GetComponent<UIElement>().OnElementSelect();
    }

    private void OnPlaceStructure(string targetElement)
    {
        // select prefab
        GameObject targetPrefab = structurePrefabs.FirstOrDefault(s => s.name == targetElement);
        if (!targetPrefab) return;

        Ray ray = editorCamera.ScreenPointToRay(Pointer.current.position.ReadValue());
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Vector3 targetPosition = hit.point;
            Quaternion targetRotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(targetPrefab.transform.forward, hit.normal).normalized, hit.normal);

            GameObject newStructure = Instantiate(targetPrefab, targetPosition, targetRotation, structureHolder);
            UpdateStructureTransform(newStructure);
        }
    }

    private void OnDestroyStructure()
    {
        Ray ray = editorCamera.ScreenPointToRay(Pointer.current.position.ReadValue());
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            foreach (Transform structure in structureHolder.transform)
            {
                if (hit.collider.transform.IsChildOf(structure))
                {
                    Destroy(structure.gameObject);
                }
            }
        }
    }

    private void UpdateStructureTransform(GameObject targetStructure)
    {
        if (targetStructure.transform.childCount == 0) SettleObjectToGround(targetStructure);
        foreach (Transform part in targetStructure.transform)
        {
            // TODO: this should be based on names
            if (part.name.Contains("Lift")) continue;
            SettleObjectToGround(part.gameObject);
        }
    }

    private void SettleObjectToGround(GameObject targetObject)
    {
        targetObject.transform.position += Vector3.up * 5f;
        List<RaycastHit> hitObjects = Physics.RaycastAll(targetObject.transform.position, Vector3.down).ToList();
        RaycastHit backupObject = hitObjects.FirstOrDefault(o => o.collider.gameObject.name == "InnerTerrain");
        if (backupObject.collider != null)
        {
            var element = hitObjects[hitObjects.IndexOf(backupObject)];
            hitObjects.RemoveAt(hitObjects.IndexOf(backupObject));
            hitObjects.Add(element);
        }
        foreach (var hit in hitObjects)
        {
            // prevent placing on the river
            if (hit.collider.gameObject.name.Contains("River")) continue;
            Vector3 forward = targetObject.transform.forward;
            if (targetObject.transform.parent != structureHolder)
            {
                Transform parent = targetObject.transform.parent;
                Quaternion parentRotation = parent.rotation;
                forward = parentRotation * Vector3.forward;
            }
            forward = Vector3.ProjectOnPlane(forward, hit.normal).normalized;
            Quaternion targetRotation = Quaternion.LookRotation(forward, hit.normal);

            targetObject.transform.SetPositionAndRotation(hit.point, targetRotation);
            break;
        }
    }

    public void OnUpdateStructureRotation(Slider targetSlider) // should be -360 - 360
    {
        if (selectedStructure == null) return;
        selectedStructure.transform.eulerAngles = new Vector3(selectedStructure.transform.eulerAngles.x, targetSlider.value, selectedStructure.transform.eulerAngles.z);
        UpdateStructureTransform(selectedStructure);
    }

    public void OnUpdateStructureScale(Slider targetSlider) // should be 0.1 - 2;
    {
        if (selectedStructure == null) return;
        selectedStructure.transform.localScale = Vector3.one * targetSlider.value;
        UpdateStructureTransform(selectedStructure);
    }

    public void OnUpdateStructurePosition(string targetDirection) // god help me this is stupid
    {
        if (selectedStructure == null) return;

        if (targetDirection == "up" || targetDirection == "down")
        {
            selectedStructure.transform.position += Vector3.forward * (targetDirection == "up" ? 2 : -2f);
        }
        else
        {
            selectedStructure.transform.position += Vector3.right * (targetDirection == "right" ? 2 : -2f);   
        }
        UpdateStructureTransform(selectedStructure);
    }

    public void OnToggleStructureView()
    {
        if (selectedStructure && structureEditorPile && editorCamera.transform.position == cameraDefaultPosition)
        {
            editorCamera.transform.position = selectedStructure.transform.position + editorCamera.transform.up * 15f + -editorCamera.transform.forward * 12f;
            editorCamera.transform.LookAt(selectedStructure.transform);
        }
        else
        {
            editorCamera.transform.position = cameraDefaultPosition;
            editorCamera.transform.rotation = cameraDefaultRotation;
        }
    }

    public void OnPrimaryAction()
    {
        if (InputManager.Instance.IsPointerOverUI()) return;

        if (activeTool == selectTool)
        {
            // structure selection logic
            Ray ray = editorCamera.ScreenPointToRay(Pointer.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                foreach (Transform structure in structureHolder.transform)
                {
                    if (hit.collider.transform.IsChildOf(structure))
                    {
                        selectedStructure = structure.gameObject;
                        structureEditorPile.SetActive(true);
                        return;
                    }
                }
            }
            selectedStructure = null;
            structureEditorPile.SetActive(false);
        }
        else if (activeTool == pencilTool)
        {
            OnPlaceStructure(selectedIcon.name);
        }
        else if (activeTool == eraserTool)
        {
            OnDestroyStructure();
        }
    }

    public void OnClearStructures()
    {
        foreach (Transform structure in structureHolder.transform)
        {
            Destroy(structure.gameObject);
        }
    }

    public void OnScrollAction(float targetValue)
    {
        editorCamera.transform.position += editorCamera.transform.forward * (targetValue * -20);
    }
}