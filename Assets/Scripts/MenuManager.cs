using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using Unity.Collections;
using System.Linq;
using UnityEngine.UI;

public class MenuManager : NetworkBehaviour
{
    public static MenuManager Instance;

    [Header("Menu Data")]
    [SerializeField] private GameObject menuObject;
    [SerializeField] private GameObject hostButton;
    [SerializeField] private GameObject joinButton;
    [SerializeField] private GameObject settingsButton;
    [SerializeField] private GameObject editorButton;

    [Header("Host Data")]
    [SerializeField] private GameObject hostObject;
    public TMP_InputField hostPlayerNameInput;
    [SerializeField] private UIHorizontalSelector playerModeSelector;
    [SerializeField] private GameObject startHostButton;
    [SerializeField] private GameObject hostBackButton;

    [Header("Join Data")]
    [SerializeField] private GameObject joinObject;
    [SerializeField] private GameObject startClientButton;
    [SerializeField] private GameObject joinBackButton;
    [SerializeField] private TMP_InputField joinCodeInput;
    public TMP_InputField joinPlayerNameInput;

    [Header("Lobby Data")]
    [SerializeField] private GameObject lobbyObject;
    [SerializeField] private GameObject lobbyReadyButton;
    [SerializeField] private GameObject lobbyQuitButton;
    [SerializeField] private GameObject lobbySettingsButton;
    [SerializeField] private GameObject lobbySettingsPage;
    [SerializeField] private Toggle lobbyLandmarkToggle;
    [SerializeField] private Toggle lobbyJournalToggle;
    [SerializeField] private Toggle lobbyInventoryToggle;
    [SerializeField] private UIHorizontalSelector lobbyStorySelector;
    public TMP_Text lobbyJoinCodeText;
    public GameObject lobbyPlayerListObject;
    [SerializeField] private TMP_Text lobbyWaitingText;
    [SerializeField] private UIProgressBar gameStateProgressBar;
    [SerializeField] private GameObject lobbyFinishButton;
    [SerializeField] private GameObject timerObject;
    public GameObject individualMapsObject;

    [Header("Player Customisation Data")]
    [SerializeField] private GameObject lobbyPlayerView;
    [SerializeField] private GameObject headArrowLeft;
    [SerializeField] private GameObject headArrowRight;
    [SerializeField] private GameObject bodyArrowLeft;
    [SerializeField] private GameObject bodyArrowRight;
    [SerializeField] private GameObject legArrowLeft;
    [SerializeField] private GameObject legArrowRight;

    private void Awake()
    {
        Instance = this;        
    }

    public void OnSetup()
    {
        Debug.Log("MenuManager: Setting up");

        // subscribe to events(watch vod)
        SetupUITriggers();

        // subscribe to joincode changes
        GameManager.Instance.joinCode.OnValueChanged += OnJoinCodeValueChanged;

        // open menu
        OnOpenMenu();
    }

    public override void OnDestroy()
    {
        GameManager.Instance.joinCode.OnValueChanged -= OnJoinCodeValueChanged;
    }

    public void ResetUIState()
    {
        hostObject.SetActive(false);
        joinObject.SetActive(false);
        menuObject.SetActive(false);
        lobbyObject.SetActive(false);
        UIManager.Instance.loadingIcon.SetActive(false);
        hostPlayerNameInput.text = "";
        joinPlayerNameInput.text = "";
        joinCodeInput.text = "";
        OnPlayerModeSelectorChanged(playerModeSelector.currentSelection);
    }

