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

    [Header("Pause Data")]
    [SerializeField] private GameObject pauseObject;
    [SerializeField] public GameObject resumeButton;
    [SerializeField] private GameObject quitButton;
    [SerializeField] private GameObject storyDropdown;
    [SerializeField] private TMP_Text joinCodeText;
    [SerializeField] private TMP_Text playerListText;
    [SerializeField] private TMP_Text waitingOnHostText;

    [Header("Menu Data")]
    [SerializeField] private GameObject menuObject;
    [SerializeField] private GameObject hostButton;
    [SerializeField] private GameObject joinButton;
    [SerializeField] private GameObject offlineButton;

    [Header("HUD Data")]
    [SerializeField] private Image crossHair;
    [SerializeField] private Image pauseIcon;
    [SerializeField] private Image inventoryIcon;
    [SerializeField] private GameObject inventoryObject;
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
        hostBackClickEntry.callback.AddListener((eventData) => { OnBackButton(); });
        hostBackButton.GetComponent<EventTrigger>().triggers.Add(hostBackClickEntry);

        hostPlayerNameInput.onValueChanged.AddListener(delegate { GameManager.Instance.OnNameChanged(hostPlayerNameInput.text); });

        // join
        EventTrigger.Entry clientStartClickEntry = new EventTrigger.Entry() {eventID = EventTriggerType.PointerClick};
        clientStartClickEntry.callback.AddListener((eventData) => { GameManager.Instance.OnStartClient(); });
        startClientButton.GetComponent<EventTrigger>().triggers.Add(clientStartClickEntry);

        EventTrigger.Entry clientBackClickEntry = new EventTrigger.Entry() {eventID = EventTriggerType.PointerClick};
        clientBackClickEntry.callback.AddListener((eventData) => { OnBackButton(); });
        joinBackButton.GetComponent<EventTrigger>().triggers.Add(clientBackClickEntry);

        joinPlayerNameInput.onValueChanged.AddListener(delegate { GameManager.Instance.OnNameChanged(joinPlayerNameInput.text); });

        // pause
        EventTrigger.Entry resumeClickEntry = new EventTrigger.Entry() {eventID = EventTriggerType.PointerClick};
        resumeClickEntry.callback.AddListener((eventData) => { GameManager.Instance.OnStartGame(); });
        resumeButton.GetComponent<EventTrigger>().triggers.Add(resumeClickEntry);

        EventTrigger.Entry quitClickEntry = new EventTrigger.Entry() {eventID = EventTriggerType.PointerClick};
        quitClickEntry.callback.AddListener((eventData) => { OnBackButton(); });
        quitButton.GetComponent<EventTrigger>().triggers.Add(quitClickEntry);

        storyDropdown.GetComponent<TMP_Dropdown>().onValueChanged.AddListener(delegate { GameManager.Instance.activeStory = storyDropdown.GetComponent<TMP_Dropdown>().captionText.text; });
    }

    private void Update()
    {
        if (PlayerController.Instance)
        {
            CrosshairHandler();            
        }
    }

    private void CrosshairHandler()
    {
        crossHair.gameObject.SetActive(!Cursor.visible);

        if (PlayerController.Instance.rayHitObject != null && PlayerController.Instance.rayHitObject.CompareTag("Interactable"))
        {
            crossHair.gameObject.GetComponent<RectTransform>().sizeDelta = new Vector3(22f, 22f, 22f);
        }
        else
        {
            crossHair.gameObject.GetComponent<RectTransform>().sizeDelta = new Vector3(15f, 15f, 15f);
        }
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

    private void ResetUIState()
    {
        waitingOnHostText.gameObject.SetActive(false);
        pauseObject.SetActive(false);
        hostObject.SetActive(false);
        joinObject.SetActive(false);
        menuObject.SetActive(false);
        loadingIcon.SetActive(false);
        inventoryIcon.gameObject.SetActive(false);
        pauseIcon.gameObject.SetActive(false);
        inventoryObject.gameObject.SetActive(true);
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
        GameManager.Instance.isOffline = true;

        // trigger offline player spawn
        GameManager.Instance.SpawnPlayer(0);

        // trigger offline sharedmap spawn
        GameManager.Instance.SpawnSharedMap(0);

        ResetUIState();
        pauseObject.SetActive(true);
        joinCodeText.gameObject.SetActive(false);

        // start game
        GameManager.Instance.OnStartGame();
    }

    private void OnBackButton()
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

    public void OnSessionConnect()
    {
        ResetUIState();
        pauseObject.SetActive(true);
        resumeButton.transform.GetChild(0).GetComponent<TMP_Text>().text = "Start";

        loadingIcon.SetActive(false);

        if (!NetworkManager.IsHost)
        {
            waitingOnHostText.gameObject.SetActive(true);
            storyDropdown.gameObject.SetActive(false);
            resumeButton.gameObject.SetActive(false);
            joinCodeText.gameObject.SetActive(false);
        }

        // setup story dropdown
        SetupStoryDropdown();

        GameManager.Instance.OnInventoryToggle();
    }

    public void OnPauseToggle()
    {
        resumeButton.transform.GetChild(0).GetComponent<TMP_Text>().text = "Resume";
        storyDropdown.gameObject.SetActive(!GameManager.Instance.isConnected);
        pauseObject.SetActive(!pauseObject.activeSelf);
        pauseIcon.gameObject.SetActive(!pauseObject.activeSelf);
        inventoryIcon.gameObject.SetActive(!pauseObject.activeSelf);
    }

    public void OnRefreshPlayerList()
    {
        playerListText.text = "";
        foreach (var player in GameManager.Instance.playerList)
        {
            string targetText = player.PlayerName.Value.ToString();
            targetText += player.OwnerClientId == NetworkManager.LocalClientId ? " (you)" : "";
            targetText += player.OwnerClientId == NetworkManager.ServerClientId ? " (host)" : "";
            playerListText.text += targetText + "\n";
        }
    }

    public void SetJoinCodeText(string targetText)
    {
        joinCodeText.text = targetText;
    }

    public string GetJoinCodeInput()
    {
        return joinCodeInput.text.Trim().ToUpper();
    }

    public void OnLobbyStart()
    {
        resumeButton.gameObject.SetActive(true);
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
