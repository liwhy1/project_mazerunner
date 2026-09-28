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
    public TMP_Text lobbyJoinCodeText;
    public TMP_Text lobbyPlayerListText;
    [SerializeField] private TMP_Text waitingOnHostText;
    [SerializeField] private GameObject playerView;
    [SerializeField] private GameObject LegArrowRight;
    [SerializeField] private GameObject LegArrowLeft;

    [SerializeField] private TMP_Text gameStateText;
    [SerializeField] private GameObject finishButton;
    [SerializeField] private GameObject sharedMapView;
    [SerializeField] private GameObject timerObject;

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
        hostPlayerNameInput.text = "";
        joinPlayerNameInput.text = "";
        joinCodeInput.text = "";
    }

    private void SetupUITriggers()
    {
        // menu
        UIManager.Instance.AddEventTrigger(hostButton.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, OnHostGame);
        UIManager.Instance.AddEventTrigger(hostButton.GetComponent<EventTrigger>(), EventTriggerType.Submit, OnHostGame);
        UIManager.Instance.AddEventTrigger(joinButton.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, OnJoinGame);
        UIManager.Instance.AddEventTrigger(joinButton.GetComponent<EventTrigger>(), EventTriggerType.Submit, OnJoinGame);
        UIManager.Instance.AddEventTrigger(offlineButton.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, OnOfflineGame);
        UIManager.Instance.AddEventTrigger(offlineButton.GetComponent<EventTrigger>(), EventTriggerType.Submit, OnOfflineGame);

        // host
        UIManager.Instance.AddEventTrigger(startMasterButton.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, GameManager.Instance.OnStartMaster);
        UIManager.Instance.AddEventTrigger(startMasterButton.GetComponent<EventTrigger>(), EventTriggerType.Submit, GameManager.Instance.OnStartMaster);
        UIManager.Instance.AddEventTrigger(startHostButton.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, GameManager.Instance.OnStartHost);
        UIManager.Instance.AddEventTrigger(startHostButton.GetComponent<EventTrigger>(), EventTriggerType.Submit, GameManager.Instance.OnStartHost);
        UIManager.Instance.AddEventTrigger(hostBackButton.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, OnOpenMenu);
        UIManager.Instance.AddEventTrigger(hostBackButton.GetComponent<EventTrigger>(), EventTriggerType.Submit, OnOpenMenu);
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
        storyDropdown.GetComponent<TMP_Dropdown>().onValueChanged.AddListener(delegate { GameManager.Instance.SetActiveStoryClientRpc(storyDropdown.GetComponent<TMP_Dropdown>().captionText.text); });

        UIManager.Instance.AddEventTrigger(LegArrowLeft.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, DecreasePlayerSkinValue, "color");
        UIManager.Instance.AddEventTrigger(LegArrowRight.GetComponent<EventTrigger>(), EventTriggerType.PointerClick, IncreasePlayerSkinValue, "color");
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
        GameManager.Instance.SetActiveStoryClientRpc(storyNames[0]);
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

    private void OnOpenMenu()
    {
        ResetUIState();
        menuObject.SetActive(true);
        UIManager.Instance.eventSystem.SetSelectedGameObject(hostButton);
    }

    private void OnJoinGame()
    {
        ResetUIState();
        UIManager.Instance.eventSystem.SetSelectedGameObject(joinPlayerNameInput.gameObject);
        joinObject.SetActive(true);
    }

    private void OnHostGame()
    {
        ResetUIState();
        UIManager.Instance.eventSystem.SetSelectedGameObject(hostPlayerNameInput.gameObject);
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
        UIManager.Instance.eventSystem.SetSelectedGameObject(storyDropdown);

        // setup inventory
        InventoryManager.Instance.OnSetup();

        playerView.SetActive(true);
        finishButton.SetActive(false);
        lobbyObject.SetActive(true);
        lobbyJoinCodeText.gameObject.SetActive(false);
        lobbyPlayerListText.gameObject.SetActive(false);
        UIManager.Instance.pauseJoinCodeText.gameObject.SetActive(false);
        UIManager.Instance.pausePlayerListText.gameObject.SetActive(false);
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
        waitingOnHostText.gameObject.SetActive(!NetworkManager.IsHost);
        storyDropdown.gameObject.SetActive(NetworkManager.IsHost);
        startButton.SetActive(NetworkManager.IsHost);
        finishButton.SetActive(false);
        sharedMapView.SetActive(false);
        timerObject.SetActive(false);
        UIManager.Instance.eventSystem.SetSelectedGameObject(NetworkManager.IsHost ? storyDropdown : lobbyQuitButton);
        SetJoinCode(string.IsNullOrEmpty(joinCodeInput.text) ? GameManager.Instance.joinCode.Value.ToString() : joinCodeInput.text);

        // setup story dropdown
        SetupStoryDropdown();
    }

    public void OnLobbyStart()
    {
        startButton.SetActive(!GameManager.Instance.isMaster);
        timerObject.SetActive(GameManager.Instance.isMaster);
        storyDropdown.SetActive(false);
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
        SharedMapManager.Instance.OnSharedMapReadyClientRpc();
        finishButton.SetActive(false);
    }

    public void OnMasterSharedMapEnabled()
    {
        SetMasterGameStateText("Shared mapping");
        finishButton.SetActive(true);
        GameManager.Instance.mapCamera.gameObject.SetActive(true);
        GameManager.Instance.mapCamera.transform.position = new Vector3(0.33f, -100f, 0.34f);
        GameManager.Instance.mapCamera.targetTexture = (RenderTexture)sharedMapView.GetComponent<RawImage>().texture;
        sharedMapView.SetActive(true);
    }
}