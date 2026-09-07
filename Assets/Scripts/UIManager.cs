using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

public class UIManager : NetworkBehaviour
{
    public static UIManager Instance;
    [SerializeField] private GameObject pauseObject;
    [SerializeField] private Button startHostButton;
    [SerializeField] private Button startClientButton;
    [SerializeField] private Button backButton;
    public Button startGameButton;
    public TMP_Text joinCodeText;
    public TMP_InputField joinCodeInput;
    public TMP_InputField playerNameInput;

    [SerializeField] private GameObject menuObject;
    [SerializeField] private Button onlineButton;
    [SerializeField] private Button offlineButton;

    private void Awake()
    {
        Instance = this;

        pauseObject.SetActive(false);
        menuObject.SetActive(false);

        ResetUIElements();

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

    public void OnSessionConnect()
    {
        startHostButton.gameObject.SetActive(false);
        startClientButton.gameObject.SetActive(false);
        joinCodeInput.gameObject.SetActive(false);
        playerNameInput.gameObject.SetActive(false);
        startGameButton.gameObject.SetActive(true);

        if (!NetworkManager.IsHost)
        {
            joinCodeText.gameObject.SetActive(false);
        }
    }

    private void OnOnlinePlay()
    {
        startGameButton.gameObject.SetActive(false);
        menuObject.SetActive(false);
        pauseObject.SetActive(true);
    }

    private void OnOfflinePlay()
    {
        GameManager.Instance.isOffline = true;

        startHostButton.gameObject.SetActive(false);
        startClientButton.gameObject.SetActive(false);
        joinCodeInput.gameObject.SetActive(false);
        joinCodeText.gameObject.SetActive(false);
        playerNameInput.gameObject.SetActive(false);
        menuObject.SetActive(false);
        pauseObject.SetActive(true);

        OnSessionConnect();
        GameManager.Instance.SpawnPlayer(0);
    }

    private void OnBackButton()
    {
        GameManager.Instance.isOffline = false;
        GameManager.Instance.OnDisconnectClient();
        ResetUIElements();

        menuObject.SetActive(true);
        pauseObject.SetActive(false);

        if (PlayerController.Instance != null)
        {
            Destroy(PlayerController.Instance.gameObject);
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
