using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

public class UIManager : NetworkBehaviour
{
    public static UIManager Instance;

    [Header("Host Data")]
    [SerializeField] private GameObject hostObject;
    [SerializeField] private Button startHostButton;
    [SerializeField] private Button hostBackButton;
    [SerializeField] private TMP_InputField hostPlayerNameInput;

    [Header("Join Data")]
    [SerializeField] private GameObject joinObject;
    [SerializeField] private Button startClientButton;
    [SerializeField] private Button joinBackButton;
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private TMP_InputField joinPlayerNameInput;

    [Header("Pause Data")]
    [SerializeField] private GameObject pauseObject;
    [SerializeField] public Button resumeButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private TMP_Text joinCodeText;
    [SerializeField] private TMP_Text playerListText;
    [SerializeField] private TMP_Text waitingOnHostText;

    [Header("Menu Data")]
    [SerializeField] private GameObject menuObject;
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private Button offlineButton;

    [Header("HUD Data")]
    [SerializeField] private Image crossHair;

    private void Awake()
    {
        Instance = this;

        // reset ui
        ResetUIState();
        menuObject.SetActive(true);

        // subscribe to events
        // join
        joinBackButton.onClick.AddListener(delegate { OnBackButton(); });
        startClientButton.onClick.AddListener(delegate { GameManager.Instance.OnStartClient(); });
        joinPlayerNameInput.onValueChanged.AddListener(delegate { GameManager.Instance.OnNameChanged(joinPlayerNameInput.text); });

        // host
        hostBackButton.onClick.AddListener(delegate { OnBackButton(); });
        startHostButton.onClick.AddListener(delegate { GameManager.Instance.OnStartHost(); });
        hostPlayerNameInput.onValueChanged.AddListener(delegate { GameManager.Instance.OnNameChanged(hostPlayerNameInput.text); });

        // menu
        joinButton.onClick.AddListener(delegate { OnJoinGame(); });
        hostButton.onClick.AddListener(delegate { OnHostGame(); });
        offlineButton.onClick.AddListener(delegate { OnOfflineGame(); });

        // pause
        resumeButton.onClick.AddListener(delegate { GameManager.Instance.OnStartGame(); });
        quitButton.onClick.AddListener(delegate { OnBackButton(); });
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

        if (PlayerController.Instance.rayHit.collider != null && PlayerController.Instance.rayHit.collider.gameObject.CompareTag("Interactable"))
        {
            crossHair.gameObject.GetComponent<RectTransform>().sizeDelta = new Vector3(22f, 22f, 22f);
        }
        else
        {
            crossHair.gameObject.GetComponent<RectTransform>().sizeDelta = new Vector3(15f, 15f, 15f);
        }        
    }

    private void ResetUIState()
    {
        waitingOnHostText.gameObject.SetActive(false);
        pauseObject.SetActive(false);
        hostObject.SetActive(false);
        joinObject.SetActive(false);
        menuObject.SetActive(false);
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

        ResetUIState();
        pauseObject.SetActive(true);
        joinCodeText.gameObject.SetActive(false);

        // start game
        GameManager.Instance.OnStartGame();
    }

    private void OnBackButton()
    {
        // reset networking & ui state
        GameManager.Instance.OnDisconnectClient();
        ResetUIState();

        menuObject.SetActive(true);

        // destroy spawned player
        if (PlayerController.Instance != null)
        {
            Destroy(PlayerController.Instance.gameObject);
        }
    }

    public void OnSessionConnect()
    {
        ResetUIState();
        pauseObject.SetActive(true);
        resumeButton.transform.GetChild(0).GetComponent<TMP_Text>().text = "Start";

        if (!NetworkManager.IsHost)
        {
            waitingOnHostText.gameObject.SetActive(true);
            resumeButton.gameObject.SetActive(false);
            joinCodeText.gameObject.SetActive(false);
        }
    }

    public void OnPauseToggle()
    {
        resumeButton.transform.GetChild(0).GetComponent<TMP_Text>().text = "Resume";
        pauseObject.SetActive(!pauseObject.activeSelf);

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
}
