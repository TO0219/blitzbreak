using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

namespace MyGame
{
    public class LobbyManager : MonoBehaviour
    {
        public static LobbyManager Instance { get; private set; }

        public Lobby CurrentLobby { get; private set; }

        public const string PlayerNameKey = "PlayerName";
        public const string MachineIndexKey = "MachineIndex";
        public const string ReadyKey = "Ready";
        public const string StartedKey = "Started";
        public const string RelayJoinCodeKey = "RelayJoinCode";

        private float m_heartbeatTimer = 0f;
        private const float HEARTBEAT_INTERVAL = 15f;
        private readonly SemaphoreSlim m_lobbyRequestLock =
            new SemaphoreSlim(1, 1);
        private ILobbyEvents m_lobbyEvents;
        private float m_lastLobbyEventTime;
        private float m_nextFallbackRefreshTime;
        private float m_nextEventSubscriptionRetryTime;
        private const float EVENT_FRESHNESS_SECONDS = 5f;
        private const float FALLBACK_REFRESH_INTERVAL = 3f;
        private const float EVENT_RETRY_INTERVAL = 10f;

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

        private async void Update()
        {
            if (CurrentLobby == null) return;

            if (CurrentLobby.HostId != AuthenticationService.Instance.PlayerId)
                return;

            m_heartbeatTimer -= Time.deltaTime;
            if (m_heartbeatTimer <= 0f)
            {
                m_heartbeatTimer = HEARTBEAT_INTERVAL;
                await SendHeartbeatAsync();
            }
        }

        public async System.Threading.Tasks.Task<Lobby> CreateLobbyAsync(string lobbyName, string playerName, int maxPlayers = 4)
        {
            if (CurrentLobby != null)
            {
                await LeaveLobbyAsync();
            }

            try
            {
                CreateLobbyOptions options = new CreateLobbyOptions
                {
                    IsPrivate = false,
                    Data = new Dictionary<string, DataObject>
                    {
                        {
                            StartedKey,
                            new DataObject(
                                DataObject.VisibilityOptions.Public,
                                "0"
                            )
                        }
                    },
                    Player = new Player
                    {
                        Data = new Dictionary<string, PlayerDataObject>
                        {
                            {
                                PlayerNameKey,
                                new PlayerDataObject(
                                    PlayerDataObject.VisibilityOptions.Public,
                                    playerName
                                )
                            },
                            {
                                MachineIndexKey,
                                new PlayerDataObject(
                                    PlayerDataObject.VisibilityOptions.Public,
                                    "0"
                                )
                            },
                            {
                                ReadyKey,
                                new PlayerDataObject(
                                    PlayerDataObject.VisibilityOptions.Public,
                                    "0"
                                )
                            }
                        }
                    }
                };

                CurrentLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, options);
                await SubscribeToCurrentLobbyEventsAsync();

                m_heartbeatTimer = HEARTBEAT_INTERVAL;

                return CurrentLobby;
            }
            catch (LobbyServiceException e)
            {
                Debug.LogError("CreateLobbyAsync Failed");
                Debug.LogError(e);
                return null;
            }
        }

