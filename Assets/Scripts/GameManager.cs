using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;
    public bool isPaused;
    public bool isOffline;
    public bool isConnected;
    [SerializeField] private GameObject mainCamera;
    public List<PlayerData> playerList = new List<PlayerData>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        Debug.Log("GameManager: Initializing");
        if (Instance != null) return;

        // create NetworkManager
        if (FindAnyObjectByType<NetworkManager>() == null)
        {
            GameObject networkManager = Instantiate(Resources.Load<GameObject>("NetworkManager"));
            networkManager.name = "NetworkManager";
            DontDestroyOnLoad(networkManager);
        }

        // create InputManager
        if (FindAnyObjectByType<InputManager>() == null)
        {
            GameObject inputManager = Instantiate(Resources.Load<GameObject>("InputManager"));
            inputManager.name = "InputManager";
            DontDestroyOnLoad(inputManager);
        }

        // create EventSystem
        if (FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject eventSystem = Instantiate(Resources.Load<GameObject>("EventSystem"));
            eventSystem.name = "EventSystem";
            DontDestroyOnLoad(eventSystem);
        }
    }

    private void Start()
    {
        Instance = this;
        isPaused = true;
        isOffline = false;
        isConnected = false;
        mainCamera = Camera.main.gameObject;
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();

        NetworkManager.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
    }

    public override void OnDestroy()
    {
        // networkmanager usually dies before this, keep it just in case
        try
        {
            NetworkManager.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;            
        }
        catch {}
    }

    public void OnPauseToggle()
    {
        if (!isConnected) return;

        isPaused = !isPaused;

        UIManager.Instance.OnPauseToggle();
    }

    public void OnMapToggle()
    {
        if (isPaused) return;

        MapManager.Instance.OnMapToggle();
    }

    public void OnNameChanged(string inputText)
    {
        PlayerPrefs.SetString("PlayerName", inputText);
        PlayerPrefs.Save();
    }

    public void OnStartGame()
    {
        isConnected = true;
        mainCamera.SetActive(false);
        OnPauseToggle();
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log("GameManager: Client connected: " + clientId);

        if (!NetworkManager.IsHost) return;
        SpawnPlayer(clientId);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!NetworkManager.IsListening) return;
        PlayerLeftClientRpc(clientId);
    }

    public async void OnStartHost()
    {
        string joinCode = await RelayManager.Instance.StartHost(4);

        if (!string.IsNullOrEmpty(joinCode))
        {
            UIManager.Instance.SetJoinCodeText("Join Code: " + joinCode);
            UIManager.Instance.OnSessionConnect();
        }
    }

    public async void OnStartClient()
    {
        string joinCode = UIManager.Instance.GetJoinCodeInput();

        if (!await RelayManager.Instance.JoinHost(joinCode))
        {
            Debug.Log("GameManager: Failed to join game.");
            return;
        }

        UIManager.Instance.OnSessionConnect();
    }

    public void OnDisconnectClient()
    {
        isConnected = false;
        try
        {
            NetworkManager.Shutdown();
        }
        catch (Exception ex)
        {
            Debug.Log("GameManager: Failed to disconnect from session. " + ex);
            return;
        }

        SceneManager.LoadScene(0);
    }

    public void SpawnPlayer(ulong clientId)
    {
        Debug.Log("GameManager: Spawning player for: " + clientId);
        GameObject playerObject = Instantiate(Resources.Load<GameObject>("Player"), Vector3.zero, Quaternion.identity);
        if (clientId == NetworkManager.LocalClientId) playerObject.name = "Player";

        if (!isOffline)
        {
            playerObject.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
        }
    }

    public void RefreshPlayerList()
    {
        playerList.Clear();

        PlayerData[] players = FindObjectsByType<PlayerData>();
        foreach (PlayerData player in players)
        {
            playerList.Add(player);
        }
        
        playerList.Sort((a, b) => a.OwnerClientId.CompareTo(b.OwnerClientId));
        UIManager.Instance.OnRefreshPlayerList();
    }

    [ClientRpc]
    private void PlayerLeftClientRpc(ulong clientId)
    {
        Debug.Log("GameManager: Client disconnected: " + clientId);

        playerList.RemoveAll(player => player.OwnerClientId == clientId);
        UIManager.Instance.OnRefreshPlayerList();
    }
}
