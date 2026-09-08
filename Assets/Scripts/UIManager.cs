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
    public TMP_InputField hostPlayerNameInput;

    [Header("Join Data")]
    [SerializeField] private GameObject joinObject;
    [SerializeField] private Button startClientButton;
    [SerializeField] private Button joinBackButton;
    public TMP_InputField joinCodeInput;
    public TMP_InputField joinPlayerNameInput;

    [Header("Pause Data")]
    [SerializeField] private GameObject pauseObject;
    [SerializeField] public Button resumeButton;
    [SerializeField] private Button quitButton;
    public TMP_Text joinCodeText;

    [Header("Menu Data")]
    [SerializeField] private GameObject menuObject;
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private Button offlineButton;

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
        joinPlayerNameInput.onValueChanged.AddListener(delegate { GameManager.Instance.OnNameChanged(hostPlayerNameInput.text); });

        // menu
        joinButton.onClick.AddListener(delegate { OnJoinGame(); });
        hostButton.onClick.AddListener(delegate { OnHostGame(); });
        offlineButton.onClick.AddListener(delegate { OnOfflineGame(); });

        // pause
        resumeButton.onClick.AddListener(delegate { GameManager.Instance.OnStartGame(); });
        quitButton.onClick.AddListener(delegate { OnBackButton(); });
    }

    private void ResetUIState()
    {
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
        GameManager.Instance.isOffline = false;
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
    }

    public void OnPauseToggle()
    {
        pauseObject.SetActive(!pauseObject.activeSelf);
    }
}
