using System.Collections;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MyGame
{
    public class VehicleSelectUIController : MonoBehaviour
    {
        [SerializeField] private string battleSceneName = "BattleScene";
        [SerializeField] private string titleSceneName = "TitleScene";
        [SerializeField] private string lobbySceneName = "LobbyScene";
        [SerializeField, Range(0f, 1f)] private float submitSoundWaitTime = 0.18f;

        [Header("Vehicle Preview")]
        [SerializeField] private GameObject previewVehiclePrefab;
        [SerializeField] private float previewSpinSpeed = 56f;
        [SerializeField] private float previewReadySpinDuration = 0.45f;
        [SerializeField] private float p1P3RightDiagonalYaw = 150f;
        [SerializeField] private float p2P4LeftDiagonalYaw = 210f;
        [SerializeField] private Vector3 previewCameraPosition = new Vector3(0.45f, 3.7f, -7.2f);
        [SerializeField] private Vector3 previewCameraEulerAngles = new Vector3(22f, 0f, 0f);
        [SerializeField] private float previewCameraSize = 4.6f;
        [SerializeField] private float previewVehicleVerticalOffset = -0.18f;
        [SerializeField] private float machineChangeInputRepeatDelay = 0.22f;

        private readonly PlayerSlotView[] m_slotViews = new PlayerSlotView[4];
        private readonly VehiclePreviewSlot[] m_previewSlots = new VehiclePreviewSlot[4];

        private int m_selectedMachineIndex;
        private bool m_isReady;
        private bool m_isHost;
        private bool m_onlineLoadoutInitialized;
        private bool m_battleLoadRequested;
        private bool m_clientConnectStarted;
        private bool m_returningToLobby;
        private bool m_submitSceneLoadRequested;
        private bool m_isUpdatingLocalReady;
        private int m_localPreviewSlotIndex;
        private Coroutine m_pollCoroutine;
        private Lobby m_latestLobby;

        private Text m_statusText;
        private Button m_previousButton;
        private Button m_nextButton;
        private Button m_readyButton;
        private Button m_startBattleButton;
        private Transform m_previewRoot;
        private readonly Camera[] m_previewCameras = new Camera[4];
        private readonly RenderTexture[] m_previewTextures = new RenderTexture[4];
        private float m_nextMachineChangeInputTime;

        private void Start()
        {
            EnsureEventSystem();
            BindSceneUi();
            EnsureVehiclePreview();
            ConfigureAutomaticNavigation(FindObjectsByType<Selectable>(FindObjectsInactive.Exclude));
            SelectButton(m_readyButton != null ? m_readyButton : m_nextButton);

            if (GameModeManager.Instance != null)
            {
                m_selectedMachineIndex = Mathf.Clamp(
                    GameModeManager.Instance.SelectedMachineIndex,
                    0,
                    MachineProfileCatalog.Count - 1
                );
            }

            UpdateMachinePreview();

            if (IsOnlineMode())
            {
                SetStatus("ロビー参加者を待っています...");
                m_pollCoroutine = StartCoroutine(PollOnlineLobbyCoroutine());
            }
            else
            {
                RefreshSoloSlots();
                SetStatus("機体を選択してください。");
            }
        }

        private void OnDestroy()
        {
            if (m_pollCoroutine != null)
            {
                StopCoroutine(m_pollCoroutine);
                m_pollCoroutine = null;
            }

            for (int i = 0; i < m_previewTextures.Length; i++)
            {
                if (m_previewTextures[i] == null)
                    continue;

                m_previewTextures[i].Release();
                Destroy(m_previewTextures[i]);
                m_previewTextures[i] = null;
            }
        }

        private void Update()
        {
            HandleMachineChangeInput();
            RotatePreviewVehicles();
        }

        public void OnClickPreviousMachine()
        {
            global::AudioManager.Instance?.PlayCue(global::AudioCue.UiSelect);
            SetSelectedMachine(m_selectedMachineIndex - 1);
        }

        public void OnClickNextMachine()
        {
            global::AudioManager.Instance?.PlayCue(global::AudioCue.UiSelect);
            SetSelectedMachine(m_selectedMachineIndex + 1);
        }

        public void OnClickReady()
        {
            if (!IsOnlineMode() && m_submitSceneLoadRequested)
                return;

            global::AudioManager.Instance?.PlayCue(global::AudioCue.UiSubmit);
            if (GameModeManager.Instance != null)
            {
                GameModeManager.Instance.SetSelectedMachineIndex(m_selectedMachineIndex);
            }

            if (!IsOnlineMode())
            {
                StartCoroutine(LoadBattleAfterSubmitSound());
                return;
            }

            StartCoroutine(SetOnlineReadyCoroutine(!m_isReady));
        }

        public void OnClickStartBattle()
        {
            if (!IsOnlineMode()) return;
            if (!m_isHost) return;
            if (m_battleLoadRequested) return;

            global::AudioManager.Instance?.PlayCue(global::AudioCue.UiSubmit);
            if (!AreAllPlayersReady(m_latestLobby))
            {
                SetStatus("全員の選択確定を待っています。");
                return;
            }

            if (!AreAllLobbyPlayersConnected(m_latestLobby))
            {
                SetStatus("プレイヤーの通信接続を待っています。");
                return;
            }

            m_battleLoadRequested = true;
            StartCoroutine(StartBattleAfterSubmitSoundCoroutine());
        }

        public void OnClickBack()
        {
            if (m_submitSceneLoadRequested)
                return;

            global::AudioManager.Instance?.PlayCue(global::AudioCue.UiSubmit);

            if (IsOnlineMode())
            {
                StartCoroutine(LoadSceneAfterSubmitSound(lobbySceneName));
                return;
            }

            StartCoroutine(LoadSceneAfterSubmitSound(titleSceneName));
        }

        private IEnumerator LoadBattleAfterSubmitSound()
        {
            if (m_submitSceneLoadRequested)
                yield break;

            m_submitSceneLoadRequested = true;
            yield return WaitForSubmitSound(submitSoundWaitTime);
            SceneLoader.Load(battleSceneName);
        }

        private IEnumerator StartBattleAfterSubmitSoundCoroutine()
        {
            yield return WaitForSubmitSound(submitSoundWaitTime);
            yield return StartBattleCoroutine();
        }

        private IEnumerator LoadSceneAfterSubmitSound(string sceneName)
        {
            if (m_submitSceneLoadRequested)
                yield break;

            m_submitSceneLoadRequested = true;
            yield return WaitForSubmitSound(submitSoundWaitTime);
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }

        private static IEnumerator WaitForSubmitSound(float waitTime)
        {
            if (waitTime > 0f)
                yield return new WaitForSecondsRealtime(waitTime);
        }

        private void BindSceneUi()
        {
            m_statusText = FindText("StatusText");

            m_previousButton = FindButton("PreviousMachineButton");
            m_nextButton = FindButton("NextMachineButton");
            m_readyButton = FindButton("ReadyButton");
            m_startBattleButton = FindButton("StartBattleButton");

            AddButtonListener(m_previousButton, OnClickPreviousMachine);
            AddButtonListener(m_nextButton, OnClickNextMachine);
            AddButtonListener(m_readyButton, OnClickReady);
            AddButtonListener(m_startBattleButton, OnClickStartBattle);
            AddButtonListener(FindButton("BackButton"), OnClickBack);

            if (m_startBattleButton != null)
            {
                m_startBattleButton.gameObject.SetActive(false);
            }

            for (int i = 0; i < m_slotViews.Length; i++)
            {
                m_slotViews[i] = new PlayerSlotView("PlayerSlot" + (i + 1));
            }
        }

        private void SetSelectedMachine(int index)
        {
            if (MachineProfileCatalog.Count == 0) return;

            if (index < 0)
            {
                index = MachineProfileCatalog.Count - 1;
            }
            else if (index >= MachineProfileCatalog.Count)
            {
                index = 0;
            }

            m_selectedMachineIndex = index;
            m_isReady = false;

            if (GameModeManager.Instance != null)
            {
                GameModeManager.Instance.SetSelectedMachineIndex(m_selectedMachineIndex);
            }

            UpdateMachinePreview();

            if (IsOnlineMode())
            {
                StartCoroutine(SetOnlineReadyCoroutine(false));
            }
            else
            {
                RefreshSoloSlots();
            }
        }

        private void HandleMachineChangeInput()
        {
            if (MachineProfileCatalog.Count <= 1)
                return;

            if (IsOnlineMode() && m_isReady)
                return;

            if (Time.unscaledTime < m_nextMachineChangeInputTime)
                return;

            int direction = GetMachineChangeInputDirection();
            if (direction == 0)
                return;

            global::AudioManager.Instance?.PlayCue(global::AudioCue.UiSelect);
            SetSelectedMachine(m_selectedMachineIndex + direction);
            m_nextMachineChangeInputTime = Time.unscaledTime + Mathf.Max(0.05f, machineChangeInputRepeatDelay);
        }

        private static int GetMachineChangeInputDirection()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame)
                    return -1;

                if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame)
                    return 1;
            }

            Gamepad gamepad = Gamepad.current;
            if (gamepad != null)
            {
                if (gamepad.dpad.left.wasPressedThisFrame || gamepad.leftStick.left.wasPressedThisFrame)
                    return -1;

                if (gamepad.dpad.right.wasPressedThisFrame || gamepad.leftStick.right.wasPressedThisFrame)
                    return 1;
            }

            return 0;
        }

        private IEnumerator PollOnlineLobbyCoroutine()
        {
            while (true)
            {
                if (LobbyManager.Instance == null || LobbyManager.Instance.CurrentLobby == null)
                {
                    yield return ReturnToLobbyAfterLobbyLostCoroutine("ロビー情報が見つかりません。ロビーへ戻ります。");
                    yield break;
                }

                if (!m_onlineLoadoutInitialized)
                {
                    m_onlineLoadoutInitialized = true;
                    yield return SetOnlineReadyCoroutine(false);
                }

                var refreshTask = LobbyManager.Instance.RefreshCurrentLobbyAsync();
                yield return new WaitUntil(() => refreshTask.IsCompleted);

                if (refreshTask.IsFaulted)
                {
                    Debug.LogError(refreshTask.Exception);
                    SetStatus("ロビー更新に失敗しました。再試行中...");
                    yield return new WaitForSeconds(1.25f);
                    continue;
                }

                Lobby lobby = refreshTask.Result;
                if (lobby == null)
                {
                    yield return ReturnToLobbyAfterLobbyLostCoroutine("ロビーが終了しました。ロビーへ戻ります。");
                    yield break;
                }

                RefreshOnlineSlots(lobby);
                UpdateOnlineButtons(lobby);

                if (!m_isHost)
                {
                    TryStartClientConnection(lobby);
                }

                yield return new WaitForSeconds(1.25f);
            }
        }

        private IEnumerator ReturnToLobbyAfterLobbyLostCoroutine(string message)
        {
            if (m_returningToLobby)
                yield break;

            m_returningToLobby = true;
            SetStatus(message);

            if (RelayConnectionManager.Instance != null)
            {
                var shutdownTask = RelayConnectionManager.Instance.ShutdownIfRunningAsync();
                yield return new WaitUntil(() => shutdownTask.IsCompleted);
            }
            else if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }

            yield return new WaitForSeconds(1f);
            SceneManager.LoadScene(lobbySceneName, LoadSceneMode.Single);
        }

        private IEnumerator StartBattleCoroutine()
        {
            m_battleLoadRequested = true;
            SetStatus("バトル開始中...");

            if (NetworkManager.Singleton != null &&
                NetworkManager.Singleton.IsServer &&
                NetworkManager.Singleton.SceneManager != null)
            {
                yield return LoadNetworkSceneCoroutine(battleSceneName);
                yield break;
            }

            m_battleLoadRequested = false;
            SetStatus("ネットワークシーン読み込みの準備ができていません。");
        }

        private IEnumerator LoadNetworkSceneCoroutine(string sceneName)
        {
            const float retryTimeoutSeconds = 5f;
            float elapsed = 0f;

            while (elapsed <= retryTimeoutSeconds)
            {
                SceneEventProgressStatus status =
                    SceneLoader.LoadNetworkScene(sceneName);

                if (status == SceneEventProgressStatus.Started)
                {
                    yield break;
                }

                if (status != SceneEventProgressStatus.SceneEventInProgress)
                {
                    Debug.LogWarning("Network scene load failed: " + status);
                    SetStatus("ネットワークシーン読み込みに失敗しました: " + status);
                    m_battleLoadRequested = false;
                    yield break;
                }

                SetStatus("前のシーン同期を待っています...");
                yield return new WaitForSeconds(0.25f);
                elapsed += 0.25f;
            }

            Debug.LogWarning("Network scene load timed out while another scene event was active.");
            SetStatus("ネットワークシーン読み込みがタイムアウトしました。");
            m_battleLoadRequested = false;
        }

        private IEnumerator SetOnlineReadyCoroutine(bool isReady)
        {
            if (LobbyManager.Instance == null || LobbyManager.Instance.CurrentLobby == null)
            {
                yield break;
            }

            m_isUpdatingLocalReady = true;
            m_isReady = isReady;
            SetPreviewSlotMachine(ResolveLocalPreviewSlotIndex(), m_selectedMachineIndex, true, m_isReady, isReady);
            SetStatus(isReady ? "選択を確定しました。他のプレイヤーを待っています..." : "選択を変更できます。");
            UpdateReadyButton();

            var task = LobbyManager.Instance.UpdateLocalPlayerLoadoutAsync(m_selectedMachineIndex, isReady);
            yield return new WaitUntil(() => task.IsCompleted);

            if (task.IsFaulted)
            {
                Debug.LogError(task.Exception);
                SetStatus("選択情報の更新に失敗しました。");
                m_isUpdatingLocalReady = false;
                yield break;
            }

            Lobby lobby = task.Result;
            m_isUpdatingLocalReady = false;
            if (lobby != null)
            {
                RefreshOnlineSlots(lobby);
                UpdateOnlineButtons(lobby);
            }
        }

        private void RefreshSoloSlots()
        {
            m_slotViews[0]?.SetOccupied(
                "P1",
                "あなた",
                GetMachineName(m_selectedMachineIndex),
                false,
                true,
                true
            );
            m_slotViews[0]?.SetMachineStats(GetMachineName(m_selectedMachineIndex), MachineProfileCatalog.GetProfile(m_selectedMachineIndex));

            for (int i = 1; i < m_slotViews.Length; i++)
            {
                int cpuMachineIndex = i % MachineProfileCatalog.Count;
                m_slotViews[i]?.SetCpu("P" + (i + 1), "CPU " + i, GetMachineName(cpuMachineIndex));
                m_slotViews[i]?.SetMachineStats(GetMachineName(cpuMachineIndex), MachineProfileCatalog.GetProfile(cpuMachineIndex));
            }

            if (m_startBattleButton != null)
            {
                m_startBattleButton.gameObject.SetActive(false);
            }

            RefreshSoloPreviewSlots();
            UpdateReadyButton();
        }

        private void RefreshOnlineSlots(Lobby lobby)
        {
            for (int i = 0; i < m_slotViews.Length; i++)
            {
                int cpuMachineIndex = i % MachineProfileCatalog.Count;
                m_slotViews[i]?.SetCpu("P" + (i + 1), "CPU " + (i + 1), GetMachineName(cpuMachineIndex));
                m_slotViews[i]?.SetMachineStats(GetMachineName(cpuMachineIndex), MachineProfileCatalog.GetProfile(cpuMachineIndex));
            }

            RefreshEmptyOnlinePreviewSlots(lobby);

            if (lobby == null || lobby.Players == null) return;

            m_latestLobby = lobby;

            string myPlayerId = AuthenticationService.Instance.PlayerId;
            m_isHost = lobby.HostId == myPlayerId;

            int slotCount = Mathf.Min(m_slotViews.Length, lobby.Players.Count);
            for (int i = 0; i < slotCount; i++)
            {
                Player player = lobby.Players[i];
                string playerName = GetPlayerName(player, i);
                int machineIndex = GetPlayerMachineIndex(player);
                bool isReady = IsPlayerReady(player);
                bool isHost = player.Id == lobby.HostId;
                bool isLocal = player.Id == myPlayerId;

                if (isLocal && m_isUpdatingLocalReady)
                {
                    machineIndex = m_selectedMachineIndex;
                    isReady = m_isReady;
                }

                if (isLocal)
                {
                    m_isReady = isReady;
                    m_localPreviewSlotIndex = i;
                }

                m_slotViews[i]?.SetOccupied(
                    "P" + (i + 1),
                    playerName,
                    GetMachineName(machineIndex),
                    isReady,
                    isHost,
                    isLocal
                );
                m_slotViews[i]?.SetMachineStats(GetMachineName(machineIndex), MachineProfileCatalog.GetProfile(machineIndex));

                SetPreviewSlotMachine(i, machineIndex, isLocal, isReady, !isLocal);
            }
        }

        private void UpdateOnlineButtons(Lobby lobby)
        {
            bool allReady = AreAllPlayersReady(lobby);
            bool allConnected = AreAllLobbyPlayersConnected(lobby);

            if (m_startBattleButton != null)
            {
                m_startBattleButton.gameObject.SetActive(m_isHost);
                m_startBattleButton.interactable = allReady && allConnected;
            }

            UpdateReadyButton();

            if (allReady)
            {
                if (allConnected)
                {
                    SetStatus(m_isHost ? "全員の準備が完了しました。" : "全員が選択確定しました。ホストを待っています。");
                }
                else
                {
                    SetStatus("全員が選択確定しました。通信接続を待っています。");
                }
            }
        }

        private void UpdateReadyButton()
        {
            Text label = m_readyButton != null ? m_readyButton.GetComponentInChildren<Text>() : null;
            if (label != null)
            {
                label.text = IsOnlineMode()
                    ? (m_isReady ? "変更する" : "確定")
                    : "スタート";
            }

            if (m_previousButton != null)
            {
                m_previousButton.interactable = !m_isReady || !IsOnlineMode();
            }

            if (m_nextButton != null)
            {
                m_nextButton.interactable = !m_isReady || !IsOnlineMode();
            }
        }

        private void UpdateMachinePreview()
        {
            m_selectedMachineIndex = MachineProfileCatalog.ClampIndex(m_selectedMachineIndex);
            MachineProfile profile = MachineProfileCatalog.GetProfile(m_selectedMachineIndex);

            if (!IsOnlineMode())
                RefreshSoloPreviewSlots();
            else
                SetPreviewSlotMachine(ResolveLocalPreviewSlotIndex(), m_selectedMachineIndex, true, m_isReady);
        }

        private void EnsureVehiclePreview()
        {
            if (previewVehiclePrefab == null)
            {
                Debug.LogWarning("Vehicle preview prefab is not assigned.");
                return;
            }

            GameObject previewRootObj = new GameObject("VehiclePreviewRoot_機体プレビュー");
            m_previewRoot = previewRootObj.transform;
            m_previewRoot.position = Vector3.zero;

            CreatePreviewLighting(m_previewRoot);

            Vector3[] positions =
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(20f, 0f, 0f),
                new Vector3(40f, 0f, 0f),
                new Vector3(60f, 0f, 0f)
            };

            for (int i = 0; i < m_previewSlots.Length; i++)
            {
                GameObject slotRootObj = new GameObject("PreviewSlot_" + (i + 1));
                Transform slotRoot = slotRootObj.transform;
                slotRoot.SetParent(m_previewRoot, false);
                slotRoot.localPosition = positions[i];
                slotRoot.localRotation = GetPreviewTargetRotation(i);
                float scale = 1.16f;
                slotRoot.localScale = new Vector3(scale, scale, scale);

                GameObject vehicle = Instantiate(previewVehiclePrefab, slotRoot);
                vehicle.name = i == 0 ? "PreviewPlayer" : "PreviewCPU_" + i;
                vehicle.transform.localPosition = Vector3.zero;
                vehicle.transform.localRotation = Quaternion.identity;

                m_previewSlots[i] = new VehiclePreviewSlot(slotRoot, vehicle, slotRoot.localRotation);
                PrepareVehicleForPreview(vehicle);
                CreatePreviewCamera(slotRoot, i);
                BindPreviewTextureToPlayerSlot(i);
            }

            RefreshSoloPreviewSlots();
        }

        private void CreatePreviewCamera(Transform targetRoot, int slotIndex)
        {
            if (targetRoot == null ||
                slotIndex < 0 ||
                slotIndex >= m_previewCameras.Length)
            {
                return;
            }

            GameObject cameraObj = new GameObject("VehiclePreviewCamera_" + (slotIndex + 1));
            cameraObj.transform.SetParent(m_previewRoot, false);
            cameraObj.transform.localPosition = targetRoot.localPosition + previewCameraPosition;
            cameraObj.transform.localRotation = Quaternion.Euler(previewCameraEulerAngles);

            Camera camera = cameraObj.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.07f, 0.095f, 0f);
            camera.orthographic = true;
            camera.orthographicSize = previewCameraSize;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 80f;
            camera.allowHDR = true;

            RenderTexture texture = new RenderTexture(1024, 576, 16, RenderTextureFormat.ARGB32);
            texture.name = "VehicleSelectPreviewTexture_" + (slotIndex + 1);
            camera.targetTexture = texture;

            m_previewCameras[slotIndex] = camera;
            m_previewTextures[slotIndex] = texture;
        }

        private void CreatePreviewLighting(Transform parent)
        {
            GameObject keyLightObj = new GameObject("VehiclePreviewKeyLight");
            keyLightObj.transform.SetParent(parent, false);
            keyLightObj.transform.localRotation = Quaternion.Euler(42f, -28f, 0f);
            Light keyLight = keyLightObj.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.color = new Color(1f, 0.96f, 0.86f, 1f);
            keyLight.intensity = 1.45f;

            GameObject fillLightObj = new GameObject("VehiclePreviewFillLight");
            fillLightObj.transform.SetParent(parent, false);
            fillLightObj.transform.localPosition = new Vector3(-3f, 2f, -2f);
            Light fillLight = fillLightObj.AddComponent<Light>();
            fillLight.type = LightType.Point;
            fillLight.color = new Color(0.45f, 0.7f, 1f, 1f);
            fillLight.intensity = 1.8f;
            fillLight.range = 8f;
        }

        private void BindPreviewTextureToPlayerSlot(int slotIndex)
        {
            if (slotIndex < 0 ||
                slotIndex >= m_previewTextures.Length ||
                m_previewTextures[slotIndex] == null)
            {
                return;
            }

            GameObject playerSlot = GameObject.Find("PlayerSlot" + (slotIndex + 1));
            if (playerSlot == null)
                return;

            Transform existing = playerSlot.transform.Find("VehiclePreviewImage");
            GameObject imageObj = existing != null ? existing.gameObject : new GameObject("VehiclePreviewImage");
            RectTransform rect = imageObj.GetComponent<RectTransform>();
            if (rect == null)
                rect = imageObj.AddComponent<RectTransform>();

            rect.SetParent(playerSlot.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(8f, 8f);
            rect.offsetMax = new Vector2(-8f, -8f);
            rect.SetAsFirstSibling();

            AspectRatioFitter fitter = imageObj.GetComponent<AspectRatioFitter>();
            if (fitter == null)
                fitter = imageObj.AddComponent<AspectRatioFitter>();

            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 16f / 9f;

            RawImage previewImage = imageObj.GetComponent<RawImage>();
            if (previewImage == null)
                previewImage = imageObj.AddComponent<RawImage>();

            previewImage.texture = m_previewTextures[slotIndex];
            previewImage.color = Color.white;
            previewImage.raycastTarget = false;
        }

        private void PrepareVehicleForPreview(GameObject vehicle)
        {
            if (vehicle == null)
                return;

            Camera[] cameras = vehicle.GetComponentsInChildren<Camera>(true);
            for (int i = 0; i < cameras.Length; i++)
                cameras[i].enabled = false;

            AudioListener[] listeners = vehicle.GetComponentsInChildren<AudioListener>(true);
            for (int i = 0; i < listeners.Length; i++)
                listeners[i].enabled = false;

            Collider[] colliders = vehicle.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
                colliders[i].enabled = false;

            Rigidbody[] bodies = vehicle.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < bodies.Length; i++)
            {
                bodies[i].isKinematic = true;
                bodies[i].useGravity = false;
            }

            MonoBehaviour[] behaviours = vehicle.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] == null)
                    continue;

                behaviours[i].enabled = false;
            }

            if (EntityManager.Instance != null)
            {
                Entity[] entities = vehicle.GetComponentsInChildren<Entity>(true);
                for (int i = 0; i < entities.Length; i++)
                {
                    EntityManager.Instance.UnregisterEntity(entities[i]);
                }
            }
        }

        private void RefreshSoloPreviewSlots()
        {
            if (MachineProfileCatalog.Count == 0)
                return;

            SetPreviewSlotMachine(0, m_selectedMachineIndex, true, false);

            for (int i = 1; i < m_previewSlots.Length; i++)
            {
                SetPreviewSlotMachine(i, i % MachineProfileCatalog.Count, false, true);
            }
        }

        private void RefreshCpuPreviewSlots()
        {
            if (MachineProfileCatalog.Count == 0)
                return;

            for (int i = 0; i < m_previewSlots.Length; i++)
            {
                SetPreviewSlotMachine(i, i % MachineProfileCatalog.Count, false, true);
            }
        }

        private void RefreshEmptyOnlinePreviewSlots(Lobby lobby)
        {
            if (MachineProfileCatalog.Count == 0)
                return;

            int occupiedSlotCount = lobby != null && lobby.Players != null
                ? Mathf.Min(lobby.Players.Count, m_previewSlots.Length)
                : 0;

            for (int i = occupiedSlotCount; i < m_previewSlots.Length; i++)
            {
                SetPreviewSlotMachine(i, i % MachineProfileCatalog.Count, false, true);
            }
        }

        private int ResolveLocalPreviewSlotIndex()
        {
            Lobby lobby = m_latestLobby != null ? m_latestLobby : LobbyManager.Instance?.CurrentLobby;
            string myPlayerId = AuthenticationService.Instance != null ? AuthenticationService.Instance.PlayerId : string.Empty;

            if (lobby != null && lobby.Players != null && !string.IsNullOrEmpty(myPlayerId))
            {
                int slotCount = Mathf.Min(lobby.Players.Count, m_previewSlots.Length);
                for (int i = 0; i < slotCount; i++)
                {
                    Player player = lobby.Players[i];
                    if (player != null && player.Id == myPlayerId)
                    {
                        m_localPreviewSlotIndex = i;
                        return i;
                    }
                }
            }

            m_localPreviewSlotIndex = Mathf.Clamp(m_localPreviewSlotIndex, 0, m_previewSlots.Length - 1);
            return m_localPreviewSlotIndex;
        }

        private Quaternion GetPreviewTargetRotation(int slotIndex)
        {
            bool isP1OrP3 = slotIndex == 0 || slotIndex == 2;
            float yaw = isP1OrP3 ? p1P3RightDiagonalYaw : p2P4LeftDiagonalYaw;
            return Quaternion.Euler(0f, yaw, 0f);
        }

        private void SetPreviewSlotMachine(int slotIndex, int machineIndex, bool isLocal, bool isReady, bool playReadySpin = false)
        {
            if (slotIndex < 0 || slotIndex >= m_previewSlots.Length)
                return;

            VehiclePreviewSlot slot = m_previewSlots[slotIndex];
            if (slot == null || slot.Vehicle == null)
                return;

            PlayerEntity playerEntity = slot.PlayerEntity != null
                ? slot.PlayerEntity
                : slot.Vehicle.GetComponentInChildren<PlayerEntity>(true);
            slot.PlayerEntity = playerEntity;

            bool wasReady = slot.IsReady;
            bool machineChanged = slot.MachineIndex != machineIndex;
            if (machineChanged && playerEntity != null)
            {
                playerEntity.ApplyMachineIndex(machineIndex);
                slot.MachineIndex = machineIndex;
            }

            if (machineChanged || isReady || wasReady)
                ResetPreviewVehiclePose(slot, playerEntity);

            MachineProfile profile = MachineProfileCatalog.GetProfile(machineIndex);
            float baseScale = 1.16f;
            float sizeCompensation = Mathf.Approximately(profile.SizeMultiplier, 0f)
                ? 1f
                : 1f / Mathf.Sqrt(profile.SizeMultiplier);
            float finalScale = baseScale * Mathf.Lerp(1f, sizeCompensation, 0.65f);

            slot.Root.localScale = new Vector3(finalScale, finalScale, finalScale);
            slot.Root.localPosition = new Vector3(
                slot.Root.localPosition.x,
                previewVehicleVerticalOffset + (profile.Type == MachineType.Heavy ? 0.08f : 0f),
                slot.Root.localPosition.z
            );
            slot.TargetRotation = GetPreviewTargetRotation(slotIndex);
            slot.IsReady = isReady;

            if (!slot.IsReady)
            {
                slot.CancelReadySpin();
                return;
            }

            if (playReadySpin && !wasReady)
            {
                slot.StartReadySpin(previewReadySpinDuration);
                return;
            }

            if (!slot.IsReadySpinActive)
                slot.Root.localRotation = slot.TargetRotation;
        }

        private void ResetPreviewVehiclePose(VehiclePreviewSlot slot, PlayerEntity playerEntity)
        {
            if (slot == null || slot.Vehicle == null)
                return;

            Transform vehicleTransform = slot.Vehicle.transform;
            vehicleTransform.localPosition = Vector3.zero;
            vehicleTransform.localRotation = Quaternion.identity;

            if (playerEntity == null)
                return;

            if (playerEntity.BodyTransform != null)
                playerEntity.BodyTransform.localRotation = Quaternion.identity;

            if (playerEntity.TurretTransform != null)
                playerEntity.TurretTransform.localRotation = Quaternion.identity;

            if (playerEntity.BarrelPitchTransform != null)
                playerEntity.BarrelPitchTransform.localRotation = Quaternion.identity;
        }

        private void RotatePreviewVehicles()
        {
            if (m_previewSlots == null || previewSpinSpeed <= 0f)
                return;

            for (int i = 0; i < m_previewSlots.Length; i++)
            {
                VehiclePreviewSlot slot = m_previewSlots[i];
                if (slot == null || slot.Root == null)
                    continue;

                if (slot.IsReady)
                {
                    if (slot.IsReadySpinActive)
                    {
                        slot.UpdateReadySpin(Time.unscaledDeltaTime);
                        ResetPreviewVehiclePose(slot, slot.PlayerEntity);
                        continue;
                    }

                    ResetPreviewVehiclePose(slot, slot.PlayerEntity);
                    slot.Root.localRotation = slot.TargetRotation;
                    continue;
                }

                slot.Root.Rotate(Vector3.up, previewSpinSpeed * Time.unscaledDeltaTime, Space.Self);
            }
        }

        private bool AreAllPlayersReady(Lobby lobby)
        {
            if (lobby == null || lobby.Players == null || lobby.Players.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < lobby.Players.Count; i++)
            {
                if (!IsPlayerReady(lobby.Players[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private bool AreAllLobbyPlayersConnected(Lobby lobby)
        {
            if (lobby == null || lobby.Players == null || lobby.Players.Count == 0)
            {
                return false;
            }

            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            {
                return true;
            }

            return NetworkManager.Singleton.ConnectedClientsIds.Count >= lobby.Players.Count;
        }

        private bool IsOnlineMode()
        {
            return GameModeManager.Instance != null && GameModeManager.Instance.IsOnlineMultiplayer();
        }

        private void TryStartClientConnection(Lobby lobby)
        {
            if (m_clientConnectStarted) return;
            if (RelayConnectionManager.Instance == null) return;
            if (NetworkManager.Singleton == null) return;
            if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsConnectedClient) return;

            string joinCode = GetRelayJoinCode(lobby);
            if (string.IsNullOrEmpty(joinCode)) return;

            m_clientConnectStarted = true;
            StartCoroutine(StartClientConnectionCoroutine(joinCode));
        }

        private IEnumerator StartClientConnectionCoroutine(string joinCode)
        {
            var task = RelayConnectionManager.Instance.StartClientWithRelayAsync(joinCode);
            yield return new WaitUntil(() => task.IsCompleted);

            if (task.IsFaulted || !task.Result)
            {
                Debug.LogWarning("Relay client connection failed.");
                SetStatus("Relay接続に失敗しました。");
                m_clientConnectStarted = false;
                yield break;
            }

            SetStatus("Relayに接続しました。");
        }

        private string GetRelayJoinCode(Lobby lobby)
        {
            if (lobby == null || lobby.Data == null) return string.Empty;
            if (!lobby.Data.ContainsKey(LobbyManager.RelayJoinCodeKey)) return string.Empty;

            return lobby.Data[LobbyManager.RelayJoinCodeKey].Value;
        }

        private string GetMachineName(int index)
        {
            if (MachineProfileCatalog.Count == 0) return "機体";
            return MachineProfileCatalog.GetProfile(index).DisplayName;
        }

        private string GetPlayerName(Player player, int index)
        {
            if (player != null &&
                player.Data != null &&
                player.Data.ContainsKey(LobbyManager.PlayerNameKey))
            {
                return player.Data[LobbyManager.PlayerNameKey].Value;
            }

            return "プレイヤー " + (index + 1);
        }

        private int GetPlayerMachineIndex(Player player)
        {
            if (player == null ||
                player.Data == null ||
                !player.Data.ContainsKey(LobbyManager.MachineIndexKey))
            {
                return 0;
            }

            if (int.TryParse(player.Data[LobbyManager.MachineIndexKey].Value, out int index))
            {
                return MachineProfileCatalog.ClampIndex(index);
            }

            return 0;
        }

        private bool IsPlayerReady(Player player)
        {
            if (player == null ||
                player.Data == null ||
                !player.Data.ContainsKey(LobbyManager.ReadyKey))
            {
                return false;
            }

            return player.Data[LobbyManager.ReadyKey].Value == "1";
        }

        private void SetStatus(string message)
        {
            if (m_statusText != null)
            {
                m_statusText.text = message;
            }
        }

        private Text FindText(string objectName)
        {
            GameObject obj = GameObject.Find(objectName);
            return obj != null ? obj.GetComponent<Text>() : null;
        }

        private Button FindButton(string objectName)
        {
            GameObject obj = GameObject.Find(objectName);
            return obj != null ? obj.GetComponent<Button>() : null;
        }

        private void AddButtonListener(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;

            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        private static void SelectButton(Selectable selectable)
        {
            if (selectable == null || EventSystem.current == null)
                return;

            EventSystem.current.SetSelectedGameObject(selectable.gameObject);
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

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;

            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<EventSystem>();
            eventSystemObj.AddComponent<StandaloneInputModule>();
        }

        private sealed class PlayerSlotView
        {
            private readonly Image m_panel;
            private readonly Text m_contentText;
            private readonly GameObject m_statsPanel;
            private readonly Text m_machineNameText;
            private readonly MachineStatBar[] m_statBars;

            public PlayerSlotView(string rootName)
            {
                GameObject rootObj = GameObject.Find(rootName);
                if (rootObj == null) return;

                m_panel = rootObj.GetComponent<Image>();
                Transform content = rootObj.transform.Find("ContentText");
                m_contentText = content != null ? content.GetComponent<Text>() : null;

                Transform stats = rootObj.transform.Find("VehicleStatsPanel");
                m_statsPanel = stats != null ? stats.gameObject : null;
                Transform machineName = stats != null ? stats.Find("MachineNameText") : null;
                m_machineNameText = machineName != null ? machineName.GetComponent<Text>() : null;

                m_statBars = new[]
                {
                    new MachineStatBar(stats, "StatHp"),
                    new MachineStatBar(stats, "StatAttack"),
                    new MachineStatBar(stats, "StatRapid"),
                    new MachineStatBar(stats, "StatMove")
                };
            }

            public void SetOccupied(string slotLabel, string playerName, string machineName, bool isReady, bool isHost, bool isLocal)
            {
                if (m_panel != null)
                {
                    m_panel.color = isLocal
                        ? new Color(0.18f, 0.22f, 0.29f, 0.98f)
                        : new Color(0.12f, 0.14f, 0.18f, 0.94f);
                }

                if (m_contentText != null)
                {
                    m_contentText.text =
                        slotLabel + "\n" +
                        playerName + (isHost ? "  ホスト" : string.Empty) + "\n" +
                        (isReady ? "準備OK" : "選択中");
                    m_contentText.color = isReady
                        ? new Color(0.78f, 1f, 0.84f, 1f)
                        : Color.white;
                }
            }

            public void SetEmpty(string slotLabel)
            {
                if (m_panel != null)
                {
                    m_panel.color = new Color(0.08f, 0.09f, 0.12f, 0.82f);
                }

                if (m_contentText != null)
                {
                    m_contentText.text = slotLabel + "\n空き\n-\n待機中";
                    m_contentText.color = new Color(0.58f, 0.66f, 0.76f, 1f);
                }
            }

            public void SetCpu(string slotLabel, string cpuName, string machineName)
            {
                if (m_panel != null)
                {
                    m_panel.color = new Color(0.1f, 0.1f, 0.13f, 0.9f);
                }

                if (m_contentText != null)
                {
                    m_contentText.text =
                        slotLabel + "\n" +
                        cpuName + "\n" +
                        "CPU 準備OK";
                    m_contentText.color = new Color(0.82f, 0.88f, 0.96f, 1f);
                }
            }

            public void SetMachineStats(string machineName, MachineProfile profile)
            {
                SetStatsVisible(true);

                if (m_machineNameText != null)
                    m_machineNameText.text = machineName;

                if (m_statBars == null || m_statBars.Length < 4)
                    return;

                m_statBars[0].SetRate(NormalizeStat(profile.HealthMultiplier, GetProfileMax(0, 1.5f)));
                m_statBars[1].SetRate(NormalizeStat(profile.AttackPowerMultiplier, GetProfileMax(1, 2f)));
                m_statBars[2].SetRate(NormalizeStat(profile.AttackSpeedMultiplier, GetProfileMax(2, 1.5f)));
                m_statBars[3].SetRate(NormalizeStat(profile.MoveSpeedMultiplier, GetProfileMax(3, 1.5f)));
            }

            private void SetStatsVisible(bool visible)
            {
                if (m_statsPanel != null)
                    m_statsPanel.SetActive(visible);
            }

            private static float NormalizeStat(float value, float max)
            {
                if (max <= 0f)
                    return 0f;

                return Mathf.Clamp01(value / max);
            }

            private static float GetProfileMax(int statIndex, float minimumMax)
            {
                float max = minimumMax;
                for (int i = 0; i < MachineProfileCatalog.Count; i++)
                {
                    MachineProfile profile = MachineProfileCatalog.GetProfile(i);
                    float value = statIndex switch
                    {
                        0 => profile.HealthMultiplier,
                        1 => profile.AttackPowerMultiplier,
                        2 => profile.AttackSpeedMultiplier,
                        3 => profile.MoveSpeedMultiplier,
                        _ => 0f
                    };

                    if (value > max)
                        max = value;
                }

                return max;
            }

            private sealed class MachineStatBar
            {
                private readonly RectTransform m_fillRect;

                public MachineStatBar(Transform parent, string rowName)
                {
                    Transform row = parent != null ? parent.Find(rowName) : null;
                    Transform fill = row != null ? row.Find("Track/Fill") : null;
                    m_fillRect = fill != null ? fill.GetComponent<RectTransform>() : null;
                }

                public void SetRate(float rate)
                {
                    if (m_fillRect == null)
                        return;

                    float width = Mathf.Clamp01(rate);
                    m_fillRect.anchorMax = new Vector2(width, 1f);
                    m_fillRect.offsetMax = Vector2.zero;
                }
            }
        }

        private sealed class VehiclePreviewSlot
        {
            public VehiclePreviewSlot(Transform root, GameObject vehicle, Quaternion frontRotation)
            {
                Root = root;
                Vehicle = vehicle;
                TargetRotation = frontRotation;
            }

            public Transform Root { get; }
            public GameObject Vehicle { get; }
            public PlayerEntity PlayerEntity { get; set; }
            public int MachineIndex { get; set; } = -1;
            public Quaternion TargetRotation { get; set; }
            public bool IsReady { get; set; }
            public bool IsReadySpinActive => m_readySpinDuration > 0f && m_readySpinElapsed < m_readySpinDuration;

            private float m_readySpinStartYaw;
            private float m_readySpinEndYaw;
            private float m_readySpinElapsed;
            private float m_readySpinDuration;

            public void StartReadySpin(float duration)
            {
                if (Root == null)
                    return;

                float targetYaw = TargetRotation.eulerAngles.y;
                m_readySpinStartYaw = targetYaw - 360f;
                m_readySpinEndYaw = targetYaw;
                m_readySpinElapsed = 0f;
                m_readySpinDuration = Mathf.Max(0.1f, duration);
                Root.localRotation = Quaternion.Euler(0f, m_readySpinStartYaw, 0f);
            }

            public void CancelReadySpin()
            {
                m_readySpinElapsed = 0f;
                m_readySpinDuration = 0f;
            }

            public void UpdateReadySpin(float deltaTime)
            {
                if (Root == null || m_readySpinDuration <= 0f)
                    return;

                m_readySpinElapsed = Mathf.Min(m_readySpinElapsed + deltaTime, m_readySpinDuration);
                float t = Mathf.SmoothStep(0f, 1f, m_readySpinElapsed / m_readySpinDuration);
                float yaw = Mathf.Lerp(m_readySpinStartYaw, m_readySpinEndYaw, t);
                Root.localRotation = Quaternion.Euler(0f, yaw, 0f);

                if (m_readySpinElapsed >= m_readySpinDuration)
                {
                    Root.localRotation = TargetRotation;
                    CancelReadySpin();
                }
            }
        }
    }
}
