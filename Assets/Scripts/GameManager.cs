using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public enum NetworkState {None, Offline, Online};
public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;
    public bool isPaused;
    public NetworkState networkState;
    public List<PlayerData> playerList = new List<PlayerData>();
    public bool isMaster;
    public bool isMasterGameStarted;

    [Header("Network vars")]
    public NetworkVariable<FixedString64Bytes> activeStory = new NetworkVariable<FixedString64Bytes>("Prototype3", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<FixedString64Bytes> joinCode = new NetworkVariable<FixedString64Bytes>("######", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> isLobbyStarted = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Reference data")]
    [SerializeField] private GameObject mainCamera;
    public Camera mapCamera;
    public Camera playerViewCamera;
    public GameObject playerSpawnPosition;
    public GameObject terrainObject;

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
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();

        NetworkManager.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
        isLobbyStarted.OnValueChanged += OnLobbyStartValueChanged;
    }

    public override void OnDestroy()
    {
        // networkmanager usually dies before this, keep it just in case
        try
        {
            NetworkManager.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            isLobbyStarted.OnValueChanged -= OnLobbyStartValueChanged;
        }
        catch {}
    }

    public void OnNameChanged(string inputText)
    {
        PlayerPrefs.SetString("PlayerName", inputText);
        PlayerPrefs.Save();
    }

    public void OnLobbyConnect()
    {
        networkState = NetworkState.Online;
        MenuManager.Instance.OnLobbyConnect();
        InventoryManager.Instance.OnSetup();
        if (isLobbyStarted.Value) MenuManager.Instance.OnLobbyStart();
    }

    public void OnLobbyStartValueChanged(bool oldValue, bool newValue)
    {
        if (newValue)
        {
            MenuManager.Instance.OnLobbyStart();
        }
    }

    public void OnLobbyStart()
    {
        // set gamestate
        if (networkState == NetworkState.Online && !isMaster) SetPlayerGameStateServerRpc(true);

        isPaused = false;
        playerViewCamera.gameObject.SetActive(false);
        SharedMapManager.Instance.gameObject.GetComponent<Canvas>().worldCamera = mapCamera;

        if (!isMaster) 
        {
            mainCamera.SetActive(false);

            // reset ui
            UIManager.Instance.ResetUIState();
            MenuManager.Instance.ResetUIState();

            // tutorial dialog
            string targetContent = "<b>Player movement:</b>\n(WASD) / (Point & Click)\n<b>Camera height control:</b>\n(Mouse Wheel) / (UI Plus & Minus icon)";
            UIManager.Instance.OnOpenDialog("Tutorial", targetContent, "");

            // move player to map
            PlayerController.Instance.SetPlayerPosition(playerSpawnPosition.transform.position + Vector3.forward * FetchPersistentPlayerId());
        }
        else isMasterGameStarted = true;

        if (NetworkManager.IsHost)
        {
            isLobbyStarted.Value = true;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log("GameManager: Client connected: " + clientId);

        if (NetworkManager.IsHost)
        {
            if (isMaster && NetworkManager.LocalClientId == clientId)
            {
                // move to lobby connect state without instantiating player data
                OnLobbyConnect();             
            }
            else
            {
                // spawn player object
                SpawnPlayer(clientId);   
            }

            // spawn shared map if it doesn't exist already
            if (!SharedMapManager.Instance)
            {
                SpawnSharedMap(clientId);
            }          
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsSpawned || !NetworkManager.IsListening || !NetworkManager.IsConnectedClient) return;
        PlayerLeftClientRpc(clientId);
    }

    public void OnStartMaster()
    {
        Debug.Log("GameManager: Starting game as Master");
        isMaster = true;
        terrainObject.transform.parent.gameObject.SetActive(false);
        OnStartHost();
    }

    public async void OnStartHost()
    {
        if (string.IsNullOrEmpty(PlayerPrefs.GetString("PlayerName")) && !isMaster) 
        {
            UIManager.Instance.OnOpenDialog("Notice", "Player name can't be empty!", "");
            return;
        }

        UIManager.Instance.loadingIcon.SetActive(true);
        string relayCode = await RelayManager.Instance.StartHost(4);

        if (!string.IsNullOrEmpty(relayCode))
        {
            joinCode.Value = relayCode;
            return;
        }

        UIManager.Instance.loadingIcon.SetActive(false);
    }

    public async void OnStartClient()
    {
        string joinCode = MenuManager.Instance.GetJoinCodeInput();

        if (string.IsNullOrEmpty(PlayerPrefs.GetString("PlayerName"))) 
        {
            UIManager.Instance.OnOpenDialog("Notice", "Player name can't be empty!", "");
            return;
        }

        if (string.IsNullOrEmpty(joinCode))
        {
            UIManager.Instance.OnOpenDialog("Notice", "Join code can't be empty!", "");
            return;
        }

        UIManager.Instance.loadingIcon.SetActive(true);
        if (!await RelayManager.Instance.JoinHost(joinCode))
        {
            Debug.Log("GameManager: Failed to join game.");
            UIManager.Instance.OnOpenDialog("Notice", "Failed to joing game!", "");
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

        SceneManager.LoadScene(1);
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

    public void SpawnPlayer(ulong clientId)
    {
        Debug.Log("GameManager: Spawning player for: " + clientId);
        GameObject playerObject = Instantiate(Resources.Load<GameObject>("Player"), Vector3.one, Quaternion.identity);

        if (networkState != NetworkState.Offline)
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
        Debug.Log("GameManager: Player list refreshed");
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Everyone)]
    public void UpdatePlayerRenderStateRpc()
    {
        foreach (var player in playerList)
        {
            bool isOwner = player.OwnerClientId == NetworkManager.LocalClientId;
            bool shouldRenderPlayer = !isOwner && player.IsGameStarted.Value && FetchPlayerDataById(NetworkManager.LocalClientId).IsGameStarted.Value;
            bool shouldRenderName = isOwner || (!isOwner && player.IsGameStarted.Value && FetchPlayerDataById(NetworkManager.LocalClientId).IsGameStarted.Value);
            player.transform.Find("NameCanvas").gameObject.SetActive(shouldRenderPlayer);
            foreach (Transform child in player.transform.Find("Model").transform)
            {
                if (child.GetComponent<Renderer>()) child.GetComponent<Renderer>().enabled = shouldRenderName;
            }            
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SetPlayerMapStateServerRpc(bool targetState, RpcParams rpcParams = default)
    {
        FetchPlayerDataById(rpcParams.Receive.SenderClientId).IsMapReady.Value = targetState;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void CheckLobbyMapStateServerRpc()
    {
        MenuManager.Instance.SetMasterGameStateText("Individual mapping\n" + playerList.Count(p => p.IsMapReady.Value == true) + "/" + playerList.Count);
        if (playerList.All(p => p.IsMapReady.Value == true))
        {
            Debug.Log("GameManager: All individual maps ready");
            if (isMaster) MenuManager.Instance.OnMasterSharedMapEnabled();
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
    public void SetPlayerPersistentIdServerRpc(ulong playerId)
    {
        int targetId = 0;
        while (playerList.Any(p => p.PersistentPlayerId.Value == targetId)) targetId++;
        FetchPlayerDataById(playerId).PersistentPlayerId.Value = targetId;
        Debug.Log("GameManager: Assigned persistent id: " + targetId + " to: " + playerId);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SetPlayerSkinDataServerRpc(SkinData skinData, RpcParams rpcParams = default)
    {
        FetchPlayerDataById(rpcParams.Receive.SenderClientId).SkinData.Value = skinData;
    }

    [ClientRpc]
    public void SetActiveStoryClientRpc(string targetStory)
    {
        if (!IsHost) return;
        Debug.Log("GameManager: Selecting story: " + targetStory);
        activeStory.Value = targetStory;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SetPlayerGameStateServerRpc(bool targetState, RpcParams rpcParams = default)
    {
        FetchPlayerDataById(rpcParams.Receive.SenderClientId).IsGameStarted.Value = targetState;
    }

    public ulong FetchLocalClientId()
    {
        return NetworkManager.LocalClientId;
    }

    public int FetchPersistentPlayerId()
    {
        if (!PlayerController.Instance) return -1; // NOTE: This should only be the case on master player
        return PlayerController.Instance.GetComponent<PlayerData>().PersistentPlayerId.Value;
    }

    public bool FetchGameStartState()
    {
        if (!PlayerController.Instance) return isMasterGameStarted;
        
        if (networkState == NetworkState.Offline)
        {
            return true;
        }
        else
        {
            return PlayerController.Instance.GetComponent<PlayerData>().IsGameStarted.Value;
        }
    }

    public PlayerData FetchPlayerDataById(ulong playerId)
    {
        return FindObjectsByType<PlayerData>(FindObjectsInactive.Include).FirstOrDefault(p => p.OwnerClientId == playerId);
    }
}
