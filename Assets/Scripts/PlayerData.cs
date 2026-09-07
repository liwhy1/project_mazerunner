using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerData : NetworkBehaviour
{
    public NetworkVariable<FixedString64Bytes> PlayerName = new NetworkVariable<FixedString64Bytes>("Player", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            string name = PlayerPrefs.GetString("PlayerName", "Player");
            SetPlayerNameServerRpc(name);
        }
    }

    [ServerRpc]
    private void SetPlayerNameServerRpc(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "Player";
        }
        PlayerName.Value = name;

        PlayerData[] players = FindObjectsByType<PlayerData>();
        foreach (PlayerData player in players)
        {
            Debug.Log("Client: " + player.OwnerClientId + ": " + player.PlayerName.Value);
        }
    }
}