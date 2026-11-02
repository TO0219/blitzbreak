using Unity.Netcode;
using UnityEngine;

namespace MyGame
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkProjectileAuthority : NetworkBehaviour
    {
        private readonly NetworkVariable<ulong> m_ownerClientId =
            new NetworkVariable<ulong>(
                ulong.MaxValue,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<ulong> m_targetNetworkObjectId =
            new NetworkVariable<ulong>(
                ulong.MaxValue,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private PlayerEntity m_cachedOwner;
        private Transform m_cachedTarget;

        public bool CanProcessHits
        {
            get
            {
                if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
                    return true;

                return !IsSpawned || IsServer;
            }
        }

        public void ConfigurePlayer(
            ulong ownerClientId,
            ulong targetNetworkObjectId = ulong.MaxValue)
        {
            m_ownerClientId.Value = ownerClientId;
            m_targetNetworkObjectId.Value = targetNetworkObjectId;
            m_cachedOwner = null;
            m_cachedTarget = null;
        }

        public void ConfigureTarget(ulong targetNetworkObjectId)
        {
            m_targetNetworkObjectId.Value = targetNetworkObjectId;
            m_cachedTarget = null;
        }

        public PlayerEntity ResolvePlayerOwner()
        {
            if (m_cachedOwner != null)
                return m_cachedOwner;

            if (NetworkManager.Singleton == null)
                return null;

            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(
                    m_ownerClientId.Value,
                    out NetworkClient client))
            {
                return null;
            }

            if (client.PlayerObject == null)
                return null;

            m_cachedOwner = client.PlayerObject.GetComponent<PlayerEntity>();
            return m_cachedOwner;
        }

        public Transform ResolveTarget()
        {
            if (m_cachedTarget != null)
                return m_cachedTarget;

            if (m_targetNetworkObjectId.Value == ulong.MaxValue ||
                NetworkManager.Singleton == null ||
                NetworkManager.Singleton.SpawnManager == null)
            {
                return null;
            }

            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(
                    m_targetNetworkObjectId.Value,
                    out NetworkObject targetObject))
            {
                return null;
            }

            m_cachedTarget = targetObject.transform;
            return m_cachedTarget;
        }

        public bool TryDespawn()
        {
            if (NetworkManager.Singleton == null ||
                !NetworkManager.Singleton.IsListening ||
                !IsSpawned)
            {
                return false;
            }

            if (IsServer)
                NetworkObject.Despawn(true);

            return true;
        }
    }
}