    private void SetupUITriggers()
    {
        // menu
        UIManager.Instance.AddEventTrigger(hostButton.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, OnHostGame);
        UIManager.Instance.AddEventTrigger(hostButton.GetComponent<EventTrigger>(), EventTriggerType.Submit, OnHostGame);
        UIManager.Instance.AddEventTrigger(joinButton.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, OnJoinGame);
        UIManager.Instance.AddEventTrigger(joinButton.GetComponent<EventTrigger>(), EventTriggerType.Submit, OnJoinGame);
        UIManager.Instance.AddEventTrigger(settingsButton.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, UIManager.Instance.OnToggleSettings);
        UIManager.Instance.AddEventTrigger(settingsButton.GetComponent<EventTrigger>(), EventTriggerType.Submit, UIManager.Instance.OnToggleSettings);
        UIManager.Instance.AddEventTrigger(editorButton.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, OnOpenEditor);
        UIManager.Instance.AddEventTrigger(editorButton.GetComponent<EventTrigger>(), EventTriggerType.Submit, OnOpenEditor);

        // host
        UIManager.Instance.AddEventTrigger(startHostButton.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, OnStartHost);
        UIManager.Instance.AddEventTrigger(startHostButton.GetComponent<EventTrigger>(), EventTriggerType.Submit, OnStartHost);
        UIManager.Instance.AddEventTrigger(hostBackButton.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, UIManager.Instance.OnQuitButton);
        UIManager.Instance.AddEventTrigger(hostBackButton.GetComponent<EventTrigger>(), EventTriggerType.Submit, UIManager.Instance.OnQuitButton);
        playerModeSelector.onValueChanged += () => OnPlayerModeSelectorChanged(playerModeSelector.currentSelection);
        hostPlayerNameInput.onSubmit.AddListener(delegate { GameManager.Instance.OnStartHost(false); });

        // join
        UIManager.Instance.AddEventTrigger(startClientButton.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, GameManager.Instance.OnStartClient);
        UIManager.Instance.AddEventTrigger(startClientButton.GetComponent<EventTrigger>(), EventTriggerType.Submit, GameManager.Instance.OnStartClient);
        UIManager.Instance.AddEventTrigger(joinBackButton.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, UIManager.Instance.OnQuitButton);
        UIManager.Instance.AddEventTrigger(joinBackButton.GetComponent<EventTrigger>(), EventTriggerType.Submit, UIManager.Instance.OnQuitButton);
        joinCodeInput.onSubmit.AddListener(delegate { GameManager.Instance.OnStartClient(); });

        // lobby
        UIManager.Instance.AddEventTrigger(lobbyReadyButton.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, OnLobbyToggleReady);
        UIManager.Instance.AddEventTrigger(lobbyReadyButton.GetComponent<EventTrigger>(), EventTriggerType.Submit, OnLobbyToggleReady);
        UIManager.Instance.AddEventTrigger(lobbyFinishButton.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, OnMasterFinishButton);
        UIManager.Instance.AddEventTrigger(lobbyFinishButton.GetComponent<EventTrigger>(), EventTriggerType.Submit, OnMasterFinishButton);
        UIManager.Instance.AddEventTrigger(lobbyQuitButton.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, UIManager.Instance.OnQuitButton);
        UIManager.Instance.AddEventTrigger(lobbyQuitButton.GetComponent<EventTrigger>(), EventTriggerType.Submit, UIManager.Instance.OnQuitButton);
        UIManager.Instance.AddEventTrigger(lobbySettingsButton.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, OnLobbyToggleSettings);
        UIManager.Instance.AddEventTrigger(lobbySettingsButton.GetComponent<EventTrigger>(), EventTriggerType.Submit, OnLobbyToggleSettings);
        lobbyStorySelector.onValueChanged += () => GameManager.Instance.SetActiveStory(lobbyStorySelector.currentSelection);
        lobbyLandmarkToggle.onValueChanged.AddListener(delegate { GameManager.Instance.allowLandmarkBacktracking = lobbyLandmarkToggle.isOn; });
        lobbyJournalToggle.onValueChanged.AddListener(delegate { GameManager.Instance.lockJournalAfterOwnMap = lobbyJournalToggle.isOn; });
        lobbyInventoryToggle.onValueChanged.AddListener(delegate { GameManager.Instance.lockInventoryAtStart = lobbyInventoryToggle.isOn; });

        // player customisation
        UIManager.Instance.AddEventTrigger(headArrowLeft.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, DecreasePlayerSkinValue, "hair");
        UIManager.Instance.AddEventTrigger(headArrowRight.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, IncreasePlayerSkinValue, "hair");
        UIManager.Instance.AddEventTrigger(bodyArrowLeft.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, DecreasePlayerSkinValue, "color");
        UIManager.Instance.AddEventTrigger(bodyArrowRight.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, IncreasePlayerSkinValue, "color");
        UIManager.Instance.AddEventTrigger(legArrowLeft.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, DecreasePlayerSkinValue, "gender");
        UIManager.Instance.AddEventTrigger(legArrowRight.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, IncreasePlayerSkinValue, "gender");
    }

