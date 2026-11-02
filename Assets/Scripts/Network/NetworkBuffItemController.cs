using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace MyGame
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkBuffItemController : NetworkBehaviour
    {
        [SerializeField, Min(0f)] private float positionSyncThreshold = 0.01f;
        [SerializeField, Min(0f)] private float clientFollowSpeed = 20f;
        [SerializeField] private ItemEntity weaponItem;
        [SerializeField] private BuffItemEntity buffItem;

        private readonly NetworkVariable<Vector3> m_position =
            new NetworkVariable<Vector3>(
                default,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> m_positionInitialized =
            new NetworkVariable<bool>(
                false,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<FixedString32Bytes> m_weaponItemId =
            new NetworkVariable<FixedString32Bytes>(
                default,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private string m_pendingWeaponItemId;
        private bool m_hasPendingServerPosition;
        private Vector3 m_pendingServerPosition;
        private bool m_hasServerPosition;

        public void ConfigureSpawnPositionServer(Vector3 position)
        {
            m_pendingServerPosition = position;
            m_hasPendingServerPosition = true;
            transform.position = position;

            if (IsSpawned && IsServer)
            {
                m_position.Value = position;
                m_positionInitialized.Value = true;
                m_hasServerPosition = true;
            }
        }

        public void ConfigureWeaponServer(string itemId)
        {
            ResolveWeaponItem();
            m_pendingWeaponItemId = itemId;
            weaponItem?.SetItemId(itemId);

            if (IsSpawned && IsServer)
                m_weaponItemId.Value = new FixedString32Bytes(itemId);
        }

        public override void OnNetworkSpawn()
        {
            ResolveWeaponItem();
            m_position.OnValueChanged += OnPositionChanged;
            m_positionInitialized.OnValueChanged += OnPositionInitializedChanged;
            m_weaponItemId.OnValueChanged += OnWeaponItemIdChanged;

            if (IsServer)
            {
                Vector3 spawnPosition = m_hasPendingServerPosition
                    ? m_pendingServerPosition
                    : transform.position;
                transform.position = spawnPosition;
                m_position.Value = spawnPosition;
                m_positionInitialized.Value = true;
                m_hasServerPosition = true;
                if (weaponItem != null)
                {
                    string itemId = string.IsNullOrWhiteSpace(m_pendingWeaponItemId)
                        ? weaponItem.ItemId
                        : m_pendingWeaponItemId;
                    m_weaponItemId.Value = new FixedString32Bytes(itemId);
                }
            }
            else
            {
                m_hasServerPosition = m_positionInitialized.Value;
                if (m_hasServerPosition)
                    transform.position = m_position.Value;

                ApplyWeaponItemId(m_weaponItemId.Value);
            }
        }

        public override void OnNetworkDespawn()
        {
            m_position.OnValueChanged -= OnPositionChanged;
            m_positionInitialized.OnValueChanged -= OnPositionInitializedChanged;
            m_weaponItemId.OnValueChanged -= OnWeaponItemIdChanged;
            m_hasPendingServerPosition = false;
            m_hasServerPosition = false;
        }

        public bool TryDespawnFromServer()
        {
            if (!IsSpawned || !IsServer)
                return false;

            NetworkObject.Despawn(true);
            return true;
        }

        public void RequestPickup(PlayerEntity player)
        {
            if (player == null || !IsSpawned)
                return;

            NetworkObject playerObject = player.GetComponent<NetworkObject>();
            if (playerObject == null || !playerObject.IsSpawned)
                return;

            if (IsServer)
            {
                TryPickupPlayer(playerObject.NetworkObjectId);
                return;
            }

            RequestPickupRpc(playerObject.NetworkObjectId);
        }

        private void LateUpdate()
        {
            if (!IsSpawned)
                return;

            if (IsServer)
            {
                if ((transform.position - m_position.Value).sqrMagnitude >=
                    positionSyncThreshold * positionSyncThreshold)
                {
                    m_position.Value = transform.position;
                    m_positionInitialized.Value = true;
                }

                return;
            }

            if (!m_hasServerPosition)
                return;

            transform.position = Vector3.Lerp(
                transform.position,
                m_position.Value,
                clientFollowSpeed * Time.deltaTime);
        }

        private void ResolveWeaponItem()
        {
            if (weaponItem == null)
                weaponItem = GetComponent<ItemEntity>();

            if (buffItem == null)
                buffItem = GetComponent<BuffItemEntity>();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestPickupRpc(ulong playerNetworkObjectId)
        {
            TryPickupPlayer(playerNetworkObjectId);
        }

        private void TryPickupPlayer(ulong playerNetworkObjectId)
        {
            ResolveWeaponItem();

            if (!IsServer ||
                NetworkManager == null ||
                !NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(
                    playerNetworkObjectId,
                    out NetworkObject playerObject))
            {
                return;
            }

            PlayerEntity player = playerObject.GetComponent<PlayerEntity>();
            if (player == null)
                player = playerObject.GetComponentInChildren<PlayerEntity>();

            if (weaponItem != null && weaponItem.Pickup(player))
                return;

            buffItem?.Pickup(player);
        }

        private void OnWeaponItemIdChanged(
            FixedString32Bytes previousValue,
            FixedString32Bytes newValue)
        {
            if (!IsServer)
                ApplyWeaponItemId(newValue);
        }

        private void OnPositionChanged(
            Vector3 previousValue,
            Vector3 newValue)
        {
            if (IsServer)
                return;

            if (!m_positionInitialized.Value && newValue == default)
                return;

            ApplyServerPosition(newValue);
        }

        private void OnPositionInitializedChanged(
            bool previousValue,
            bool newValue)
        {
            if (IsServer || !newValue)
                return;

            ApplyServerPosition(m_position.Value);
        }

        private void ApplyServerPosition(Vector3 position)
        {
            m_hasServerPosition = true;
            transform.position = position;
        }

        private void ApplyWeaponItemId(FixedString32Bytes itemId)
        {
            ResolveWeaponItem();
            if (weaponItem != null && !itemId.IsEmpty)
                weaponItem.SetItemId(itemId.ToString());
        }
    }
}
