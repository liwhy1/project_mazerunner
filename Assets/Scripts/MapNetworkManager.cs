using System.Linq;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class MapNetworkManager : NetworkBehaviour
{
    public static MapNetworkManager Instance;

    private void Awake()
    {
        Instance = this;
    }

    [ClientRpc]
    public void SendMapInstanceClientRpc()
    {
        MapManager.Instance.OnSendMapData();
    }

    [ClientRpc]
    public void OnSharedMapReadyClientRpc()
    {
        MapManager.SharedInstance.OnDisableMapUI();
        UIManager.Instance.MirrorSharedmaptoMinimap();
        InventoryManager.Instance.OnJournalEnable();
        InventoryManager.Instance.individualViewButton.GetComponent<UIElement>().OnElementEnable();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SpawnMapElementServerRpc(string iconPrefab, string iconSprite, Vector3 iconPosition)
    {
        Debug.Log("SMM: Spawning new object with type: " + iconPrefab);
        GameObject newObject = Instantiate(Resources.Load<GameObject>("MapPrefabs/" + iconPrefab));
        newObject.GetComponent<NetworkObject>().Spawn();
        newObject.transform.SetParent(MapManager.SharedInstance.transform, false);
        newObject.transform.localPosition = new Vector3(iconPosition.x, iconPosition.y, 1f);

        ulong objectId = newObject.GetComponent<NetworkObject>().NetworkObjectId;
        SetMapElementDataClientRpc(objectId, iconSprite);
    }

    [ClientRpc]
    public void SetMapElementDataClientRpc(ulong targetElement, string targetSprite)
    {
        if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetElement, out NetworkObject targetObject))
        {
            targetObject.name = targetSprite;
            if (targetSprite == "DrawDot") 
            {
                targetObject.transform.SetSiblingIndex(MapManager.SharedInstance.iconPile.transform.GetSiblingIndex());
                MapManager.SharedInstance.SetupDrawDotEventTriggers(targetObject.gameObject);
            }
            else
            {
                targetObject.transform.Find("Sprite").GetComponent<Image>().sprite = MapManager.Instance.mapSprites.FirstOrDefault(s => s.name.Contains(targetSprite));
                targetObject.transform.Find("Sprite").GetComponent<Image>().preserveAspect = true;
                targetObject.transform.Find("Name").gameObject.SetActive(false);
                MapManager.SharedInstance.SetupElementTriggers(targetObject.gameObject);
            }
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void MoveMapElementServerRpc(ulong targetElement, Vector3 targetPosition)
    {
        if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetElement, out NetworkObject targetObject))
        {
            SetElementOwnershipServerRpc(targetElement, true);
            targetObject.transform.position = new Vector3(targetPosition.x, targetPosition.y, 1f);
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void DestroyMapElementServerRpc(ulong targetElement)
    {
        Debug.Log("SMM: Destroying element: " + targetElement);
        if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetElement, out NetworkObject targetObject))
        {
            targetObject.Despawn();
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void ClearMapServerRpc()
    {
        Debug.Log("SMM: Clearing map");
        foreach (Transform element in transform)
        {
            element.gameObject.GetComponent<NetworkObject>()?.Despawn();
        }
    }

    [ClientRpc]
    public void SpawnMapInstanceClientRpc(int persistentId, string playerName, MapElementData[] mapElements)
    {
        GameObject targetMap = persistentId == 0 ? InventoryManager.Instance.mapObjectP0 : persistentId == 1 ? InventoryManager.Instance.mapObjectP1 : InventoryManager.Instance.mapObjectP2;
        targetMap.transform.Find("Title").GetComponent<TMP_Text>().text = playerName;

        Debug.Log("SMM: Spawning map objects for player: " + playerName);
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