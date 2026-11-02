using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Unity.Services.Authentication;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

namespace MyGame
{
    public class LobbyUIController : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private TMP_Text roomCodeValueText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private GameObject startMatchButton;

        [Header("Room List")]
        [SerializeField] private Transform roomListContent;
        [SerializeField] private GameObject roomListItemPrefab;

        [Header("Player List")]
        [SerializeField] private Transform playerListContent;
        [SerializeField] private GameObject playerListItemPrefab;

        [Header("Panels")]
        [SerializeField] private GameObject lobbyBrowserPanel;
        [SerializeField] private GameObject currentRoomPanel;

        [Header("Player Name")]
        [SerializeField] private TMP_InputField playerNameInputField;
        [SerializeField] private TMP_InputField roomNameInputField;

        [Header("Scene Names")]
        [SerializeField] private string vehicleSelectSceneName = "VehicleSelectScene";
        [SerializeField] private string titleSceneName = "TitleScene";

        private bool m_isHost = false;
        private bool m_isReturningToTitle;
        private bool m_matchStartRequested;
        private bool m_isHandlingLobbyLoss;
        private Coroutine m_pollCoroutine;

        private void Start()
        {
            SetRoomCode("----");
            SetStatus("参加するルームを選択してください");
            SetHostUI(false);
            ShowBrowserPanel();
            OnClickRefresh();
        }

        public async void OnClickCreateRoom()
        {
            if (LobbyManager.Instance == null)
            {
                SetStatus("LobbyManager がありません");
                return;
            }

            SetStatus("部屋を作成中...");

            await PrepareForLobbyChangeAsync();

            string playerName = GetPlayerName();
            string roomName = GetRoomName(playerName);

            Lobby lobby = await LobbyManager.Instance.CreateLobbyAsync(roomName, playerName, 4);

            if (lobby != null)
            {
                m_isHost = true;
                SetRoomCode(lobby.LobbyCode);
                SetStatus("部屋を作成しました");
                SetHostUI(true);
                RefreshPlayerList(lobby);
                StartPollingLobby();
                ShowCurrentRoomPanel();
            }
            else
            {
                m_isHost = false;
                SetRoomCode("----");
                SetStatus("部屋作成に失敗しました");
                SetHostUI(false);
            }
        }

        public async void OnClickRefresh()
        {
            if (LobbyManager.Instance == null)
            {
                SetStatus("LobbyManager がありません");
                return;
            }

            SetStatus("部屋一覧を更新中...");

            List<Lobby> lobbies = await LobbyManager.Instance.QueryLobbiesAsync();

            ClearRoomList();

            if (lobbies == null)
            {
                SetStatus("部屋一覧の取得に失敗しました");
                return;
            }

            for (int i = 0; i < lobbies.Count; i++)
            {
                Lobby lobby = lobbies[i];

                if (roomListItemPrefab == null || roomListContent == null)
                    continue;

                GameObject itemObj = Instantiate(roomListItemPrefab, roomListContent);
                RoomListItemUI itemUI = itemObj.GetComponent<RoomListItemUI>();

                if (itemUI != null)
                {
                    itemUI.Setup(
                        lobby.Id,
                        lobby.Name,
                        GetLobbyPlayerCount(lobby),
                        lobby.MaxPlayers,
                        this
                    );
                }
            }

            SetStatus(lobbies.Count > 0
                ? $"{lobbies.Count}件のルームが見つかりました"
                : "参加可能なルームはありません。新しいルームを作成できます");
        }

        private static int GetLobbyPlayerCount(Lobby lobby)
        {
            if (lobby == null)
                return 0;

            int maxPlayers = Mathf.Max(1, lobby.MaxPlayers);
            int availableSlots = Mathf.Clamp(lobby.AvailableSlots, 0, maxPlayers);
            int countFromSlots = maxPlayers - availableSlots;
            int countFromPlayers = lobby.Players != null ? lobby.Players.Count : 0;

            return Mathf.Clamp(
                Mathf.Max(countFromSlots, countFromPlayers),
                0,
                maxPlayers);
        }

