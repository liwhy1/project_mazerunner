using System.Collections;
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
    public bool isLocked;
    [SerializeField] private GameObject innerLayer;
    [SerializeField] private GameObject backButton;
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
    public GameObject mapOwnViewPage;
    public GameObject mapSharedViewPage;
    public GameObject mapIndividualViewPage;
    public GameObject activeMapPage;

    private void Start()
    {
        Instance = this;
        isLocked = GameManager.Instance.isMaster;

        // reset inventory
        ResetInventoryState();
    }

    private void LateUpdate()
    {
        bool isSharedMapActive = MapManager.SharedInstance && MapManager.SharedInstance.mapObject.transform.localPosition.y != 40;
        innerLayer.SetActive(!editorObject.activeSelf && (isSharedMapActive || !MapManager.SharedInstance));
    }

    public void OnSetup()
    {
        Debug.Log("InventoryManager: Setting up");
        isInventoryActive = false;

        // setup map
        mapOwnViewPage.GetComponent<MapManager>().OnSetup();
        activeMapPage = mapOwnViewPage;
    }

    private void ResetInventoryState()
    {
        inventoryObject.SetActive(false);
        journalObject.SetActive(false);
        mapObject.SetActive(false);
        noteObject.SetActive(true);
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
        else if (GameManager.Instance.lockInventoryAtStart)
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
        if (isInventoryActive) OnOpenMap();
        UIManager.Instance.landmarkText.gameObject.SetActive(!isInventoryActive && !string.IsNullOrEmpty(UIManager.Instance.landmarkText.text));
        UIManager.Instance.cameraZoomInIcon.gameObject.SetActive(!isInventoryActive);
        UIManager.Instance.cameraZoomOutIcon.gameObject.SetActive(!isInventoryActive);
        UIManager.Instance.inventoryIcon.gameObject.SetActive(!isInventoryActive);
        UIManager.Instance.pauseIcon.gameObject.SetActive(!isInventoryActive);
        UIManager.Instance.minimapObject.SetActive(!isInventoryActive);
    }

    public void OnSetupStory()
    {
        string activeStory = GameManager.Instance.activeStory.Value.ToString();

        // setup map
        Sprite mapSprite = Resources.Load<Sprite>("Information/" + activeStory + "/MapSprite");
        MapManager.Instance.mapBackground.GetComponent<Image>().sprite = mapSprite;
        MapManager.SharedInstance.mapBackground.GetComponent<Image>().sprite = mapSprite;

        // setup journal
        journalPage1Layout.SetActive(true);
        journalPage2Layout.SetActive(false);
        journalPageView.content = journalPage1Layout.GetComponent<RectTransform>();
        journalPageNumber.text = "Page 1";

        // generate pages
        GenerateJournalPage(journalPage1Layout, 1);
        GenerateJournalPage(journalPage2Layout, 2);
        journalPageView.transform.Find("Blocker").gameObject.SetActive(false);
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
        SetMapPage(activeMapPage);
    }

    public void SetMapPage(GameObject pageObject)
    {
        if (!pageObject) return;
        activeMapPage = pageObject;

        mapOwnViewPage.SetActive(false);
        mapIndividualViewPage.SetActive(false);
        mapSharedViewPage.SetActive(false);
        pageObject.SetActive(true);
        journalObject.SetActive(false);

        mapOwnViewButton.GetComponent<UIElement>().OnElementDeSelect();
        mapIndividualViewButton.GetComponent<UIElement>().OnElementDeSelect();
        mapSharedViewButton.GetComponent<UIElement>().OnElementDeSelect();
        if (MapNetworkManager.Instance?.mapState.Value == MapState.Individual) MapTabMoveHandler(activeMapPage != mapOwnViewPage);
        if (pageObject == mapOwnViewPage)
        {
            if (!MapManager.Instance.isLoaded) SetInventoryInitialState();
            mapOwnViewButton.GetComponent<UIElement>().OnElementSelect();
            journalObject.SetActive(MapNetworkManager.Instance?.mapState.Value == MapState.Own || MapNetworkManager.Instance?.mapState.Value == MapState.Individual || !GameManager.Instance.lockJournalAfterOwnMap);
            if (MapNetworkManager.Instance?.mapState.Value == MapState.Individual) StartCoroutine(MapManager.Instance.MapMoveHandler(false));
        }
        else if (pageObject == mapSharedViewPage)
        {
            mapSharedViewButton.GetComponent<UIElement>().OnElementSelect();
            MapManager.SharedInstance.mapObject.transform.GetChild(0).gameObject.SetActive(true);
        }
        else if (pageObject == mapIndividualViewPage)
        {
            mapIndividualViewButton.GetComponent<UIElement>().OnElementSelect();
        }
    }

    private void SetInventoryInitialState()
    {
        // map object
        MapManager.Instance.iconPile.SetActive(true);
        MapManager.Instance.OnMapToggleReady(false, false);
        MapManager.Instance.saveIcon.SetActive(MapNetworkManager.Instance.mapState.Value == MapState.Own);

        // map tabs
        mapOwnViewButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(36f, mapOwnViewButton.GetComponent<RectTransform>().anchoredPosition.y);
        mapSharedViewButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(230f, mapSharedViewButton.GetComponent<RectTransform>().anchoredPosition.y);
        mapIndividualViewButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(425f, mapIndividualViewButton.GetComponent<RectTransform>().anchoredPosition.y);

        // journal
        journalObject.GetComponent<RectTransform>().anchoredPosition = new Vector2(-700f, journalObject.GetComponent<RectTransform>().anchoredPosition.y);

        // conditionally run first animation
        if (!GameManager.Instance.lockInventoryAtStart && MapNetworkManager.Instance.mapState.Value == MapState.Own) StartCoroutine(MapManager.Instance.MapLoadAnimator());
    }

    public void OnToggleNote() => StartCoroutine(UIManager.Instance.UIAnimator(noteObject.GetComponent<RectTransform>(), new Vector2(noteObject.GetComponent<RectTransform>().anchoredPosition.x < -20f ? 370f : -30f, 0f)));

    public void OnInventoryBack()
    {
        OnToggleInventory();
        editorObject.SetActive(false);
        if (EditorManager.Instance) EditorManager.Instance.OnDisableEditor();
    }

    public void OnMapNavigateDown()
    {
        GameObject nextMapPage;
        if (activeMapPage == mapOwnViewPage)
        {
            nextMapPage = mapSharedViewButton.GetComponent<UIElement>().isEnabled ? mapSharedViewPage : activeMapPage;
        }
        else if (activeMapPage == mapSharedViewPage)
        {
            nextMapPage = mapIndividualViewButton.GetComponent<UIElement>().isEnabled ? mapIndividualViewPage : mapOwnViewPage;
        }
        else nextMapPage = mapOwnViewPage;
        if (activeMapPage != nextMapPage) SetMapPage(nextMapPage);
    }

    public void OnOpenEditor()
    {
        editorObject.SetActive(true);
        noteObject.SetActive(false);
        mapObject.SetActive(false);
        if (EditorManager.Instance) EditorManager.Instance.OnEnableEditor();
    }

    public IEnumerator JournalMoveHandler(bool shouldCenter)
    {
        yield return new WaitForSeconds(shouldCenter ? 0f : .1f);
        Vector2 targetPosition = new Vector2(shouldCenter ? -700f : 30f, journalObject.GetComponent<RectTransform>().anchoredPosition.y);
        StartCoroutine(UIManager.Instance.UIAnimator(journalObject.GetComponent<RectTransform>(), targetPosition));
    }

    public void MapTabMoveHandler(bool shouldCenter)
    {
        Vector2 targetPosition = new Vector2(!shouldCenter ? 340f : 36f, mapOwnViewButton.GetComponent<RectTransform>().anchoredPosition.y);
        StartCoroutine(UIManager.Instance.UIAnimator(mapOwnViewButton.GetComponent<RectTransform>(), targetPosition));
    
        targetPosition = new Vector2(!shouldCenter ? 535f : 230f, mapSharedViewButton.GetComponent<RectTransform>().anchoredPosition.y);
        StartCoroutine(UIManager.Instance.UIAnimator(mapSharedViewButton.GetComponent<RectTransform>(), targetPosition));

        targetPosition = new Vector2(!shouldCenter ? 730f : 425f, mapIndividualViewButton.GetComponent<RectTransform>().anchoredPosition.y);
        StartCoroutine(UIManager.Instance.UIAnimator(mapIndividualViewButton.GetComponent<RectTransform>(), targetPosition));
    }

    public void OnJournalDisable() => journalPageView.transform.Find("Blocker").gameObject.SetActive(true);
    public void OnJournalEnable() => journalPageView.transform.Find("Blocker").gameObject.SetActive(false);
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
