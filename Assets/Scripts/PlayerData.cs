using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerData : NetworkBehaviour
{
    public NetworkVariable<FixedString64Bytes> PlayerName = new NetworkVariable<FixedString64Bytes>("Player", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private TMP_Text nameText;

    public override void OnNetworkSpawn()
    {
        nameText = transform.Find("NameCanvas").Find("Name").GetComponent<TMP_Text>();

        // update ui with the current value
        UpdateNameUI(PlayerName.Value);

        // subscribe to name updates from the server
        PlayerName.OnValueChanged += OnPlayerNameChanged;

        if (IsOwner)
        {
            string name = PlayerPrefs.GetString("PlayerName", "Player");
            SetPlayerNameServerRpc(name);
        }
    }

    private void OnPlayerNameChanged(FixedString64Bytes oldName, FixedString64Bytes newName)
    {
        UpdateNameUI(newName);
    }

    private void UpdateNameUI(FixedString64Bytes name)
    {
        nameText.text = name.ToString();
        GameManager.Instance.RefreshPlayerList();
    }

    [ServerRpc]
    private void SetPlayerNameServerRpc(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "Player";
        }

        PlayerName.Value = name;
    }

    public override void OnNetworkDespawn()
    {
        PlayerName.OnValueChanged -= OnPlayerNameChanged;
    }
}