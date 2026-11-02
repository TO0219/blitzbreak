using Unity.Netcode;
using Unity.Collections;
using Unity.Services.Authentication;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MyGame
{
    public class PlayerNetworkController : NetworkBehaviour
    {
        [SerializeField] private PlayerInput playerInput;
        [SerializeField] private PlayerEntity playerEntity;
        [SerializeField] private TPSCameraEntity tpsCameraEntity;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private AudioListener audioListener;
        [SerializeField] private Transform turretTransform;
        [SerializeField, Min(0.02f)] private float legacyFireCooldown = 0.2f;
        [SerializeField, Min(0.5f)] private float airModeAuthorizationGrace = 1f;

        private NetworkVariable<Quaternion> m_turretRotation =
            new NetworkVariable<Quaternion>(
                Quaternion.identity,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Owner
            );

        private NetworkVariable<int> m_score =
            new NetworkVariable<int>(
                0,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server
            );

        private NetworkVariable<float> m_airGauge =
            new NetworkVariable<float>(
                0f,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server
            );

        private NetworkVariable<float> m_health =
            new NetworkVariable<float>(
                0f,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server
            );

        private NetworkVariable<bool> m_isRespawning =
            new NetworkVariable<bool>(
                false,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server
            );

        private NetworkVariable<float> m_respawnRemaining =
            new NetworkVariable<float>(
                0f,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server
            );

        private NetworkVariable<int> m_machineIndex =
            new NetworkVariable<int>(
                0,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server
            );

        private NetworkVariable<Quaternion> m_bodyRotation =
            new NetworkVariable<Quaternion>(
                Quaternion.identity,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Owner
            );

        private NetworkVariable<int> m_airModeStock =
            new NetworkVariable<int>(
                0,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server
            );

        private NetworkVariable<FixedString64Bytes> m_playerName =
            new NetworkVariable<FixedString64Bytes>(
                new FixedString64Bytes("PLAYER"),
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server
            );

        private NetworkVariable<int> m_weaponType =
            new NetworkVariable<int>(
                0,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server
            );

        private NetworkVariable<float> m_weaponRemaining =
            new NetworkVariable<float>(
                0f,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server
            );

        public int NetworkScore => m_score.Value;
        public float NetworkAirGauge => m_airGauge.Value;
        public float NetworkHealth => m_health.Value;
        public float NetworkRespawnRemaining => m_respawnRemaining.Value;
        public int NetworkMachineIndex => m_machineIndex.Value;
        public string NetworkPlayerName => m_playerName.Value.ToString();

        private float m_nextLegacyFireTime;
        private float m_nextWeaponStateSyncTime;
        private int m_authorizedAirStock;
        private float m_authorizedAirModeUntil;
        private const float WeaponStateSyncInterval = 0.1f;

        private void Start()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            {
                RegisterEntities();
                SetLocalControl(true);
                BindCameraToPlayer();
            }
        }

        public override void OnNetworkSpawn()
        {
            RegisterEntities();

            m_score.OnValueChanged += OnScoreChanged;
            m_airGauge.OnValueChanged += OnAirGaugeChanged;
            m_health.OnValueChanged += OnHealthChanged;
            m_isRespawning.OnValueChanged += OnRespawningChanged;
            m_respawnRemaining.OnValueChanged += OnRespawnRemainingChanged;
            m_machineIndex.OnValueChanged += OnMachineIndexChanged;
            m_airModeStock.OnValueChanged += OnAirModeStockChanged;
            m_weaponType.OnValueChanged += OnWeaponTypeChanged;
            m_weaponRemaining.OnValueChanged += OnWeaponRemainingChanged;

            if (IsServer)
            {
                InitializeNetworkStatsFromPlayer();
                InitializeNetworkMachineFromPlayer();
                SyncWeaponStateFromServer(true);
            }

            RequestOwnerMachineSelection();
            RequestOwnerPlayerName();
            ApplyNetworkStatsToPlayer();
            ApplyNetworkMachineToPlayer();
            ApplyNetworkAirModeToPlayer();
            ApplyNetworkWeaponState();
            SetLocalControl(IsOwner);

            if (IsOwner)
            {
                BindCameraToPlayer();
            }
            else
            {
                if (playerEntity != null)
                    playerEntity.SetCameraTransform(null);
            }
        }

        public override void OnNetworkDespawn()
        {
            m_score.OnValueChanged -= OnScoreChanged;
            m_airGauge.OnValueChanged -= OnAirGaugeChanged;
            m_health.OnValueChanged -= OnHealthChanged;
            m_isRespawning.OnValueChanged -= OnRespawningChanged;
            m_respawnRemaining.OnValueChanged -= OnRespawnRemainingChanged;
            m_machineIndex.OnValueChanged -= OnMachineIndexChanged;
            m_airModeStock.OnValueChanged -= OnAirModeStockChanged;
            m_weaponType.OnValueChanged -= OnWeaponTypeChanged;
            m_weaponRemaining.OnValueChanged -= OnWeaponRemainingChanged;
        }

        private void LateUpdate()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
                return;

            if (playerEntity != null && IsOwner)
            {
                m_bodyRotation.Value = playerEntity.NetworkVisualRotation;
            }
            else if (playerEntity != null)
            {
                playerEntity.ApplyNetworkVisualRotation(
                    m_bodyRotation.Value,
                    12f,
                    Time.deltaTime);
            }

            if (turretTransform != null && IsOwner)
            {
                m_turretRotation.Value = turretTransform.rotation;
            }
            else if (turretTransform != null)
            {
                turretTransform.rotation = Quaternion.Slerp(
                    turretTransform.rotation,
                    m_turretRotation.Value,
                    12f * Time.deltaTime
                );
            }

            if (IsServer && m_airModeStock.Value > 0)
            {
                float remaining =
                    Mathf.Max(0f, m_authorizedAirModeUntil - Time.time);
                playerEntity?.ApplyNetworkAirModeState(
                    m_airModeStock.Value,
                    remaining);

                if (remaining <= 0f)
                    EndAuthorizedAirMode();
            }

            if (IsServer && Time.unscaledTime >= m_nextWeaponStateSyncTime)
            {
                m_nextWeaponStateSyncTime =
                    Time.unscaledTime + WeaponStateSyncInterval;
                SyncWeaponStateFromServer(false);
            }
        }

        public bool TryRequestFire(Vector3 position, Quaternion rotation)
        {
            if (!IsSpawned || !IsOwner)
                return false;

            FireServerRpc(position, rotation);
            return true;
        }

        public bool TryRequestProjectileFire(
            NetworkProjectileType projectileType,
            Vector3 position,
            Quaternion rotation,
            ulong targetNetworkObjectId = ulong.MaxValue)
        {
            if (!IsSpawned || !IsOwner)
                return false;

            FireProjectileServerRpc(
                projectileType,
                position,
                rotation,
                targetNetworkObjectId);
            return true;
        }

        public bool TryRequestRailgunFire(Vector3 position, Vector3 direction)
        {
            if (!IsSpawned || !IsOwner)
                return false;

            FireRailgunServerRpc(position, direction);
            return true;
        }

        public bool TryRequestEnvironmentDamage(float damage, Vector3 hitPosition)
        {
            if (!IsSpawned || !IsOwner || damage <= 0f)
                return false;

            RequestEnvironmentDamageServerRpc(damage, hitPosition);
            return true;
        }

        public void SetRespawnPoint(Vector3 position, Quaternion rotation)
        {
            if (playerEntity != null)
                playerEntity.SetRespawnPoint(position, rotation);

            if (IsServer && IsSpawned)
                SetRespawnPointClientRpc(position, rotation);
        }

        public void RespawnOwnerClient(Vector3 position, Quaternion rotation)
        {
            if (!IsServer || !IsSpawned)
                return;

            ClientRpcParams clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { OwnerClientId }
                }
            };

            RespawnOwnerClientRpc(position, rotation, clientRpcParams);
        }

        public bool TryAddScore(int value)
        {
            if (!IsSpawned || !IsServer)
                return false;

            m_score.Value += value;

            if (playerEntity != null)
                playerEntity.ApplyNetworkScore(m_score.Value);

            return true;
        }

        public void SyncHealthFromServer(float value)
        {
            if (!IsSpawned || !IsServer)
                return;

            float maxHealth = playerEntity != null ? playerEntity.GroundHealth : 100f;
            m_health.Value = Mathf.Clamp(value, 0f, maxHealth);

            if (playerEntity != null)
                playerEntity.ApplyNetworkHealth(m_health.Value);
        }

        public void SyncRespawnStateFromServer(
            bool isRespawning,
            float remaining = 0f)
        {
            if (!IsSpawned || !IsServer)
                return;

            m_isRespawning.Value = isRespawning;
            m_respawnRemaining.Value = isRespawning
                ? Mathf.Ceil(Mathf.Max(0f, remaining) * 10f) / 10f
                : 0f;

            if (isRespawning)
            {
                EndAuthorizedAirMode();
            }
        }

        public void SyncAirModeRemainingFromServer(float remaining)
        {
            if (!IsSpawned || !IsServer || m_airModeStock.Value <= 0)
                return;

            float clampedRemaining = Mathf.Max(0f, remaining);
            if (clampedRemaining <= 0f)
            {
                EndAuthorizedAirMode();
                return;
            }

            m_authorizedAirModeUntil =
                Time.time + clampedRemaining;
            playerEntity?.ApplyNetworkAirModeState(
                m_airModeStock.Value,
                clampedRemaining);
            SyncAirModeRemainingClientRpc(
                m_airModeStock.Value,
                clampedRemaining);
        }

        public void SyncBuffStateFromServer()
        {
            if (!IsSpawned || !IsServer || playerEntity == null)
                return;

            BuffManagerEntity buffManager =
                playerEntity.GetComponent<BuffManagerEntity>();
            if (buffManager == null)
                return;

            SyncBuffStateOwnerClientRpc(
                buffManager.GetStack("moveSpeed"),
                buffManager.GetStack("attackPower"),
                buffManager.GetStack("attackRange"),
                buffManager.GetStack("attackSpeed"),
                buffManager.GetStack("durability"),
                CreateOwnerClientRpcParams());
        }

        public bool TrySetAirGauge(float value)
        {
            if (!IsSpawned)
                return false;

            if (IsServer)
            {
                SetNetworkAirGauge(value);
                return true;
            }

            if (IsOwner)
            {
                if (value > m_airGauge.Value)
                    return false;

                SetAirGaugeServerRpc(value);
                return true;
            }

            return false;
        }

        public bool TryConsumeAirGaugeForMode(int stock)
        {
            if (!IsSpawned ||
                playerEntity == null ||
                stock <= 0 ||
                stock > 3)
            {
                return false;
            }

            float consumeValue = playerEntity.GaugePerStock * stock;
            if (m_airGauge.Value + 0.01f < consumeValue)
                return false;

            if (IsServer)
            {
                AuthorizeAirMode(stock);
                return true;
            }

            if (!IsOwner)
                return false;

            playerEntity.ApplyNetworkAirGauge(m_airGauge.Value - consumeValue);
            ActivateAirModeServerRpc(stock);
            return true;
        }

        public bool TryAddAirGauge(float value)
        {
            if (!IsSpawned)
                return false;

            if (IsServer)
            {
                SetNetworkAirGauge(m_airGauge.Value + value);
                return true;
            }

            if (IsOwner)
            {
                if (value >= 0f)
                    return false;

                AddAirGaugeServerRpc(value);
                return true;
            }

            return false;
        }

        public void TryCancelRespawnProtection()
        {
            if (!IsSpawned)
                return;

            if (IsServer)
            {
                if (playerEntity != null)
                    playerEntity.CancelRespawnProtectionFromNetwork();
                return;
            }

            if (IsOwner)
                CancelRespawnProtectionServerRpc();
        }

        public void PlayDamageFeedbackOwnerClient(float intensity, float duration)
        {
            if (!IsServer || !IsSpawned)
                return;

            ClientRpcParams clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { OwnerClientId }
                }
            };

            PlayDamageFeedbackOwnerClientRpc(
                intensity,
                duration,
                clientRpcParams);
        }

        public void PlayHitMarkerOwnerClient(
            bool defeated,
            bool countsForPriorityDefeatFeedback = false)
        {
            if (!IsServer || !IsSpawned)
                return;

            PlayHitMarkerOwnerClientRpc(
                defeated,
                countsForPriorityDefeatFeedback,
                CreateOwnerClientRpcParams());
        }

        public void PlayDamageDirectionOwnerClient(Vector3 damageSourcePosition)
        {
            if (!IsServer || !IsSpawned)
                return;

            PlayDamageDirectionOwnerClientRpc(
                damageSourcePosition,
                CreateOwnerClientRpcParams());
        }

        public void BroadcastDeathExplosion(Vector3 position)
        {
            if (!IsServer || !IsSpawned)
                return;

            PlayDeathExplosionClientRpc(position);
        }

        public void ShowPickupNoticeOwnerClient(string category, string itemName)
        {
            if (!IsServer || !IsSpawned)
                return;

            ShowPickupNoticeOwnerClientRpc(
                category,
                itemName,
                CreateOwnerClientRpcParams());
        }

        private ClientRpcParams CreateOwnerClientRpcParams()
        {
            return new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { OwnerClientId }
                }
            };
        }

        [ServerRpc]
        private void FireServerRpc(Vector3 position, Quaternion rotation, ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId)
                return;

            GameRuleEntity gameRule = FindAnyObjectByType<GameRuleEntity>();
            if (gameRule != null && !gameRule.CanPlay)
                return;

            if (playerEntity == null ||
                playerEntity.IsRespawning ||
                Time.time < m_nextLegacyFireTime)
                return;

            Transform muzzle = playerEntity.MuzzleTransform;
            if (muzzle == null ||
                (position - muzzle.position).sqrMagnitude > 16f ||
                Quaternion.Angle(rotation, muzzle.rotation) > 75f)
            {
                return;
            }

            m_nextLegacyFireTime = Time.time + legacyFireCooldown;
            playerEntity.SpawnNetworkBullet(
                muzzle.position,
                muzzle.rotation,
                OwnerClientId);
        }

        [ServerRpc]
        private void FireProjectileServerRpc(
            NetworkProjectileType projectileType,
            Vector3 position,
            Quaternion rotation,
            ulong targetNetworkObjectId,
            ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId)
                return;

            GameRuleEntity gameRule = FindAnyObjectByType<GameRuleEntity>();
            if (gameRule != null && !gameRule.CanPlay)
                return;

            if (playerEntity == null || playerEntity.IsRespawning)
                return;

            TurretManagerEntity turretManager =
                playerEntity.GetComponent<TurretManagerEntity>();
            if (turretManager == null)
                return;

            int authorizedAirStock =
                Time.time <=
                m_authorizedAirModeUntil + airModeAuthorizationGrace
                ? m_authorizedAirStock
                : 0;
            if (!turretManager.TryAuthorizeNetworkProjectile(
                    projectileType,
                    position,
                    rotation,
                    authorizedAirStock,
                    targetNetworkObjectId,
                    out Vector3 spawnPosition,
                    out Quaternion spawnRotation,
                    out ulong validatedTargetNetworkObjectId))
            {
                return;
            }

            turretManager.SpawnNetworkProjectile(
                projectileType,
                spawnPosition,
                spawnRotation,
                OwnerClientId,
                validatedTargetNetworkObjectId);
        }

        [ServerRpc]
        private void FireRailgunServerRpc(
            Vector3 position,
            Vector3 direction,
            ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId)
                return;

            GameRuleEntity gameRule = FindAnyObjectByType<GameRuleEntity>();
            if (gameRule != null && !gameRule.CanPlay)
                return;

            if (playerEntity == null || playerEntity.IsRespawning)
                return;

            TurretManagerEntity turretManager =
                playerEntity.GetComponent<TurretManagerEntity>();
            if (turretManager == null)
                return;

            if (!turretManager.TryFireRailgunFromNetwork(
                    position,
                    direction,
                    out Vector3 endPoint))
            {
                return;
            }

            PlayRailgunBeamClientRpc(position, endPoint);
        }

        [ServerRpc]
        private void CancelRespawnProtectionServerRpc()
        {
            if (playerEntity != null)
                playerEntity.CancelRespawnProtectionFromNetwork();
        }

        [ServerRpc]
        private void RequestEnvironmentDamageServerRpc(
            float damage,
            Vector3 hitPosition,
            ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId)
                return;

            if (playerEntity == null || playerEntity.IsRespawning)
                return;

            GameRuleEntity gameRule = FindAnyObjectByType<GameRuleEntity>();
            if (gameRule != null && !gameRule.CanPlay)
                return;

            playerEntity.Damage(Mathf.Clamp(damage, 0f, 100f), hitPosition);
        }

        [ClientRpc]
        private void SetRespawnPointClientRpc(Vector3 position, Quaternion rotation)
        {
            if (playerEntity != null)
            {
                playerEntity.SetRespawnPoint(position, rotation);
                if (IsOwner)
                    playerEntity.ApplyInitialNetworkSpawn(position, rotation);
            }
        }

        [ClientRpc]
        private void RespawnOwnerClientRpc(Vector3 position, Quaternion rotation, ClientRpcParams clientRpcParams = default)
        {
            if (!IsOwner)
                return;

            if (playerEntity != null)
                playerEntity.RespawnFromNetwork(position, rotation);
        }

        [ClientRpc]
        private void PlayDamageFeedbackOwnerClientRpc(
            float intensity,
            float duration,
            ClientRpcParams clientRpcParams = default)
        {
            if (IsOwner && playerEntity != null)
                playerEntity.RequestCameraShake(intensity, duration);
        }

        [ClientRpc]
        private void PlayHitMarkerOwnerClientRpc(
            bool defeated,
            bool countsForPriorityDefeatFeedback,
            ClientRpcParams clientRpcParams = default)
        {
            if (IsOwner && playerEntity != null)
            {
                playerEntity.ShowHitMarkerLocal(defeated);
                if (defeated && countsForPriorityDefeatFeedback)
                    playerEntity.RegisterPriorityDefeatFeedback();
            }
        }

        [ClientRpc]
        private void PlayDamageDirectionOwnerClientRpc(
            Vector3 damageSourcePosition,
            ClientRpcParams clientRpcParams = default)
        {
            if (IsOwner && playerEntity != null)
                playerEntity.ShowDamageDirectionLocal(damageSourcePosition);
        }

        [ClientRpc]
        private void PlayRailgunBeamClientRpc(Vector3 start, Vector3 end)
        {
            RailgunBeamEffect.Play(start, end);
        }

        [ClientRpc]
        private void ShowPickupNoticeOwnerClientRpc(
            string category,
            string itemName,
            ClientRpcParams clientRpcParams = default)
        {
            if (IsOwner && playerEntity != null)
                BattleHudUIEntity.ShowPickupNoticeFor(playerEntity, category, itemName);
        }

        [ClientRpc]
        private void PlayDeathExplosionClientRpc(Vector3 position)
        {
            if (IsServer || playerEntity == null)
                return;

            playerEntity.PlayDeathExplosionAt(position);
        }

        [ClientRpc]
        private void SyncAirModeRemainingClientRpc(
            int stock,
            float remaining)
        {
            if (IsServer || playerEntity == null)
                return;

            playerEntity.ApplyNetworkAirModeState(stock, remaining);
        }

        [ClientRpc]
        private void SyncBuffStateOwnerClientRpc(
            int moveSpeed,
            int attackPower,
            int attackRange,
            int attackSpeed,
            int durability,
            ClientRpcParams clientRpcParams = default)
        {
            if (IsServer || playerEntity == null)
                return;

            BuffManagerEntity buffManager =
                playerEntity.GetComponent<BuffManagerEntity>();
            buffManager?.ApplyNetworkStackCounts(
                moveSpeed,
                attackPower,
                attackRange,
                attackSpeed,
                durability);
        }

        [ServerRpc]
        private void SetAirGaugeServerRpc(float value)
        {
            if (value <= m_airGauge.Value)
                SetNetworkAirGauge(value);
        }

        [ServerRpc]
        private void AddAirGaugeServerRpc(float value)
        {
            if (value < 0f)
                SetNetworkAirGauge(m_airGauge.Value + value);
        }

        [ServerRpc]
        private void ActivateAirModeServerRpc(int stock)
        {
            AuthorizeAirMode(stock);
        }

        [ServerRpc]
        private void SetMachineIndexServerRpc(int value)
        {
            SetNetworkMachineIndex(value);
        }

        [ServerRpc]
        private void SetPlayerNameServerRpc(string playerName)
        {
            SetNetworkPlayerName(playerName);
        }

        private void RegisterEntities()
        {
            if (playerEntity != null)
                playerEntity.SetNetworkController(this);

            if (EntityManager.Instance == null)
            {
                Debug.LogError("EntityManager.Instance is null");
                return;
            }

            Entity[] entities = GetComponentsInChildren<Entity>(true);
            for (int i = 0; i < entities.Length; i++)
            {
                EntityManager.Instance.AddEntity(entities[i]);
            }
        }

        private void SetLocalControl(bool isMine)
        {
            if (playerInput != null)
                playerInput.enabled = isMine;

            if (playerEntity != null)
            {
                playerEntity.enabled = true;
                playerEntity.SetLocalControl(isMine);
            }

            if (tpsCameraEntity != null)
            {
                tpsCameraEntity.enabled = true;
                tpsCameraEntity.SetLocalControl(isMine);
            }

            if (playerCamera != null)
            {
                playerCamera.gameObject.SetActive(isMine);
                playerCamera.enabled = isMine;
                playerCamera.targetDisplay = 0;
            }

            if (audioListener != null)
                audioListener.enabled = isMine;
        }

        private void BindCameraToPlayer()
        {
            if (playerEntity == null) return;
            if (playerCamera == null) return;

            playerEntity.SetCameraTransform(playerCamera.transform);
        }

        private void InitializeNetworkStatsFromPlayer()
        {
            if (playerEntity == null)
                return;

            m_score.Value = playerEntity.LocalScore;
            SetNetworkAirGauge(0f);
            SyncHealthFromServer(playerEntity.CurrentHealth);
            SyncRespawnStateFromServer(
                playerEntity.IsRespawning,
                playerEntity.RespawnRemaining);
        }

        private void InitializeNetworkMachineFromPlayer()
        {
            if (playerEntity == null)
                return;

            SetNetworkMachineIndex(playerEntity.MachineIndex);
        }

        private void ApplyNetworkStatsToPlayer()
        {
            if (playerEntity == null)
                return;

            playerEntity.ApplyNetworkScore(m_score.Value);
            playerEntity.ApplyNetworkAirGauge(m_airGauge.Value);
            playerEntity.ApplyNetworkHealth(m_health.Value);
            playerEntity.ApplyNetworkRespawnState(
                m_isRespawning.Value,
                m_respawnRemaining.Value);
        }

        private void ApplyNetworkMachineToPlayer()
        {
            if (playerEntity == null)
                return;

            playerEntity.ApplyMachineIndex(m_machineIndex.Value);
        }

        private void ApplyNetworkAirModeToPlayer()
        {
            if (playerEntity == null)
                return;

            float remaining = m_airModeStock.Value > 0
                ? IsServer
                    ? Mathf.Max(0f, m_authorizedAirModeUntil - Time.time)
                    : playerEntity.GetAirModeDuration(m_airModeStock.Value)
                : 0f;
            playerEntity.ApplyNetworkAirModeState(
                m_airModeStock.Value,
                remaining);
        }

        private void RequestOwnerPlayerName()
        {
            if (!IsOwner)
                return;

            string playerName = ResolveLocalLobbyPlayerName();
            if (IsServer)
            {
                SetNetworkPlayerName(playerName);
                return;
            }

            SetPlayerNameServerRpc(playerName);
        }

        private string ResolveLocalLobbyPlayerName()
        {
            if (LobbyManager.Instance != null &&
                LobbyManager.Instance.CurrentLobby != null &&
                LobbyManager.Instance.CurrentLobby.Players != null &&
                AuthenticationService.Instance.IsSignedIn)
            {
                string localPlayerId = AuthenticationService.Instance.PlayerId;
                var lobbyPlayers = LobbyManager.Instance.CurrentLobby.Players;

                for (int i = 0; i < lobbyPlayers.Count; i++)
                {
                    var lobbyPlayer = lobbyPlayers[i];
                    if (lobbyPlayer == null || lobbyPlayer.Id != localPlayerId)
                        continue;

                    if (lobbyPlayer.Data != null &&
                        lobbyPlayer.Data.TryGetValue(
                            LobbyManager.PlayerNameKey,
                            out var playerNameData) &&
                        playerNameData != null &&
                        !string.IsNullOrWhiteSpace(playerNameData.Value))
                    {
                        return playerNameData.Value;
                    }
                }
            }

            return "P" + (OwnerClientId + 1);
        }

        private void RequestOwnerMachineSelection()
        {
            if (!IsOwner || GameModeManager.Instance == null)
                return;

            int selectedMachineIndex = GameModeManager.Instance.SelectedMachineIndex;

            if (playerEntity != null)
                playerEntity.ApplyMachineIndex(selectedMachineIndex);

            if (IsServer)
            {
                SetNetworkMachineIndex(selectedMachineIndex);
                return;
            }

            SetMachineIndexServerRpc(selectedMachineIndex);
        }

        private void OnScoreChanged(int previousValue, int newValue)
        {
            if (playerEntity != null)
                playerEntity.ApplyNetworkScore(newValue);
        }

        private void OnAirGaugeChanged(float previousValue, float newValue)
        {
            if (playerEntity != null)
                playerEntity.ApplyNetworkAirGauge(newValue);
        }

        private void OnHealthChanged(float previousValue, float newValue)
        {
            if (playerEntity != null)
                playerEntity.ApplyNetworkHealth(newValue);
        }

        private void OnRespawningChanged(bool previousValue, bool newValue)
        {
            if (playerEntity != null)
            {
                playerEntity.ApplyNetworkRespawnState(
                    newValue,
                    m_respawnRemaining.Value);
            }
        }

        private void OnRespawnRemainingChanged(
            float previousValue,
            float newValue)
        {
            if (playerEntity != null)
                playerEntity.ApplyNetworkRespawnRemaining(newValue);
        }

        private void OnMachineIndexChanged(int previousValue, int newValue)
        {
            if (playerEntity != null)
                playerEntity.ApplyMachineIndex(newValue);
        }

        private void OnAirModeStockChanged(int previousValue, int newValue)
        {
            if (playerEntity == null)
                return;

            float remaining = newValue > 0
                ? playerEntity.GetAirModeDuration(newValue)
                : 0f;
            playerEntity.ApplyNetworkAirModeState(newValue, remaining);
        }

        private void OnWeaponTypeChanged(int previousValue, int newValue)
        {
            ApplyNetworkWeaponState();
        }

        private void OnWeaponRemainingChanged(
            float previousValue,
            float newValue)
        {
            ApplyNetworkWeaponState();
        }

        private void ApplyNetworkWeaponState()
        {
            if (playerEntity == null)
                return;

            TurretManagerEntity turretManager =
                playerEntity.GetComponent<TurretManagerEntity>();
            turretManager?.ApplyNetworkWeaponState(
                m_weaponType.Value,
                m_weaponRemaining.Value);
        }

        private void SyncWeaponStateFromServer(bool force)
        {
            if (!IsServer || playerEntity == null)
                return;

            TurretManagerEntity turretManager =
                playerEntity.GetComponent<TurretManagerEntity>();
            if (turretManager == null)
                return;

            int weaponType = turretManager.CurrentWeaponType;
            float remaining = turretManager.TimedWeaponRemaining;

            if (force || m_weaponType.Value != weaponType)
                m_weaponType.Value = weaponType;

            if (force ||
                !Mathf.Approximately(m_weaponRemaining.Value, remaining))
            {
                m_weaponRemaining.Value = remaining;
            }
        }

        private void SetNetworkAirGauge(float value)
        {
            float maxGauge = playerEntity != null ? playerEntity.MaxAirGauge : 300f;
            m_airGauge.Value = Mathf.Clamp(value, 0f, maxGauge);

            if (playerEntity != null)
                playerEntity.ApplyNetworkAirGauge(m_airGauge.Value);
        }

        private void AuthorizeAirMode(int stock)
        {
            if (!IsServer ||
                playerEntity == null ||
                playerEntity.IsRespawning ||
                stock <= 0 ||
                stock > 3)
            {
                return;
            }

            float consumeValue = playerEntity.GaugePerStock * stock;
            if (m_airGauge.Value + 0.01f < consumeValue)
                return;

            SetNetworkAirGauge(m_airGauge.Value - consumeValue);
            m_authorizedAirStock = stock;
            m_authorizedAirModeUntil =
                Time.time +
                playerEntity.GetAirModeDuration(stock);
            m_airModeStock.Value = stock;
            playerEntity.ApplyNetworkAirModeState(
                stock,
                playerEntity.GetAirModeDuration(stock));
        }

        private void EndAuthorizedAirMode()
        {
            m_authorizedAirStock = 0;
            m_authorizedAirModeUntil = 0f;

            if (IsServer && m_airModeStock.Value != 0)
                m_airModeStock.Value = 0;

            playerEntity?.ApplyNetworkAirModeState(0, 0f);
        }

        private void SetNetworkMachineIndex(int value)
        {
            m_machineIndex.Value = MachineProfileCatalog.ClampIndex(value);

            if (playerEntity != null)
                playerEntity.ApplyMachineIndex(m_machineIndex.Value);
        }

        private void SetNetworkPlayerName(string playerName)
        {
            string normalizedName = string.IsNullOrWhiteSpace(playerName)
                ? "P" + (OwnerClientId + 1)
                : playerName.Trim();

            if (normalizedName.Length > 32)
                normalizedName = normalizedName.Substring(0, 32);

            m_playerName.Value = new FixedString64Bytes(normalizedName);
        }
    }
}
