using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Events;

public class UIManager : NetworkBehaviour
{
    public static UIManager Instance;
    private EventSystem eventSystem;

    [Header("Menu Data")]
    [SerializeField] private GameObject menuObject;
    [SerializeField] private GameObject hostButton;
    [SerializeField] private GameObject joinButton;
    [SerializeField] private GameObject offlineButton;

    [Header("Host Data")]
    [SerializeField] private GameObject hostObject;
    [SerializeField] private GameObject startHostButton;
    [SerializeField] private GameObject startMasterButton;
    [SerializeField] private GameObject hostBackButton;
    [SerializeField] private TMP_InputField hostPlayerNameInput;

    [Header("Join Data")]
    [SerializeField] private GameObject joinObject;
    [SerializeField] private GameObject startClientButton;
    [SerializeField] private GameObject joinBackButton;
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private TMP_InputField joinPlayerNameInput;

    [Header("Lobby Data")]
    [SerializeField] private GameObject lobbyObject;
    [SerializeField] private GameObject startButton;
    [SerializeField] private GameObject lobbyQuitButton;
    [SerializeField] private GameObject storyDropdown;
    [SerializeField] private TMP_Text lobbyJoinCodeText;
    [SerializeField] private TMP_Text lobbyPlayerListText;
    [SerializeField] private TMP_Text waitingOnHostText;
    [SerializeField] private GameObject playerView;
    [SerializeField] private GameObject LegArrowRight;
    [SerializeField] private GameObject LegArrowLeft;

    [SerializeField] private TMP_Text gameStateText;
    [SerializeField] private GameObject finishButton;
    [SerializeField] private GameObject sharedMapView;
    [SerializeField] private GameObject timerObject;

    [Header("Pause Data")]
    [SerializeField] private GameObject pauseObject;
    [SerializeField] public GameObject resumeButton;
    [SerializeField] private GameObject pauseQuitButton;
    [SerializeField] private TMP_Text pauseJoinCodeText;
    [SerializeField] private TMP_Text pausePlayerListText;

    [Header("HUD Data")]
    [SerializeField] private Image pauseIcon;
    [SerializeField] private Image inventoryIcon;
    public Image cameraZoomInIcon;
    public Image cameraZoomOutIcon;
    public GameObject loadingIcon;
    public Image minimapIcon;
    [SerializeField] private Image minimapPlayerIcon;
    [SerializeField] private Image minimapPlayer2Icon;
    [SerializeField] private Image minimapPlayer3Icon;
    private GameObject lastSelectedObject;

    [Header("Dialog Data")]
    public GameObject activeDialog;

    private void Start()
    {
        Debug.Log("UIManager: Setting up");

        // setup vars
        Instance = this;
        eventSystem = FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include);

        // call setup on uielemenets
        foreach (var element in FindObjectsByType<UIElement>(FindObjectsInactive.Include))
        {
            element.OnSetup();
        }

        // reset ui
        ResetUIState();

        // enable menu
        menuObject.SetActive(true);
        eventSystem.firstSelectedGameObject = hostButton;
        eventSystem.SetSelectedGameObject(hostButton);

