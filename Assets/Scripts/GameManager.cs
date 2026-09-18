using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public enum NetworkState {None, Offline, Online};
public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;
    public bool isPaused;
    public NetworkState networkState;
    public bool isGameStarted;
    public string joinCode;

    [SerializeField] private GameObject mainCamera;
    public Camera mapCamera;
    public Camera playerViewCamera;
    public List<PlayerData> playerList = new List<PlayerData>();
    public string activeStory;

    public Material blueMat;
    public Material greenMat;
    public Material redMat;

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
        networkState = NetworkState.None;
        isGameStarted = false;
        mainCamera = Camera.main.gameObject;
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();

        // call setup on uielemenets
        foreach (var element in FindObjectsByType<UIElement>(FindObjectsInactive.Include))
        {
            element.OnSetup();
        }

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
        if (!isGameStarted) return;

        isPaused = !isPaused;
        UIManager.Instance.OnPauseToggle();
    }

    public void OnInventoryToggle()
    {
        if (isPaused) return;

        InventoryManager.Instance.OnToggleInventory();
    }

    public void OnNameChanged(string inputText)
    {
        PlayerPrefs.SetString("PlayerName", inputText);
        PlayerPrefs.Save();
    }

    public void OnLobbyStart()
    {
        // set gamestate
        isGameStarted = true;
        isPaused = false;
        mainCamera.SetActive(false);
        SharedMapManager.Instance.gameObject.GetComponent<Canvas>().worldCamera = mapCamera;

        UIManager.Instance.ResetUIState();
        FetchActiveStoryServerRpc();

        if (!NetworkManager.IsHost) return;

        // notify clients about lobby start
        OnLobbyStartServerRpc();
    }

    [ClientRpc]
    private void OnLobbyConnectClientRpc(ulong clientId)
    {
        if (NetworkManager.LocalClientId != clientId) return;
        networkState = NetworkState.Online;
        UIManager.Instance.OnLobbyConnect();
        InventoryManager.Instance.OnSetup();
        FetchLobbyCodeServerRpc();
        SetPlayerPropertiesServerRpc();
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log("GameManager: Client connected: " + clientId);

        if (NetworkManager.IsHost)
        {
            // spawn player object
            SpawnPlayer(clientId);

            // spawn shared map if it doesn't exist already
            if (!SharedMapManager.Instance)
            {
                SpawnSharedMap(clientId);
            }

            // notify new clients about game status
            if (isGameStarted == true)
            {
                OnLobbyStartServerRpc();
            }            
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
        string relayCode = await RelayManager.Instance.StartHost(3);

        if (!string.IsNullOrEmpty(relayCode))
        {
            joinCode = relayCode;
            UIManager.Instance.SetJoinCodeText(relayCode);
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
        GameObject playerObject = Instantiate(Resources.Load<GameObject>("Player"), Vector3.one, Quaternion.identity);
        if (clientId == NetworkManager.LocalClientId) playerObject.name = "Player";

        if (networkState != NetworkState.Offline)
        {
            playerObject.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
            AssignPersistentPlayerIdServerRpc(clientId);
        }
    }

    public void SpawnSharedMap(ulong clientId)
    {
        Debug.Log("GameManager: Spawning SharedMap for: " + clientId);
        GameObject mapObject = Instantiate(Resources.Load<GameObject>("SharedMapUI"), Vector3.zero, Quaternion.identity);
        mapObject.GetComponent<Canvas>().worldCamera = mapCamera;
        mapObject.transform.position = new Vector3(0f, -100f, 0f);
        mapCamera.transform.position = new Vector3(0f, -100f, 0f);
        if (networkState != NetworkState.Offline)
        {
            mapObject.GetComponent<NetworkObject>().Spawn();
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

    public void OnPrimaryAction()
    {
        if (isPaused) return;

        if (PlayerController.Instance)
        {
            Ray ray = PlayerController.Instance.playerCamera.GetComponent<Camera>().ScreenPointToRay(Pointer.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (hit.collider.gameObject.CompareTag("Interactable"))
                {
                    UIManager.Instance.OnOpenDialog();
                }
                else
                {
                    PlayerController.Instance.OnMove(hit.point);
                }
            }
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

    [ServerRpc]
    public void OnLobbyStartServerRpc()
    {
        OnLobbyStartClientRpc();
    }

    [ClientRpc]
    public void OnLobbyStartClientRpc()
    {
        UIManager.Instance.OnLobbyStart();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SetPlayerMapStateServerRpc(bool targetState, RpcParams rpcParams = default)
    {
        playerList.FirstOrDefault(p => p.OwnerClientId == rpcParams.Receive.SenderClientId).IsMapReady.Value = targetState;
        if (playerList.All(p => p.IsMapReady.Value == true))
        {
            Debug.Log("GameManager: All individual maps ready");
            SharedMapManager.Instance.OnSendMapInsanceClientRpc();
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SyncPlayerMapStateServerRpc()
    {
        // send a map sync rpc for joined clients, if shared map state is availible
        if (MapManager.Instance.FetchSharedViewState())
        {
            SharedMapManager.Instance.OnSendMapInsanceClientRpc();

            // sync shared map element data
            foreach (Transform element in SharedMapManager.Instance.transform)
            {
                if (element.GetComponent<NetworkObject>())
                {
                    SharedMapManager.Instance.SetMapElementDataClientRpc(element.GetComponent<NetworkObject>().NetworkObjectId, element.name);
                }
            }
        }

        if (MapManager.Instance.FetchIndividualViewState())
        {
            SetMapIndividualButtonStatusClientRpc();
        }
    }

    [ClientRpc]
    public void SetMapIndividualButtonStatusClientRpc()
    {
        MapManager.Instance.individualViewButton.GetComponent<UIElement>().OnElementEnable();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void AssignPersistentPlayerIdServerRpc(ulong clientId)
    {
        int targetId = 0;
        while (playerList.Any(p => p.persistentPlayerId.Value == targetId)) targetId++;
        Debug.Log("GameManager: Assigned persistent id: " + targetId + " to: " + clientId);
        playerList.FirstOrDefault(p => p.OwnerClientId == clientId).persistentPlayerId.Value = targetId;

        //TODO: this should be here
        OnLobbyConnectClientRpc(clientId);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SetPlayerPropertiesServerRpc()
    {
        foreach (var player in playerList)
        {
            SetPlayerPropertiesClientRpc(player.persistentPlayerId.Value);
        }
    }

    [ClientRpc]
    public void SetPlayerPropertiesClientRpc(int targetId)
    {
        Material targetMaterial = targetId == 0 ? blueMat : targetId == 1 ? greenMat : redMat;
        PlayerData targetPlayer = playerList.FirstOrDefault(p => p.persistentPlayerId.Value == targetId);
        if (!targetPlayer) return;

        Debug.Log("GameManager: Updating player properties for: " + targetId);

        targetPlayer.gameObject.GetComponent<Renderer>().material = targetMaterial;
        PlayerController.Instance.gameObject.transform.position = Vector3.one + Vector3.forward * 3 * playerList.FirstOrDefault(p => p.OwnerClientId == NetworkManager.LocalClientId).persistentPlayerId.Value;
        PlayerController.Instance.GetComponent<Rigidbody>().isKinematic = true;
        if (targetId != (int)NetworkManager.LocalClientId) targetPlayer.GetComponent<Renderer>().enabled = false;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void FetchActiveStoryServerRpc(RpcParams rpcParams = default)
    {
        SetActiveStoryClientRpc(rpcParams.Receive.SenderClientId, activeStory);
    }

    [ClientRpc]
    public void SetActiveStoryClientRpc(ulong targetPlayer, string targetStory)
    {
        if (NetworkManager.LocalClientId == targetPlayer)
        {
            Debug.Log("GameManager: Selecting story: " + targetStory);
            activeStory = targetStory;
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void FetchLobbyCodeServerRpc()
    {
        FetchLobbyCodeClientRpc(joinCode);
    }

    [ClientRpc]
    public void FetchLobbyCodeClientRpc(string lobbyCode)
    {
        if (!NetworkManager.IsHost)
        {
            UIManager.Instance.SetJoinCodeText(lobbyCode);
        }
    }

    public ulong FetchLocalClientId()
    {
        return NetworkManager.LocalClientId;
    }

    public int FetchPersistentPlayerId()
    {
        return PlayerController.Instance.gameObject.GetComponent<PlayerData>().persistentPlayerId.Value;
    }
}
