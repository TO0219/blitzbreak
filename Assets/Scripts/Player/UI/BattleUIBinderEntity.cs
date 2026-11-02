using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

namespace MyGame
{
    public class BattleUIBinderEntity : Entity
    {
        [SerializeField] private PlayerEntity playerEntity;
        [SerializeField] private GameRuleEntity gameRuleEntity;

        [Header("UI")]
        [SerializeField] private PlayerHealthUIEntity playerHealthUI;
        [SerializeField] private AirGaugeUIEntity airGaugeUI;
        [SerializeField] private AirTimeUIEntity airTimeUI;
        [SerializeField] private ScoreUIEntity scoreUI;
        [SerializeField] private TimerUIEntity timerUI;
        [SerializeField] private TurretReticleUIEntity turretReticleUI;
        [SerializeField] private CountdownUIEntity countdownUI;
        [SerializeField] private BattleHudUIEntity battleHudUI;
        [SerializeField] private bool useUnifiedBattleHud = true;
        [SerializeField] private bool hideLegacyBattleWidgets = true;

        [Header("Countdown Camera")]
        [SerializeField] private bool useCountdownCameraShots = true;
        [SerializeField] private Vector3 countdownShot3Offset = new Vector3(0f, 2.2f, 5.5f);
        [SerializeField] private Vector3 countdownShot2Offset = new Vector3(-4f, 2.4f, -4.5f);
        [SerializeField] private Vector3 countdownShot1Offset = new Vector3(0f, 2.6f, -5.5f);
        [SerializeField] private float countdownLookHeight = 1.2f;
        [SerializeField] private float countdownCameraLerp = 10f;
        [Header("Defeat Feedback")]
        [SerializeField, Range(0f, 1f)] private float defeatShakeIntensity = 1f;
        [SerializeField] private float defeatShakeDuration = 0.55f;

        private TPSCameraEntity m_tpsCameraEntity;
        private Camera m_countdownCamera;
        private TurretManagerEntity m_turretManagerEntity;
        private BuffManagerEntity m_buffManagerEntity;
        private bool m_legacyWidgetsHidden;
        private readonly List<PlayerEntity> m_participantPlayers =
            new List<PlayerEntity>(4);
        private readonly List<EnemyCPUEntity> m_participantCpus =
            new List<EnemyCPUEntity>(4);
        private float m_nextParticipantRefreshTime;
        private int m_cachedParticipantCount = 4;
        private int m_cachedLocalRank = 1;
        private int m_observedPriorityDefeatFeedbackCount;
        private bool m_hasObservedPriorityDefeatFeedbackCount;
        private int m_observedNoGaugeFeedbackCount;
        private bool m_hasObservedNoGaugeFeedbackCount;
        private const float ParticipantRefreshInterval = 0.25f;

        public override void InitEntity()
        {
            if (playerEntity == null)
                FindPlayerEntity();

            if (gameRuleEntity == null)
                FindGameRuleEntity();

            ResolveCountdownUI();
            ResolveBattleHudUI();
            ResolveTurretReticleUI();
            ApplyLegacyWidgetVisibility();
        }

