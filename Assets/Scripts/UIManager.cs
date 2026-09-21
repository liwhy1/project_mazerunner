using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class UIManager : NetworkBehaviour
{
    public static UIManager Instance;

    [Header("Host Data")]
    [SerializeField] private GameObject hostObject;
    [SerializeField] private GameObject startHostButton;
    [SerializeField] private GameObject hostBackButton;
    [SerializeField] private TMP_InputField hostPlayerNameInput;

    [Header("Join Data")]
    [SerializeField] private GameObject joinObject;
    [SerializeField] private GameObject startClientButton;
    [SerializeField] private GameObject joinBackButton;
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private TMP_InputField joinPlayerNameInput;

    [Header("Menu Data")]
    [SerializeField] private GameObject menuObject;
    [SerializeField] private GameObject hostButton;
    [SerializeField] private GameObject joinButton;
    [SerializeField] private GameObject offlineButton;

    [Header("Pause Data")]
    [SerializeField] private GameObject pauseObject;
    [SerializeField] public GameObject resumeButton;
    [SerializeField] private GameObject pauseQuitButton;
    [SerializeField] private TMP_Text pauseJoinCodeText;
    [SerializeField] private TMP_Text pausePlayerListText;

    [Header("Lobby Data")]
    [SerializeField] private GameObject lobbyObject;
    [SerializeField] public GameObject startButton;
    [SerializeField] private GameObject lobbyQuitButton;
    [SerializeField] private GameObject storyDropdown;
    [SerializeField] private TMP_Text lobbyJoinCodeText;
    [SerializeField] private TMP_Text lobbyPlayerListText;
    [SerializeField] private TMP_Text waitingOnHostText;

    [Header("HUD Data")]
    [SerializeField] private Image pauseIcon;
    [SerializeField] private Image inventoryIcon;
    public Image cameraZoomInIcon;
    public Image cameraZoomOutIcon;
    public GameObject loadingIcon;
    public GameObject pageBackground;

    [Header("Dialog Data")]
    public GameObject activeDialog;

    private void Start()
    {
        Debug.Log("UIManager: Setting up");

        // setup vars
        Instance = this;

        // reset ui
        ResetUIState();

        // enable menu
        menuObject.SetActive(true);

        // subscribe to events(watch vod)
        // menu
        EventTrigger.Entry hostClickEntry = new EventTrigger.Entry() {eventID = EventTriggerType.PointerClick};
        hostClickEntry.callback.AddListener((eventData) => { OnHostGame(); });
        hostButton.GetComponent<EventTrigger>().triggers.Add(hostClickEntry);

        EventTrigger.Entry joinClickEntry = new EventTrigger.Entry() {eventID = EventTriggerType.PointerClick};
        joinClickEntry.callback.AddListener((eventData) => { OnJoinGame(); });
        joinButton.GetComponent<EventTrigger>().triggers.Add(joinClickEntry);

        EventTrigger.Entry offlineClickEntry = new EventTrigger.Entry() {eventID = EventTriggerType.PointerClick};
        offlineClickEntry.callback.AddListener((eventData) => { OnOfflineGame(); });
        offlineButton.GetComponent<EventTrigger>().triggers.Add(offlineClickEntry);

        // host
        EventTrigger.Entry hostStartClickEntry = new EventTrigger.Entry() {eventID = EventTriggerType.PointerClick};
        hostStartClickEntry.callback.AddListener((eventData) => { GameManager.Instance.OnStartHost(); });
        startHostButton.GetComponent<EventTrigger>().triggers.Add(hostStartClickEntry);

        EventTrigger.Entry hostBackClickEntry = new EventTrigger.Entry() {eventID = EventTriggerType.PointerClick};
        hostBackClickEntry.callback.AddListener((eventData) => { OnQuitButton(); });
        hostBackButton.GetComponent<EventTrigger>().triggers.Add(hostBackClickEntry);

        hostPlayerNameInput.onValueChanged.AddListener(delegate { GameManager.Instance.OnNameChanged(hostPlayerNameInput.text); });

        // join
        EventTrigger.Entry clientStartClickEntry = new EventTrigger.Entry() {eventID = EventTriggerType.PointerClick};
        clientStartClickEntry.callback.AddListener((eventData) => { GameManager.Instance.OnStartClient(); });
        startClientButton.GetComponent<EventTrigger>().triggers.Add(clientStartClickEntry);

        EventTrigger.Entry clientBackClickEntry = new EventTrigger.Entry() {eventID = EventTriggerType.PointerClick};
        clientBackClickEntry.callback.AddListener((eventData) => { OnQuitButton(); });
        joinBackButton.GetComponent<EventTrigger>().triggers.Add(clientBackClickEntry);

        joinPlayerNameInput.onValueChanged.AddListener(delegate { GameManager.Instance.OnNameChanged(joinPlayerNameInput.text); });

        // lobby
        EventTrigger.Entry startClickEntry = new EventTrigger.Entry() {eventID = EventTriggerType.PointerClick};
        startClickEntry.callback.AddListener((eventData) => { GameManager.Instance.OnLobbyStart(); });
        startButton.GetComponent<EventTrigger>().triggers.Add(startClickEntry);

        EventTrigger.Entry lobbyQuitClickEntry = new EventTrigger.Entry() {eventID = EventTriggerType.PointerClick};
        lobbyQuitClickEntry.callback.AddListener((eventData) => { OnQuitButton(); });
        lobbyQuitButton.GetComponent<EventTrigger>().triggers.Add(lobbyQuitClickEntry);

        // pause
        EventTrigger.Entry resumeClickEntry = new EventTrigger.Entry() {eventID = EventTriggerType.PointerClick};
        resumeClickEntry.callback.AddListener((eventData) => { GameManager.Instance.OnPauseToggle(); });
        resumeButton.GetComponent<EventTrigger>().triggers.Add(resumeClickEntry);

        EventTrigger.Entry quitClickEntry = new EventTrigger.Entry() {eventID = EventTriggerType.PointerClick};
        quitClickEntry.callback.AddListener((eventData) => { OnQuitButton(); });
        pauseQuitButton.GetComponent<EventTrigger>().triggers.Add(quitClickEntry);

        storyDropdown.GetComponent<TMP_Dropdown>().onValueChanged.AddListener(delegate { GameManager.Instance.activeStory = storyDropdown.GetComponent<TMP_Dropdown>().captionText.text; });
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
        pauseIcon.gameObject.SetActive(true);
    }

    private void OnJoinGame()
    {
        ResetUIState();
        joinObject.SetActive(true);
    }

    private void OnHostGame()
    {
        ResetUIState();
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

        if (!NetworkManager.IsHost)
        {
            waitingOnHostText.gameObject.SetActive(true);
            storyDropdown.gameObject.SetActive(false);
            startButton.gameObject.SetActive(false);
        }
        else
        {
            // setup story dropdown
            SetupStoryDropdown();
        }
    }

    public void OnPauseToggle()
    {
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
        lobbyJoinCodeText.text = "Join Code: " + targetText;
        pauseJoinCodeText.text = "Join Code: " + targetText;
    }

    public string GetJoinCodeInput()
    {
        return joinCodeInput.text.Trim().ToUpper();
    }

    public void OnLobbyStart()
    {
        startButton.gameObject.SetActive(true);
        waitingOnHostText.gameObject.SetActive(false);
    }

    public void OnOpenDialog()
    {
        // close any active dialogs
        OnCloseDialog();

        // create new dialog
        GameObject newDialog = Instantiate(Resources.Load<GameObject>("TextDialog"), transform);
        newDialog.transform.localPosition = Vector3.zero;
        activeDialog = newDialog;

        // setup triggers
        EventTrigger.Entry pointerClickEntry = new EventTrigger.Entry() {eventID = EventTriggerType.PointerClick};
        pointerClickEntry.callback.AddListener((eventData) => { OnCloseDialog(); });
        activeDialog.transform.Find("CloseButton").GetComponent<EventTrigger>().triggers.Add(pointerClickEntry);
    }

    public void OnCloseDialog()
    {
        if (activeDialog)
        {
            Destroy(activeDialog);
            activeDialog = null;
        }
    }
}
