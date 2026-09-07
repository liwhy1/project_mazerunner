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

    private void Awake()
    {
        Instance = this;
        pauseObject.SetActive(false);

        startHostButton.onClick.AddListener(delegate { GameManager.Instance.OnStartHost(); });
        startClientButton.onClick.AddListener(delegate { GameManager.Instance.OnStartClient(); });
        stopClientButton.onClick.AddListener(delegate { GameManager.Instance.OnDisconnectClient(); });
        startGameButton.onClick.AddListener(delegate { GameManager.Instance.OnStartGame(); });
        playerNameInput.onValueChanged.AddListener(delegate { GameManager.Instance.OnNameChanged(); });
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

    public void OnPauseToggle()
    {
        pauseObject.SetActive(!pauseObject.activeSelf);
    }
}
