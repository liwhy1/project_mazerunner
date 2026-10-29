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

    [Header("Network vars")]
    public NetworkVariable<FixedString64Bytes> activeStory = new NetworkVariable<FixedString64Bytes>("Prototype3", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<FixedString64Bytes> joinCode = new NetworkVariable<FixedString64Bytes>("######", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> isLobbyStarted = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> currentTravelCost = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Reference data")]
    [SerializeField] private GameObject mainCamera;
    public Camera playerViewCamera;
    public Shader transparentShader;

    [Header("Map Data")]
    public GameObject terrainObject;
    public GameObject terrainEdgeObject;
    public GameObject riverObject;
    public GameObject playerSpawnPosition;
    public List<GameObject> visitedLandmarks;
    [SerializeField] private GameObject navMeshObject;

    [Header("Lobby data")]
    public bool allowLandmarkBacktracking = true;
    public bool lockJournalAfterOwnMap = true;
    public bool lockInventoryAtStart = true;

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
        // setup vars
        Instance = this;
        isPaused = true;

        // enable wakelock
        GetComponent<WebGLWakeLock>().Enable();

        // subscribe to network events
        NetworkManager.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
        isLobbyStarted.OnValueChanged += OnLobbyStartValueChanged;
    }

    public override void OnDestroy()
    {
        if (NetworkManager) NetworkManager.OnClientConnectedCallback -= OnClientConnected;
        if (NetworkManager) NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        if (NetworkManager && NetworkManager.SceneManager != null) NetworkManager.SceneManager.OnLoadEventCompleted -= OnMapLoaded;
        isLobbyStarted.OnValueChanged -= OnLobbyStartValueChanged;
    }

    public void OnLobbyConnect()
    {
        // setup ui
        MenuManager.Instance.OnLobbyConnect();
        InventoryManager.Instance.OnSetup();

        // subscribe to scene load events
        NetworkManager.SceneManager.OnLoadEventCompleted += OnMapLoaded;

        // load empty map
        if (IsHost) OnLoadEmptyMap();

        // manually run load event, if the empty map is already loaded (client behaviour)
        if (FindAnyObjectByType<AssetSpawner>()) OnMapLoaded("EmptyMap", LoadSceneMode.Additive, null, null);
    }

    public void OnLobbyStartValueChanged(bool oldValue, bool newValue)
    {
        if (newValue) StartCoroutine(OnLobbyStart());
    }

    public IEnumerator OnLobbyStart()
    {
        Debug.Log("GameManager: Starting lobby");
        UIManager.Instance.loadingIcon.SetActive(true);

        // setup ui
        InventoryManager.Instance.OnSetupStory();
        MenuManager.Instance.OnLobbyStart();
        MapManager.Instance.UpdateMapReadyState();

        // force lobby start state in master mode (ignore ready states)
        if (isMaster) isLobbyStarted.Value = true;
        
        // update player renderstates
        UpdatePlayerRenderStateRpc();

        // start map load
        yield return new WaitForSeconds(.5f);
        if (IsHost) OnLoadMap();

        // manually run load event, if the main map is already loaded (client behaviour)
        if (FindAnyObjectByType<Volume>()) OnMapLoaded(activeStory.Value.ToString(), LoadSceneMode.Additive, null, null);
    }

    private void OnLoadEmptyMap()
    {
        string targetScene = "EmptyMap";
        SceneEventProgressStatus status = NetworkManager.SceneManager.LoadScene(targetScene, LoadSceneMode.Additive);

        Debug.Log("GameManager: Loading level " + targetScene);
        if (status != SceneEventProgressStatus.Started)
        {
            Debug.Log("GameManager: Failed to start scene load");
            OnDisconnectClient();
            return;
        }
    }

    public void OnLoadMap()
    {
        if (!IsHost) return;
        string targetScene = GetSceneBuildIndex(activeStory.Value.ToString()) != -1 ? activeStory.Value.ToString() : "Prototype8";
        SceneEventProgressStatus status = NetworkManager.SceneManager.LoadScene(targetScene, LoadSceneMode.Additive);

        Debug.Log("GameManager: Loading level " + targetScene);
        if (status != SceneEventProgressStatus.Started)
        {
            Debug.Log("GameManager: Failed to start scene load");
            OnDisconnectClient();
            return;
        }
    }

    private void OnMapLoaded(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        Scene loadedScene = SceneManager.GetSceneByName(sceneName);
        if (loadedScene == null) UIManager.Instance.OnOpenDialog(DialogId.HostLostConnection);
        if (sceneName == "EmptyMap")
        {
            foreach (GameObject rootObject in loadedScene.GetRootGameObjects())
            {
                if (rootObject.GetComponent<Camera>()) rootObject.SetActive(false);
                if (rootObject.name == "Inner terrain") terrainObject = rootObject;
                if (rootObject.name == "Outer Terrain") terrainEdgeObject = rootObject;
                if (rootObject.name == "River")
                {
                    riverObject = rootObject;
                    AudioManager.Instance.riverAudioSource = riverObject.GetComponentInChildren<AudioSource>();
                }
            }

            // build navmesh
            navMeshObject.GetComponent<NavMeshSurface>().BuildNavMesh();
        }
        else
        {
            foreach (GameObject rootObject in loadedScene.GetRootGameObjects())
            {
                if (rootObject.GetComponent<Camera>()) rootObject.SetActive(false);

                if (rootObject.name.Contains("Level"))
                {
                    if (isMaster)
                    {
                        Destroy(rootObject);
                        UIManager.Instance.loadingIcon.SetActive(false);
                        return;
                    }
                    // rotate map
                    rootObject.transform.eulerAngles = new Vector3(0f, -180f, 0f);

                    // set spawn position
                    playerSpawnPosition = rootObject.transform.Find("Terrain objects").Find("Starting point").gameObject;

                    // setup audio
                    AudioManager.Instance.environmentAudioSource = rootObject.transform.Find("World Settings").GetComponentInChildren<AudioSource>();
                    AudioManager.Instance.environmentAudioSource.Stop();
                }
            }

            // build navmesh
            navMeshObject.GetComponent<NavMeshSurface>().BuildNavMesh();

            // hide start platform
            navMeshObject.GetComponent<NavMeshSurface>().GetComponent<Renderer>().enabled = false;

            // reset ui
            if (MapNetworkManager.Instance.mapState.Value == MapState.Own || MapNetworkManager.Instance.mapState.Value == MapState.Shared) InventoryManager.Instance.OnToggleInventoryLock();
            if (lockInventoryAtStart && MapNetworkManager.Instance.mapState.Value == MapState.Own) MapManager.Instance.MapLoadAnimator();
            playerViewCamera.gameObject.SetActive(false);
            MenuManager.Instance.ResetUIState();

            // start using the player's camera
            mainCamera.SetActive(false);

            if (MapNetworkManager.Instance.mapState.Value == MapState.Explore) OnExploreGameState();

            isPaused = false;
        }
        UIManager.Instance.loadingIcon.SetActive(false);
        Debug.Log("GameManager: Map loaded: " + loadedScene.name);
    }

    public void OnExploreGameState()
    {
        // start environment audio
        if (MapNetworkManager.Instance.mapState.Value == MapState.Explore || MapNetworkManager.Instance.mapState.Value == MapState.Individual) AudioManager.Instance.OnGameStarted();

        // move player to map
        RespawnPlayer();

        // update player renderstates
        UpdatePlayerRenderStateRpc();

        // reset player camera
        PlayerController.Instance.playerCamera.GetComponent<Camera>().farClipPlane = 100f;

        // close inventory
        if (InventoryManager.Instance.isLocked) InventoryManager.Instance.OnToggleInventoryLock();
    }

    public int GetSceneBuildIndex(string sceneName)
    {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);

            if (name == sceneName) return i;
        }

        return -1;
    }

    public void RespawnPlayer()
    {
        // force player position
        PlayerController.Instance.SetPlayerPosition(playerSpawnPosition.transform.position + Vector3.up + Vector3.forward * FetchPersistentPlayerId());

        // toggle pause menu if its active
        if (UIManager.Instance.pauseObject.activeSelf) UIManager.Instance.OnPauseToggle();

        isPaused = false;
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log("GameManager: Client connected: " + clientId);

        if (IsHost)
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
                SpawnSharedMap();
            }          
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsSpawned || !NetworkManager.IsListening || !NetworkManager.IsConnectedClient) return;

        Debug.Log("GameManager: Client disconnected: " + clientId);

        // check for host disconnect
        if (!IsHost && !isMaster)
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
        MapNetworkManager.Instance.CheckLobbyMapStateServerRpc();
        CheckLobbyReadyStateServerRpc();
    }

    public void OnDisconnectClient()
    {
        NetworkManager.Shutdown();
        SceneManager.LoadScene(1);
    }

    [ClientRpc]
    public void PlayerKickNotifyClientRpc(ulong clientId)
    {
        if (clientId == NetworkManager.LocalClientId) OnDisconnectClient();
    }

    public async void OnStartHost(bool masterStart)
    {
        isMaster = masterStart;
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

    public void SpawnSharedMap()
    {
        Debug.Log("GameManager: Spawning SharedMap");
        GameObject mapObject = Instantiate(Resources.Load<GameObject>("MapPrefabs/SharedMapUI"), Vector2.zero, Quaternion.identity);
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

    public void OnLandmarkEntered(GameObject targetLandmark)
    {
        LandmarkObject landmarkObject = targetLandmark.GetComponent<LandmarkObject>();
        if (visitedLandmarks.Contains(targetLandmark))
        {
            // reset landmark progress on start area re-entry
            if (landmarkObject.isStartArea)
            {
                visitedLandmarks.Clear();
                UpdateTravelCostRpc(0);
                visitedLandmarks.Add(targetLandmark);
            }
            // handle backtracking
            else if (allowLandmarkBacktracking && visitedLandmarks.Count > 1 && visitedLandmarks[visitedLandmarks.Count - 2] == targetLandmark)
            {
                int targetCost = visitedLandmarks.Last().GetComponent<LandmarkObject>().travelRelations.FirstOrDefault(t => t.connectedLandmark == targetLandmark).travelCost;
                UpdateTravelCostRpc(currentTravelCost.Value - targetCost);
                visitedLandmarks.Remove(visitedLandmarks.Last());
            }
        }
        else
        {
            LandmarkData landmarkData = landmarkObject.travelRelations.Count == 0 ? null : landmarkObject.travelRelations.FirstOrDefault(t => t.connectedLandmark == visitedLandmarks.Last());
            // store new landmark and add travel cost
            if (landmarkData != null)
            {
                visitedLandmarks.Add(targetLandmark);
                UpdateTravelCostRpc(currentTravelCost.Value + landmarkData.travelCost);
            }
            // store start landmark (no relations)
            else if (landmarkObject.isStartArea)
            {
                visitedLandmarks.Add(targetLandmark);
            }
        }

        UIManager.Instance.OnUpdateLandmarkText();
    }

    public void OnLandmarkExited(GameObject targetLandmark)
    {
        // if the landmark was stored due to a previous relation, but no other landmarks have it in their relations data, aka the player is not able to continue through this landmark, remove it
        if (visitedLandmarks.Contains(targetLandmark))
        {
            var relationMentioners = FindObjectsByType<LandmarkObject>().Any(l => l.travelRelations.Any(t => t.connectedLandmark == targetLandmark));
            if (!relationMentioners) 
            {
                visitedLandmarks.Remove(targetLandmark);
                LandmarkData lastRelation = targetLandmark.GetComponent<LandmarkObject>().travelRelations.FirstOrDefault(t => t.connectedLandmark == visitedLandmarks.Last());
                UpdateTravelCostRpc(currentTravelCost.Value - lastRelation.travelCost);
            }

            UIManager.Instance.OnUpdateLandmarkText();
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void UpdateTravelCostRpc(int newValue) => currentTravelCost.Value = newValue;

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
        if (!isLobbyStarted.Value && playerList.Count > 0 && playerList.All(p => p.IsLobbyReady.Value == true))
        {
            Debug.Log("GameManager: All lobby players ready");
            isLobbyStarted.Value = true;
        }
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
