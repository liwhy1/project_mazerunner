using System;
using Unity.Netcode;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;
    public bool isPaused;
    [SerializeField] private GameObject playerObject;
    [SerializeField] private GameObject mainCamera;

    private void Start()
    {
        Instance = this;
        isPaused = false;
        UIManager.Instance.OnPauseToggle();

        NetworkManager.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
    }

    public override void OnDestroy()
    {
        NetworkManager.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    public void OnPauseToggle()
    {
        isPaused = !isPaused;
        UIManager.Instance.OnPauseToggle();
        InputManager.Instance.ToggleCursor();
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log("Client connected: " + clientId);
        if (!NetworkManager.IsHost) return;
        SpawnPlayer(clientId);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log("Client disconnected: " + clientId);
        if (!NetworkManager.IsHost) return;
    }

    public void OnStartHost()
    {
        try
        {
            NetworkManager.StartHost();
            mainCamera.SetActive(false);
        }
        catch (Exception ex)
        {
            Debug.Log("Failed to host session. " + ex);
            return;
        }
    }

    public void OnStartClient()
    {
        try
        {
            NetworkManager.StartClient();
        }
        catch (Exception ex)
        {
            Debug.Log("Failed to join session. " + ex);
            return;
        }
    }

    public void OnDisconnectClient()
    {
        try
        {
            NetworkManager.Shutdown();
        }
        catch (Exception ex)
        {
            Debug.Log("Failed to disconnect from session. " + ex);
            return;
        }
    }

    private void SpawnPlayer(ulong clientId)
    {
        Debug.Log("Spawning player for: " + clientId);
        GameObject player = Instantiate(playerObject, Vector3.zero, Quaternion.identity);
        player.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
    }
}
