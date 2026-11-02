using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MyGame
{
    public class GameRuleEntity : NetworkBehaviour
    {
        [SerializeField] private float matchTime = 180f;
        [SerializeField] private float startCountdownTime = 3f;
        [SerializeField] private int finalCountdownStartSeconds = 10;
        [SerializeField] private string resultSceneName = "ResultScene";
        [SerializeField] private float resultLoadDelay = 1f;

        private NetworkVariable<float> m_timeRemaining = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        private NetworkVariable<bool> m_isFinished = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        private NetworkVariable<float> m_startCountdownRemaining = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        private NetworkVariable<bool> m_isMatchStarted = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        private float m_localTimeRemaining;
        private bool m_localIsFinished;
        private float m_localStartCountdownRemaining;
        private bool m_localIsMatchStarted;

        public float TimeRemaining => UseNetworkState ? m_timeRemaining.Value : m_localTimeRemaining;
        public bool IsFinished => UseNetworkState ? m_isFinished.Value : m_localIsFinished;
        public float StartCountdownRemaining => UseNetworkState ? m_startCountdownRemaining.Value : m_localStartCountdownRemaining;
        public bool IsMatchStarted => UseNetworkState ? m_isMatchStarted.Value : m_localIsMatchStarted;
        public bool CanPlay => IsMatchStarted && !IsFinished;

        private bool m_resultHandled;
        private int m_lastCountdownSecond = -1;
        private bool m_hasPlayedFinalCountdownSound;
        private bool m_hasPlayedStartCountdownSound;

        private bool HasActiveNetworkSession =>
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        private bool UseNetworkState => IsSpawned && HasActiveNetworkSession;

        private float StateTimeRemaining
        {
            get => UseNetworkState ? m_timeRemaining.Value : m_localTimeRemaining;
            set
            {
                if (UseNetworkState)
                    m_timeRemaining.Value = value;
                else
                    m_localTimeRemaining = value;
            }
        }

        private bool StateIsFinished
        {
            get => UseNetworkState ? m_isFinished.Value : m_localIsFinished;
            set
            {
                if (UseNetworkState)
                    m_isFinished.Value = value;
                else
                    m_localIsFinished = value;
            }
        }

        private float StateStartCountdownRemaining
        {
            get => UseNetworkState ? m_startCountdownRemaining.Value : m_localStartCountdownRemaining;
            set
            {
                if (UseNetworkState)
                    m_startCountdownRemaining.Value = value;
                else
                    m_localStartCountdownRemaining = value;
            }
        }

        private bool StateIsMatchStarted
        {
            get => UseNetworkState ? m_isMatchStarted.Value : m_localIsMatchStarted;
            set
            {
                if (UseNetworkState)
                    m_isMatchStarted.Value = value;
                else
                    m_localIsMatchStarted = value;
            }
        }

        private void Awake()
        {
            BattleResultData.Clear();
            InitializeLocalMatchState();
        }

        private void Start()
        {
            // ソロ用
            if (!HasActiveNetworkSession)
            {
                InitializeLocalMatchState();
            }
        }

        public override void OnNetworkSpawn()
        {
            // ホストだけ初期化
            if (IsServer)
            {
                InitializeNetworkMatchState();
            }
        }

        private void Update()
        {
            // ソロ
            if (!HasActiveNetworkSession)
            {
                UpdateTimer();
                return;
            }

            // マルチはホストだけ進める
            if (!IsSpawned) return;
            if (!IsServer) return;

            UpdateTimer();
        }

        private void UpdateTimer()
        {
            if (StateIsFinished) return;

            if (!StateIsMatchStarted)
            {
                UpdateStartCountdown();
                return;
            }

            StateTimeRemaining -= Time.deltaTime;
            TryPlayFinalCountdownSound();

            if (StateTimeRemaining <= 0f)
            {
                StateTimeRemaining = 0f;
                StateIsFinished = true;
                HandleMatchFinished();
            }
        }

        private void InitializeLocalMatchState()
        {
            m_localTimeRemaining = matchTime;
            m_localStartCountdownRemaining = Mathf.Max(0f, startCountdownTime);
            m_localIsMatchStarted = m_localStartCountdownRemaining <= 0f;
            m_localIsFinished = false;
            m_resultHandled = false;
            m_lastCountdownSecond = -1;
            m_hasPlayedFinalCountdownSound = false;
            m_hasPlayedStartCountdownSound = false;
        }

        private void InitializeNetworkMatchState()
        {
            m_timeRemaining.Value = matchTime;
            m_startCountdownRemaining.Value = Mathf.Max(0f, startCountdownTime);
            m_isMatchStarted.Value = m_startCountdownRemaining.Value <= 0f;
            m_isFinished.Value = false;
            m_resultHandled = false;
            m_lastCountdownSecond = -1;
            m_hasPlayedFinalCountdownSound = false;
            m_hasPlayedStartCountdownSound = false;
        }

        private void UpdateStartCountdown()
        {
            if (StateStartCountdownRemaining <= 0f)
            {
                StartMatch();
                return;
            }

            int countdownSecond = Mathf.CeilToInt(StateStartCountdownRemaining);
            if (countdownSecond != m_lastCountdownSecond && countdownSecond > 0)
            {
                m_lastCountdownSecond = countdownSecond;
                if (!m_hasPlayedStartCountdownSound)
                {
                    m_hasPlayedStartCountdownSound = true;
                    global::AudioManager.Instance?.PlayCue(global::AudioCue.Countdown);
                }
            }

            StateStartCountdownRemaining -= Time.deltaTime;

            if (StateStartCountdownRemaining <= 0f)
            {
                StateStartCountdownRemaining = 0f;
                StartMatch();
            }
        }

        private void TryPlayFinalCountdownSound()
        {
            if (!StateIsMatchStarted || StateIsFinished)
                return;

            int startSeconds = Mathf.Max(0, finalCountdownStartSeconds);
            if (m_hasPlayedFinalCountdownSound ||
                startSeconds <= 0 ||
                StateTimeRemaining <= 0f ||
                StateTimeRemaining > startSeconds)
            {
                return;
            }

            m_hasPlayedFinalCountdownSound = true;
            if (UseNetworkState)
            {
                PlayFinalCountdownSoundClientRpc();
                return;
            }

            PlayFinalCountdownSound();
        }

        [ClientRpc]
        private void PlayFinalCountdownSoundClientRpc()
        {
            PlayFinalCountdownSound();
        }

        private static void PlayFinalCountdownSound()
        {
            global::AudioManager.Instance?.PlayCue(global::AudioCue.TimeUp);
        }

        private void StartMatch()
        {
            if (StateIsMatchStarted) return;

            StateStartCountdownRemaining = 0f;
            StateIsMatchStarted = true;
        }

        private void HandleMatchFinished()
        {
            if (m_resultHandled) return;
            m_resultHandled = true;
            if (!m_hasPlayedFinalCountdownSound)
            {
                global::AudioManager.Instance?.PlayCue(global::AudioCue.TimeUp);
            }

            bool isOnline = IsOnlineMatch();
            if (isOnline && IsServer)
                ResetLobbyStartedState();

            List<BattleResultEntry> results = CollectResults(isOnline);
            BattleResultData.SetResults(results, isOnline);

            if (isOnline)
            {
                PublishResultsClientRpc(BattleResultData.Serialize(results), isOnline);
                StartCoroutine(LoadResultNetworkSceneAfterDelay());
                return;
            }

            StartCoroutine(LoadResultLocalAfterDelay());
        }

        private async void ResetLobbyStartedState()
        {
            if (LobbyManager.Instance == null ||
                LobbyManager.Instance.CurrentLobby == null)
            {
                return;
            }

            bool updated =
                await LobbyManager.Instance.SetLobbyStartedAsync(false);
            if (!updated)
                Debug.LogWarning("試合終了後のロビー状態を戻せませんでした。");
        }

        [ClientRpc]
        private void PublishResultsClientRpc(string payload, bool wasOnline)
        {
            BattleResultData.DeserializeAndSet(payload, wasOnline);
        }

        private IEnumerator LoadResultLocalAfterDelay()
        {
            yield return new WaitForSeconds(resultLoadDelay);
            SceneLoader.Load(resultSceneName);
        }

        private IEnumerator LoadResultNetworkSceneAfterDelay()
        {
            yield return new WaitForSeconds(resultLoadDelay);

            if (NetworkManager.Singleton == null ||
                !NetworkManager.Singleton.IsServer ||
                NetworkManager.Singleton.SceneManager == null)
            {
                yield break;
            }

            SceneLoader.LoadNetworkScene(resultSceneName);
        }

        private List<BattleResultEntry> CollectResults(bool isOnline)
        {
            List<BattleResultEntry> results = isOnline
                ? CollectNetworkResults()
                : CollectLocalResults();

            return results;
        }

        private List<BattleResultEntry> CollectNetworkResults()
        {
            List<BattleResultEntry> results = new List<BattleResultEntry>(4);

            if (NetworkManager.Singleton == null)
            {
                return results;
            }

            for (int i = 0; i < NetworkManager.Singleton.ConnectedClientsIds.Count; i++)
            {
                ulong clientId = NetworkManager.Singleton.ConnectedClientsIds[i];
                int score = 0;
                int defeatedEnemies = 0;
                float damageTaken = 0f;
                int deathCount = 0;
                int machineIndex = 0;
                string playerName = "P" + (i + 1);

                if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out NetworkClient client) &&
                    client.PlayerObject != null)
                {
                    PlayerNetworkController networkController =
                        client.PlayerObject.GetComponent<PlayerNetworkController>();

                    if (networkController != null)
                    {
                        score = networkController.NetworkScore;
                        machineIndex = networkController.NetworkMachineIndex;
                        if (!string.IsNullOrWhiteSpace(networkController.NetworkPlayerName))
                            playerName = networkController.NetworkPlayerName;
                    }

                    PlayerEntity player = client.PlayerObject.GetComponent<PlayerEntity>();
                    if (player != null)
                    {
                        if (networkController == null)
                            score = player.Score;

                        defeatedEnemies = player.DefeatedEnemies;
                        damageTaken = player.DamageTaken;
                        deathCount = player.DeathCount;
                        machineIndex = player.MachineIndex;
                    }
                }

                results.Add(new BattleResultEntry(
                    playerName,
                    score,
                    clientId,
                    false,
                    machineIndex,
                    defeatedEnemies,
                    damageTaken,
                    deathCount
                ));
            }

            EnemyCPUEntity[] cpus = FindObjectsByType<EnemyCPUEntity>();
            System.Array.Sort(
                cpus,
                (a, b) => string.CompareOrdinal(
                    a != null ? a.gameObject.name : string.Empty,
                    b != null ? b.gameObject.name : string.Empty));

            for (int i = 0; i < cpus.Length; i++)
            {
                EnemyCPUEntity cpu = cpus[i];
                if (cpu == null)
                    continue;

                results.Add(new BattleResultEntry(
                    "CPU " + (i + 1),
                    cpu.Score,
                    (ulong)(1000 + i),
                    true,
                    cpu.MachineIndex,
                    cpu.DefeatedEnemies,
                    cpu.DamageTaken,
                    cpu.DeathCount));
            }

            return results;
        }

        private List<BattleResultEntry> CollectLocalResults()
        {
            List<BattleResultEntry> results = new List<BattleResultEntry>(4);
            PlayerEntity[] players = FindObjectsByType<PlayerEntity>();

            for (int i = 0; i < players.Length; i++)
            {
                PlayerEntity player = players[i];
                results.Add(new BattleResultEntry(
                    "P" + (i + 1),
                    player.Score,
                    (ulong)i,
                    false,
                    player.MachineIndex,
                    player.DefeatedEnemies,
                    player.DamageTaken,
                    player.DeathCount));
            }

            EnemyCPUEntity[] cpus = FindObjectsByType<EnemyCPUEntity>();
            for (int i = 0; i < cpus.Length; i++)
            {
                EnemyCPUEntity cpu = cpus[i];
                results.Add(new BattleResultEntry(
                    "CPU " + (i + 1),
                    cpu.Score,
                    (ulong)(1000 + i),
                    true,
                    cpu.MachineIndex,
                    cpu.DefeatedEnemies,
                    cpu.DamageTaken,
                    cpu.DeathCount));
            }

            return results;
        }

        private bool IsOnlineMatch()
        {
            return NetworkManager.Singleton != null &&
                   NetworkManager.Singleton.IsListening &&
                   IsServer;
        }
    }
}
