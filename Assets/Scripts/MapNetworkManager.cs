using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public enum MapState {Null, Own, Shared, Individual};
public class MapNetworkManager : NetworkBehaviour
{
    public static MapNetworkManager Instance;
    public NetworkVariable<MapState> mapState = new NetworkVariable<MapState>(MapState.Own, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        Instance = this;
        mapState.OnValueChanged += OnMapStateChanged;
        OnMapStateChanged(MapState.Null, mapState.Value);
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
            if (GameManager.Instance.isMaster) MenuManager.Instance.OnMasterSharedMapEnabled();
            MapManager.Instance?.OnDisableMapUI();
            InventoryManager.Instance.OnToggleSharedView(true);
            InventoryManager.Instance.OnJournalDisable();
            if (oldValue != MapState.Null) MapManager.Instance.OnSendMapData();
        }
        else if (newValue == MapState.Individual)
        {
            MapManager.Instance?.OnDisableMapUI();
            MapManager.SharedInstance.OnDisableMapUI();
            UIManager.Instance.MirrorSharedmaptoMinimap();
            InventoryManager.Instance.OnToggleSharedView(true);
            InventoryManager.Instance.OnToggleIndividualView(true);
            InventoryManager.Instance.OnJournalEnable();
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
        MenuManager.Instance.SetMasterGameStateText("Individual mapping\n" + GameManager.Instance.playerList.Count(p => p.IsMapReady.Value == true) + "/" + GameManager.Instance.playerList.Count);
        if (GameManager.Instance.playerList.All(p => p.IsMapReady.Value == true))
        {
            Debug.Log("MNM: All individual maps ready");
            SetMapStateServerRpc(MapState.Shared);
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SyncMapDataServerRpc()
    {
        if (mapState.Value == MapState.Own) return;

        // sync shared map element data
        foreach (Transform element in MapManager.SharedInstance.transform)
        {
            if (!element.GetComponent<NetworkObject>()) continue;
            SetMapElementDataClientRpc(0, element.GetComponent<NetworkObject>().NetworkObjectId, element.name);
        }
        // sync individual view data
        // NOTE: This assumes the player limit is 3
        for (int i = 0; i < 3; i++)
        {
            GameObject targetMap = InventoryManager.Instance.FetchMapObjectById(i);
            List<MapElementData> mapElements = new List<MapElementData>();
            foreach (Transform icon in targetMap.transform)
            {
                if (icon.name.Contains("Background") || icon.name.Contains("Title")) continue;
                string iconSprite = icon.transform.Find("Sprite") ? icon.transform.Find("Sprite").GetComponent<Image>().sprite.name : "";
                string iconPrefab = icon.name.Contains("Dot") ? "DrawDot" : "MapIcon";
                mapElements.Add(new MapElementData{iconPrefab = iconPrefab, iconSprite = iconSprite, iconPosition = icon.transform.localPosition});
            }
            string playerName = targetMap.transform.Find("Title").GetComponent<TMP_Text>().text;
            SpawnMapInstanceClientRpc(i, playerName, mapElements.ToArray());
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SpawnMapElementServerRpc(ulong requestId, string iconPrefab, string iconSprite, Vector3 iconPosition)
    {
        Debug.Log("MNM: Spawning new object with type: " + iconPrefab);
        GameObject newObject = Instantiate(Resources.Load<GameObject>("MapPrefabs/" + iconPrefab), MapManager.SharedInstance.transform.position, Quaternion.identity);

        newObject.name = iconSprite;
        newObject.GetComponent<NetworkObject>().Spawn();
        newObject.transform.SetParent(MapManager.SharedInstance.transform, false);
        newObject.transform.localPosition = new Vector3(iconPosition.x, iconPosition.y, 1f);

        ulong objectId = newObject.GetComponent<NetworkObject>().NetworkObjectId;
        SetMapElementDataClientRpc(requestId, objectId, iconSprite);
    }

    [ClientRpc]
    public void SetMapElementDataClientRpc(ulong requestId, ulong targetElement, string targetSprite)
    {
        if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetElement, out NetworkObject targetObject))
        {
                    targetObject.transform.Find("Sprite")?.gameObject.SetActive(false);
        if (targetObject.GetComponent<Image>()) targetObject.GetComponent<Image>().enabled = false;
            targetObject.name = targetSprite;
            if (targetSprite == "DrawDot") 
            {
                targetObject.transform.SetSiblingIndex(MapManager.SharedInstance.iconPile.transform.GetSiblingIndex());
                MapManager.SharedInstance.SetupDrawDotEventTriggers(targetObject.gameObject);
            }
            else
            {
                targetObject.transform.Find("Sprite").GetComponent<Image>().sprite = MapManager.SharedInstance.mapSprites.FirstOrDefault(s => s.name.Contains(targetSprite));
                targetObject.transform.Find("Sprite").GetComponent<Image>().preserveAspect = true;
                targetObject.transform.Find("Name").gameObject.SetActive(false);
                MapManager.SharedInstance.SetupElementTriggers(targetObject.gameObject);
            }

            var pendingObject = MapManager.SharedInstance.pendingPlacements.FirstOrDefault(p => p.Key == requestId);
            if (pendingObject.Value) StartCoroutine(MapManager.SharedInstance.ElementReplaceRoutine(pendingObject.Value, targetObject.gameObject));
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
        foreach (Transform element in transform)
        {
            element.gameObject.GetComponent<NetworkObject>()?.Despawn();
        }
    }

    [ClientRpc]
    public void SpawnMapInstanceClientRpc(int persistentId, string playerName, MapElementData[] mapElements)
    {
        GameObject targetMap = InventoryManager.Instance.FetchMapObjectById(persistentId);
        // prevent spawning instance if elements already exits on the map
        if (targetMap.transform.childCount > 2) return;
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
            newObject.transform.SetParent(targetMap.transform);
            newObject.transform.localPosition = element.iconPosition;
            newObject.transform.localScale = new Vector3(1f, 1f, 1f);
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
            ulong clientId = resetOwnership ? NetworkManager.ServerClientId : rpcParams.Receive.SenderClientId;
            targetObject.ChangeOwnership(clientId);    
        }
    }
}