        // subscribe to events(watch vod)
        SetupUITriggers();
    }

    private void Update()
    {
        if (PlayerController.Instance)
        {
            UpdateMinimapPosition();            
        }

        HandleActiveNavigationElement();
    }

    private void AddEventTrigger(EventTrigger trigger, EventTriggerType type, UnityAction action)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry{eventID = type};
        entry.callback.AddListener(_ => action());
        trigger.triggers.Add(entry);
    }

    private void SetupUITriggers()
    {
        // menu
        AddEventTrigger(hostButton.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, OnHostGame);
        AddEventTrigger(hostButton.GetComponent<EventTrigger>(), EventTriggerType.Submit, OnHostGame);
        AddEventTrigger(joinButton.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, OnJoinGame);
        AddEventTrigger(joinButton.GetComponent<EventTrigger>(), EventTriggerType.Submit, OnJoinGame);
        AddEventTrigger(offlineButton.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, OnOfflineGame);
        AddEventTrigger(offlineButton.GetComponent<EventTrigger>(), EventTriggerType.Submit, OnOfflineGame);

        // host
        AddEventTrigger(startMasterButton.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, GameManager.Instance.OnStartMaster);
        AddEventTrigger(startMasterButton.GetComponent<EventTrigger>(), EventTriggerType.Submit, GameManager.Instance.OnStartMaster);
        AddEventTrigger(startHostButton.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, GameManager.Instance.OnStartHost);
        AddEventTrigger(startHostButton.GetComponent<EventTrigger>(), EventTriggerType.Submit, GameManager.Instance.OnStartHost);
        AddEventTrigger(hostBackButton.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, OnQuitButton);
        AddEventTrigger(hostBackButton.GetComponent<EventTrigger>(), EventTriggerType.Submit, OnQuitButton);
        hostPlayerNameInput.onValueChanged.AddListener(delegate { GameManager.Instance.OnNameChanged(hostPlayerNameInput.text); });
        hostPlayerNameInput.onSubmit.AddListener(delegate { GameManager.Instance.OnStartHost(); });

        // join
        AddEventTrigger(startClientButton.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, GameManager.Instance.OnStartClient);
        AddEventTrigger(startClientButton.GetComponent<EventTrigger>(), EventTriggerType.Submit, GameManager.Instance.OnStartClient);
        AddEventTrigger(joinBackButton.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, OnQuitButton);
        AddEventTrigger(joinBackButton.GetComponent<EventTrigger>(), EventTriggerType.Submit, OnQuitButton);
        joinPlayerNameInput.onValueChanged.AddListener(delegate { GameManager.Instance.OnNameChanged(joinPlayerNameInput.text); });
        joinCodeInput.onSubmit.AddListener(delegate { GameManager.Instance.OnStartClient(); });

        // lobby
        AddEventTrigger(startButton.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, GameManager.Instance.OnLobbyStart);
        AddEventTrigger(startButton.GetComponent<EventTrigger>(), EventTriggerType.Submit, GameManager.Instance.OnLobbyStart);
        AddEventTrigger(finishButton.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, OnSharedMapReady);
        AddEventTrigger(finishButton.GetComponent<EventTrigger>(), EventTriggerType.Submit, OnSharedMapReady);
        AddEventTrigger(lobbyQuitButton.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, OnQuitButton);
        AddEventTrigger(lobbyQuitButton.GetComponent<EventTrigger>(), EventTriggerType.Submit, OnQuitButton);

        EventTrigger.Entry leftArrowClickEntry = new EventTrigger.Entry() {eventID = EventTriggerType.PointerClick};
        leftArrowClickEntry.callback.AddListener((eventData) => { SetPlayerSkinId(-1); });
        LegArrowLeft.GetComponent<EventTrigger>().triggers.Add(leftArrowClickEntry);        

        EventTrigger.Entry rightArrowClickEntry = new EventTrigger.Entry() {eventID = EventTriggerType.PointerClick};
        rightArrowClickEntry.callback.AddListener((eventData) => { SetPlayerSkinId(1); });
        LegArrowRight.GetComponent<EventTrigger>().triggers.Add(rightArrowClickEntry);

        // pause
        AddEventTrigger(resumeButton.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, GameManager.Instance.OnPauseToggle);
        AddEventTrigger(resumeButton.GetComponent<EventTrigger>(), EventTriggerType.Submit, GameManager.Instance.OnPauseToggle);
        AddEventTrigger(pauseQuitButton.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, OnQuitButton);
        AddEventTrigger(pauseQuitButton.GetComponent<EventTrigger>(), EventTriggerType.Submit, OnQuitButton);
        storyDropdown.GetComponent<TMP_Dropdown>().onValueChanged.AddListener(delegate { GameManager.Instance.activeStory = storyDropdown.GetComponent<TMP_Dropdown>().captionText.text; });
    }

    public void HandleActiveNavigationElement()
    {
        GameObject currentSelectedObject = eventSystem.currentSelectedGameObject;
        if (currentSelectedObject != lastSelectedObject)
        {
            // prevent deselecting on click in the menu
            if (currentSelectedObject == null && GameManager.Instance.networkState == NetworkState.None) 
            {
                currentSelectedObject = lastSelectedObject;
                eventSystem.SetSelectedGameObject(currentSelectedObject);
            }

            // unhighlight previous element
            if (lastSelectedObject && lastSelectedObject.GetComponent<UIElement>()) lastSelectedObject.GetComponent<UIElement>().OnElementShrink();

            // highlight selected element
            if (currentSelectedObject && currentSelectedObject.GetComponent<UIElement>()) currentSelectedObject.GetComponent<UIElement>().OnElementGrow();

            // activate input filed on current selection, if exits
            if (currentSelectedObject && currentSelectedObject.GetComponent<TMP_InputField>()) currentSelectedObject.GetComponent<TMP_InputField>().ActivateInputField();

            lastSelectedObject = currentSelectedObject;
        }
    }

    public void OnNavigationDown()
    {
        if (!eventSystem.currentSelectedGameObject) return;
        Selectable currentSelectable = eventSystem.currentSelectedGameObject.GetComponent<Selectable>();
        Selectable nextSelectable = currentSelectable.navigation.selectOnDown;
        if (nextSelectable != null)
        {
            EventSystem.current.SetSelectedGameObject(nextSelectable.gameObject);
            if (nextSelectable.GetComponent<TMP_InputField>()) nextSelectable.GetComponent<TMP_InputField>().ActivateInputField();
        }
    }

    private void UpdateMinimapPosition()
    {
        // TODO: don't read any of this :)
        // setup vars
        GameObject terrainObject = GameManager.Instance.terrainObject;
        MeshRenderer renderer = terrainObject.GetComponent<MeshRenderer>();
        Bounds bounds = renderer.bounds;
        RectTransform minimapRect = minimapIcon.GetComponent<RectTransform>();

        minimapPlayerIcon.gameObject.SetActive(false);
        minimapPlayer2Icon.gameObject.SetActive(false);
        minimapPlayer3Icon.gameObject.SetActive(false);
        foreach (var player in GameManager.Instance.playerList)
        {
            int playerId = player.PersistentPlayerId.Value;
            if (playerId > -1)
            {
                Vector3 playerPosition = player.gameObject.transform.position;
                float normalizedX = Mathf.InverseLerp(bounds.min.x, bounds.max.x, playerPosition.x);
                float normalizedY = Mathf.InverseLerp(bounds.min.z, bounds.max.z, playerPosition.z);
                RectTransform playerIconRect = playerId == 0 ? minimapPlayerIcon.GetComponent<RectTransform>() : playerId == 1 ?minimapPlayer2Icon.GetComponent<RectTransform>() : minimapPlayer3Icon.GetComponent<RectTransform>();
                playerIconRect.gameObject.GetComponent<Image>().color = player.transform.Find("Model").Find("Character_Body").GetComponent<Renderer>().material.color;
                playerIconRect.gameObject.SetActive(true);
                playerIconRect.anchoredPosition = new Vector2(Mathf.Lerp(minimapRect.rect.xMin, minimapRect.rect.xMax, normalizedX), Mathf.Lerp(minimapRect.rect.yMin, minimapRect.rect.yMax, normalizedY));                
            }
        }
    }

    public void MirrorSharedmaptoMinimap()
    {
        // TODO: this is hardcoded, has hacks and is unnecessarily complex (but it works)
        minimapIcon.transform.localScale = new Vector3(.7f, .7f, .7f);
        foreach (Transform icon in SharedMapManager.Instance.transform)
        {
            // prevent mirroring drawdots
            if (!icon.GetComponent<NetworkObject>() || icon.name.Contains("Dot")) continue;
            GameObject newIcon = Instantiate(Resources.Load<GameObject>("MapIcon"), minimapIcon.transform.parent);

            // calculate world canvas pos to screen canvas
            Vector2 screenPosition = GameManager.Instance.mapCamera.WorldToScreenPoint(icon.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(transform.GetComponent<RectTransform>(), screenPosition, null, out Vector2 localPosition);

            // apply position with slightly made up offset
            newIcon.GetComponent<RectTransform>().anchoredPosition = localPosition;
            newIcon.GetComponent<RectTransform>().anchoredPosition -= new Vector2(300f, 0f);

            // anchor hack pt1
            newIcon.transform.SetParent(minimapIcon.transform);

            // apply icon values
            newIcon.transform.localEulerAngles = Vector3.zero;
            newIcon.transform.Find("Sprite").GetComponent<Image>().sprite = Resources.LoadAll<Sprite>("MapIconsNew").FirstOrDefault(s => s.name.Contains(icon.name));
            newIcon.transform.Find("Sprite").GetComponent<Image>().preserveAspect = true;
            newIcon.transform.Find("Name").gameObject.SetActive(false);
        }
        // resize minimap sprite aka anchor hack pt2
        minimapIcon.transform.localScale = Vector3.one;
    }

    private void SetupStoryDropdown()
    {
        storyDropdown.GetComponent<TMP_Dropdown>().ClearOptions();
        var storyFolders = Resources.LoadAll<Texture2D>("Information");
        List<string> storyNames = new List<string>();
        foreach (var item in storyFolders)
        {
            if (item.name.Contains("image")) continue;
            storyNames.Add(item.name);
        }
        storyDropdown.GetComponent<TMP_Dropdown>().AddOptions(storyNames);

        if (!NetworkManager.IsHost) return;
        GameManager.Instance.activeStory = storyNames[0];
    }

    public void SetPlayerSkinId(int targetValue)
    {
        int currentId = PlayerController.Instance.GetComponent<PlayerData>().SkinId.Value;
        targetValue = currentId + targetValue > 3 ? 0 : currentId + targetValue < 0 ? 3 : currentId + targetValue;
        Material targetMaterial = targetValue == 0 ? GameManager.Instance.playerMat1 : targetValue == 1 ? GameManager.Instance.playerMat2 : targetValue == 2 ? GameManager.Instance.playerMat3 : GameManager.Instance.playerMat4;
        Debug.Log(targetValue + ", " + targetMaterial.name);
        PlayerController.Instance.transform.Find("Model").Find("Character_Body").GetComponent<Renderer>().material = targetMaterial;
        GameManager.Instance.SetPlayerSkinIdServerRpc(targetValue);
    }

    public void ResetUIState()
    {
        waitingOnHostText.gameObject.SetActive(false);
        pauseObject.SetActive(false);
        hostObject.SetActive(false);
        joinObject.SetActive(false);
        menuObject.SetActive(false);
        lobbyObject.SetActive(false);
        loadingIcon.SetActive(false);
        inventoryIcon.gameObject.SetActive(true);
        cameraZoomInIcon.gameObject.SetActive(true);
        cameraZoomOutIcon.gameObject.SetActive(true);
        minimapIcon.transform.parent.gameObject.SetActive(true);
        pauseIcon.gameObject.SetActive(true);
    }

    private void OnJoinGame()
    {
        ResetUIState();
        eventSystem.SetSelectedGameObject(joinPlayerNameInput.gameObject);
        joinObject.SetActive(true);
    }

    private void OnHostGame()
    {
        ResetUIState();
        eventSystem.SetSelectedGameObject(hostPlayerNameInput.gameObject);
        hostObject.SetActive(true);
    }

    private void OnOfflineGame()
    {
        // set gamestate to offline
        GameManager.Instance.networkState = NetworkState.Offline;

        // trigger offline player spawn
        GameManager.Instance.SpawnPlayer(0);

        // trigger offline sharedmap spawn
        GameManager.Instance.SpawnSharedMap(0);

        // setup story dropdown
        SetupStoryDropdown();

        // set active ui element
        eventSystem.SetSelectedGameObject(storyDropdown);

        // setup inventory
        InventoryManager.Instance.OnSetup();

        lobbyObject.SetActive(true);
        lobbyJoinCodeText.gameObject.SetActive(false);
        pauseJoinCodeText.gameObject.SetActive(false);
        lobbyPlayerListText.gameObject.SetActive(false);
        pausePlayerListText.gameObject.SetActive(false);
    }

    private void OnQuitButton()
    {
        // reset playerprefs
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();

        // destroy spawned player
        if (PlayerController.Instance != null)
        {
            Destroy(PlayerController.Instance.gameObject);
        }

        // reset networking
        GameManager.Instance.OnDisconnectClient();
    }

    public void OnLobbyConnect()
    {
        ResetUIState();
        lobbyObject.SetActive(true);
        loadingIcon.SetActive(false);

        // conditionally enable elements
        playerView.SetActive(!GameManager.Instance.isMaster);
        gameStateText.gameObject.SetActive(GameManager.Instance.isMaster);
        gameStateText.text = "";
        waitingOnHostText.gameObject.SetActive(!NetworkManager.IsHost);
        storyDropdown.gameObject.SetActive(NetworkManager.IsHost);
        startButton.SetActive(NetworkManager.IsHost);
        finishButton.SetActive(false);
        sharedMapView.SetActive(false);
        timerObject.SetActive(false);
        eventSystem.SetSelectedGameObject(NetworkManager.IsHost ? storyDropdown : lobbyQuitButton);

        // setup story dropdown
        SetupStoryDropdown();
    }

    public void OnPauseToggle()
    {
        eventSystem.SetSelectedGameObject(resumeButton);
        pauseObject.SetActive(!pauseObject.activeSelf);
        pauseIcon.gameObject.SetActive(!pauseObject.activeSelf);
        inventoryIcon.gameObject.SetActive(!pauseObject.activeSelf);
    }

    public void OnRefreshPlayerList()
    {
        lobbyPlayerListText.text = "Players:\n";
        pausePlayerListText.text = "Players:\n";
        foreach (var player in GameManager.Instance.playerList)
        {
            string targetText = player.PlayerName.Value.ToString();
            targetText += player.OwnerClientId == NetworkManager.LocalClientId ? " (you)" : "";
            targetText += player.OwnerClientId == NetworkManager.ServerClientId ? " (host)" : "";
            lobbyPlayerListText.text += targetText + "\n";
            pausePlayerListText.text += targetText + "\n";
        }
    }

    public void SetJoinCodeText(string targetText)
    {
        lobbyJoinCodeText.text = "Join Code: \n<b>" + targetText;
        pauseJoinCodeText.text = "Join Code: \n<b>" + targetText;
    }

    public string GetJoinCodeInput()
    {
        return joinCodeInput.text.Trim().ToUpper();
    }

    public void OnLobbyStart()
    {
        startButton.SetActive(!GameManager.Instance.isMaster);
        timerObject.SetActive(GameManager.Instance.isMaster);
        storyDropdown.SetActive(false);
        waitingOnHostText.gameObject.SetActive(false);
        SetMasterGameStateText("Individual mapping\n" + GameManager.Instance.playerList.Count(p => p.IsMapReady.Value == true) + "/" + GameManager.Instance.playerList.Count);
    }

    public void OnOpenDialog(string titleText, string contentText, string buttonText, string targetAction = "")
    {
        // close any active dialogs
        OnCloseDialog();

        // create new dialog
        GameObject newDialog = Instantiate(Resources.Load<GameObject>("Elements/TextDialog"), transform);
        newDialog.transform.localPosition = Vector3.zero;
        activeDialog = newDialog;

        // setup vars
        GameObject dialogTitle = activeDialog.transform.Find("DialogTitle").gameObject;
        GameObject dialogText = activeDialog.transform.Find("DialogText").gameObject;
        GameObject buttonLayout = activeDialog.transform.Find("ButtonLayout").gameObject;
        GameObject closeButton = buttonLayout.transform.Find("CloseButton").gameObject;
        GameObject mainButton = buttonLayout.transform.Find("MainButton").gameObject;

        // setup text
        dialogTitle.GetComponent<TMP_Text>().text = titleText;
        dialogText.GetComponent<TMP_Text>().text = contentText;
        mainButton.SetActive(!string.IsNullOrEmpty(buttonText));
        mainButton.transform.GetChild(0).GetComponent<TMP_Text>().text = buttonText;

        // set ui selected button
        eventSystem.SetSelectedGameObject(mainButton.activeSelf ? mainButton : closeButton);

        // setup triggers
        EventTrigger.Entry closeClickEntry = new EventTrigger.Entry() {eventID = EventTriggerType.PointerClick};
        closeClickEntry.callback.AddListener((eventData) => { OnCloseDialog(); });
        closeButton.GetComponent<EventTrigger>().triggers.Add(closeClickEntry);

        EventTrigger.Entry closeSubmitEntry = new EventTrigger.Entry() {eventID = EventTriggerType.Submit};
        closeSubmitEntry.callback.AddListener((eventData) => { OnCloseDialog(); });
        closeButton.GetComponent<EventTrigger>().triggers.Add(closeSubmitEntry);

        EventTrigger.Entry mainClickEntry = new EventTrigger.Entry() {eventID = EventTriggerType.PointerClick};
        mainClickEntry.callback.AddListener((eventData) => { DialogActionHandler(targetAction); });
        mainButton.GetComponent<EventTrigger>().triggers.Add(mainClickEntry);

        EventTrigger.Entry mainSubmitEntry = new EventTrigger.Entry() {eventID = EventTriggerType.Submit};
        mainSubmitEntry.callback.AddListener((eventData) => { DialogActionHandler(targetAction); });
        mainButton.GetComponent<EventTrigger>().triggers.Add(mainSubmitEntry);
    }

    private void DialogActionHandler(string targetAction)
    {
        switch (targetAction)
        {
            case "mapclear":
                MapManager.Instance.OnClearMap();
                break;
            case "sharedmapclear":
                SharedMapManager.Instance.OnClearMap();
                break;
        }
        OnCloseDialog();
    }

    public void OnCloseDialog()
    {
        if (activeDialog)
        {
            Destroy(activeDialog);
            activeDialog = null;
        }
    }

    public void SetMasterGameStateText(string targetText)
    {
        gameStateText.text = "<b>Game State:</b>\n";
        gameStateText.text += targetText;
    }

    public void OnSharedMapEnabled()
    {
        SetMasterGameStateText("Shared mapping");
        finishButton.SetActive(true);
        GameManager.Instance.mapCamera.gameObject.SetActive(true);
        GameManager.Instance.mapCamera.transform.position = new Vector3(0.33f, -100f, 0.34f);
        GameManager.Instance.mapCamera.targetTexture = (RenderTexture)sharedMapView.GetComponent<RawImage>().texture;
        sharedMapView.SetActive(true);
    }

    public void OnSharedMapReady()
    {
        SetMasterGameStateText("Explore map");
        GameManager.Instance.OnSharedMapReady();
        finishButton.SetActive(false);
    }
}
