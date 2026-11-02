using System;
using Unity.Netcode;
using UnityEngine;

namespace MyGame
{
    public struct NetworkCpuState : INetworkSerializable, IEquatable<NetworkCpuState>
    {
        public float Health;
        public float MaxHealth;
        public int Score;
        public int DefeatedEnemies;
        public float DamageTaken;
        public int DeathCount;
        public int MachineIndex;
        public bool IsRespawning;
        public int WeaponType;
        public double SpecialWeaponEndTime;
        public float BarrelPitch;
        public Quaternion BodyRotation;
        public Quaternion TurretRotation;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref Health);
            serializer.SerializeValue(ref MaxHealth);
            serializer.SerializeValue(ref Score);
            serializer.SerializeValue(ref DefeatedEnemies);
            serializer.SerializeValue(ref DamageTaken);
            serializer.SerializeValue(ref DeathCount);
            serializer.SerializeValue(ref MachineIndex);
            serializer.SerializeValue(ref IsRespawning);
            serializer.SerializeValue(ref WeaponType);
            serializer.SerializeValue(ref SpecialWeaponEndTime);
            serializer.SerializeValue(ref BarrelPitch);
            serializer.SerializeValue(ref BodyRotation);
            serializer.SerializeValue(ref TurretRotation);
        }

        public bool Equals(NetworkCpuState other)
        {
            return Health.Equals(other.Health) &&
                   MaxHealth.Equals(other.MaxHealth) &&
                   Score == other.Score &&
                   DefeatedEnemies == other.DefeatedEnemies &&
                   DamageTaken.Equals(other.DamageTaken) &&
                   DeathCount == other.DeathCount &&
                   MachineIndex == other.MachineIndex &&
                   IsRespawning == other.IsRespawning &&
                   WeaponType == other.WeaponType &&
                   SpecialWeaponEndTime.Equals(other.SpecialWeaponEndTime) &&
                   BarrelPitch.Equals(other.BarrelPitch) &&
                   BodyRotation.Equals(other.BodyRotation) &&
                   TurretRotation.Equals(other.TurretRotation);
        }
    }

    public sealed class NetworkCpuController : NetworkBehaviour
    {
        [SerializeField] private EnemyCPUEntity cpuEntity;

        private readonly NetworkVariable<NetworkCpuState> m_state =
            new NetworkVariable<NetworkCpuState>(
                default,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private int m_appliedWeaponType;
        private float m_nextVisualSyncTime;
        private const float VisualSyncInterval = 0.08f;

        public override void OnNetworkSpawn()
        {
            if (cpuEntity == null)
                cpuEntity = GetComponent<EnemyCPUEntity>();

            if (cpuEntity == null)
                return;

            cpuEntity.SetNetworkController(this);
            cpuEntity.ConfigureNetworkReplica(IsServer);
            m_state.OnValueChanged += OnStateChanged;

            if (IsServer)
                SyncFromServer();
            else
                ApplyNetworkState(m_state.Value);
        }

        private void Update()
        {
            if (IsServer)
            {
                if (cpuEntity != null &&
                    Time.unscaledTime >= m_nextVisualSyncTime)
                {
                    m_nextVisualSyncTime =
                        Time.unscaledTime + VisualSyncInterval;
                    SyncFromServer();
                }

                return;
            }

            if (cpuEntity == null ||
                m_appliedWeaponType == 0 ||
                NetworkManager == null)
            {
                return;
            }

            if (NetworkManager.ServerTime.Time >= m_state.Value.SpecialWeaponEndTime)
            {
                m_appliedWeaponType = 0;
                cpuEntity.ApplyNetworkWeaponState(0, 0f);
            }
        }

        public override void OnNetworkDespawn()
        {
            m_state.OnValueChanged -= OnStateChanged;
        }

        public void BroadcastDeathExplosion(Vector3 position)
        {
            if (!IsServer || !IsSpawned)
                return;

            PlayDeathExplosionClientRpc(position);
        }

        public void BroadcastRailgunBeam(Vector3 start, Vector3 end)
        {
            if (!IsServer || !IsSpawned)
                return;

            PlayRailgunBeamClientRpc(start, end);
        }

        public void SyncFromServer()
        {
            if (!IsServer || !IsSpawned || cpuEntity == null)
                return;

            m_state.Value = new NetworkCpuState
            {
                Health = cpuEntity.CPUCurrentHealth,
                MaxHealth = cpuEntity.CPUMaxHealth,
                Score = cpuEntity.Score,
                DefeatedEnemies = cpuEntity.DefeatedEnemies,
                DamageTaken = cpuEntity.DamageTaken,
                DeathCount = cpuEntity.DeathCount,
                MachineIndex = cpuEntity.MachineIndex,
                IsRespawning = cpuEntity.IsRespawning,
                WeaponType = cpuEntity.CurrentWeaponType,
                SpecialWeaponEndTime = cpuEntity.CurrentWeaponType == 0
                    ? 0d
                    : NetworkManager.ServerTime.Time +
                      cpuEntity.SpecialWeaponRemaining,
                BarrelPitch = cpuEntity.BarrelPitch,
                BodyRotation = cpuEntity.NetworkBodyRotation,
                TurretRotation = cpuEntity.NetworkTurretRotation
            };
        }

        private void OnStateChanged(NetworkCpuState previousValue, NetworkCpuState newValue)
        {
            if (!IsServer && cpuEntity != null)
                ApplyNetworkState(newValue);
        }

        private void ApplyNetworkState(NetworkCpuState state)
        {
            cpuEntity.ApplyNetworkState(state);

            m_appliedWeaponType = state.WeaponType;
            float remaining = NetworkManager != null
                ? Mathf.Max(
                    0f,
                    (float)(state.SpecialWeaponEndTime -
                            NetworkManager.ServerTime.Time))
                : 0f;
            cpuEntity.ApplyNetworkWeaponState(state.WeaponType, remaining);
        }

        [ClientRpc]
        private void PlayDeathExplosionClientRpc(Vector3 position)
        {
            if (IsServer || cpuEntity == null)
                return;

            cpuEntity.PlayDeathExplosionAt(position);
        }

        [ClientRpc]
        private void PlayRailgunBeamClientRpc(Vector3 start, Vector3 end)
        {
            RailgunBeamEffect.Play(start, end);
        }
    }
}