        public async System.Threading.Tasks.Task<Lobby> JoinLobbyByIdAsync(string lobbyId, string playerName)
        {
            if (CurrentLobby != null)
            {
                await LeaveLobbyAsync();
            }

            try
            {
                JoinLobbyByIdOptions options = new JoinLobbyByIdOptions
                {
                    Player = new Player
                    {
                        Data = new Dictionary<string, PlayerDataObject>
                        {
                            {
                                PlayerNameKey,
                                new PlayerDataObject(
                                    PlayerDataObject.VisibilityOptions.Public,
                                    playerName
                                )
                            },
                            {
                                MachineIndexKey,
                                new PlayerDataObject(
                                    PlayerDataObject.VisibilityOptions.Public,
                                    "0"
                                )
                            },
                            {
                                ReadyKey,
                                new PlayerDataObject(
                                    PlayerDataObject.VisibilityOptions.Public,
                                    "0"
                                )
                            }
                        }
                    }
                };

                CurrentLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId, options);
                await SubscribeToCurrentLobbyEventsAsync();

                return CurrentLobby;
            }
            catch (LobbyServiceException e)
            {
                Debug.LogError("JoinLobbyByIdAsync Failed");
                Debug.LogError(e);
                return null;
            }
        }

        public async System.Threading.Tasks.Task<List<Lobby>> QueryLobbiesAsync()
        {
            try
            {
                QueryLobbiesOptions options = new QueryLobbiesOptions
                {
                    Count = 20
                };

                QueryResponse response = await LobbyService.Instance.QueryLobbiesAsync(options);
                return response.Results;
            }
            catch (LobbyServiceException e)
            {
                Debug.LogError("QueryLobbiesAsync Failed");
                Debug.LogError(e);
                return null;
            }
        }

        public async Task<Lobby> RefreshCurrentLobbyAsync()
        {
            Lobby lobby = CurrentLobby;
            if (lobby == null || string.IsNullOrEmpty(lobby.Id))
                return null;

            float now = Time.realtimeSinceStartup;
            bool eventConnectionIsFresh =
                m_lobbyEvents != null &&
                now - m_lastLobbyEventTime < EVENT_FRESHNESS_SECONDS;
            if (eventConnectionIsFresh || now < m_nextFallbackRefreshTime)
                return CurrentLobby;

            string lobbyId = lobby.Id;
            m_nextFallbackRefreshTime = now + FALLBACK_REFRESH_INTERVAL;
            await m_lobbyRequestLock.WaitAsync();

            try
            {
                if (CurrentLobby == null || CurrentLobby.Id != lobbyId)
                    return CurrentLobby;

                Lobby refreshedLobby =
                    await LobbyService.Instance.GetLobbyAsync(lobbyId);
                if (refreshedLobby != null &&
                    CurrentLobby != null &&
                    CurrentLobby.Id == lobbyId)
                {
                    CurrentLobby = refreshedLobby;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning(
                    "RefreshCurrentLobbyAsync Failed: " +
                    e.GetType().Name +
                    " - " +
                    e.Message);
            }
            finally
            {
                m_lobbyRequestLock.Release();
            }

            now = Time.realtimeSinceStartup;
            if (CurrentLobby != null &&
                CurrentLobby.Id == lobbyId &&
                m_lobbyEvents == null &&
                now >= m_nextEventSubscriptionRetryTime)
            {
                m_nextEventSubscriptionRetryTime =
                    now + EVENT_RETRY_INTERVAL;
                await SubscribeToCurrentLobbyEventsAsync();
            }

            return CurrentLobby;
        }

        public async System.Threading.Tasks.Task LeaveLobbyAsync()
        {
            Lobby lobby = CurrentLobby;
            if (lobby == null) return;

            CurrentLobby = null;
            m_lastLobbyEventTime = 0f;
            m_nextFallbackRefreshTime = 0f;
            m_nextEventSubscriptionRetryTime = 0f;
            await UnsubscribeLobbyEventsAsync();

            try
            {
                string playerId = AuthenticationService.Instance.PlayerId;
                await LobbyService.Instance.RemovePlayerAsync(lobby.Id, playerId);
            }
            catch (System.Exception e)
            {
                Debug.LogError("LeaveLobbyAsync Failed");
                Debug.LogError(e);
            }
            finally
            {
                CurrentLobby = null;
            }
        }

        private async Task SubscribeToCurrentLobbyEventsAsync()
        {
            Lobby lobby = CurrentLobby;
            if (lobby == null || string.IsNullOrEmpty(lobby.Id))
                return;

            await UnsubscribeLobbyEventsAsync();

            string lobbyId = lobby.Id;
            LobbyEventCallbacks callbacks = new LobbyEventCallbacks();
            callbacks.LobbyChanged += changes =>
            {
                if (CurrentLobby == null || CurrentLobby.Id != lobbyId)
                    return;

                if (changes.LobbyDeleted)
                {
                    CurrentLobby = null;
                    return;
                }

                if (changes.Version.Changed &&
                    changes.Version.Value <= CurrentLobby.Version)
                {
                    return;
                }

                changes.ApplyToLobby(CurrentLobby);
                m_lastLobbyEventTime = Time.realtimeSinceStartup;
            };
            callbacks.KickedFromLobby += () =>
            {
                if (CurrentLobby != null && CurrentLobby.Id == lobbyId)
                    CurrentLobby = null;
            };

            try
            {
                m_lobbyEvents =
                    await LobbyService.Instance.SubscribeToLobbyEventsAsync(
                        lobbyId,
                        callbacks);

                if (m_lobbyEvents == null)
                    Debug.LogWarning("Lobby event subscription is unavailable.");
                else
                    m_lastLobbyEventTime = Time.realtimeSinceStartup;
            }
            catch (System.Exception e)
            {
                m_lobbyEvents = null;
                Debug.LogWarning(
                    "Lobby event subscription failed: " +
                    e.GetType().Name +
                    " - " +
                    e.Message);
            }
        }

        private async Task UnsubscribeLobbyEventsAsync()
        {
            ILobbyEvents lobbyEvents = m_lobbyEvents;
            m_lobbyEvents = null;

            if (lobbyEvents == null)
                return;

            try
            {
                await lobbyEvents.UnsubscribeAsync();
            }
            catch (System.Exception)
            {
                // The subscription may already be closed by the service.
            }
        }

        private async System.Threading.Tasks.Task SendHeartbeatAsync()
        {
            if (CurrentLobby == null) return;

            try
            {
                await LobbyService.Instance.SendHeartbeatPingAsync(CurrentLobby.Id);
            }
            catch (LobbyServiceException e)
            {
                Debug.LogError("Heartbeat Failed");
                Debug.LogError(e);
            }
        }

        public async System.Threading.Tasks.Task<bool> SetLobbyStartedAsync(bool started)
        {
            if (CurrentLobby == null) return false;

            try
            {
                var lobbyData = new Dictionary<string, DataObject>
                {
                    {
                        StartedKey,
                        new DataObject(
                            visibility: DataObject.VisibilityOptions.Public,
                            value: started ? "1" : "0"
                        )
                    }
                };

                if (!started)
                {
                    lobbyData[RelayJoinCodeKey] =
                        new DataObject(
                            visibility: DataObject.VisibilityOptions.Public,
                            value: string.Empty
                        );
                }

                var options = new UpdateLobbyOptions
                {
                    Data = lobbyData
                };

                CurrentLobby = await LobbyService.Instance.UpdateLobbyAsync(CurrentLobby.Id, options);
                return true;
            }
            catch (LobbyServiceException e)
            {
                Debug.LogError("SetLobbyStartedAsync Failed");
                Debug.LogError(e);
                return false;
            }
        }

        public async System.Threading.Tasks.Task<bool> SetRelayJoinCodeAsync(string joinCode)
        {
            if (CurrentLobby == null) return false;

            try
            {
                var options = new UpdateLobbyOptions
                {
                    Data = new Dictionary<string, DataObject>
                    {
                        {
                            RelayJoinCodeKey,
                            new DataObject(
                                visibility: DataObject.VisibilityOptions.Public,
                                value: joinCode
                            )
                        }
                    }
                };

                CurrentLobby = await LobbyService.Instance.UpdateLobbyAsync(CurrentLobby.Id, options);
                return true;
            }
            catch (LobbyServiceException e)
            {
                Debug.LogError("SetRelayJoinCodeAsync Failed");
                Debug.LogError(e);
                return false;
            }
        }

        public async Task<Lobby> UpdateLocalPlayerLoadoutAsync(int machineIndex, bool isReady)
        {
            Lobby lobby = CurrentLobby;
            if (lobby == null || string.IsNullOrEmpty(lobby.Id))
                return null;

            string lobbyId = lobby.Id;
            string playerId = AuthenticationService.Instance.PlayerId;
            await m_lobbyRequestLock.WaitAsync();

            try
            {
                var options = new UpdatePlayerOptions
                {
                    Data = new Dictionary<string, PlayerDataObject>
            {
                {
                    MachineIndexKey,
                    new PlayerDataObject(
                        PlayerDataObject.VisibilityOptions.Public,
                        machineIndex.ToString()
                    )
                },
                {
                    ReadyKey,
                    new PlayerDataObject(
                        PlayerDataObject.VisibilityOptions.Public,
                        isReady ? "1" : "0"
                    )
                }
            }
                };

                Lobby updatedLobby =
                    await LobbyService.Instance.UpdatePlayerAsync(
                        lobbyId,
                        playerId,
                        options);

                if (CurrentLobby != null &&
                    CurrentLobby.Id == lobbyId &&
                    updatedLobby != null)
                {
                    CurrentLobby = updatedLobby;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("UpdateLocalPlayerLoadoutAsync UpdatePlayer Failed");
                Debug.LogError(e);
                return CurrentLobby;
            }
            finally
            {
                m_lobbyRequestLock.Release();
            }

            if (CurrentLobby == null || CurrentLobby.Id != lobbyId)
                return null;

            return CurrentLobby;
        }
    }
}
