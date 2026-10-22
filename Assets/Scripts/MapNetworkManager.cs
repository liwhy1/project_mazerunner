using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public enum MapState {Null, Own, Shared, Explore, Individual};
public class MapNetworkManager : NetworkBehaviour
{
    public static MapNetworkManager Instance;
    public NetworkVariable<MapState> mapState = new NetworkVariable<MapState>(MapState.Own, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        Instance = this;
        mapState.OnValueChanged += OnMapStateChanged;
        OnMapStateChanged(MapState.Null, mapState.Value);

        if (IsHost)
        {
            var mapObject = Instantiate(Resources.Load<GameObject>("MapPrefabs/MapObject"));
            mapObject.GetComponent<NetworkObject>().Spawn();
            mapObject.GetComponent<NetworkObject>().TrySetParent(gameObject);
        }
    }

    public override void OnNetworkDespawn() => mapState.OnValueChanged -= OnMapStateChanged;

    public void OnMapStateChanged(MapState oldValue, MapState newValue)
    {
        Debug.Log("MNM: Map state changed from: " + oldValue + " to: " + newValue);
        InventoryManager.Instance.OnToggleSharedView(false);
        InventoryManager.Instance.OnToggleIndividualView(false);
        if (oldValue == MapState.Null && !IsHost) SyncMapDataServerRpc();
        if (newValue == MapState.Shared)
        {
            if (GameManager.Instance.isMaster) MapManager.SharedInstance?.OnDisableMapUI();
            MapManager.Instance?.OnDisableMapUI();
            InventoryManager.Instance.OnToggleSharedView(true);
            if (GameManager.Instance.isMaster) MenuManager.Instance.OnMasterSharedMapEnabled();
            if (oldValue != MapState.Null) MapManager.Instance.OnSendMapData();
        }
        else if (newValue == MapState.Explore)
        {
            GameManager.Instance.OnLoadMap();
            MapManager.SharedInstance?.OnDisableMapUI();
            MapManager.Instance?.OnDisableMapUI();
            InventoryManager.Instance.OnToggleSharedView(true);
            if (GameManager.Instance.lockJournalAfterOwnMap) InventoryManager.Instance.OnJournalDisable();
            UIManager.Instance.MirrorSharedmaptoMinimap();
            if (!GameManager.Instance.isMaster && AudioManager.Instance.environmentAudioSource) AudioManager.Instance.OnGameStarted();
        }
        else if (newValue == MapState.Individual)
        {
            InventoryManager.Instance.OnToggleIndividualView(true);
            InventoryManager.Instance.OnToggleSharedView(true);
            InventoryManager.Instance.SetMapPage(InventoryManager.Instance.mapIndividualViewPage);
            InventoryManager.Instance.OnJournalEnable();
            if (!GameManager.Instance.isMaster && AudioManager.Instance.environmentAudioSource) AudioManager.Instance.OnGameStarted();
        }
    }