        public override void UpdateEntity()
        {
            if (playerEntity == null)
                FindPlayerEntity();

            if (gameRuleEntity == null)
                FindGameRuleEntity();

            ResolveCountdownUI();
            ResolveBattleHudUI();
            ResolveTurretReticleUI();
            ApplyLegacyWidgetVisibility();

            if (playerEntity != null)
            {
                ApplyMatchControlGate();
                UpdateDefeatFeedback();
                UpdateNoGaugeFeedback();

                if (playerHealthUI != null)
                {
                    if (playerEntity.IsAirMode)
                    {
                        playerHealthUI.SetHealth(playerEntity.CurrentHealth, playerEntity.AirHealth);
                    }
                    else
                    {
                        playerHealthUI.SetHealth(playerEntity.CurrentHealth, playerEntity.GroundHealth);
                    }
                }

                if (airGaugeUI != null)
                    airGaugeUI.SetGauge(playerEntity.CurrentAirGauge, playerEntity.MaxAirGauge);

                if (airTimeUI != null)
                {
                    float rate = 0f;
                    if (playerEntity.CurrentActivatedAirModeMaxTime > 0f)
                    {
                        rate = playerEntity.CurrentAirModeTime / playerEntity.CurrentActivatedAirModeMaxTime;
                    }

                    airTimeUI.SetRate(rate);
                }

                if (scoreUI != null)
                    scoreUI.SetScore(playerEntity.Score);

                UpdateUnifiedHud();

                if (turretReticleUI != null)
                    turretReticleUI.UpdateReticle(playerEntity);
            }
            else if (turretReticleUI != null)
            {
                turretReticleUI.SetVisible(false);
            }

            if (gameRuleEntity != null && timerUI != null)
            {
                timerUI.SetTime(gameRuleEntity.TimeRemaining);
            }

            if (countdownUI != null && playerEntity != null && playerEntity.IsRespawning)
            {
                countdownUI.SetRespawnCountdown(
                    playerEntity.RespawnRemaining,
                    true,
                    gameRuleEntity != null && gameRuleEntity.IsFinished
                );
            }
            else if (gameRuleEntity != null && countdownUI != null)
            {
                countdownUI.SetCountdown(
                    gameRuleEntity.StartCountdownRemaining,
                    gameRuleEntity.IsMatchStarted,
                    gameRuleEntity.IsFinished
                );
                countdownUI.SetMatchTimeWarning(
                    gameRuleEntity.TimeRemaining,
                    gameRuleEntity.IsMatchStarted,
                    gameRuleEntity.IsFinished
                );
            }

            if (gameRuleEntity != null)
                ApplyCountdownCameraShot();
        }

        public void SetPlayerEntity(PlayerEntity player)
        {
            playerEntity = player;
            m_tpsCameraEntity = null;
            m_turretManagerEntity = null;
            m_buffManagerEntity = null;
            m_nextParticipantRefreshTime = 0f;
            m_hasObservedPriorityDefeatFeedbackCount = false;
            m_hasObservedNoGaugeFeedbackCount = false;
            ResetCachedCamera();
        }

        public void SetGameRuleEntity(GameRuleEntity gameRule)
        {
            gameRuleEntity = gameRule;
        }

        private void FindPlayerEntity()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkObject localPlayerObject = NetworkManager.Singleton.LocalClient.PlayerObject;
                if (localPlayerObject != null)
                {
                    PlayerEntity localPlayer = localPlayerObject.GetComponent<PlayerEntity>();
                    if (localPlayer != null)
                    {
                        playerEntity = localPlayer;
                        m_tpsCameraEntity = null;
                        m_turretManagerEntity = null;
                        m_buffManagerEntity = null;
                        m_hasObservedPriorityDefeatFeedbackCount = false;
                        m_hasObservedNoGaugeFeedbackCount = false;
                        ResetCachedCamera();
                        return;
                    }
                }
            }

            PlayerEntity[] players = FindObjectsByType<PlayerEntity>();

