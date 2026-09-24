using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System.Linq;

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
    [SerializeField] private GameObject LegArrowRight;
    [SerializeField] private GameObject LegArrowLeft;

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

        EventTrigger.Entry leftArrowClickEntry = new EventTrigger.Entry() {eventID = EventTriggerType.PointerClick};
        leftArrowClickEntry.callback.AddListener((eventData) => { GameManager.Instance.SetPlayerSkinIdServerRpc(Mathf.Clamp(GameManager.Instance.FetchPlayerDataById(NetworkManager.LocalClientId).SkinId.Value - 1, 0, 3)); });
        LegArrowLeft.GetComponent<EventTrigger>().triggers.Add(leftArrowClickEntry);        

        EventTrigger.Entry rightArrowClickEntry = new EventTrigger.Entry() {eventID = EventTriggerType.PointerClick};
        rightArrowClickEntry.callback.AddListener((eventData) => { GameManager.Instance.SetPlayerSkinIdServerRpc(Mathf.Clamp(GameManager.Instance.FetchPlayerDataById(NetworkManager.LocalClientId).SkinId.Value + 1, 0, 3)); });
        LegArrowRight.GetComponent<EventTrigger>().triggers.Add(rightArrowClickEntry);

        // pause
        EventTrigger.Entry resumeClickEntry = new EventTrigger.Entry() {eventID = EventTriggerType.PointerClick};
        resumeClickEntry.callback.AddListener((eventData) => { GameManager.Instance.OnPauseToggle(); });
        resumeButton.GetComponent<EventTrigger>().triggers.Add(resumeClickEntry);

        EventTrigger.Entry quitClickEntry = new EventTrigger.Entry() {eventID = EventTriggerType.PointerClick};
        quitClickEntry.callback.AddListener((eventData) => { OnQuitButton(); });
        pauseQuitButton.GetComponent<EventTrigger>().triggers.Add(quitClickEntry);

        storyDropdown.GetComponent<TMP_Dropdown>().onValueChanged.AddListener(delegate { GameManager.Instance.activeStory = storyDropdown.GetComponent<TMP_Dropdown>().captionText.text; });
    }

    private void Update()
    {
        if (PlayerController.Instance)
        {
            UpdateMinimapPosition();            
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

        // setup triggers
        EventTrigger.Entry closeClickEntry = new EventTrigger.Entry() {eventID = EventTriggerType.PointerClick};
        closeClickEntry.callback.AddListener((eventData) => { OnCloseDialog(); });
        closeButton.GetComponent<EventTrigger>().triggers.Add(closeClickEntry);

        EventTrigger.Entry mainClickEntry = new EventTrigger.Entry() {eventID = EventTriggerType.PointerClick};
        mainClickEntry.callback.AddListener((eventData) => { DialogActionHandler(targetAction); });
        mainButton.GetComponent<EventTrigger>().triggers.Add(mainClickEntry);
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
}