    [ServerRpc]
    public void SetMapStateServerRpc(MapState targetState) => mapState.Value = targetState;

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SetPlayerMapStateServerRpc(bool targetState, RpcParams rpcParams = default)
    {
        GameManager.Instance.FetchPlayerDataById(rpcParams.Receive.SenderClientId).IsMapReady.Value = targetState;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void CheckLobbyMapStateServerRpc()
    {
        if (GameManager.Instance.isLobbyStarted.Value && mapState.Value == MapState.Own) 
        {
            MenuManager.Instance.SetMasterGameState(1);
            if (GameManager.Instance.playerList.All(p => p.IsMapReady.Value == true))
            {
                Debug.Log("MNM: All individual maps ready");
                SetMapStateServerRpc(MapState.Shared);
            }
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SyncMapDataServerRpc()
    {
        if (mapState.Value == MapState.Own) return;

        // sync shared map element data
        foreach (Transform element in MapManager.SharedInstance.mapBackground.transform)
        {
            if (!element.GetComponent<NetworkObject>()) continue;
            SetMapElementDataClientRpc(0, element.GetComponent<NetworkObject>().NetworkObjectId, element.name, element.transform.localScale);
            Vector2 normalisedPosition = UIManager.Instance.GetNormalizedPosition(element.transform.parent.GetComponent<RectTransform>(), element.transform.position);
            SetMapElementPositionRpc(element.GetComponent<NetworkObject>().NetworkObjectId, normalisedPosition);
        }
        // sync individual view data
        // NOTE: This assumes the player limit is 3
        for (int i = 0; i < 3; i++)
        {
            GameObject targetMap = InventoryManager.Instance.FetchMapObjectById(i);
            List<MapElementData> mapElements = new List<MapElementData>();
            foreach (Transform icon in targetMap.transform.Find("Background"))
            {
                string iconSprite = icon.transform.Find("Sprite") ? icon.transform.Find("Sprite").GetComponent<Image>().sprite.name : "DrawDot";
                string iconPrefab = icon.name.Contains("Dot") ? "DrawDot" : "MapIcon";
                Vector2 normalisedPosition = UIManager.Instance.GetNormalizedPosition(icon.transform.parent.GetComponent<RectTransform>(), icon.transform.position);
                mapElements.Add(new MapElementData{iconPrefab = iconPrefab, iconSprite = iconSprite, iconPosition = normalisedPosition, iconScale = icon.transform.localScale});
            }
            string playerName = targetMap.transform.Find("Title").GetComponent<TMP_Text>().text;
            SpawnMapInstanceClientRpc(i, playerName, mapElements.ToArray());
        }

        UIManager.Instance.MirrorSharedmaptoMinimap();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SpawnMapElementServerRpc(ulong requestId, string iconPrefab, string iconSprite, Vector2 iconPosition, Vector2 iconScale)
    {
        Debug.Log("MNM: Spawning new object with type: " + iconPrefab);
        GameObject newObject = Instantiate(Resources.Load<GameObject>("MapPrefabs/" + iconPrefab), MapManager.SharedInstance.mapObject.transform.position, Quaternion.identity);
        newObject.GetComponent<NetworkObject>().Spawn();
        newObject.transform.SetParent(MapManager.SharedInstance.mapBackground.transform);

        ulong objectId = newObject.GetComponent<NetworkObject>().NetworkObjectId;
        SetMapElementDataClientRpc(requestId, objectId, iconSprite, iconScale);
        SetMapElementPositionRpc(objectId, iconPosition);
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Everyone)]
    public void SetMapElementPositionRpc(ulong targetElement, Vector2 targetPosition, bool ignoreOwner = false, RpcParams rpcParams = default)
    {
        // conditionally ignore sender
        if (ignoreOwner && rpcParams.Receive.SenderClientId == NetworkManager.LocalClientId) return;

        if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetElement, out NetworkObject targetObject))
        {
            Vector2 targetPos = UIManager.Instance.ApplyNormalizedPosition(targetObject.transform.parent.GetComponent<RectTransform>(), targetPosition);
            targetObject.GetComponent<RectTransform>().anchoredPosition = targetPos;
        }
    }

    [ClientRpc]
    public void SetMapElementDataClientRpc(ulong requestId, ulong targetElement, string targetSprite, Vector2 targetScale)
    {
        if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetElement, out NetworkObject targetObject))
        {
            var pendingObject = MapManager.SharedInstance.pendingPlacements.FirstOrDefault(p => p.Key == requestId);
            if (pendingObject.Value != null) 
            {
                targetObject.transform.Find("Sprite")?.gameObject.SetActive(false);
                if (targetObject.GetComponent<Image>()) targetObject.GetComponent<Image>().enabled = false;
            }

            targetObject.name = targetSprite;
            targetObject.transform.localScale = targetScale;
            if (targetSprite == "DrawDot") 
            {
                targetObject.transform.SetSiblingIndex(MapManager.SharedInstance.iconPile.transform.GetSiblingIndex());
            }
            else
            {
                targetObject.transform.Find("Sprite").GetComponent<Image>().sprite = MapManager.SharedInstance.mapSprites.FirstOrDefault(s => s.name.Contains(targetSprite));
                targetObject.transform.Find("Sprite").GetComponent<Image>().preserveAspect = true;
                targetObject.transform.Find("Name").gameObject.SetActive(false);
                MapManager.SharedInstance.SetupElementTriggers(targetObject.gameObject);
            }

            if (pendingObject.Value) StartCoroutine(MapManager.SharedInstance.ElementReplaceRoutine(pendingObject.Value, targetObject.gameObject));
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SetMapElementParentServerRpc(ulong targetElement, ulong targetParent)
    {
        if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetElement, out NetworkObject targetObject))
        {
            if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetParent, out NetworkObject targetParentObject))
            {
                targetObject.transform.SetParent(targetParentObject.transform);                
            }
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void DestroyMapElementServerRpc(ulong targetElement)
    {
        Debug.Log("MNM: Destroying element: " + targetElement);
        if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetElement, out NetworkObject targetObject))
        {
            targetObject.Despawn();
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void ClearMapServerRpc()
    {
        Debug.Log("MNM: Clearing map");
        foreach (Transform element in MapManager.SharedInstance.mapBackground.transform)
        {
            if (element.name.Contains("player")) continue;
            if (element.gameObject.GetComponent<NetworkObject>() && element.gameObject.GetComponent<NetworkObject>().IsSpawned)
            {
                element.gameObject.GetComponent<NetworkObject>().Despawn();                
            }
            else Destroy(element.gameObject);
        }
    }

    [ClientRpc]
    public void SpawnMapInstanceClientRpc(int persistentId, string playerName, MapElementData[] mapElements)
    {
        GameObject targetMap = InventoryManager.Instance.FetchMapObjectById(persistentId);
        targetMap.transform.Find("Title").GetComponent<TMP_Text>().text = playerName;
        Debug.Log("MNM: Spawning map objects for player: " + playerName);

        foreach (var element in mapElements)
        {
            GameObject newObject = Instantiate(Resources.Load<GameObject>("MapPrefabs/" + element.iconPrefab));
            newObject.name = element.iconSprite;
            if (element.iconPrefab == "MapIcon")
            {
                newObject.transform.Find("Sprite").GetComponent<Image>().sprite = MapManager.SharedInstance.mapSprites.FirstOrDefault(i => i.name.Contains(element.iconSprite));
                newObject.transform.Find("Sprite").GetComponent<Image>().preserveAspect = true;
                newObject.transform.Find("Name").gameObject.SetActive(false);
            }
            newObject.transform.SetParent(targetMap.transform.Find("Background"));
            Vector2 targetPos = UIManager.Instance.ApplyNormalizedPosition(newObject.transform.parent.GetComponent<RectTransform>(), element.iconPosition);
            newObject.transform.GetComponent<RectTransform>().anchoredPosition = targetPos;
            newObject.transform.localScale = element.iconScale;
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SpawnMapInstanceServerRpc(MapElementData[] mapElements, RpcParams rpcParams = default)
    {
        int persistentId = GameManager.Instance.FetchPlayerDataById(rpcParams.Receive.SenderClientId).PersistentPlayerId.Value;
        string playerName = GameManager.Instance.FetchPlayerDataById(rpcParams.Receive.SenderClientId).PlayerName.Value.ToString();
        SpawnMapInstanceClientRpc(persistentId, playerName, mapElements);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SetElementOwnershipServerRpc(ulong targetElement, bool resetOwnership = false, RpcParams rpcParams = default)
    {
        if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetElement, out NetworkObject targetObject))
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            targetObject.ChangeOwnership(resetOwnership ? NetworkManager.ServerClientId : clientId);    
        }
    }
}
