using System.Linq;
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
    [SerializeField] private Button stopClientButton;
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

        startHostButton.onClick.AddListener(delegate { GameManager.Instance.OnStartHost(); });
        startClientButton.onClick.AddListener(delegate { GameManager.Instance.OnStartClient(); });
        stopClientButton.onClick.AddListener(delegate { GameManager.Instance.OnDisconnectClient(); });
        startGameButton.onClick.AddListener(delegate { GameManager.Instance.OnStartGame(); });
        playerNameInput.onValueChanged.AddListener(delegate { GameManager.Instance.OnNameChanged(); });

        onlineButton.onClick.AddListener(delegate { OnOnlinePlay(); });
        offlineButton.onClick.AddListener(delegate { OnOfflinePlay(); });
    }

    public void OnSessionConnect()
    {
        startHostButton.gameObject.SetActive(false);
        startClientButton.gameObject.SetActive(false);
        joinCodeInput.gameObject.SetActive(false);
        playerNameInput.gameObject.SetActive(false);
        startGameButton.gameObject.SetActive(true);

        if (!GameManager.Instance.isOffline)
        {
            stopClientButton.gameObject.SetActive(true);            
        }

        if (!NetworkManager.IsHost)
        {
            joinCodeText.gameObject.SetActive(false);
        }
    }

    private void OnOnlinePlay()
    {
        OnMenuToggle();
        OnPauseToggle();
    }

    private void OnOfflinePlay()
    {
        GameManager.Instance.isOffline = true;
        OnMenuToggle();
        OnPauseToggle();
        OnSessionConnect();
        GameManager.Instance.SpawnPlayer(0);
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