    private void SetupLobbyStorySelector()
    {
        // fetch available stories
        var storyFolders = Resources.LoadAll<Texture2D>("Information");
        List<string> storyNames = new List<string>();
        foreach (var item in storyFolders)
        {
            if (item.name.Contains("image") || item.name.Contains("Map")) continue;
            storyNames.Add(item.name);
        }

        // update selector
        lobbyStorySelector.selectorItems.Clear();
        lobbyStorySelector.selectorItems = storyNames;
        lobbyStorySelector.OnUpdateSelection(lobbyStorySelector.selectorItems.Count - 1);

        // force rebuild ui layout
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(lobbyStorySelector.transform.parent.parent.parent.GetComponent<RectTransform>());
        LayoutRebuilder.ForceRebuildLayoutImmediate(lobbyStorySelector.transform.parent.parent.GetComponent<RectTransform>());
    }

    public void IncreasePlayerSkinValue(string targetValue)
    {
        SkinData currentData = PlayerController.Instance.GetComponent<PlayerData>().SkinData.Value;
        switch (targetValue)
        {
            case "color": currentData.colorId = (currentData.colorId + 1) % 5; break;
            case "gender": currentData.genderId = (currentData.genderId + 1) % 4; break;
            case "hair": currentData.hairId = (currentData.hairId + 1) % 4; break;
        }

        // update value locally first
        PlayerController.Instance.SetPlayerSkinData(currentData);

        // let the server confirm the new data
        GameManager.Instance.SetPlayerSkinDataServerRpc(currentData);
    }

    public void DecreasePlayerSkinValue(string targetValue)
    {
        SkinData currentData = PlayerController.Instance.GetComponent<PlayerData>().SkinData.Value;
        switch (targetValue)
        {
            case "color": currentData.colorId = (currentData.colorId - 1 + 5) % 5; break;
            case "gender": currentData.genderId = (currentData.genderId - 1 + 4) % 4; break;
            case "hair": currentData.hairId = (currentData.hairId - 1 + 4) % 4; break;
        }

        // update value locally first
        PlayerController.Instance.SetPlayerSkinData(currentData);

        // let the server confirm the new data
        GameManager.Instance.SetPlayerSkinDataServerRpc(currentData);
    }

    private void OnPlayerModeSelectorChanged(string newValue)
    {
        hostPlayerNameInput.gameObject.SetActive(newValue == "Player");
        UIManager.Instance.eventSystem.SetSelectedGameObject(hostPlayerNameInput.gameObject);
    }

    private void OnStartHost()
    {
        GameManager.Instance.OnStartHost(playerModeSelector.currentSelection != "Player");
    }

    private void OnOpenEditor()
    {
        UIManager.Instance.OnOpenDialog(DialogId.EditorOpen);
    }

    public void OnOpenMenu()
    {
        ResetUIState();
        menuObject.SetActive(true);
    }

    private void OnJoinGame()
    {
        ResetUIState();
        joinObject.SetActive(true);
        UIManager.Instance.eventSystem.SetSelectedGameObject(joinPlayerNameInput.gameObject);
    }

    private void OnHostGame()
    {
        ResetUIState();
        hostObject.SetActive(true);
        UIManager.Instance.eventSystem.SetSelectedGameObject(hostPlayerNameInput.gameObject);
    }

    public void OnLobbyConnect()
    {
        ResetUIState();
        lobbyObject.SetActive(true);

        // conditionally enable elements
        lobbyPlayerView.SetActive(!GameManager.Instance.isMaster);
        lobbyWaitingText.gameObject.SetActive(true);
        lobbyStorySelector.transform.parent.parent.gameObject.SetActive(IsHost);
        lobbyReadyButton.SetActive(!GameManager.Instance.isMaster);
        lobbyReadyButton.transform.Find("Text").GetComponent<TMP_Text>().text = "Ready";
        lobbyWaitingText.text = "";
        gameStateProgressBar.gameObject.SetActive(false);
        lobbyPlayerListObject.SetActive(!GameManager.Instance.isMaster);
        UIManager.Instance.pausePlayerListObject.SetActive(!GameManager.Instance.isMaster);
        individualMapsObject.SetActive(false);
        lobbyFinishButton.SetActive(false);
        lobbyFinishButton.transform.Find("Text").GetComponent<TMP_Text>().text = "Save Map";
        timerObject.SetActive(false);
        lobbySettingsButton.SetActive(IsHost || GameManager.Instance.isMaster);

        // set join code
        SetJoinCode(string.IsNullOrEmpty(joinCodeInput.text) ? GameManager.Instance.joinCode.Value.ToString() : joinCodeInput.text);

        // setup story dropdown
        SetupLobbyStorySelector();

        // update lobby ready state
        UpdateLobbyReadyState();
    }

