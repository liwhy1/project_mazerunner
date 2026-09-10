using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;
    public bool isPaused;
    public bool isOffline;
    public bool isConnected;
    [SerializeField] private GameObject mainCamera;
    public Camera mapCamera;
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

    public void OnInventoryToggle()
    {
        if (isPaused) return;

        UIManager.Instance.OnToggleInventory();
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
        OnInventoryToggle();

        if (isOffline) return;
        if (SharedMapManager.Instance)
        {
            SharedMapManager.Instance.gameObject.GetComponent<Canvas>().worldCamera = mapCamera;
        }
        // notify clients about lobby start
        OnLobbyStartClientRpc();
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log("GameManager: Client connected: " + clientId);

        // move to lobby ui
        if (clientId == NetworkManager.LocalClientId)
        {
            UIManager.Instance.OnSessionConnect();
        }

        if (!NetworkManager.IsHost) return;

        SpawnPlayer(clientId);

        if (!SharedMapManager.Instance)
        {
            SpawnSharedMap(clientId);
        }

        // notify new clients about lobby status
        if (!isOffline && isConnected)
        {
            OnLobbyStartClientRpc();            
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsSpawned || !NetworkManager.IsListening || !NetworkManager.IsConnectedClient) return;
        PlayerLeftClientRpc(clientId);            
    }

    public async void OnStartHost()
    {
        if (string.IsNullOrEmpty(PlayerPrefs.GetString("PlayerName"))) return;

        UIManager.Instance.loadingIcon.SetActive(true);
        string joinCode = await RelayManager.Instance.StartHost(3);

        if (!string.IsNullOrEmpty(joinCode))
        {
            UIManager.Instance.SetJoinCodeText("Join Code: " + joinCode);
            return;
        }

        UIManager.Instance.loadingIcon.SetActive(false);
    }

    public async void OnStartClient()
    {
        string joinCode = UIManager.Instance.GetJoinCodeInput();

        if (string.IsNullOrEmpty(PlayerPrefs.GetString("PlayerName")) || string.IsNullOrEmpty(joinCode)) return;

        UIManager.Instance.loadingIcon.SetActive(true);
        if (!await RelayManager.Instance.JoinHost(joinCode))
        {
            Debug.Log("GameManager: Failed to join game.");
            UIManager.Instance.loadingIcon.SetActive(false);
            return;
        }
    }

    public void OnDisconnectClient()
    {
        isOffline = false;
        isConnected = false;

        // notify clients on the disconnect intent of the host
        if (NetworkManager.IsHost)
        {
            PlayerLeftClientRpc(NetworkManager.LocalClientId);
        }

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

    public void SpawnSharedMap(ulong clientId)
    {
        Debug.Log("GameManager: Spawning SharedMap for: " + clientId);
        GameObject mapObject = Instantiate(Resources.Load<GameObject>("MapUI2"), Vector3.zero, Quaternion.identity);
        mapObject.GetComponent<Canvas>().worldCamera = mapCamera;
        mapObject.transform.position = new Vector3(0f, -100f, 0f);
        mapCamera.transform.position = new Vector3(0f, -100f, 0f);
        if (!isOffline)
        {
            mapObject.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
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

    public void OnInteract()
    {
        if (PlayerController.Instance)
        {
            PlayerController.Instance.OnInteract();
        }
    }

    [ClientRpc]
    private void PlayerLeftClientRpc(ulong clientId)
    {
        Debug.Log("GameManager: Client disconnected: " + clientId);

        // host disconnect & self kick
        if (!NetworkManager.IsHost && (clientId == NetworkManager.ServerClientId || clientId == NetworkManager.LocalClientId))
        {
            OnDisconnectClient();
            return;
        }

        playerList.RemoveAll(player => player.OwnerClientId == clientId);
        UIManager.Instance.OnRefreshPlayerList();
    }

    [ClientRpc]
    public void OnLobbyStartClientRpc()
    {
        if (NetworkManager.IsHost) return;
        UIManager.Instance.OnLobbyStart();
    }
}
