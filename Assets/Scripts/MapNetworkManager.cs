using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum MapState {Null, Own, Shared, Explore, Individual};
public class MapNetworkManager : NetworkBehaviour
{
    public static MapNetworkManager Instance;
    public NetworkVariable<MapState> mapState = new NetworkVariable<MapState>(MapState.Own, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public GameObject sharedCanvas;
    public GameObject mapBackground;
 
    public override void OnNetworkSpawn()
    {
        Instance = this;
        mapState.OnValueChanged += OnMapStateChanged;
        ResetRectTransforms();
        OnMapStateChanged(MapState.Null, mapState.Value);
        ToggleMapBackground(false);

        // host spawns the shared canvas, clients wait for it to replicate
        if (IsHost)
        {
            // spawn shared canvas
            sharedCanvas = Instantiate(Resources.Load<GameObject>("MapPrefabs/Canvas"));
            sharedCanvas.GetComponent<NetworkObject>().Spawn();
            StartCoroutine(SetupCanvas(sharedCanvas.GetComponent<NetworkObject>()));
        }
        else StartCoroutine(SetupCanvas(GetComponent<NetworkObject>())); // dummy argument for clients
    }

    private IEnumerator SetupCanvas(NetworkObject targetCanvas)
    {
        if (IsHost)
        {
            yield return new WaitUntil(() => targetCanvas.IsSpawned);
            sharedCanvas = targetCanvas.gameObject;
        }
        else
        {
            // NOTE: This is still not ideal, but oh well
            while (transform.childCount != 2) yield return new WaitForSeconds(.1f);
            sharedCanvas = GetComponentInChildren<EventTrigger>().gameObject;
        }
        sharedCanvas.name = "Canvas";
        sharedCanvas.GetComponent<NetworkObject>().TrySetParent(GetComponent<NetworkObject>(), true);
        sharedCanvas.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
        ToggleSharedCanvas(false);
    }

    public void ResetRectTransforms()
    {
        if (mapBackground) mapBackground.GetComponent<RectTransform>().sizeDelta = Vector2.zero;
        if (sharedCanvas) sharedCanvas.GetComponent<RectTransform>().localScale = Vector2.one;
    }

    public void ToggleSharedCanvas(bool targetState) => sharedCanvas.SetActive(targetState);
    public void ToggleMapBackground(bool targetState) => mapBackground.SetActive(targetState);
    public override void OnNetworkDespawn() => mapState.OnValueChanged -= OnMapStateChanged;

    public void OnMapStateChanged(MapState oldValue, MapState newValue)
    {
        Debug.Log("MNM: Map state changed from: " + oldValue + " to: " + newValue);

        // call class specific functions
        InventoryManager.Instance?.OnMapStateChanged(oldValue, newValue);
        MapManager.Instance?.OnMapStateChanged(oldValue, newValue);

        // sync map data for clients joining after the game has started
        if (oldValue == MapState.Null && !IsHost) SyncMapDataServerRpc();
        switch (newValue)
        {
            case MapState.Shared:
                // master gets the combined map to review
                if (GameManager.Instance.isMaster) MenuManager.Instance.OnMasterSharedMapEnabled();
                break;
            case MapState.Explore:
                // reveal the maze for players that were already connected
                if (oldValue != MapState.Null && !GameManager.Instance.isMaster) StartCoroutine(GameManager.Instance.OnExploreGameState());
                UIManager.Instance.MirrorSharedmaptoMinimap();
                if (AudioManager.Instance.environmentAudioSource) AudioManager.Instance.OnGameStarted();
                break;
            case MapState.Individual:
                if (AudioManager.Instance.environmentAudioSource) AudioManager.Instance.OnGameStarted();
                break;
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
            // all individual maps ready -> move to the shared map
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
        // TODO: This (and the rpcs below) should only run for a target client
        if (mapState.Value == MapState.Own) return;

        // sync shared map element data
        foreach (Transform element in sharedCanvas.transform)
        {
            if (!element.GetComponent<NetworkObject>()) continue;
            SetMapElementDataClientRpc(0, element.GetComponent<NetworkObject>().NetworkObjectId, element.name, element.transform.localScale);
            Vector2 normalisedPosition = UIManager.Instance.GetNormalizedPosition(element.transform.parent.GetComponent<RectTransform>(), element.transform.position);
            SetMapElementPositionRpc(element.GetComponent<NetworkObject>().NetworkObjectId, normalisedPosition);
        }
        // sync individual view data
        foreach (var player in GameManager.Instance.playerList)
        {
            int id = player.PersistentPlayerId.Value;
            if (id < 0) continue; // ignore master

            GameObject targetMap = InventoryManager.Instance.FetchMapObjectById(id);
            List<MapElementData> mapElements = new List<MapElementData>();
            foreach (Transform icon in targetMap.transform.Find("Canvas"))
            {
                string iconSprite = icon.transform.Find("Sprite") ? icon.transform.Find("Sprite").GetComponent<Image>().sprite.name : "DrawDot";
                string iconPrefab = icon.name.Contains("Dot") ? "DrawDot" : "MapIcon";
                Vector2 normalisedPosition = UIManager.Instance.GetNormalizedPosition(icon.transform.parent.GetComponent<RectTransform>(), icon.transform.position);
                mapElements.Add(new MapElementData{iconPrefab = iconPrefab, iconSprite = iconSprite, iconPosition = normalisedPosition, iconScale = icon.transform.localScale});
            }
            string playerName = targetMap.transform.Find("Title").GetComponent<TMP_Text>().text;
            SpawnMapInstanceClientRpc(id, playerName, mapElements.ToArray());
        }

        UIManager.Instance.MirrorSharedmaptoMinimap();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SpawnMapElementServerRpc(ulong requestId, string iconPrefab, string iconSprite, Vector2 iconPosition, Vector2 iconScale)
    {
        Debug.Log("MNM: Spawning new object with type: " + iconPrefab);
        GameObject newObject = Instantiate(Resources.Load<GameObject>("MapPrefabs/" + iconPrefab), MapManager.Instance.mapObject.transform.position, Quaternion.identity);
        newObject.GetComponent<NetworkObject>().Spawn();
        newObject.transform.SetParent(sharedCanvas.transform);

        ulong objectId = newObject.GetComponent<NetworkObject>().NetworkObjectId;
        SetMapElementDataClientRpc(requestId, objectId, iconSprite, iconScale);
        SetMapElementPositionRpc(objectId, iconPosition);
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Everyone)]
    public void SetMapElementPositionRpc(ulong targetElement, Vector2 targetPosition, bool ignoreOwner = false, RpcParams rpcParams = default)
    {
        // skip the sender so dragging stays smooth
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
            // replace the local placeholder with the networked object
            var pendingObject = MapManager.Instance.pendingPlacements.FirstOrDefault(p => p.Key == requestId);
            if (pendingObject.Value != null) 
            {
                targetObject.transform.Find("Sprite")?.gameObject.SetActive(false);
                if (targetObject.GetComponent<Image>()) targetObject.GetComponent<Image>().enabled = false;
            }

            targetObject.name = targetSprite;
            targetObject.transform.localScale = targetScale;
            if (targetSprite == "DrawDot") 
            {
                targetObject.transform.SetSiblingIndex(MapManager.Instance.iconPile.transform.GetSiblingIndex());
            }
            else
            {
                targetObject.transform.Find("Sprite").GetComponent<Image>().sprite = MapManager.Instance.mapSprites.FirstOrDefault(s => s.name.Contains(targetSprite));
                targetObject.transform.Find("Sprite").GetComponent<Image>().preserveAspect = true;
                targetObject.transform.Find("Name").gameObject.SetActive(false);
                MapManager.Instance.SetupElementTriggers(targetObject.gameObject);
            }

            if (pendingObject.Value) StartCoroutine(MapManager.Instance.ElementReplaceRoutine(pendingObject.Value, targetObject.gameObject));
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
        // despawn through the server so every client removes it
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
        foreach (Transform element in sharedCanvas.transform)
        {
            // keep the player icons when clearing the map
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
        // rebuild a player's map from serialized data
        GameObject targetMap = InventoryManager.Instance.FetchMapObjectById(persistentId);
        targetMap.transform.Find("Title").GetComponent<TMP_Text>().text = playerName;
        Debug.Log("MNM: Spawning map objects for player: " + playerName);

        foreach (var element in mapElements)
        {
            GameObject newObject = Instantiate(Resources.Load<GameObject>("MapPrefabs/" + element.iconPrefab));
            newObject.name = element.iconSprite;
            if (element.iconPrefab == "MapIcon")
            {
                newObject.transform.Find("Sprite").GetComponent<Image>().sprite = MapManager.Instance.mapSprites.FirstOrDefault(i => i.name.Contains(element.iconSprite));
                newObject.transform.Find("Sprite").GetComponent<Image>().preserveAspect = true;
                newObject.transform.Find("Name").gameObject.SetActive(false);
            }
            newObject.transform.SetParent(targetMap.transform.Find("Canvas"));
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
            // whoever drags the element keeps ownership until it is released
            ulong clientId = rpcParams.Receive.SenderClientId;
            targetObject.ChangeOwnership(resetOwnership ? NetworkManager.ServerClientId : clientId);    
        }
    }
}
