using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace MyGame
{
    public class RelayConnectionManager : MonoBehaviour
    {
        public static RelayConnectionManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public async Task<string> StartHostWithRelayAsync(int maxConnections)
        {
            try
            {
                if (!await UGSBootstrap.EnsureInitializedAsync())
                    return string.Empty;

                await ShutdownIfRunningAsync();

                if (!TryGetNetworkComponents(out NetworkManager networkManager, out UnityTransport transport))
                    return string.Empty;

                Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
                string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, "udp"));

                bool started = networkManager.StartHost();
                if (!started)
                {
                    Debug.LogError("Relay host could not start NetworkManager.");
                    return string.Empty;
                }

                return joinCode;
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("Relay host start failed: " + exception.Message);
                await ShutdownIfRunningAsync();
                return string.Empty;
            }
        }

        public async Task<bool> StartClientWithRelayAsync(string joinCode)
        {
            try
            {
                if (!await UGSBootstrap.EnsureInitializedAsync())
                    return false;

                if (!TryGetNetworkComponents(out NetworkManager networkManager, out UnityTransport transport))
                    return false;

                if (networkManager.IsListening)
                {
                    if (networkManager.IsHost || networkManager.IsConnectedClient)
                        return true;

                    bool connected = await WaitForClientConnectedAsync();
                    if (!connected)
                        await ShutdownIfRunningAsync();

                    return connected;
                }

                JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
                transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, "udp"));

                if (!networkManager.StartClient())
                    return false;

                bool result = await WaitForClientConnectedAsync();
                if (!result)
                    await ShutdownIfRunningAsync();

                return result;
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("Relay client start failed: " + exception.Message);
                await ShutdownIfRunningAsync();
                return false;
            }
        }

        public async Task ShutdownIfRunningAsync()
        {
            if (NetworkManager.Singleton == null) return;
            if (!NetworkManager.Singleton.IsListening) return;

            NetworkManager.Singleton.Shutdown();
            await Task.Delay(250);
        }

        private static bool TryGetNetworkComponents(
            out NetworkManager networkManager,
            out UnityTransport transport)
        {
            networkManager = NetworkManager.Singleton;
            transport = networkManager != null
                ? networkManager.GetComponent<UnityTransport>()
                : null;

            if (networkManager != null && transport != null)
                return true;

            Debug.LogError("NetworkManager または UnityTransport が見つかりません。");
            return false;
        }

        private async Task<bool> WaitForClientConnectedAsync()
        {
            const int timeoutMs = 10000;
            const int intervalMs = 100;
            int elapsedMs = 0;

            while (NetworkManager.Singleton != null &&
                   NetworkManager.Singleton.IsListening &&
                   elapsedMs < timeoutMs)
            {
                if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsConnectedClient)
                {
                    return true;
                }

                await Task.Delay(intervalMs);
                elapsedMs += intervalMs;
            }

            return NetworkManager.Singleton != null &&
                   (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsConnectedClient);
        }
    }
}
