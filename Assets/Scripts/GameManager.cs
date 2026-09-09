using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WebSocketSharp;

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

        if (isOffline) return;
        if (SharedMapManager.Instance)
        {
            SharedMapManager.Instance.gameObject.GetComponent<Canvas>().worldCamera = PlayerController.Instance.cameraObject;
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
        if (PlayerPrefs.GetString("PlayerName").IsNullOrEmpty()) return;

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

        if (PlayerPrefs.GetString("PlayerName").IsNullOrEmpty() || joinCode.IsNullOrEmpty()) return;

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
        mapObject.GetComponent<Canvas>().worldCamera = PlayerController.Instance.cameraObject;
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

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SpawnMapElementServerRpc(string iconPrefab, string iconSprite, Vector3 iconPosition)
    {
        Debug.Log("GameManager: Spawning new object with type: " + iconPrefab);
        GameObject newObject = Instantiate(Resources.Load<GameObject>(iconPrefab + "2"));
        newObject.GetComponent<NetworkObject>().Spawn();
        newObject.name = iconPrefab;

        if (iconPrefab == "MapIcon")
        {
            newObject.GetComponent<Image>().sprite = Resources.LoadAll<Sprite>("MapIcons").FirstOrDefault(i => i.name.Contains(iconSprite));                
            newObject.transform.GetChild(0).GetComponent<TMP_Text>().text = "";
            SharedMapManager.Instance.SetupElementTriggers(newObject);
        }
        newObject.transform.SetParent(SharedMapManager.Instance.transform);
        newObject.transform.localPosition = new Vector3(iconPosition.x, iconPosition.y, 1f);
        newObject.transform.localEulerAngles = Vector3.zero;
        newObject.transform.localScale = new Vector3(1f, 1f, 1f);
        if (iconPrefab == "DrawDot") SharedMapManager.Instance.SetupDotEventTriggers(newObject);

        ulong objectId = newObject.GetComponent<NetworkObject>().NetworkObjectId;
        SetElementDataClientRpc(objectId, iconSprite);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void MoveMapElementServerRpc(ulong targetElement, Vector3 targetPosition)
    {
        if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetElement, out NetworkObject targetObject))
        {
            targetObject.transform.localPosition = new Vector3(targetPosition.x, targetPosition.y, 1f);
        }
    }

    [ClientRpc]
    public void SetElementDataClientRpc(ulong targetElement, string targetSprite)
    {
        if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetElement, out NetworkObject targetObject))
        {
            targetObject.GetComponent<Image>().sprite = Resources.LoadAll<Sprite>("MapIcons").FirstOrDefault(i => i.name.Contains(targetSprite));
            targetObject.name = targetSprite;
            if (targetSprite != "DrawDot")
            {
                SharedMapManager.Instance.SetupElementTriggers(targetObject.gameObject);                
            }
            else
            {
                SharedMapManager.Instance.SetupDotEventTriggers(targetObject.gameObject);
            }
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void DestroyElementServerRpc(ulong targetElement)
    {
        Debug.Log("destory" + targetElement);
        if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetElement, out NetworkObject targetObject))
        {
            targetObject.Despawn();
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void ClearMapServerRpc()
    {
        foreach (Transform element in SharedMapManager.Instance.transform)
        {
            if (element.name.Contains("DrawDot") || element.name.Contains("Icon"))
            {
                element.gameObject.GetComponent<NetworkObject>().Despawn();
            }
        }
    }

    [ClientRpc]
    public void SpawnMapElementsClientRpc(ulong clientId, MapElementData[] mapElements)
    {
        if (clientId == NetworkManager.LocalClientId) return;

        foreach (var element in mapElements)
        {
            Debug.Log("GameManager: Spawning new object with type: " + element.iconPrefab);
            GameObject newObject = Instantiate(Resources.Load<GameObject>(element.iconPrefab));
            if (element.iconPrefab == "MapIcon")
            {
                newObject.GetComponent<Image>().sprite = Resources.LoadAll<Sprite>("MapIcons").FirstOrDefault(i => i.name.Contains(element.iconSprite));                
                newObject.transform.GetChild(0).GetComponent<TMP_Text>().text = "";
            }
            newObject.transform.SetParent(MapManager.Instance.mapObject.transform);
            newObject.transform.localPosition = element.iconPosition;
            newObject.transform.localEulerAngles = Vector3.zero;
            newObject.transform.localScale = new Vector3(1f, 1f, 1f);
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SpawnMapElementsServerRpc(MapElementData[] mapElements, RpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;
        SpawnMapElementsClientRpc(senderClientId, mapElements);
    }

    [ClientRpc]
    public void OnLobbyStartClientRpc()
    {
        if (NetworkManager.IsHost) return;
        UIManager.Instance.OnLobbyStart();
    }

    public ulong FetchLocalClientID()
    {
        return NetworkManager.LocalClientId;
    }

    public void OnInteract()
    {
        if (PlayerController.Instance)
        {
            PlayerController.Instance.OnInteract();
        }
    }
}