        public async void JoinLobbyById(string lobbyId)
        {
            if (LobbyManager.Instance == null)
            {
                SetStatus("LobbyManager がありません");
                return;
            }

            SetStatus("部屋に参加中...");

            await PrepareForLobbyChangeAsync();

            string playerName = GetPlayerName();
            Lobby lobby = await LobbyManager.Instance.JoinLobbyByIdAsync(lobbyId, playerName);

            if (lobby != null)
            {
                string myPlayerId = AuthenticationService.Instance.PlayerId;
                m_isHost = lobby.HostId == myPlayerId;

                SetRoomCode(lobby.LobbyCode);
                SetStatus("部屋に参加しました");
                SetHostUI(m_isHost);
                RefreshPlayerList(lobby);
                StartPollingLobby();
                ShowCurrentRoomPanel();
            }
            else
            {
                SetStatus("部屋参加に失敗しました");
            }
        }

        public async void OnClickLeaveRoom()
        {
            StopPollingLobby();

            if (LobbyManager.Instance != null)
            {
                await LobbyManager.Instance.LeaveLobbyAsync();
            }

            if (RelayConnectionManager.Instance != null)
            {
                await RelayConnectionManager.Instance.ShutdownIfRunningAsync();
            }
            else if (NetworkManager.Singleton != null &&
                     NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }

            m_isHost = false;
            SetRoomCode("----");
            SetStatus("部屋を出ました");
            SetHostUI(false);
            ClearPlayerList();
            ShowBrowserPanel();
        }

        public void OnGamepadCancel()
        {
            if (currentRoomPanel != null && currentRoomPanel.activeSelf)
            {
                OnClickLeaveRoom();
                return;
            }

            OnClickBackToTitle();
        }

        public async void OnClickBackToTitle()
        {
            if (m_isReturningToTitle)
                return;

            m_isReturningToTitle = true;
            StopPollingLobby();

            if (LobbyManager.Instance != null)
            {
                await LobbyManager.Instance.LeaveLobbyAsync();
            }

            if (RelayConnectionManager.Instance != null)
            {
                await RelayConnectionManager.Instance.ShutdownIfRunningAsync();
            }
            else if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }

