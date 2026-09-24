using System.Linq;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerData : NetworkBehaviour
{
    public NetworkVariable<FixedString64Bytes> PlayerName = new NetworkVariable<FixedString64Bytes>("Player", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> IsMapReady = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> IsGameStarted = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> PersistentPlayerId = new NetworkVariable<int>(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> SkinId = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private TMP_Text nameText;

    public override void OnNetworkSpawn()
    {
        nameText = transform.Find("NameCanvas").Find("Name").GetComponent<TMP_Text>();
        nameText.text = PlayerName.Value.ToString();

        // subscribe to value updates from the server
        PlayerName.OnValueChanged += OnPlayerNameChanged;
        IsGameStarted.OnValueChanged += OnReadyStateChanged;
        PersistentPlayerId.OnValueChanged += OnPersistentIdChanged;
        SkinId.OnValueChanged += OnSkinIdChanged;
        IsMapReady.OnValueChanged += OnMapReadyStateChanged;

        if (IsOwner)
        {
            SetPlayerNameServerRpc(PlayerPrefs.GetString("PlayerName", "Player"));
            GameManager.Instance.SetPlayerPersistentIdServerRpc(NetworkManager.LocalClientId);
        }
    }

    private void OnMapReadyStateChanged(bool oldValue, bool newValue)
    {
        if (!IsOwner) return;
        GameManager.Instance.CheckLobbyMapStateServerRpc();
    }

    private void OnSkinIdChanged(int oldValue, int newValue)
    {
        if (!IsOwner) return;
        GameManager.Instance.SetPlayerPropertiesServerRpc();
    }

    private void OnPersistentIdChanged(int oldValue, int newValue)
    {
        if (!IsOwner) return;
        if (oldValue != -1) return;
        GameManager.Instance.OnLobbyConnect();
    }

    private void OnReadyStateChanged(bool oldValue, bool newValue)
    {
        if (!IsOwner) return;
        GameManager.Instance.FetchPlayerRenderStateServerRpc();
    }

    private void OnPlayerNameChanged(FixedString64Bytes oldName, FixedString64Bytes newName)
    {
        nameText.text = newName.ToString();

        if (!IsOwner) return;
        // refresh player list for each client
        GameManager.Instance.RefreshPlayerListClientRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void SetPlayerNameServerRpc(string name, RpcParams rpcParams = default)
    {
        if (string.IsNullOrEmpty(name)) return;

        ulong targetId = rpcParams.Receive.SenderClientId;
        Debug.Log("PlayerData: Setting playername: " + name + " to: " + targetId);
        FindObjectsByType<PlayerData>(FindObjectsInactive.Include).FirstOrDefault(p => p.OwnerClientId == targetId).PlayerName.Value = name;
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
        {
            PlayerName.OnValueChanged -= OnPlayerNameChanged;
            IsGameStarted.OnValueChanged -= OnReadyStateChanged;
            PersistentPlayerId.OnValueChanged -= OnPersistentIdChanged;
            SkinId.OnValueChanged -= OnSkinIdChanged;
            IsMapReady.OnValueChanged -= OnMapReadyStateChanged;
        }
    }
}