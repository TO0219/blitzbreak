using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MyGame
{
    public class NetworkBattleManager : NetworkBehaviour
    {
        public static NetworkBattleManager Instance { get; private set; }

        [SerializeField] private GameObject networkPlayerPrefab;
        [SerializeField] private GameObject networkCpuPrefab;
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private Transform centralLandmark;
        [SerializeField, Min(1)] private int competitorCount = 4;

        private readonly Dictionary<ulong, NetworkObject> m_spawnedPlayers = new();
        private readonly Dictionary<ulong, int> m_playerSlots = new();
        private readonly Dictionary<int, NetworkObject> m_spawnedCpus = new();
        private readonly Dictionary<int, DisconnectedResult> m_disconnectedResults = new();
        private bool m_canSpawnPlayers;

        private struct DisconnectedResult
        {
            public int Score;
            public int DefeatedEnemies;
            public float DamageTaken;
            public int DeathCount;
        }

        private void Awake()
        {
            Instance = this;
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;

            m_canSpawnPlayers = false;

            NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
            NetworkManager.SceneManager.OnSceneEvent += OnSceneEvent;
        }

        public override void OnNetworkDespawn()
        {
            if (NetworkManager == null) return;

            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;

            if (NetworkManager.SceneManager != null)
            {
                NetworkManager.SceneManager.OnSceneEvent -= OnSceneEvent;
            }
        }

        public override void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            base.OnDestroy();
        }

        public void BroadcastBuildingDestroyed(
            int buildingId,
            Vector3 hitPoint,
            bool isPlayerImpact = false)
        {
            if (!IsServer || !IsSpawned)
                return;

            BuildingDestroyedClientRpc(buildingId, hitPoint, isPlayerImpact);
        }

        [ClientRpc]
        private void BuildingDestroyedClientRpc(
            int buildingId,
            Vector3 hitPoint,
            bool isPlayerImpact)
        {
            if (IsServer)
                return;

            DestructibleBuilding.TryBreakFromNetwork(
                buildingId,
                hitPoint,
                isPlayerImpact);
        }

        public void RequestBuildingDamage(int buildingId, float damage, Vector3 hitPoint)
        {
            if (!IsSpawned)
                return;

            if (IsServer)
            {
                DestructibleBuilding.TryDamageFromNetwork(
                    buildingId,
                    damage,
                    hitPoint);
                return;
            }

            RequestBuildingDamageRpc(buildingId, damage, hitPoint);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestBuildingDamageRpc(
            int buildingId,
            float damage,
            Vector3 hitPoint)
        {
            DestructibleBuilding.TryDamageFromNetwork(
                buildingId,
                Mathf.Clamp(damage, 0f, 9999f),
                hitPoint);
        }

        public void RequestBuildingImpactDamage(int buildingId, float damage, Vector3 hitPoint)
        {
            if (!IsSpawned)
                return;

            if (IsServer)
            {
                DestructibleBuilding.TryDamageByPlayerImpactFromNetwork(
                    buildingId,
                    damage,
                    hitPoint);
                return;
            }

            RequestBuildingImpactDamageRpc(buildingId, damage, hitPoint);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestBuildingImpactDamageRpc(
            int buildingId,
            float damage,
            Vector3 hitPoint)
        {
            DestructibleBuilding.TryDamageByPlayerImpactFromNetwork(
                buildingId,
                Mathf.Clamp(damage, 0f, 9999f),
                hitPoint);
        }

        private void OnSceneEvent(SceneEvent sceneEvent)
        {
            if (!IsServer) return;

            if (sceneEvent.SceneName != gameObject.scene.name)
                return;

            if (sceneEvent.SceneEventType == SceneEventType.LoadEventCompleted)
            {
                m_canSpawnPlayers = true;
                SpawnMissingConnectedClients();
                FillEmptySlotsWithCpus();
                return;
            }

            if (sceneEvent.SceneEventType == SceneEventType.SynchronizeComplete)
            {
                if (!m_canSpawnPlayers)
                    return;

                SpawnPlayerForClient(sceneEvent.ClientId);
                FillEmptySlotsWithCpus();
            }
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (!m_spawnedPlayers.ContainsKey(clientId)) return;

            CaptureDisconnectedResult(clientId);

            if (m_spawnedPlayers[clientId] != null && m_spawnedPlayers[clientId].IsSpawned)
                m_spawnedPlayers[clientId].Despawn();

            m_spawnedPlayers.Remove(clientId);
            m_playerSlots.Remove(clientId);
            FillEmptySlotsWithCpus();
        }

        private void SpawnMissingConnectedClients()
        {
            if (NetworkManager == null) return;

            for (int i = 0; i < NetworkManager.ConnectedClientsIds.Count; i++)
            {
                ulong clientId = NetworkManager.ConnectedClientsIds[i];
                SpawnPlayerForClient(clientId);
            }
        }

        private void SpawnPlayerForClient(ulong clientId)
        {
            if (!m_canSpawnPlayers)
            {
                return;
            }

            if (networkPlayerPrefab == null)
            {
                Debug.LogError("networkPlayerPrefab is null");
                return;
            }

            if (m_spawnedPlayers.TryGetValue(clientId, out NetworkObject spawnedPlayer))
            {
                if (spawnedPlayer != null && spawnedPlayer.IsSpawned)
                {
                    return;
                }

                m_spawnedPlayers.Remove(clientId);
                m_playerSlots.Remove(clientId);
            }

            if (NetworkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient client) &&
                client.PlayerObject != null &&
                client.PlayerObject.IsSpawned)
            {
                m_spawnedPlayers[clientId] = client.PlayerObject;
                if (!m_playerSlots.ContainsKey(clientId))
                {
                    int existingPlayerSlot = FindAvailableSlot();
                    if (existingPlayerSlot >= 0)
                    {
                        DespawnCpuInSlot(existingPlayerSlot);
                        m_playerSlots[clientId] = existingPlayerSlot;
                    }
                }
                return;
            }

            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                Debug.LogError("spawnPoints is empty");
                return;
            }

            int slotIndex = FindAvailableSlot();
            if (slotIndex < 0)
            {
                Debug.LogWarning("No battle spawn slot is available for client : " + clientId);
                return;
            }

            DespawnCpuInSlot(slotIndex);
            Transform spawnPoint = spawnPoints[slotIndex];
            if (spawnPoint == null)
            {
                Debug.LogError("Spawn point is null at slot : " + slotIndex);
                return;
            }

            GameObject playerObj = Instantiate(
                networkPlayerPrefab,
                spawnPoint.position,
                spawnPoint.rotation
            );

            SceneManager.MoveGameObjectToScene(playerObj, gameObject.scene);

            PlayerEntity playerEntity = playerObj.GetComponent<PlayerEntity>();
            if (playerEntity != null)
                playerEntity.ApplyMachineIndex(GetInitialMachineIndex(clientId));

            NetworkObject netObj = playerObj.GetComponent<NetworkObject>();
            if (netObj == null)
            {
                Debug.LogError("NetworkObject not found on player prefab");
                Destroy(playerObj);
                return;
            }

            netObj.SpawnAsPlayerObject(clientId, true);
            m_spawnedPlayers[clientId] = netObj;
            m_playerSlots[clientId] = slotIndex;

            PlayerNetworkController playerNetworkController = playerObj.GetComponent<PlayerNetworkController>();
            if (playerNetworkController != null)
                playerNetworkController.SetRespawnPoint(spawnPoint.position, spawnPoint.rotation);
        }

        private void FillEmptySlotsWithCpus()
        {
            if (!IsServer || !m_canSpawnPlayers || networkCpuPrefab == null)
                return;

            RemoveInvalidCpuEntries();

            int slotCount = GetSlotCount();
            for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
            {
                if (IsPlayerSlotOccupied(slotIndex) ||
                    m_spawnedCpus.ContainsKey(slotIndex))
                {
                    continue;
                }

                SpawnCpu(slotIndex);
            }

            List<int> invalidSlots = null;
            foreach (KeyValuePair<int, NetworkObject> pair in m_spawnedCpus)
            {
                if (pair.Key < slotCount && !IsPlayerSlotOccupied(pair.Key))
                    continue;

                invalidSlots ??= new List<int>();
                invalidSlots.Add(pair.Key);
            }

            if (invalidSlots == null)
                return;

            for (int i = 0; i < invalidSlots.Count; i++)
                DespawnCpuInSlot(invalidSlots[i]);
        }

        private void SpawnCpu(int slotIndex)
        {
            if (spawnPoints == null || spawnPoints.Length == 0)
                return;

            if (slotIndex < 0 ||
                slotIndex >= GetSlotCount() ||
                IsPlayerSlotOccupied(slotIndex) ||
                m_spawnedCpus.ContainsKey(slotIndex))
            {
                return;
            }

            Transform spawnPoint = spawnPoints[slotIndex];
            if (spawnPoint == null)
                return;

            GameObject cpuObject = Instantiate(
                networkCpuPrefab,
                spawnPoint.position,
                spawnPoint.rotation);
            cpuObject.name = $"NetworkCPU_{slotIndex + 1}";
            SceneManager.MoveGameObjectToScene(cpuObject, gameObject.scene);

            EnemyCPUEntity cpu = cpuObject.GetComponent<EnemyCPUEntity>();
            if (cpu != null)
            {
                cpu.ApplyMachineIndex(
                    MachineProfileCatalog.Count > 0
                        ? slotIndex % MachineProfileCatalog.Count
                        : 0);
                cpu.SetObstacleAvoidanceSide(slotIndex % 2 == 0 ? 1 : -1);
                cpu.SetRespawnPoint(spawnPoint.position, spawnPoint.rotation);
                cpu.SetCentralLandmark(centralLandmark, spawnPoint.position);
                ApplyDisconnectedResultToCpu(slotIndex, cpu);
            }

            NetworkObject networkObject = cpuObject.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                Debug.LogError("NetworkObject not found on CPU prefab");
                Destroy(cpuObject);
                return;
            }

            networkObject.Spawn(true);
            m_spawnedCpus[slotIndex] = networkObject;
        }

        private int FindAvailableSlot()
        {
            RemoveInvalidCpuEntries();

            int slotCount = GetSlotCount();
            for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
            {
                if (!IsPlayerSlotOccupied(slotIndex) &&
                    !m_spawnedCpus.ContainsKey(slotIndex))
                {
                    return slotIndex;
                }
            }

            for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
            {
                if (!IsPlayerSlotOccupied(slotIndex) &&
                    m_spawnedCpus.ContainsKey(slotIndex))
                {
                    return slotIndex;
                }
            }

            return -1;
        }

        private void DespawnCpuInSlot(int slotIndex)
        {
            RemoveInvalidCpuEntries();
            if (!m_spawnedCpus.TryGetValue(slotIndex, out NetworkObject cpu))
                return;

            m_spawnedCpus.Remove(slotIndex);

            if (cpu != null && cpu.IsSpawned)
                cpu.Despawn(true);
        }

        private void RemoveInvalidCpuEntries()
        {
            List<int> invalidSlots = null;
            foreach (KeyValuePair<int, NetworkObject> pair in m_spawnedCpus)
            {
                if (pair.Value != null && pair.Value.IsSpawned)
                    continue;

                invalidSlots ??= new List<int>();
                invalidSlots.Add(pair.Key);
            }

            if (invalidSlots == null)
                return;

            for (int i = 0; i < invalidSlots.Count; i++)
                m_spawnedCpus.Remove(invalidSlots[i]);
        }

        private bool IsPlayerSlotOccupied(int slotIndex)
        {
            foreach (KeyValuePair<ulong, int> pair in m_playerSlots)
            {
                if (pair.Value == slotIndex &&
                    m_spawnedPlayers.TryGetValue(pair.Key, out NetworkObject player) &&
                    player != null &&
                    player.IsSpawned)
                {
                    return true;
                }
            }

            return false;
        }

        private int GetSlotCount()
        {
            int spawnPointCount = spawnPoints != null ? spawnPoints.Length : 0;
            return Mathf.Min(Mathf.Max(1, competitorCount), spawnPointCount);
        }

        private int GetInitialMachineIndex(ulong clientId)
        {
            if (NetworkManager != null &&
                clientId == NetworkManager.LocalClientId &&
                GameModeManager.Instance != null)
            {
                return GameModeManager.Instance.SelectedMachineIndex;
            }

            return 0;
        }

        private void CaptureDisconnectedResult(ulong clientId)
        {
            if (!m_playerSlots.TryGetValue(clientId, out int slotIndex))
                return;

            if (!m_spawnedPlayers.TryGetValue(clientId, out NetworkObject playerObject) ||
                playerObject == null)
            {
                return;
            }

            PlayerEntity player = playerObject.GetComponent<PlayerEntity>();
            PlayerNetworkController networkSetup = playerObject.GetComponent<PlayerNetworkController>();

            DisconnectedResult result = new DisconnectedResult
            {
                Score = networkSetup != null ? networkSetup.NetworkScore : player != null ? player.Score : 0,
                DefeatedEnemies = player != null ? player.DefeatedEnemies : 0,
                DamageTaken = player != null ? player.DamageTaken : 0f,
                DeathCount = player != null ? player.DeathCount : 0
            };

            if (m_disconnectedResults.TryGetValue(slotIndex, out DisconnectedResult existing))
            {
                result.Score += existing.Score;
                result.DefeatedEnemies += existing.DefeatedEnemies;
                result.DamageTaken += existing.DamageTaken;
                result.DeathCount += existing.DeathCount;
            }

            m_disconnectedResults[slotIndex] = result;
        }

        private void ApplyDisconnectedResultToCpu(int slotIndex, EnemyCPUEntity cpu)
        {
            if (cpu == null ||
                !m_disconnectedResults.TryGetValue(slotIndex, out DisconnectedResult result))
            {
                return;
            }

            cpu.AddResultCarryOver(
                result.Score,
                result.DefeatedEnemies,
                result.DamageTaken,
                result.DeathCount);
            m_disconnectedResults.Remove(slotIndex);
        }
    }
}