            for (int i = 0; i < players.Length; i++)
            {
                NetworkObject netObj = players[i].GetComponent<NetworkObject>();

                if (netObj == null)
                {
                    playerEntity = players[i];
                    m_tpsCameraEntity = null;
                    m_turretManagerEntity = null;
                    m_buffManagerEntity = null;
                    m_hasObservedPriorityDefeatFeedbackCount = false;
                    m_hasObservedNoGaugeFeedbackCount = false;
                    ResetCachedCamera();
                    return;
                }

                if (!netObj.IsSpawned)
                {
                    playerEntity = players[i];
                    m_tpsCameraEntity = null;
                    m_turretManagerEntity = null;
                    m_buffManagerEntity = null;
                    m_hasObservedPriorityDefeatFeedbackCount = false;
                    m_hasObservedNoGaugeFeedbackCount = false;
                    ResetCachedCamera();
                    return;
                }

                if (netObj.IsOwner)
                {
                    playerEntity = players[i];
                    m_tpsCameraEntity = null;
                    m_turretManagerEntity = null;
                    m_buffManagerEntity = null;
                    m_hasObservedPriorityDefeatFeedbackCount = false;
                    m_hasObservedNoGaugeFeedbackCount = false;
                    ResetCachedCamera();
                    return;
                }
            }
        }

        private void FindGameRuleEntity()
        {
            gameRuleEntity = FindAnyObjectByType<GameRuleEntity>();
        }

        private void ApplyMatchControlGate()
        {
            if (gameRuleEntity == null || playerEntity == null) return;

            bool canControl =
                gameRuleEntity.CanPlay &&
                !BattlePauseUIController.IsPaused &&
                !playerEntity.IsRespawning;
            playerEntity.SetLocalControl(canControl);

            if (m_tpsCameraEntity == null)
                m_tpsCameraEntity = playerEntity.GetComponentInChildren<TPSCameraEntity>(true);

            if (m_tpsCameraEntity != null)
                m_tpsCameraEntity.SetLocalControl(canControl);
        }

        private void ApplyCountdownCameraShot()
        {
            if (!useCountdownCameraShots || playerEntity == null)
            {
                return;
            }

            if (gameRuleEntity.IsMatchStarted || gameRuleEntity.IsFinished)
            {
                return;
            }

            Camera targetCamera = FindLocalCamera();
            if (targetCamera == null) return;

            m_countdownCamera = targetCamera;

            int countdownNumber = Mathf.Clamp(
                Mathf.CeilToInt(gameRuleEntity.StartCountdownRemaining),
                1,
                3
            );

            Vector3 localOffset = GetCountdownCameraOffset(countdownNumber);
            Transform playerTransform = playerEntity.transform;
            Vector3 targetPosition =
                playerTransform.position +
                playerTransform.right * localOffset.x +
                Vector3.up * localOffset.y +
                playerTransform.forward * localOffset.z;

            Vector3 lookTarget = playerTransform.position + Vector3.up * countdownLookHeight;
            Vector3 lookDirection = lookTarget - targetPosition;
            if (lookDirection.sqrMagnitude <= 0.001f)
                lookDirection = playerTransform.forward;

            Quaternion targetRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
            float t = 1f - Mathf.Exp(-countdownCameraLerp * Time.deltaTime);

            targetCamera.transform.position = Vector3.Lerp(
                targetCamera.transform.position,
                targetPosition,
                t
            );
            targetCamera.transform.rotation = Quaternion.Slerp(
                targetCamera.transform.rotation,
                targetRotation,
                t
            );
        }

        private Vector3 GetCountdownCameraOffset(int countdownNumber)
        {
            if (countdownNumber >= 3) return countdownShot3Offset;
            if (countdownNumber == 2) return countdownShot2Offset;
            return countdownShot1Offset;
        }

        private Camera FindLocalCamera()
        {
            if (m_countdownCamera != null)
                return m_countdownCamera;

            if (playerEntity != null)
            {
                Camera playerCamera = playerEntity.GetComponentInChildren<Camera>(true);
                if (playerCamera != null)
                    return playerCamera;
            }

            return Camera.main;
        }

        private void ResetCachedCamera()
        {
            m_countdownCamera = null;
        }

        private void ResolveBattleHudUI()
        {
            if (!useUnifiedBattleHud) return;
            if (battleHudUI != null) return;

            battleHudUI = FindAnyObjectByType<BattleHudUIEntity>();
        }

        private void UpdateUnifiedHud()
        {
            if (!useUnifiedBattleHud || battleHudUI == null || playerEntity == null) return;

            RefreshParticipantSnapshot();
            float timeRemaining = gameRuleEntity != null ? gameRuleEntity.TimeRemaining : 0f;

            float maxHp = playerEntity.IsAirMode ? playerEntity.AirHealth : playerEntity.GroundHealth;
            battleHudUI.SetCore(
                timeRemaining,
                playerEntity.Score,
                m_cachedLocalRank,
                m_cachedParticipantCount);
            battleHudUI.SetVitals(
                playerEntity.CurrentHealth,
                maxHp,
                playerEntity.CurrentAirGauge,
                playerEntity.MaxAirGauge,
                playerEntity.IsAirMode
            );
            battleHudUI.SetAirTime(
                playerEntity.CurrentAirModeTime,
                playerEntity.CurrentActivatedAirModeMaxTime,
                playerEntity.IsAirMode
            );
            battleHudUI.SetCombat(GetWeaponLabel(), GetWeaponTimeLabel(), HasLockTarget());
            battleHudUI.SetBuffs(GetBuffManager());
        }

        private void UpdateDefeatFeedback()
        {
            if (playerEntity == null)
                return;

            int feedbackCount = playerEntity.PriorityDefeatFeedbackCount;
            if (!m_hasObservedPriorityDefeatFeedbackCount)
            {
                m_observedPriorityDefeatFeedbackCount = feedbackCount;
                m_hasObservedPriorityDefeatFeedbackCount = true;
                return;
            }

            if (feedbackCount <= m_observedPriorityDefeatFeedbackCount)
            {
                m_observedPriorityDefeatFeedbackCount = feedbackCount;
                return;
            }

            int gainedDefeats = feedbackCount - m_observedPriorityDefeatFeedbackCount;
            m_observedPriorityDefeatFeedbackCount = feedbackCount;

            if (battleHudUI != null)
                battleHudUI.ShowDefeatNotice(gainedDefeats);

            playerEntity.RequestCameraShake(defeatShakeIntensity, defeatShakeDuration);
        }

        private void UpdateNoGaugeFeedback()
        {
            if (playerEntity == null)
                return;

            int feedbackCount = playerEntity.NoGaugeFeedbackCount;
            if (!m_hasObservedNoGaugeFeedbackCount)
            {
                m_observedNoGaugeFeedbackCount = feedbackCount;
                m_hasObservedNoGaugeFeedbackCount = true;
                return;
            }

            if (feedbackCount <= m_observedNoGaugeFeedbackCount)
            {
                m_observedNoGaugeFeedbackCount = feedbackCount;
                return;
            }

            m_observedNoGaugeFeedbackCount = feedbackCount;

            if (battleHudUI != null)
                battleHudUI.ShowNoGaugeNotice();
        }

        private void RefreshParticipantSnapshot()
        {
            if (Time.unscaledTime < m_nextParticipantRefreshTime)
                return;

            m_nextParticipantRefreshTime =
                Time.unscaledTime + ParticipantRefreshInterval;
            m_participantPlayers.Clear();
            m_participantCpus.Clear();

            if (EntityManager.Instance != null)
            {
                EntityManager.Instance.GetEntities(m_participantPlayers);
                EntityManager.Instance.GetEntities(m_participantCpus);
            }
            else
            {
                PlayerEntity[] players = FindObjectsByType<PlayerEntity>();
                for (int i = 0; i < players.Length; i++)
                    m_participantPlayers.Add(players[i]);

                EnemyCPUEntity[] cpus = FindObjectsByType<EnemyCPUEntity>();
                for (int i = 0; i < cpus.Length; i++)
                    m_participantCpus.Add(cpus[i]);
            }

            int participantCount = 0;
            int localScore = playerEntity.Score;
            int rank = 1;

            for (int i = 0; i < m_participantPlayers.Count; i++)
            {
                PlayerEntity participant = m_participantPlayers[i];
                if (participant == null)
                    continue;

                participantCount++;
                if (participant != playerEntity &&
                    participant.Score > localScore)
                {
                    rank++;
                }
            }

            for (int i = 0; i < m_participantCpus.Count; i++)
            {
                EnemyCPUEntity cpu = m_participantCpus[i];
                if (cpu == null)
                    continue;

                participantCount++;
                if (cpu.Score > localScore)
                    rank++;
            }

            m_cachedParticipantCount =
                Mathf.Clamp(Mathf.Max(participantCount, 4), 1, 4);
            m_cachedLocalRank =
                Mathf.Clamp(rank, 1, m_cachedParticipantCount);
        }

        private string GetWeaponLabel()
        {
            if (playerEntity == null) return "なし";

            if (m_turretManagerEntity == null)
                m_turretManagerEntity = playerEntity.GetComponent<TurretManagerEntity>();

            if (m_turretManagerEntity == null)
                return "通常砲";

            return m_turretManagerEntity.CurrentTurretLabel;
        }

        private string GetWeaponTimeLabel()
        {
            if (playerEntity == null) return "--";

            if (m_turretManagerEntity == null)
                m_turretManagerEntity = playerEntity.GetComponent<TurretManagerEntity>();

            if (m_turretManagerEntity == null || !m_turretManagerEntity.HasTimedWeapon)
                return "--";

            return Mathf.CeilToInt(m_turretManagerEntity.TimedWeaponRemaining).ToString() + "秒";
        }

        private bool HasLockTarget()
        {
            if (playerEntity == null)
                return false;

            if (m_turretManagerEntity == null)
                m_turretManagerEntity = playerEntity.GetComponent<TurretManagerEntity>();

            return m_turretManagerEntity != null &&
                   m_turretManagerEntity.TryGetMissileLockTarget(out _);
        }

        private BuffManagerEntity GetBuffManager()
        {
            if (playerEntity == null) return null;

            if (m_buffManagerEntity == null)
                m_buffManagerEntity = playerEntity.GetComponent<BuffManagerEntity>();

            return m_buffManagerEntity;
        }

        private bool TryGetTurretAimRay(out Ray ray)
        {
            if (playerEntity == null)
            {
                ray = default;
                return false;
            }

            if (m_turretManagerEntity == null)
                m_turretManagerEntity = playerEntity.GetComponent<TurretManagerEntity>();

            if (playerEntity.TryGetAirAimRay(out ray))
                return true;

            if (m_turretManagerEntity != null && m_turretManagerEntity.TryGetAimRay(out ray))
                return true;

            if (playerEntity.MuzzleTransform != null)
            {
                ray = new Ray(playerEntity.MuzzleTransform.position, playerEntity.MuzzleTransform.forward);
                return true;
            }

            ray = default;
            return false;
        }

        private void ApplyLegacyWidgetVisibility()
        {
            if (!useUnifiedBattleHud || !hideLegacyBattleWidgets || m_legacyWidgetsHidden) return;
            if (battleHudUI == null) return;

            SetWidgetActive(playerHealthUI, false);
            SetWidgetActive(airGaugeUI, false);
            SetWidgetActive(airTimeUI, false);
            SetWidgetActive(scoreUI, false);
            SetWidgetActive(timerUI, false);

            m_legacyWidgetsHidden = true;
        }

        private void SetWidgetActive(UnityEngine.Component widget, bool isActive)
        {
            if (widget == null) return;
            widget.gameObject.SetActive(isActive);
        }

        private void ResolveCountdownUI()
        {
            if (countdownUI != null) return;

            countdownUI = FindAnyObjectByType<CountdownUIEntity>();
        }

        private void ResolveTurretReticleUI()
        {
            if (turretReticleUI != null) return;

            turretReticleUI = FindAnyObjectByType<TurretReticleUIEntity>();
        }
    }
}
