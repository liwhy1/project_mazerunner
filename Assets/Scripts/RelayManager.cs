using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using Unity.Netcode.Transports.UTP;

public class RelayManager : MonoBehaviour
{
    public static RelayManager Instance;

    private void Awake()
    {
        Instance = this;
    }

    public async Task<string> StartHost(int maxPlayers)
    {
        await InitializeUnityServices();

        Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers - 1);
        var relayServerData = AllocationUtils.ToRelayServerData(allocation, "wss");
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

        transport.SetRelayServerData(relayServerData);
        transport.UseWebSockets = true;

        bool started = NetworkManager.Singleton.StartHost();

        if (!started)
        {
            Debug.LogError("RelayManager: Failed to start host.");
            return null;
        }

        string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

        Debug.Log("RelayManager: Relay Join Code: " + joinCode);

        return joinCode;
    }

    public async Task<bool> JoinHost(string joinCode)
    {
        await InitializeUnityServices();

        JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
        var relayServerData = AllocationUtils.ToRelayServerData(allocation, "wss");
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

        transport.SetRelayServerData(relayServerData);
        transport.UseWebSockets = true;

        return NetworkManager.Singleton.StartClient();
    }

    private async Task InitializeUnityServices()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
        {
            await UnityServices.InitializeAsync();
        }

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
    }
}