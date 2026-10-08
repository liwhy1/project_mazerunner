using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;
    public bool isPaused;
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
    public Shader transparentShader;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        Debug.Log("GameManager: Initializing");
        if (Instance != null) return;

        // create NetworkManager
        if (FindAnyObjectByType<NetworkManager>() == null)
        {
            GameObject networkManager = Instantiate(Resources.Load<GameObject>("GameComponents/NetworkManager"));
            networkManager.name = "NetworkManager";
            DontDestroyOnLoad(networkManager);
        }

        // create InputManager
        if (FindAnyObjectByType<InputManager>() == null)
        {
            GameObject inputManager = Instantiate(Resources.Load<GameObject>("GameComponents/InputManager"));
            inputManager.name = "InputManager";
            DontDestroyOnLoad(inputManager);
        }

        // create EventSystem
        if (FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject eventSystem = Instantiate(Resources.Load<GameObject>("GameComponents/EventSystem"));
            eventSystem.name = "EventSystem";
            DontDestroyOnLoad(eventSystem);
        }

        // create AudioManager
        if (FindAnyObjectByType<AudioManager>() == null)
        {
            GameObject audioManager = Instantiate(Resources.Load<GameObject>("GameComponents/AudioManager"));
            audioManager.name = "AudioManager";
            DontDestroyOnLoad(audioManager);
        }
    }

    private void Start()
    {
        Instance = this;
        isPaused = true;

        NetworkManager.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
        isLobbyStarted.OnValueChanged += OnLobbyStartValueChanged;
    }

    public override void OnDestroy()
    {
        NetworkManager.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        isLobbyStarted.OnValueChanged -= OnLobbyStartValueChanged;
    }

    public void OnLobbyConnect()
    {
        MenuManager.Instance.OnLobbyConnect();
        InventoryManager.Instance.OnSetup();
        FindObjectsByType<AudioListener>().All(o => o.GetComponent<AudioListener>().enabled = o.GetComponent<AudioManager>() != null);
    }

    public void OnLobbyStartValueChanged(bool oldValue, bool newValue)
    {
        if (newValue) OnLobbyStart();
    }

    public void OnLobbyStart()
    {
        isPaused = false;
        if (isMaster) isLobbyStarted.Value = true;
        MenuManager.Instance.OnLobbyStart();
        playerViewCamera.gameObject.SetActive(false);
        MapManager.SharedInstance.gameObject.GetComponent<Canvas>().worldCamera = mapCamera;
        InventoryManager.Instance.OnSetupStory();

        if (!isMaster) OnLoadMap(activeStory.Value.ToString());
        else isMasterGameStarted = true;
    }

    private void OnLoadMap(string sceneName) => StartCoroutine(AsynchronousLevelLoad(sceneName));

    private IEnumerator AsynchronousLevelLoad(string sceneName)
    {
        UIManager.Instance.loadingIcon.SetActive(true);
        Time.timeScale = 1f;
        yield return new WaitForSeconds(1f);

        if (IsHost || !FindAnyObjectByType<Volume>())
        {
            AsyncOperation ao = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            ao.allowSceneActivation = false;
            Debug.Log("GameManager: Loading level " + sceneName);
            while (!ao.isDone)
            {
                if (ao.progress == 0.9f)
                {
                    ao.allowSceneActivation = true;
                }
                yield return null;
            }
        }

        OnMapLoaded();
    }

    private void OnMapLoaded()
    {
        // assign scene vars
        // TODO: rewrite this shit
        foreach (GameObject rootObject in SceneManager.GetSceneByName(activeStory.Value.ToString()).GetRootGameObjects())
        {
            if (rootObject.GetComponent<Camera>()) rootObject.SetActive(false);
            if (rootObject.name.Contains("Level")) 
            {
                rootObject.transform.eulerAngles = new Vector3(0f, -180f, 0f);
                terrainObject = rootObject.transform.Find("Terrain").Find("Inner terrain").gameObject;
                playerSpawnPosition = rootObject.transform.Find("Terrain objects").Find("Starting point").gameObject;
            }
        }

        // build navmesh
        FindAnyObjectByType<NavMeshSurface>().BuildNavMesh();

        // hide start platform
        FindAnyObjectByType<NavMeshSurface>().GetComponent<Renderer>().enabled = false;

        // move player to map
        RespawnPlayer();

        // reset ui
        mainCamera.SetActive(false);
        UIManager.Instance.ResetUIState();
        MenuManager.Instance.ResetUIState();
        InventoryManager.Instance.OnToggleInventoryLock();

        // start game audio
        AudioManager.Instance.OnGameStarted();

        // update player render states
        UpdatePlayerRenderStateRpc();

        Debug.Log("GameManager: Map loaded");
    }

    public void RespawnPlayer()
    {
        PlayerController.Instance.SetPlayerPosition(playerSpawnPosition.transform.position + Vector3.up + Vector3.forward * FetchPersistentPlayerId());
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
            if (!MapManager.SharedInstance)
            {
                SpawnSharedMap(clientId);
            }          
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsSpawned || !NetworkManager.IsListening || !NetworkManager.IsConnectedClient) return;

        Debug.Log("GameManager: Client disconnected: " + clientId);

        // check for host disconnect
        if (!NetworkManager.IsHost && !isMaster)
        {
            if (clientId == NetworkManager.ServerClientId || FetchPlayerDataById(clientId)?.PersistentPlayerId.Value == 0)
            {
                UIManager.Instance.OnOpenDialog(DialogId.HostLostConnection);
                return;
            }
        }

        // manually remove player from playerlist, since playerdata is still alive at this point
        playerList.RemoveAll(p => p.OwnerClientId == clientId);
        UIManager.Instance.OnRefreshPlayerList();

        // update lobby ready state
        MenuManager.Instance.UpdateLobbyReadyState();
        CheckLobbyReadyStateServerRpc();
    }

    public void OnDisconnectClient()
    {
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
    public void PlayerKickNotifyClientRpc(ulong clientId)
    {
        if (clientId == NetworkManager.LocalClientId) OnDisconnectClient();
    }

    public void OnStartMaster()
    {
        Debug.Log("GameManager: Starting game as Master");
        isMaster = true;
        OnStartHost();
    }

    public async void OnStartHost()
    {
        if (string.IsNullOrEmpty(MenuManager.Instance.hostPlayerNameInput.text) && !isMaster) 
        {
            UIManager.Instance.OnOpenDialog(DialogId.InvalidPlayerName);
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

        if (string.IsNullOrEmpty(MenuManager.Instance.joinPlayerNameInput.text))
        {
            UIManager.Instance.OnOpenDialog(DialogId.InvalidPlayerName);
            return;
        }

        if (string.IsNullOrEmpty(joinCode))
        {
            UIManager.Instance.OnOpenDialog(DialogId.InvalidJoinCode);
            return;
        }

        UIManager.Instance.loadingIcon.SetActive(true);
        if (!await RelayManager.Instance.JoinHost(joinCode))
        {
            Debug.Log("GameManager: Failed to join game.");
            UIManager.Instance.OnOpenDialog(DialogId.FailedGameJoin);
            UIManager.Instance.loadingIcon.SetActive(false);
            return;
        }
    }

    public void SpawnPlayer(ulong clientId)
    {
        Debug.Log("GameManager: Spawning player for: " + clientId);
        GameObject playerObject = Instantiate(Resources.Load<GameObject>("GameComponents/Player"));

        playerObject.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
    }

    public void SpawnSharedMap(ulong clientId)
    {
        Debug.Log("GameManager: Spawning SharedMap for: " + clientId);
        GameObject mapObject = Instantiate(Resources.Load<GameObject>("MapPrefabs/SharedMapUI"));
        mapObject.GetComponent<Canvas>().worldCamera = mapCamera;
        mapObject.transform.position = new Vector3(0f, 100f, 0f);
        mapCamera.transform.position = mapObject.transform.position;
        mapObject.GetComponent<NetworkObject>().Spawn();
    }

    public void RefreshPlayerList()
    {
        playerList.Clear();
        playerList = FindObjectsByType<PlayerData>().ToList();
        playerList.Sort((a, b) => a.OwnerClientId.CompareTo(b.OwnerClientId));
        UIManager.Instance.OnRefreshPlayerList();
        Debug.Log("GameManager: Player list refreshed");
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Everyone)]
    public void UpdatePlayerRenderStateRpc()
    {
        if (isMaster) return;
        foreach (var player in playerList)
        {
            bool isOwner = player.OwnerClientId == NetworkManager.LocalClientId;
            bool shouldRenderObject = isLobbyStarted.Value && FetchPlayerDataById(NetworkManager.LocalClientId).IsLobbyReady.Value;
            player.transform.Find("NameCanvas").gameObject.SetActive(!isOwner && shouldRenderObject);
            foreach (Transform child in player.transform.Find("Model").transform)
            {
                if (child.GetComponent<Renderer>()) child.GetComponent<Renderer>().enabled = isOwner || (!isOwner && shouldRenderObject);
            }            
        }
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

    public void SetActiveStory(string targetStory)
    {
        if (!IsHost) return;
        Debug.Log("GameManager: Selecting story: " + targetStory);
        activeStory.Value = targetStory;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SetLobbyReadyStateServerRpc(bool targetState, RpcParams rpcParams = default)
    {
        FetchPlayerDataById(rpcParams.Receive.SenderClientId).IsLobbyReady.Value = targetState;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void CheckLobbyReadyStateServerRpc()
    {
        if (playerList.Count > 0 && playerList.All(p => p.IsLobbyReady.Value == true))
        {
            Debug.Log("GameManager: All lobby players ready");
            if (!isMaster) isLobbyStarted.Value = true;
        }
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

    public PlayerData FetchPlayerDataById(ulong playerId)
    {
        return FindObjectsByType<PlayerData>(FindObjectsInactive.Include).FirstOrDefault(p => p.OwnerClientId == playerId);
    }
}
