using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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
    public GameObject journalObject;
    [SerializeField] private GameObject mapObject;
    [SerializeField] private GameObject noteObject;
    [SerializeField] private GameObject editorObject;

    [Header("Journal Data")]
    [SerializeField] private TMP_Text journalPageNumber;
    [SerializeField] private GameObject journalBlocker;
    [SerializeField] private GameObject journalNextPageButton;
    [SerializeField] private List<TextAsset> journalPages;
    public bool enableOwnJournal;
    public bool enableSharedJournal;

    [Header("Map Data")]
    public GameObject mapPageButtons;
    [SerializeField] private GameObject mapOwnViewButton;
    [SerializeField] private GameObject mapSharedViewButton;
    [SerializeField] private GameObject mapIndividualViewButton;
    public GameObject mapOwnViewPage;
    public GameObject mapSharedViewPage;
    public GameObject mapIndividualViewPage;
    public GameObject activeMapPage;
    private bool isNoteActive;

    private void Awake() => Instance = this;

    private void Start()
    {
        isLocked = GameManager.Instance.isMaster;

        // reset inventory
        ResetInventoryState();
    }

    public void OnSetup()
    {
        Debug.Log("InventoryManager: Setting up");
        isInventoryActive = false;

        // setup the individual map and its buttons
        mapOwnViewPage.GetComponent<MapManager>().OnSetup();
        activeMapPage = mapOwnViewPage;
        SetupEventTriggers();
    }

    private void ResetInventoryState()
    {
        inventoryObject.SetActive(false);
        journalObject.SetActive(false);
        mapObject.SetActive(false);
        noteObject.SetActive(true);
        editorObject.SetActive(false);
    }

    private void SetupEventTriggers()
    {
        UIManager.Instance.AddEventTrigger(mapOwnViewButton.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, () => SetMapPage(mapOwnViewPage));
        UIManager.Instance.AddEventTrigger(mapSharedViewButton.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, () => SetMapPage(mapSharedViewPage));
        UIManager.Instance.AddEventTrigger(mapIndividualViewButton.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, () => SetMapPage(mapIndividualViewPage));
    }

    public void OnInventoryToggleLock(bool targetState)
    {
        if (GameManager.Instance.isMaster) return;

        // locking the inventory also closes or opens it automatically
        if (targetState)
        {
            OnToggleInventory();
            isLocked = targetState;
        }
        else
        {
            isLocked = targetState;
            OnToggleInventory();            
        }
        UIManager.Instance.ToggleInventoryIcon(!isLocked);

        // tutorial dialog
        if (!isLocked) UIDialog.Instance.OnOpenDialog(DialogId.PlayerTutorial);
    }

    public void OnToggleInventory()
    {
        if (isLocked) return;
        isInventoryActive = !isInventoryActive;
        inventoryObject.SetActive(isInventoryActive);

        // open on the map page and hide the hud while it's up
        if (isInventoryActive) OnOpenMap();
        else
        {
            MapNetworkManager.Instance.ToggleSharedCanvas(false);
            MapNetworkManager.Instance.ToggleMapBackground(false);
        }
        UIManager.Instance.ToggleHUDIcons(!isInventoryActive);
    }

    public void OnSetupStory()
    {
        string activeStory = GameManager.Instance.activeStory.Value.ToString();

        // setup map
        Sprite mapSprite = Resources.Load<Sprite>("Information/" + activeStory + "/MapSprite");
        Sprite correctMapSprite = Resources.Load<Sprite>("Information/" + activeStory + "/CorrectMap");
        MapManager.Instance.individualCanvas.GetComponent<Image>().sprite = mapSprite;
        MapNetworkManager.Instance.sharedCanvas.GetComponent<Image>().sprite = mapSprite;
        UIManager.Instance.minimapObject.transform.Find("Canvas").GetComponent<Image>().sprite = mapSprite;

        // setup individual maps
        var allTransforms = mapIndividualViewPage.transform.Cast<Transform>().Concat(MenuManager.Instance.individualMapsObject.transform.Cast<Transform>());
        foreach (Transform child in allTransforms)
        {
            Sprite targetSprite = child.name.Contains("Correct") ? correctMapSprite : mapSprite;
            Image targetBackground = child.Find("Canvas")?.GetComponent<Image>();
            if (!targetSprite || !targetBackground) continue;
            targetBackground.sprite = targetSprite;
        }

        // setup journal
        int persistentId = GameManager.Instance.FetchPersistentPlayerId();
        journalPages = Resources.LoadAll<TextAsset>("Information/" + activeStory).Where(p => GameManager.Instance.isMaster || p.name.Contains("info" + persistentId)).OrderBy(p => p.name).ToList();
        if (journalPages.Count > 0)
        {
            TMP_InputField textComponent = journalObject.GetComponentInChildren<TMP_InputField>();
            textComponent.text = journalPages[0].text;
            int currentIndex = journalPages.IndexOf(journalPages.FirstOrDefault(p => p.text == textComponent.text));
            journalPageNumber.text = UIManager.Instance.GetLocalUIString("INVENTORY_PAGE_SUBSTRING") + " " + (currentIndex + 1);
            textComponent.textComponent.ForceMeshUpdate();
        }

        // reserve manual paging to master mode
        OnJournalTogglePaging(GameManager.Instance.gameType.Value == GameType.Player);
    }

    public void OnJournalNextPage()
    {
        TMP_InputField textComponent = journalObject.GetComponentInChildren<TMP_InputField>();
        int currentIndex = journalPages.IndexOf(journalPages.FirstOrDefault(p => p.text == textComponent.text));

        // loop back to the first page
        int nextIndex = (currentIndex + 1) % journalPages.Count;
        textComponent.text = journalPages[nextIndex].text;
        journalPageNumber.text = UIManager.Instance.GetLocalUIString("INVENTORY_PAGE_SUBSTRING") + " " + (nextIndex + 1);
    }

    public void OnJournalSetPage(int targetPage)
    {
        // master and empty journals ignore manual paging
        if (GameManager.Instance.isMaster || journalPages.Count == 0) return;

        TMP_InputField textComponent = journalObject.GetComponentInChildren<TMP_InputField>();
        journalPageNumber.text = UIManager.Instance.GetLocalUIString("INVENTORY_PAGE_SUBSTRING") + " " + targetPage;
        textComponent.text = journalPages[targetPage - 1].text;
    }

    public void OnJournalToggle(bool targetState) 
    {
        if (!MapManager.Instance.isLoaded || !isInventoryActive) return;
        JournalMoveHandler(targetState);
        MapManager.Instance.MapMoveHandler(!targetState);
    }

    public void OnOwnJournalToggle(bool targetState)
    {
        enableOwnJournal = targetState;
        if (activeMapPage == mapOwnViewPage) OnJournalToggle(targetState);
    }

    public void OnSharedJournalToggle(bool targetState)
    {
        enableSharedJournal = targetState;
        if (activeMapPage == mapSharedViewPage) OnJournalToggle(targetState);
    }

    public void OnJournalTogglePaging(bool targetState) 
    {
        MapManager.Instance.saveIcon.GetComponent<UIElement>().OnElementToggleEnable(targetState);
        journalNextPageButton.SetActive(targetState);
    }

    public void JournalMoveHandler(bool shouldShow)
    {
        Vector2 targetPosition = new Vector2(shouldShow ? 30f : -700f, journalObject.GetComponent<RectTransform>().anchoredPosition.y);
        StartCoroutine(UIManager.Instance.UIAnimator(journalObject.GetComponent<RectTransform>(), targetPosition));

        journalBlocker.SetActive(!shouldShow);
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

        // only one map page is visible at a time
        mapOwnViewPage.SetActive(true);
        mapIndividualViewPage.SetActive(false);
        mapSharedViewPage.SetActive(false);
        pageObject.SetActive(true);
        journalObject.SetActive(true);

        mapOwnViewButton.GetComponent<UIElement>().OnElementDeSelect();
        mapIndividualViewButton.GetComponent<UIElement>().OnElementDeSelect();
        mapSharedViewButton.GetComponent<UIElement>().OnElementDeSelect();

        var showJournal = false;
        if (pageObject == mapOwnViewPage)
        {
            mapOwnViewButton.GetComponent<UIElement>().OnElementSelect();
            MapManager.Instance.SetActiveCanvas(MapManager.Instance.individualCanvas);
            showJournal = enableOwnJournal;
        }
        else if (pageObject == mapSharedViewPage)
        {
            mapSharedViewButton.GetComponent<UIElement>().OnElementSelect();
            MapManager.Instance.SetActiveCanvas(MapNetworkManager.Instance.sharedCanvas);
            showJournal = enableSharedJournal;
        }
        else if (pageObject == mapIndividualViewPage)
        {
            mapIndividualViewButton.GetComponent<UIElement>().OnElementSelect();
            MapManager.Instance.SetActiveCanvas(MapManager.Instance.individualCanvas);
            mapOwnViewPage.SetActive(false);
        }

        // handle journal visibility & map positions
        OnJournalToggle(showJournal);
    }

    public void ToggleBackground(bool targetState) => innerLayer.SetActive(targetState);

    public void OnToggleNote() 
    {
        isNoteActive = !isNoteActive;
        Vector2 targetPosition = new Vector2(isNoteActive ? -30f : 370f, 0f);
        StartCoroutine(UIManager.Instance.UIAnimator(noteObject.GetComponent<RectTransform>(), targetPosition));
    }

    public void OnInventoryBack()
    {
        OnToggleInventory();
        editorObject.SetActive(false);
        if (EditorManager.Instance) EditorManager.Instance.OnDisableEditor();
    }

    public void OnMapNavigateDown()
    {
        // cycle through the enabled map tabs
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

    public void MapTabMoveHandler(bool shouldCenter)
    {
        Vector2 targetPosition = new Vector2(shouldCenter ? 0f : 307f, 0f);
        StartCoroutine(UIManager.Instance.UIAnimator(mapPageButtons.GetComponent<RectTransform>(), targetPosition));
    }

    public void OnMapStateChanged(MapState oldValue, MapState newValue)
    {
        OnToggleSharedView(false);
        OnToggleIndividualView(false);
        switch (newValue)
        {
            case MapState.Shared:
                // shared map replaces the individual map once all maps are done
                OnOwnJournalToggle(false);
                OnToggleSharedView(true);
                break;
            case MapState.Explore:
                // players can still look at the shared map while exploring
                OnToggleSharedView(true);
                break;
            case MapState.Individual:
                // reveal every player's map at the end
                OnToggleSharedView(true);
                OnToggleIndividualView(true);
                SetMapPage(mapIndividualViewPage);
                OnOwnJournalToggle(true);
            break;
        }
    }

    public GameObject FetchMapObjectById(int targetId) 
    {
        if (GameManager.Instance.isMaster) return MenuManager.Instance.individualMapsObject.transform.Find("MapP" + targetId).gameObject;
        else return mapIndividualViewPage.transform.Find("MapP" + targetId).gameObject;
    }
    public void OnToggleSharedView(bool targetState) 
    {
        if (targetState) mapSharedViewButton.GetComponent<UIElement>().OnElementToggleEnable(true);
        else mapSharedViewButton.GetComponent<UIElement>().OnElementToggleEnable(false);
    }

    public void OnToggleIndividualView(bool targetState) 
    {
        if (targetState) mapIndividualViewButton.GetComponent<UIElement>().OnElementToggleEnable(true);
        else mapIndividualViewButton.GetComponent<UIElement>().OnElementToggleEnable(false);
    }
}