            SceneManager.LoadScene(titleSceneName, LoadSceneMode.Single);
        }

        public async void OnClickStartMatch()
        {
            if (!m_isHost) return;
            if (m_matchStartRequested) return;
            if (LobbyManager.Instance == null) return;
            if (RelayConnectionManager.Instance == null) return;
            if (NetworkManager.Singleton == null) return;

            m_matchStartRequested = true;
            SetStatus("Relay 準備中...");

            try
            {
                string joinCode = await RelayConnectionManager.Instance.StartHostWithRelayAsync(3);
                if (string.IsNullOrEmpty(joinCode))
                {
                    SetStatus("Host 起動に失敗しました");
                    await ResetFailedMatchStartAsync();
                    return;
                }

                bool codeSet = await LobbyManager.Instance.SetRelayJoinCodeAsync(joinCode);
                if (!codeSet)
                {
                    SetStatus("JoinCode 保存に失敗しました");
                    await ResetFailedMatchStartAsync();
                    return;
                }

                Lobby latestLobby = await LobbyManager.Instance.RefreshCurrentLobbyAsync();
                Lobby currentLobby = latestLobby ?? LobbyManager.Instance.CurrentLobby;
                int expectedPlayerCount = currentLobby?.Players?.Count ?? 1;

                SetStatus("参加プレイヤーの接続を待っています...");
                bool allPlayersConnected = await WaitForLobbyPlayersConnectedAsync(expectedPlayerCount, 20f);
                if (!allPlayersConnected)
                {
                    SetStatus("参加プレイヤーの接続を確認できませんでした。もう一度開始してください。");
                    await ResetFailedMatchStartAsync();
                    return;
                }

                bool started = await LobbyManager.Instance.SetLobbyStartedAsync(true);
                if (!started)
                {
                    SetStatus("試合開始フラグ設定に失敗しました");
                    await ResetFailedMatchStartAsync();
                    return;
                }

                if (NetworkManager.Singleton.SceneManager != null)
                {
                    SceneEventProgressStatus status = NetworkManager.Singleton.SceneManager.LoadScene(
                        vehicleSelectSceneName,
                        UnityEngine.SceneManagement.LoadSceneMode.Single
                    );

                    if (status != SceneEventProgressStatus.Started)
                    {
                        SetStatus("ネットワークシーン読み込みに失敗しました: " + status);
                        Debug.LogWarning("VehicleSelectScene network load failed: " + status);
                        await ResetFailedMatchStartAsync();
                    }
                }
                else
                {
                    SetStatus("ネットワークシーン管理の準備ができていません。");
                    Debug.LogWarning("NetworkManager.SceneManager is null. VehicleSelectScene was not loaded locally.");
                    await ResetFailedMatchStartAsync();
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("試合開始処理に失敗しました: " + exception.Message);
                SetStatus("試合開始に失敗しました。もう一度お試しください。");
                await ResetFailedMatchStartAsync();
            }
        }

        private async System.Threading.Tasks.Task ResetFailedMatchStartAsync()
        {
            try
            {
                if (m_isHost &&
                    LobbyManager.Instance != null &&
                    LobbyManager.Instance.CurrentLobby != null)
                {
                    await LobbyManager.Instance.SetLobbyStartedAsync(false);
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("試合開始フラグの復旧に失敗しました: " + exception.Message);
            }

            try
            {
                if (RelayConnectionManager.Instance != null)
                {
                    await RelayConnectionManager.Instance.ShutdownIfRunningAsync();
                }
                else if (NetworkManager.Singleton != null &&
                         NetworkManager.Singleton.IsListening)
                {
                    NetworkManager.Singleton.Shutdown();
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("ネットワーク停止処理に失敗しました: " + exception.Message);
            }
            finally
            {
                m_matchStartRequested = false;
            }
        }

        private static async System.Threading.Tasks.Task<bool> WaitForLobbyPlayersConnectedAsync(
            int expectedPlayerCount,
            float timeoutSeconds)
        {
            int expectedCount = Mathf.Max(1, expectedPlayerCount);
            int timeoutMs = Mathf.CeilToInt(timeoutSeconds * 1000f);
            const int intervalMs = 100;
            int elapsedMs = 0;

            while (NetworkManager.Singleton != null &&
                   NetworkManager.Singleton.IsListening &&
                   elapsedMs < timeoutMs)
            {
                if (NetworkManager.Singleton.ConnectedClientsIds.Count >= expectedCount)
                {
                    return true;
                }

                await System.Threading.Tasks.Task.Delay(intervalMs);
                elapsedMs += intervalMs;
            }

            return NetworkManager.Singleton != null &&
                   NetworkManager.Singleton.IsListening &&
                   NetworkManager.Singleton.ConnectedClientsIds.Count >= expectedCount;
        }

        private void StartPollingLobby()
        {
            StopPollingLobby();
            m_pollCoroutine = StartCoroutine(PollLobbyCoroutine());
        }

        private async System.Threading.Tasks.Task PrepareForLobbyChangeAsync()
        {
            StopPollingLobby();

            if (RelayConnectionManager.Instance != null)
            {
                await RelayConnectionManager.Instance.ShutdownIfRunningAsync();
            }
            else if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
                await System.Threading.Tasks.Task.Delay(250);
            }

            m_isHost = false;
            SetHostUI(false);
        }

        private void StopPollingLobby()
        {
            if (m_pollCoroutine != null)
            {
                StopCoroutine(m_pollCoroutine);
                m_pollCoroutine = null;
            }
        }

        private IEnumerator PollLobbyCoroutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(2f);

                if (LobbyManager.Instance == null)
                {
                    SetStatus("LobbyManager がありません");
                    continue;
                }

                if (LobbyManager.Instance.CurrentLobby == null)
                {
                    yield return HandleLobbyLostCoroutine("ロビーから切断されました。");
                    yield break;
                }

                var task = LobbyManager.Instance.RefreshCurrentLobbyAsync();
                yield return new WaitUntil(() => task.IsCompleted);

                if (task.IsFaulted)
                {
                    SetStatus("ロビー情報の更新に失敗しました。再試行します...");
                    continue;
                }

                Lobby lobby = task.Result;
                if (lobby != null)
                {
                    string myPlayerId = AuthenticationService.Instance.PlayerId;
                    m_isHost = lobby.HostId == myPlayerId;
                    SetHostUI(m_isHost);
                    RefreshPlayerList(lobby);

                    bool relayConnected = NetworkManager.Singleton != null &&
                                          NetworkManager.Singleton.IsListening &&
                                          NetworkManager.Singleton.IsConnectedClient;
                    bool lobbyStarted = IsLobbyStarted(lobby);

                    if (!m_isHost && !relayConnected)
                    {
                        string joinCode = GetRelayJoinCode(lobby);
                        if (!string.IsNullOrEmpty(joinCode) && RelayConnectionManager.Instance != null)
                        {
                            SetStatus("ホストへ接続しています...");
                            var connectTask = RelayConnectionManager.Instance.StartClientWithRelayAsync(joinCode);
                            yield return new WaitUntil(() => connectTask.IsCompleted);

                            relayConnected = !connectTask.IsFaulted && connectTask.Result;
                            if (!relayConnected)
                            {
                                SetStatus("ホストへの接続に失敗しました。再接続します...");
                                yield return new WaitForSeconds(1f);
                                continue;
                            }

                            SetStatus("ほかのプレイヤーを待っています...");
                        }
                    }

                    if (lobbyStarted)
                    {
                        if (m_isHost)
                        {
                            m_pollCoroutine = null;
                            yield break;
                        }

                        if (!relayConnected)
                        {
                            SetStatus("ホストへの接続を待っています...");
                            continue;
                        }

                        yield return WaitForSceneSyncCoroutine(vehicleSelectSceneName, 15f);
                        if (SceneManager.GetActiveScene().name == vehicleSelectSceneName)
                        {
                            m_pollCoroutine = null;
                            yield break;
                        }

                        SetStatus("機体選択画面の同期を待っています...");
                        Debug.LogWarning("VehicleSelectScene was not synchronized by Netcode yet.");
                    }
                }
                else
                {
                    yield return HandleLobbyLostCoroutine("ロビーが終了しました。");
                    yield break;
                }
            }
        }

        private IEnumerator HandleLobbyLostCoroutine(string message)
        {
            if (m_isHandlingLobbyLoss)
                yield break;

            m_isHandlingLobbyLoss = true;
            m_matchStartRequested = false;
            m_isHost = false;
            SetHostUI(false);

            if (RelayConnectionManager.Instance != null)
            {
                var shutdownTask = RelayConnectionManager.Instance.ShutdownIfRunningAsync();
                yield return new WaitUntil(() => shutdownTask.IsCompleted);
            }
            else if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }

            SetRoomCode("----");
            ClearPlayerList();
            ShowBrowserPanel();
            SetStatus(message);
            m_isHandlingLobbyLoss = false;
        }

        private IEnumerator WaitForSceneSyncCoroutine(string sceneName, float timeoutSeconds)
        {
            float elapsed = 0f;
            while (SceneManager.GetActiveScene().name != sceneName && elapsed < timeoutSeconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private void RefreshPlayerList(Lobby lobby)
        {
            ClearPlayerList();

            if (lobby == null || playerListContent == null || playerListItemPrefab == null)
                return;

            for (int i = 0; i < lobby.Players.Count; i++)
            {
                Player player = lobby.Players[i];

                string playerName = "Player";
                if (player.Data != null && player.Data.ContainsKey(LobbyManager.PlayerNameKey))
                {
                    playerName = player.Data[LobbyManager.PlayerNameKey].Value;
                }
                else
                {
                    playerName = "Player_" + (i + 1);
                }

                bool isHost = player.Id == lobby.HostId;

                GameObject itemObj = Instantiate(playerListItemPrefab, playerListContent);
                PlayerListItemUI itemUI = itemObj.GetComponent<PlayerListItemUI>();

                if (itemUI != null)
                {
                    itemUI.Setup(playerName, isHost);
                }
            }
        }

        private void ClearRoomList()
        {
            if (roomListContent == null) return;

            for (int i = roomListContent.childCount - 1; i >= 0; i--)
            {
                Destroy(roomListContent.GetChild(i).gameObject);
            }
        }

        private void ClearPlayerList()
        {
            if (playerListContent == null) return;

            for (int i = playerListContent.childCount - 1; i >= 0; i--)
            {
                Destroy(playerListContent.GetChild(i).gameObject);
            }
        }

        private void SetRoomCode(string code)
        {
            if (roomCodeValueText != null)
                roomCodeValueText.text = code;
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
                statusText.text = message;
        }

        private void SetHostUI(bool isHost)
        {
            if (startMatchButton != null)
                startMatchButton.SetActive(isHost);
        }

        private void ShowBrowserPanel()
        {
            if (lobbyBrowserPanel != null)
                lobbyBrowserPanel.SetActive(true);

            if (currentRoomPanel != null)
                currentRoomPanel.SetActive(false);

            SelectFirstInteractable(lobbyBrowserPanel);
        }

        private void ShowCurrentRoomPanel()
        {
            if (lobbyBrowserPanel != null)
                lobbyBrowserPanel.SetActive(false);

            if (currentRoomPanel != null)
                currentRoomPanel.SetActive(true);

            SelectFirstInteractable(currentRoomPanel);
        }

        private static void SelectFirstInteractable(GameObject root)
        {
            if (root == null || EventSystem.current == null)
                return;

            Selectable[] selectables = root.GetComponentsInChildren<Selectable>(false);
            ConfigureAutomaticNavigation(selectables);
            for (int i = 0; i < selectables.Length; i++)
            {
                if (selectables[i] == null || !selectables[i].IsInteractable())
                    continue;

                EventSystem.current.SetSelectedGameObject(selectables[i].gameObject);
                return;
            }
        }

        private static void ConfigureAutomaticNavigation(Selectable[] selectables)
        {
            if (selectables == null)
                return;

            for (int i = 0; i < selectables.Length; i++)
            {
                if (selectables[i] == null)
                    continue;

                Navigation navigation = selectables[i].navigation;
                navigation.mode = Navigation.Mode.Automatic;
                selectables[i].navigation = navigation;
            }
        }

        private string GetPlayerName()
        {
            if (playerNameInputField == null)
                return "Player";

            string nameText = playerNameInputField.text.Trim();

            if (string.IsNullOrEmpty(nameText))
                return "Player";

            return nameText;
        }

        private string GetRoomName(string playerName)
        {
            if (roomNameInputField != null)
            {
                string roomNameText = roomNameInputField.text.Trim();
                if (!string.IsNullOrEmpty(roomNameText))
                    return roomNameText;
            }

            string ownerName = string.IsNullOrWhiteSpace(playerName)
                ? "プレイヤー"
                : playerName.Trim();

            return ownerName + "の部屋";
        }

        private bool IsLobbyStarted(Lobby lobby)
        {
            if (lobby == null || lobby.Data == null) return false;
            if (!lobby.Data.ContainsKey(LobbyManager.StartedKey)) return false;

            return lobby.Data[LobbyManager.StartedKey].Value == "1";
        }

        private string GetRelayJoinCode(Lobby lobby)
        {
            if (lobby == null || lobby.Data == null) return string.Empty;
            if (!lobby.Data.ContainsKey(LobbyManager.RelayJoinCodeKey)) return string.Empty;

            return lobby.Data[LobbyManager.RelayJoinCodeKey].Value;
        }
    }
}
