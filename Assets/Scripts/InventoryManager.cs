using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance;

    [Header("Inventory Data")]
    public bool isInventoryActive;
    public GameObject inventoryObject;
    public GameObject pageBackground;
    public GameObject buttonLayout;
    public GameObject backButton;
    public bool isLocked;
    [SerializeField] private GameObject journalObject;
    [SerializeField] private GameObject mapObject;
    [SerializeField] private GameObject noteObject;
    [SerializeField] private GameObject editorObject;

    [Header("Journal Data")]
    [SerializeField] private ScrollRect journalPageView;
    [SerializeField] private TMP_Text journalPageNumber;
    [SerializeField] private GameObject journalText;
    [SerializeField] private GameObject journalImage;
    [SerializeField] private GameObject journalPage1Layout;
    [SerializeField] private GameObject journalPage2Layout;

    [Header("Map Data")]
    [SerializeField] private GameObject mapOwnViewButton;
    [SerializeField] private GameObject mapSharedViewButton;
    [SerializeField] private GameObject mapIndividualViewButton;
    [SerializeField] private GameObject mapOwnViewPage;
    [SerializeField] private GameObject mapSharedViewPage;
    [SerializeField] private GameObject mapIndividualViewPage;
    public GameObject activeMapPage;

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        pageBackground.SetActive(PlayerController.Instance && PlayerController.Instance.playerCamera.activeSelf);
        if (!GameManager.Instance.isMaster) GameManager.Instance.mapCamera.gameObject.SetActive(isInventoryActive && mapObject.activeSelf && activeMapPage == mapSharedViewPage);
        else GameManager.Instance.mapCamera.gameObject.SetActive(true);
        if (MapManager.SharedInstance) MapManager.SharedInstance.GetComponent<GraphicRaycaster>().enabled = isInventoryActive && mapObject.activeSelf && activeMapPage == mapSharedViewPage;
    }

    public void OnSetup()
    {
        Debug.Log("InventoryManager: Setting up");
        isInventoryActive = false;

        // setup map
        mapOwnViewPage.GetComponent<MapManager>().OnSetup();
        activeMapPage = mapOwnViewPage;

        // reset inventory
        ResetInventoryState();
    }

    private void ResetInventoryState()
    {
        inventoryObject.SetActive(false);
        buttonLayout.SetActive(true);
        journalObject.SetActive(false);
        mapObject.SetActive(false);
        noteObject.transform.parent.gameObject.SetActive(true);
        noteObject.SetActive(false);
        editorObject.SetActive(false);
    }

    public void OnToggleInventoryLock()
    {
        if (GameManager.Instance.isMaster) return;
        if (isLocked) 
        {
            isLocked = false;
            OnToggleInventory();
        }
        else
        {
            OnToggleInventory();
            UIManager.Instance.pauseIcon.gameObject.SetActive(true);
            isLocked = true;
        }
        backButton.SetActive(!isLocked);

        // tutorial dialog
        if (!isLocked) UIManager.Instance.OnOpenDialog(DialogId.PlayerTutorial);
    }

    public void OnToggleInventory()
    {
        if (isLocked) return;
        isInventoryActive = !isInventoryActive;
        inventoryObject.SetActive(isInventoryActive);
        UIManager.Instance.cameraZoomInIcon.gameObject.SetActive(!isInventoryActive);
        UIManager.Instance.cameraZoomOutIcon.gameObject.SetActive(!isInventoryActive);
        UIManager.Instance.inventoryIcon.gameObject.SetActive(!isInventoryActive);
        UIManager.Instance.pauseIcon.gameObject.SetActive(!isInventoryActive);
        UIManager.Instance.minimapObject.SetActive(!isInventoryActive);
    }

    public void OnOpenJournal()
    {
        buttonLayout.gameObject.SetActive(false);
        journalObject.SetActive(true);
    }

    public void OnSetupStory()
    {
        string activeStory = GameManager.Instance.activeStory.Value.ToString();

        // setup map
        GameObject mapBackground = mapOwnViewPage.transform.Find("Background").gameObject;
        GameObject sharedMapBackground = MapManager.SharedInstance.transform.Find("Background").gameObject;
        Sprite mapSprite = Resources.Load<Sprite>("Information/" + activeStory + "/MapSprite");
        mapBackground.GetComponent<Image>().sprite = mapSprite;
        sharedMapBackground.GetComponent<Image>().sprite = mapSprite;

        // setup journal
        journalPage1Layout.SetActive(true);
        journalPage2Layout.SetActive(false);
        journalPageView.content = journalPage1Layout.GetComponent<RectTransform>();
        journalPageNumber.text = "Page 1";

        // generate pages
        GenerateJournalPage(journalPage1Layout, 1);
        GenerateJournalPage(journalPage2Layout, 2);
    }

    private void GenerateJournalPage(GameObject targetView, int targetPage)
    {
        string activeStory = GameManager.Instance.activeStory.Value.ToString();
        int persistentId = GameManager.Instance.FetchPersistentPlayerId();
        string textTargetPath = "Information/" + activeStory + "/info" + persistentId.ToString();
        string imageTargetPath = "Information/" + activeStory + "/image" + persistentId.ToString();

        // don't generate page if the path target doesn't exist
        if (!Resources.Load<TextAsset>(textTargetPath + "_" + targetPage))
        {
            Destroy(targetView);
            journalObject.transform.Find("NextPageButton").gameObject.SetActive(false);
            return;
        }

        string loadedText = Resources.Load<TextAsset>(textTargetPath + "_" + targetPage).text;
        string[] loadedTextBlocks = Regex.Split(loadedText, @"(\[image[12345]\])");

        foreach (string block in loadedTextBlocks)
        {
            // generate image blocks
            if (block.Contains("[image"))
            {
                GameObject newComponent = Instantiate(journalImage, targetView.transform);
                newComponent.SetActive(true);
                newComponent.GetComponent<Image>().sprite = Resources.Load<Sprite>(imageTargetPath+ "_" + block[block.Length - 2]);

                newComponent.GetComponent<Image>().preserveAspect = true;
                newComponent.GetComponent<Image>().SetNativeSize();
            }
            // generate text blocks
            else
            {
                GameObject newComponent = Instantiate(journalText, targetView.transform);
                newComponent.SetActive(true);
                TMP_Text textComponent = newComponent.GetComponent<TMP_Text>();
                textComponent.text = block;
                textComponent.ForceMeshUpdate();
            }
        }
    }

    public void OnJournalNextPage()
    {
        journalPage1Layout.SetActive(!journalPage1Layout.activeSelf);
        journalPage2Layout.SetActive(!journalPage2Layout.activeSelf);
        journalPageView.content = journalPage1Layout.activeSelf ? journalPage1Layout.GetComponent<RectTransform>() : journalPage2Layout.GetComponent<RectTransform>();
        journalPageNumber.text = "Page " + (journalPage1Layout.activeSelf ? "1" : "2");
    }

    public void OnOpenMap() 
    {
        mapObject.SetActive(true);
        buttonLayout.gameObject.SetActive(false);
        SetMapPage(activeMapPage);
    }

    public void SetMapPage(GameObject pageObject)
    {
        activeMapPage = pageObject;

        mapOwnViewPage.SetActive(false);
        mapIndividualViewPage.SetActive(false);
        mapSharedViewPage.SetActive(false);
        pageObject.SetActive(true);

        mapOwnViewButton.GetComponent<UIElement>().OnElementDeSelect();
        mapIndividualViewButton.GetComponent<UIElement>().OnElementDeSelect();
        mapSharedViewButton.GetComponent<UIElement>().OnElementDeSelect();
        if (pageObject == mapOwnViewPage)
        {
            mapOwnViewButton.GetComponent<UIElement>().OnElementSelect();
        }
        else if (pageObject == mapIndividualViewPage)
        {
            mapIndividualViewButton.GetComponent<UIElement>().OnElementSelect();
        }
        else if (pageObject == mapSharedViewPage)
        {
            mapSharedViewButton.GetComponent<UIElement>().OnElementSelect();
        }
    }

    public void OnToggleNote() => noteObject.SetActive(!noteObject.activeSelf);

    public void OnInventoryBack()
    {
        buttonLayout.SetActive(true);
        journalObject.SetActive(false);
        mapObject.SetActive(false);
        editorObject.SetActive(false);
        if (EditorManager.Instance) EditorManager.Instance.OnDisableEditor();
    }

    public void OnOpenEditor()
    {
        editorObject.SetActive(true);
        buttonLayout.gameObject.SetActive(false);
        noteObject.transform.parent.gameObject.SetActive(false);
        if (EditorManager.Instance) EditorManager.Instance.OnEnableEditor();
    }

    public void OnJournalDisable() => buttonLayout.transform.Find("JournalIcon").GetComponent<UIElement>().OnElementDisable();
    public void OnJournalEnable() => buttonLayout.transform.Find("JournalIcon").GetComponent<UIElement>().OnElementEnable();
    public GameObject FetchMapObjectById(int targetId) 
    {
        if (GameManager.Instance.isMaster) return MenuManager.Instance.individualMapsObject.transform.Find("MapP" + targetId).gameObject;
        else return mapIndividualViewPage.transform.Find("MapP" + targetId).gameObject;
    }
    public void OnToggleSharedView(bool targetState) 
    {
        if (targetState) mapSharedViewButton.GetComponent<UIElement>().OnElementEnable();
        else mapSharedViewButton.GetComponent<UIElement>().OnElementDisable();
    }

    public void OnToggleIndividualView(bool targetState) 
    {
        if (targetState) mapIndividualViewButton.GetComponent<UIElement>().OnElementEnable();
        else mapIndividualViewButton.GetComponent<UIElement>().OnElementDisable();
    }
}
