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
    [SerializeField] private TMP_InputField hostPlayerNameInput;
    [SerializeField] private UIVerticalSelector playerModeSelector;
    [SerializeField] private GameObject startHostButton;
    [SerializeField] private GameObject hostBackButton;

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
    [SerializeField] private UIVerticalSelector storySelector;
    public TMP_Text lobbyJoinCodeText;
    public TMP_Text lobbyPlayerListText;
    [SerializeField] private TMP_Text waitingOnHostText;
    [SerializeField] private TMP_Text gameStateText;
    [SerializeField] private GameObject finishButton;
    public GameObject sharedMapView;
    [SerializeField] private GameObject timerObject;
    public GameObject individualMapsObject;

    [Header("Player Customisation Data")]
    [SerializeField] private GameObject playerView;
    [SerializeField] private GameObject headArrowLeft;
    [SerializeField] private GameObject headArrowRight;
    [SerializeField] private GameObject bodyArrowLeft;
    [SerializeField] private GameObject bodyArrowRight;
    [SerializeField] private GameObject legArrowLeft;
    [SerializeField] private GameObject legArrowRight;

    public void OnSetup()
    {
        Debug.Log("MenuManager: Setting up");
        Instance = this;

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
        UIManager.Instance.AddEventTrigger(settingsButton.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, UIManager.Instance.OnOpenSettings);
        UIManager.Instance.AddEventTrigger(settingsButton.GetComponent<EventTrigger>(), EventTriggerType.Submit, UIManager.Instance.OnOpenSettings);
        UIManager.Instance.AddEventTrigger(editorButton.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, OnOpenEditor);
        UIManager.Instance.AddEventTrigger(editorButton.GetComponent<EventTrigger>(), EventTriggerType.Submit, UIManager.Instance.OnOpenSettings);

        // host
        UIManager.Instance.AddEventTrigger(startHostButton.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, OnStartHost);
        UIManager.Instance.AddEventTrigger(startHostButton.GetComponent<EventTrigger>(), EventTriggerType.Submit, OnStartHost);
        UIManager.Instance.AddEventTrigger(hostBackButton.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, OnOpenMenu);
        UIManager.Instance.AddEventTrigger(hostBackButton.GetComponent<EventTrigger>(), EventTriggerType.Submit, OnOpenMenu);
        playerModeSelector.onValueChanged += () => OnPlayerModeSelectorChanged(playerModeSelector.currentSelection);
        hostPlayerNameInput.onValueChanged.AddListener(delegate { GameManager.Instance.OnNameChanged(hostPlayerNameInput.text); });
        hostPlayerNameInput.onSubmit.AddListener(delegate { GameManager.Instance.OnStartHost(); });

        // join
        UIManager.Instance.AddEventTrigger(startClientButton.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, GameManager.Instance.OnStartClient);
        UIManager.Instance.AddEventTrigger(startClientButton.GetComponent<EventTrigger>(), EventTriggerType.Submit, GameManager.Instance.OnStartClient);
        UIManager.Instance.AddEventTrigger(joinBackButton.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, OnOpenMenu);
        UIManager.Instance.AddEventTrigger(joinBackButton.GetComponent<EventTrigger>(), EventTriggerType.Submit, OnOpenMenu);
        joinPlayerNameInput.onValueChanged.AddListener(delegate { GameManager.Instance.OnNameChanged(joinPlayerNameInput.text); });
        joinCodeInput.onSubmit.AddListener(delegate { GameManager.Instance.OnStartClient(); });

        // lobby
        UIManager.Instance.AddEventTrigger(startButton.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, GameManager.Instance.OnLobbyStart);
        UIManager.Instance.AddEventTrigger(startButton.GetComponent<EventTrigger>(), EventTriggerType.Submit, GameManager.Instance.OnLobbyStart);
        UIManager.Instance.AddEventTrigger(finishButton.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, OnMasterSharedMapReady);
        UIManager.Instance.AddEventTrigger(finishButton.GetComponent<EventTrigger>(), EventTriggerType.Submit, OnMasterSharedMapReady);
        UIManager.Instance.AddEventTrigger(lobbyQuitButton.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, UIManager.Instance.OnQuitButton);
        UIManager.Instance.AddEventTrigger(lobbyQuitButton.GetComponent<EventTrigger>(), EventTriggerType.Submit, OnOpenMenu);
        storySelector.onValueChanged += () => GameManager.Instance.SetActiveStory(storySelector.currentSelection);

        // player customisation
        UIManager.Instance.AddEventTrigger(headArrowLeft.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, DecreasePlayerSkinValue, "hair");
        UIManager.Instance.AddEventTrigger(headArrowRight.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, IncreasePlayerSkinValue, "hair");
        UIManager.Instance.AddEventTrigger(bodyArrowLeft.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, DecreasePlayerSkinValue, "color");
        UIManager.Instance.AddEventTrigger(bodyArrowRight.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, IncreasePlayerSkinValue, "color");
        UIManager.Instance.AddEventTrigger(legArrowLeft.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, DecreasePlayerSkinValue, "gender");
        UIManager.Instance.AddEventTrigger(legArrowRight.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, IncreasePlayerSkinValue, "gender");
    }

    private void SetupStorySelector()
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
        storySelector.selectorItems.Clear();
        storySelector.selectorItems = storyNames;
        storySelector.OnUpdateSelection(storySelector.selectorItems.Count - 1);
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
        if (playerModeSelector.currentSelection == "Player")
        {
            GameManager.Instance.OnStartHost();
        }
        else
        {
            GameManager.Instance.OnStartMaster();
        }
    }

    private void OnOpenEditor()
    {
        UIManager.Instance.OnOpenDialog("Notice", "Would you like to open the map editor?", "Continue", "openeditor");
    }

    public void OnOpenMenu()
    {
        ResetUIState();
        menuObject.SetActive(true);
        //UIManager.Instance.eventSystem.SetSelectedGameObject(hostButton);
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
        UIManager.Instance.loadingIcon.SetActive(false);

        // conditionally enable elements
        playerView.SetActive(!GameManager.Instance.isMaster);
        gameStateText.gameObject.SetActive(GameManager.Instance.isMaster);
        gameStateText.text = "";
        individualMapsObject.SetActive(false);
        waitingOnHostText.gameObject.SetActive(!NetworkManager.IsHost);
        storySelector.gameObject.SetActive(NetworkManager.IsHost);
        startButton.SetActive(NetworkManager.IsHost);
        finishButton.SetActive(false);
        sharedMapView.SetActive(false);
        timerObject.SetActive(false);
        //UIManager.Instance.eventSystem.SetSelectedGameObject(NetworkManager.IsHost ? storyDropdown : lobbyQuitButton);
        SetJoinCode(string.IsNullOrEmpty(joinCodeInput.text) ? GameManager.Instance.joinCode.Value.ToString() : joinCodeInput.text);

        // setup story dropdown
        SetupStorySelector();
    }

    public void OnLobbyStart()
    {
        startButton.SetActive(!GameManager.Instance.isMaster);
        timerObject.SetActive(GameManager.Instance.isMaster);
        storySelector.gameObject.SetActive(false);
        waitingOnHostText.gameObject.SetActive(false);
        SetMasterGameStateText("Individual mapping\n" + GameManager.Instance.playerList.Count(p => p.IsMapReady.Value == true) + "/" + GameManager.Instance.playerList.Count);
    }

    private void OnJoinCodeValueChanged(FixedString64Bytes oldValue, FixedString64Bytes newValue)
    {
        SetJoinCode(newValue.ToString());
    }

    public void SetJoinCode(string newValue)
    {
        lobbyJoinCodeText.text = "Join Code: \n<b>" + newValue;
        UIManager.Instance.pauseJoinCodeText.text = "Join Code: \n<b>" + newValue;
    }

    public string GetJoinCodeInput()
    {
        return joinCodeInput.text.Trim().ToUpper();
    }

    public void SetMasterGameStateText(string targetText)
    {
        gameStateText.text = "<b>Game State:</b>\n";
        gameStateText.text += targetText;
    }

    public void OnMasterSharedMapReady()
    {
        SetMasterGameStateText("Explore map");
        MapNetworkManager.Instance.SetMapStateServerRpc(MapState.Individual);
        sharedMapView.SetActive(false);
        finishButton.SetActive(false);
        individualMapsObject.SetActive(true);
        timerObject.SetActive(false);
    }

    public void OnMasterSharedMapEnabled()
    {
        SetMasterGameStateText("Shared mapping");
        finishButton.SetActive(true);
        GameManager.Instance.mapCamera.gameObject.SetActive(true);
        GameManager.Instance.mapCamera.transform.position = new Vector3(0.33f, GameManager.Instance.mapCamera.transform.position.y, 0.34f);
        GameManager.Instance.mapCamera.targetTexture = (RenderTexture)sharedMapView.GetComponent<RawImage>().texture;
        sharedMapView.SetActive(true);
    }
}