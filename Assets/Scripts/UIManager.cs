using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

public class UIManager : NetworkBehaviour
{
    public static UIManager Instance;

    [Header("Pause Data")]
    [SerializeField] private GameObject pauseObject;
    [SerializeField] private Button startHostButton;
    [SerializeField] private Button startClientButton;
    [SerializeField] private Button backButton;
    public Button startGameButton;
    public TMP_Text joinCodeText;
    public TMP_InputField joinCodeInput;
    public TMP_InputField playerNameInput;

    [Header("Menu Data")]
    [SerializeField] private GameObject menuObject;
    [SerializeField] private Button onlineButton;
    [SerializeField] private Button offlineButton;

    private void Awake()
    {
        Instance = this;

        // setup vars
        pauseObject.SetActive(false);
        menuObject.SetActive(false);

        // reset ui active state
        ResetUIElements();

        // subscribe to events 
        startHostButton.onClick.AddListener(delegate { GameManager.Instance.OnStartHost(); });
        startClientButton.onClick.AddListener(delegate { GameManager.Instance.OnStartClient(); });
        startGameButton.onClick.AddListener(delegate { GameManager.Instance.OnStartGame(); });
        backButton.onClick.AddListener(delegate { OnBackButton(); });
        playerNameInput.onValueChanged.AddListener(delegate { GameManager.Instance.OnNameChanged(); });

        onlineButton.onClick.AddListener(delegate { OnOnlinePlay(); });
        offlineButton.onClick.AddListener(delegate { OnOfflinePlay(); });
    }

    private void ResetUIElements()
    {
        startHostButton.gameObject.SetActive(true);
        startGameButton.gameObject.SetActive(true);
        startClientButton.gameObject.SetActive(true);
        backButton.gameObject.SetActive(true);
        onlineButton.gameObject.SetActive(true);
        offlineButton.gameObject.SetActive(true);
        joinCodeInput.gameObject.SetActive(true);
        joinCodeText.gameObject.SetActive(true);
        playerNameInput.gameObject.SetActive(true);
    }

    private void OnOnlinePlay()
    {
        startGameButton.gameObject.SetActive(false);
        menuObject.SetActive(false);
        pauseObject.SetActive(true);
    }

    private void OnOfflinePlay()
    {
        // set gamestate to offline
        GameManager.Instance.isOffline = true;

        startHostButton.gameObject.SetActive(false);
        startClientButton.gameObject.SetActive(false);
        joinCodeInput.gameObject.SetActive(false);
        joinCodeText.gameObject.SetActive(false);
        playerNameInput.gameObject.SetActive(false);
        menuObject.SetActive(false);
        pauseObject.SetActive(true);

        // move to "connected session" ui state
        OnSessionConnect();

        // trigger offline player spawn
        GameManager.Instance.SpawnPlayer(0);
    }

    private void OnBackButton()
    {
        // reset networking & ui state
        GameManager.Instance.isOffline = false;
        GameManager.Instance.OnDisconnectClient();
        ResetUIElements();

        menuObject.SetActive(true);
        pauseObject.SetActive(false);

        // destroy spawned player
        if (PlayerController.Instance != null)
        {
            Destroy(PlayerController.Instance.gameObject);
        }
    }

    public void OnSessionConnect()
    {
        startHostButton.gameObject.SetActive(false);
        startClientButton.gameObject.SetActive(false);
        joinCodeInput.gameObject.SetActive(false);
        playerNameInput.gameObject.SetActive(false);
        startGameButton.gameObject.SetActive(true);

        // disable session code text on clients
        if (!NetworkManager.IsHost)
        {
            joinCodeText.gameObject.SetActive(false);
        }
    }

    public void OnPauseToggle()
    {
        pauseObject.SetActive(!pauseObject.activeSelf);
    }

    public void OnMenuToggle()
    {
        menuObject.SetActive(!menuObject.activeSelf);
    }
}
