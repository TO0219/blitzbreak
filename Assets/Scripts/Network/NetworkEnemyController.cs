using System;
using Unity.Netcode;
using UnityEngine;

namespace MyGame
{
    public struct NetworkEnemyState : INetworkSerializable, IEquatable<NetworkEnemyState>
    {
        public float Health;
        public bool IsAir;
        public bool IsInfight;
        public int DropType;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref Health);
            serializer.SerializeValue(ref IsAir);
            serializer.SerializeValue(ref IsInfight);
            serializer.SerializeValue(ref DropType);
        }

        public bool Equals(NetworkEnemyState other)
        {
            return Health.Equals(other.Health) &&
                   IsAir == other.IsAir &&
                   IsInfight == other.IsInfight &&
                   DropType == other.DropType;
        }
    }

    public sealed class NetworkEnemyController : NetworkBehaviour
    {
        [SerializeField] private EnemyEntity enemyEntity;

        private readonly NetworkVariable<NetworkEnemyState> m_state =
            new NetworkVariable<NetworkEnemyState>(
                default,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        public void ConfigureServer(bool isAir, bool isInfight)
        {
            if (enemyEntity == null)
                enemyEntity = GetComponent<EnemyEntity>();

            if (enemyEntity == null)
                return;

            enemyEntity.SetNetworkController(this);
            enemyEntity.ConfigureNetworkReplica(true);
            enemyEntity.Configure(isAir, isInfight);
            m_state.Value = CreateCurrentState();
        }

        public override void OnNetworkSpawn()
        {
            if (enemyEntity == null)
                enemyEntity = GetComponent<EnemyEntity>();

            if (enemyEntity == null)
                return;

            enemyEntity.SetNetworkController(this);
            enemyEntity.ConfigureNetworkReplica(IsServer);
            m_state.OnValueChanged += OnStateChanged;

            if (IsServer)
                SyncFromServer();
            else
                enemyEntity.ApplyNetworkState(m_state.Value);
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer && enemyEntity != null && m_state.Value.Health <= 0f)
                enemyEntity.PlayDeathDestructionEffect();

            m_state.OnValueChanged -= OnStateChanged;
        }

        public void SyncFromServer()
        {
            if (!IsSpawned || !IsServer || enemyEntity == null)
                return;

            m_state.Value = CreateCurrentState();
        }

        private NetworkEnemyState CreateCurrentState()
        {
            return new NetworkEnemyState
            {
                Health = enemyEntity.CurrentHealth,
                IsAir = enemyEntity.IsAir,
                IsInfight = enemyEntity.IsInfight,
                DropType = enemyEntity.DropTypeIndex
            };
        }

        public bool DespawnFromServer()
        {
            if (!IsServer || !IsSpawned)
                return false;

            PlayDeathDestructionClientRpc();
            NetworkObject.Despawn(true);
            return true;
        }

        [ClientRpc]
        private void PlayDeathDestructionClientRpc()
        {
            if (enemyEntity == null)
                enemyEntity = GetComponent<EnemyEntity>();

            enemyEntity?.PlayDeathDestructionEffect();
        }

        private void OnStateChanged(
            NetworkEnemyState previousValue,
            NetworkEnemyState newValue)
        {
            if (!IsServer && enemyEntity != null)
                enemyEntity.ApplyNetworkState(newValue);
        }
    }
}
