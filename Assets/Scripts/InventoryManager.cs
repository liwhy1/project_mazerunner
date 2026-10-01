using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance;

    [Header("Inventory Data")]
    public bool isInventoryActive;
    [SerializeField] private GameObject inventoryObject;
    public GameObject pageBackground;
    public GameObject buttonLayout;
    [SerializeField] private GameObject journalObject;
    [SerializeField] private GameObject mapObject;
    [SerializeField] private GameObject noteObject;
    [SerializeField] private GameObject editorObject;
    [SerializeField] private GameObject editorIconObject;

    [Header("Journal Data")]
    [SerializeField] private ScrollRect journalPageView;
    [SerializeField] private TMP_Text journalPageNumber;
    [SerializeField] private GameObject journalText;
    [SerializeField] private GameObject journalImage;
    [SerializeField] private GameObject journalPage1Layout;
    [SerializeField] private GameObject journalPage2Layout;

    [Header("Map Data")]
    public GameObject mapObjectP0;
    public GameObject mapObjectP1;
    public GameObject mapObjectP2;
    public GameObject ownViewButton;
    public GameObject sharedViewButton;
    public GameObject individualViewButton;
    public GameObject ownViewPage;
    public GameObject sharedViewPage;
    public GameObject individualViewPage;
    public GameObject activeMapPage;

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        pageBackground.SetActive(PlayerController.Instance && PlayerController.Instance.playerCamera.activeSelf);
        if (!GameManager.Instance.isMaster) GameManager.Instance.mapCamera.gameObject.SetActive(isInventoryActive && mapObject.activeSelf && activeMapPage == sharedViewPage);
        else GameManager.Instance.mapCamera.gameObject.SetActive(true);
        if (MapManager.SharedInstance) MapManager.SharedInstance.GetComponent<GraphicRaycaster>().enabled = isInventoryActive && mapObject.activeSelf && activeMapPage == sharedViewPage;
    }

    public void OnSetup()
    {
        Debug.Log("InventoryManager: Setting up");
        isInventoryActive = false;

        // setup map
        ownViewPage.GetComponent<MapManager>().OnSetup();
        sharedViewButton.GetComponent<UIElement>().OnElementDisable();
        individualViewButton.GetComponent<UIElement>().OnElementDisable();
        activeMapPage = ownViewPage;

        // reset inventory
        ResetInventoryState();
    }

    public void ResetInventoryState()
    {
        inventoryObject.SetActive(false);
        buttonLayout.SetActive(true);
        journalObject.SetActive(false);
        mapObject.SetActive(false);
        noteObject.transform.parent.gameObject.SetActive(true);
        noteObject.SetActive(false);
        editorObject.SetActive(false);
        editorIconObject.SetActive(false);
    }

    public void OnToggleInventory()
    {
        isInventoryActive = !isInventoryActive;
        inventoryObject.SetActive(isInventoryActive);
        UIManager.Instance.cameraZoomInIcon.gameObject.SetActive(!isInventoryActive);
        UIManager.Instance.cameraZoomOutIcon.gameObject.SetActive(!isInventoryActive);
        UIManager.Instance.minimapObject.SetActive(!isInventoryActive);
        editorIconObject.SetActive(GameManager.Instance.networkState == NetworkState.Offline);
    }

    public void OnOpenJournal()
    {
        buttonLayout.gameObject.SetActive(false);
        journalObject.SetActive(true);
    }

    public void OnSetupStory()
    {
        string activeStory = GameManager.Instance.networkState == NetworkState.Online ? GameManager.Instance.activeStory.Value.ToString() : "Prototype1";

        // setup map
        GameObject mapBackground = ownViewPage.transform.Find("Background").gameObject;
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
        string activeStory = GameManager.Instance.networkState == NetworkState.Online ? GameManager.Instance.activeStory.Value.ToString() : "Prototype1";
        int persistentId = GameManager.Instance.networkState == NetworkState.Online ? GameManager.Instance.FetchPersistentPlayerId() : 0;
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
            bool isEditable = !MapManager.Instance.saveIcon.transform.GetChild(0).gameObject.activeSelf;
            MapManager.Instance.iconPile.SetActive(isEditable);
            MapManager.Instance.toolBar.gameObject.SetActive(isEditable);
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
        }
    }

    public bool FetchSharedViewState() => sharedViewButton.GetComponent<UIElement>().isEnabled;
    public bool FetchIndividualViewState() => individualViewButton.GetComponent<UIElement>().isEnabled;

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
}
