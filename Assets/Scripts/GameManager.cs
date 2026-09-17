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

    [SerializeField] private GameObject mainCamera;
    public Camera mapCamera;
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

    public void OnStartGame()
    {
        if (isGameStarted)
        {
            OnPauseToggle();
            return;
        }

        // set gamestate
        isGameStarted = true;

        // disable menu
        OnPauseToggle();

        // setup inventory
        InventoryManager.Instance.OnSetup();
        OnInventoryToggle();

        mainCamera.SetActive(false);

        if (networkState == NetworkState.Online)
        {
            // request player properties
            SetPlayerPropertiesServerRpc();
            SharedMapManager.Instance.gameObject.GetComponent<Canvas>().worldCamera = mapCamera;
        }

        if (!NetworkManager.IsHost) return;

        // notify clients about lobby start
        OnLobbyStartServerRpc();
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log("GameManager: Client connected: " + clientId);

        // move to lobby
        if (clientId == NetworkManager.LocalClientId)
        {
            networkState = NetworkState.Online;
            UIManager.Instance.OnSessionConnect();
        }

        if (!NetworkManager.IsHost) return;

        // spawn player object
        SpawnPlayer(clientId);

        // spawn shared map if it doesn't exist already
        if (!SharedMapManager.Instance)
        {
            SpawnSharedMap(clientId);
        }

        // assign persistent id
        AssignPersistentPlayerIdServerRpc(clientId);

        // notify new clients about game status
        if (isGameStarted == true)
        {
            OnLobbyStartServerRpc();
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

        if (networkState == NetworkState.Online)
        {
            playerObject.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
        }
    }

    public void SpawnSharedMap(ulong clientId)
    {
        Debug.Log("GameManager: Spawning SharedMap for: " + clientId);
        GameObject mapObject = Instantiate(Resources.Load<GameObject>("SharedMapUI"), Vector3.zero, Quaternion.identity);
        mapObject.GetComponent<Canvas>().worldCamera = mapCamera;
        mapObject.transform.position = new Vector3(0f, -100f, 0f);
        mapCamera.transform.position = new Vector3(0f, -100f, 0f);
        if (networkState == NetworkState.Online)
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

    [ServerRpc]
    public void AssignPersistentPlayerIdServerRpc(ulong clientId)
    {
        int targetId = 0;
        while (playerList.Any(p => p.persistentPlayerId.Value == targetId)) targetId++;
        Debug.Log("GameManager: Assigned persistent id: " + targetId + " to: " + clientId);
        playerList.FirstOrDefault(p => p.OwnerClientId == clientId).persistentPlayerId.Value = targetId;
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

        targetPlayer.gameObject.GetComponent<Renderer>().material = targetMaterial;
        PlayerController.Instance.gameObject.transform.position = Vector3.one + Vector3.forward * 3 * playerList.FirstOrDefault(p => p.OwnerClientId == NetworkManager.LocalClientId).persistentPlayerId.Value;
        PlayerController.Instance.GetComponent<Rigidbody>().isKinematic = false;
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

    public ulong FetchLocalClientId()
    {
        return NetworkManager.LocalClientId;
    }

    public int FetchPersistentPlayerId()
    {
        return PlayerController.Instance.gameObject.GetComponent<PlayerData>().persistentPlayerId.Value;
    }
}