    public void OnLobbyToggleReady()
    {
        if (GameManager.Instance.isLobbyStarted.Value == true || GameManager.Instance.isMaster) StartCoroutine(GameManager.Instance.OnLobbyStart());

        TMP_Text targetText = lobbyReadyButton.transform.Find("Text").GetComponent<TMP_Text>();
        bool currentState = targetText.text == "Ready" ? false : true;

        targetText.text = targetText.text == "Ready" ? "Unready" : "Ready";
        foreach (Transform child in lobbyPlayerView.transform) 
        { 
            if (!currentState) child.GetComponent<UIElement>().OnElementDisable(); 
            else child.GetComponent<UIElement>().OnElementEnable(); 
        }

        if (!GameManager.Instance.isMaster) GameManager.Instance.SetLobbyReadyStateServerRpc(!currentState);
    }

    public void UpdateLobbyReadyState()
    {
        if (GameManager.Instance.isLobbyStarted.Value || GameManager.Instance.playerList.Count == 0) return;
        lobbyWaitingText.text = "Waiting for players to ready up! ";
        lobbyWaitingText.text += GameManager.Instance.playerList.Count(p => p.IsLobbyReady.Value == true) + "/" + GameManager.Instance.playerList.Count;
    }

    public void OnLobbyStart()
    {
        lobbyStorySelector.transform.parent.parent.gameObject.SetActive(false);
        lobbyWaitingText.text = GameManager.Instance.isMaster ? "" : "Starting Game!";
        lobbyReadyButton.SetActive(false);
        foreach (Transform child in lobbyPlayerView.transform) { child.GetComponent<UIElement>().OnElementDisable(); }
        MapManager.Instance.UpdateMapReadyState();

        // master
        timerObject.SetActive(GameManager.Instance.isMaster);
        gameStateProgressBar.gameObject.SetActive(GameManager.Instance.isMaster);
        SetMasterGameState(1);
    }

    private void OnJoinCodeValueChanged(FixedString64Bytes oldValue, FixedString64Bytes newValue)
    {
        SetJoinCode(newValue.ToString());
    }

    public void SetJoinCode(string newValue)
    {
        lobbyJoinCodeText.text = newValue;
        UIManager.Instance.pauseJoinCodeText.text = newValue;
    }

    public string GetJoinCodeInput()
    {
        return joinCodeInput.text.Trim().ToUpper();
    }

    private void OnLobbyToggleSettings()
    {
        Vector2 targetPosition = new Vector2(lobbySettingsPage.GetComponent<RectTransform>().anchoredPosition.x > -290f ? -300f : 30f, -150f);
        StartCoroutine(UIManager.Instance.UIAnimator(lobbySettingsPage.GetComponent<RectTransform>(), targetPosition));
    }

    public void SetMasterGameState(int targetLevel)
    {
        if (!GameManager.Instance.isMaster) return;
        gameStateProgressBar.OnProgressUpdate(targetLevel);
    }

    private void OnMasterFinishButton()
    {
        if (MapNetworkManager.Instance.mapState.Value == MapState.Shared)
        {
            OnMasterSharedMapReady();
        }
        else
        {
            OnMasterEndGame();
        }
    }

    private void OnMasterSharedMapReady()
    {
        SetMasterGameState(3);
        UIManager.Instance.OnSyncTimerClientRpc(0f, false, false);
        MapNetworkManager.Instance.SetMapStateServerRpc(MapState.Explore);
        lobbyFinishButton.transform.Find("Text").GetComponent<TMP_Text>().text = "End Game";
        timerObject.SetActive(false);
    }

    private void OnMasterEndGame()
    {
        SetMasterGameState(4);
        MapManager.SharedInstance.mapObject.SetActive(false);
        individualMapsObject.SetActive(true);
        lobbyFinishButton.SetActive(false);
        MapNetworkManager.Instance.SetMapStateServerRpc(MapState.Individual);
    }

    public void OnMasterSharedMapEnabled()
    {
        SetMasterGameState(2);
        lobbyFinishButton.SetActive(true);
        MapManager.SharedInstance.mapObject.SetActive(true);
        MapManager.SharedInstance.iconPile.SetActive(true);
        MapManager.SharedInstance.OnMapToggleReady(false, false);
        MapManager.SharedInstance.mapObject.transform.Find("InnerLayer").gameObject.SetActive(false);
        MapManager.SharedInstance.SetupLayout();
    }
}