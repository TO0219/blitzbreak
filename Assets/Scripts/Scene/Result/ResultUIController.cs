using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Services.Authentication;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MyGame
{
    [ExecuteAlways]
    public class ResultUIController : MonoBehaviour
    {
        [Header("Scene")]
        [SerializeField] private string lobbySceneName = "LobbyScene";
        [SerializeField] private string titleSceneName = "TitleScene";
        [SerializeField] private string vehicleSelectSceneName = "VehicleSelectScene";

        [Header("Animation")]
        [SerializeField] private float rowDelay = 0.22f;
        [SerializeField] private float rowSlideDuration = 0.45f;
        [SerializeField] private float rowStartYOffset = -520f;

        [Header("Score Count Up")]
        [SerializeField] private float scoreCountUpDuration = 1.8f;
        [SerializeField] private int scoreCountUpReferenceScore = 1000;
        [SerializeField] private float scoreCountUpMinDuration = 0.65f;
        [SerializeField] private float rankSwapDuration = 0.4f;
        [SerializeField] private float rankSwapPauseDuration = 0.15f;

        [Header("UI")]
        [SerializeField] private Font uiFont;
        [SerializeField] private Text titleText;
        [SerializeField] private Text winnerText;
        [SerializeField] private RectTransform[] resultRows = new RectTransform[4];
        [SerializeField] private Text[] rankTexts = new Text[4];
        [SerializeField] private Text[] nameTexts = new Text[4];
        [SerializeField] private Text[] scoreTexts = new Text[4];
        [SerializeField] private Button lobbyButton;
        [SerializeField] private Button titleButton;
        [SerializeField] private RectTransform leftInfoPanel;
        [SerializeField] private RectTransform rightInfoPanel;
        [SerializeField] private Text leftInfoTitleText;
        [SerializeField] private Text leftInfoBodyText;
        [SerializeField] private Text rightInfoTitleText;
        [SerializeField] private Text rightInfoBodyText;

        [Header("Audio")]
        [SerializeField, Range(0f, 1f)] private float submitSoundWaitTime = 0.18f;

        private readonly Vector2[] finalRowPositions = new Vector2[4];
        private List<BattleResultEntry> rankedEntries;
        private int localPlayerRankedIndex = -1;
        private int[] displaySlot = new int[4];
        private int localPlayerSlotIndex = -1;
        private int displayedEntryCount;
        private bool m_isReturningToMenu;
        private CanvasGroup sideInfoGroup;


        private readonly Color playerHighlightColor = new Color(0.93f, 0.75f, 0.22f, 0.95f);
        private readonly Color playerTextColor = new Color(1f, 0.97f, 0.78f, 1f);
        private readonly Color playerGlowColor = new Color(1f, 0.9f, 0.22f, 0.9f);
        private readonly Color normalPlaceColor = new Color(0.17f, 0.2f, 0.26f, 0.95f);


        private void Awake()
        {
            EnsureEventSystem();
            EnsureUi();
        }

        private void OnValidate()
        {
            ApplyDefaultLayout();
        }

        private void Start()
        {
            ShowResults();
            SetupButtons();
            StartCoroutine(SelectInitialButtonNextFrame());
        }

        public bool TryGetDisplayedEntry(Transform clickedTransform, out BattleResultEntry entry)
        {
            entry = default;

            if (clickedTransform == null || rankedEntries == null)
                return false;

            for (int slotIndex = 0; slotIndex < displayedEntryCount; slotIndex++)
            {
                RectTransform row = resultRows[slotIndex];
                if (row == null)
                    continue;

                if (clickedTransform != row && !clickedTransform.IsChildOf(row))
                    continue;

                int entryIndex = displaySlot[slotIndex];
                if (entryIndex < 0 || entryIndex >= rankedEntries.Count)
                    return false;

                entry = rankedEntries[entryIndex];
                return true;
            }

            return false;
        }

        public bool TryGetDisplayedEntryBySlot(int slotIndex, out BattleResultEntry entry)
        {
            entry = default;

            if (rankedEntries == null ||
                slotIndex < 0 ||
                slotIndex >= displayedEntryCount)
            {
                return false;
            }

            int entryIndex = displaySlot[slotIndex];
            if (entryIndex < 0 || entryIndex >= rankedEntries.Count)
                return false;

            entry = rankedEntries[entryIndex];
            return true;
        }

        // ─────────────────────────────────────────────
        // 順位色更新
        // ─────────────────────────────────────────────

        private void UpdateRowColors()
        {
            for (int i = 0; i < resultRows.Length && i < 4; i++)
            {
                if (resultRows[i] == null) continue;

                Image rowImage = resultRows[i].GetComponent<Image>();
                bool isPlayerRow = IsLocalPlayerDisplaySlot(i);

                if (rowImage != null)
                {
                    rowImage.color = isPlayerRow
                        ? playerHighlightColor
                        : normalPlaceColor;
                }

                ApplyRowGlow(resultRows[i], isPlayerRow);
                ApplyTextGlow(rankTexts, i, isPlayerRow);
                ApplyTextGlow(nameTexts, i, isPlayerRow);
                ApplyTextGlow(scoreTexts, i, isPlayerRow);
            }
        }

        private bool IsLocalPlayerDisplaySlot(int slotIndex)
        {
            return localPlayerRankedIndex >= 0 &&
                   slotIndex >= 0 &&
                   slotIndex < displayedEntryCount &&
                   displaySlot != null &&
                   slotIndex < displaySlot.Length &&
                   displaySlot[slotIndex] == localPlayerRankedIndex;
        }

        private void ApplyRowGlow(RectTransform row, bool enabled)
        {
            if (row == null)
                return;

            Outline outline = row.GetComponent<Outline>();
            if (!enabled)
            {
                if (outline != null)
                    outline.enabled = false;

                return;
            }

            if (outline == null && Application.isPlaying)
                outline = row.gameObject.AddComponent<Outline>();

            if (outline == null)
                return;

            outline.effectColor = playerGlowColor;
            outline.effectDistance = new Vector2(5f, -5f);
            outline.useGraphicAlpha = true;
            outline.enabled = true;
        }

        private void ApplyTextGlow(Text[] texts, int index, bool enabled)
        {
            if (texts == null || index < 0 || index >= texts.Length || texts[index] == null)
                return;

            Text text = texts[index];
            text.color = enabled ? playerTextColor : Color.white;

            Outline outline = text.GetComponent<Outline>();
            if (!enabled)
            {
                if (outline != null)
                    outline.enabled = false;

                return;
            }

            if (outline == null && Application.isPlaying)
                outline = text.gameObject.AddComponent<Outline>();

            if (outline == null)
                return;

            outline.effectColor = playerGlowColor;
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = false;
            outline.enabled = true;
        }



        // ─────────────────────────────────────────────
        // 初期化・データ準備
        // ─────────────────────────────────────────────

        private void ShowResults()
        {
            if (!BattleResultData.HasResult)
            {
                BattleResultData.TryRestoreSavedResults();
            }

            if (!BattleResultData.HasResult)
            {
                ShowMissingResultData();
                return;
            }

            rankedEntries = BattleResultData.GetRankedEntries();

            if (rankedEntries.Count > 4)
            {
                rankedEntries.RemoveRange(4, rankedEntries.Count - 4);
            }

            displayedEntryCount = rankedEntries.Count;

            localPlayerRankedIndex = FindLocalPlayerIndex(rankedEntries);
            if (localPlayerRankedIndex >= 0)
            {
                global::AudioManager.Instance?.PlayCue(
                    localPlayerRankedIndex == 0
                        ? global::AudioCue.RankFirst
                        : global::AudioCue.RankOther);
            }

            if (titleText != null)
                titleText.text = "リザルト";

            if (winnerText != null)
                winnerText.gameObject.SetActive(false);

            // ローカルプレイヤーを最下位スロット(3)に固定し、
            // 残りのスロットには最終ランク順でCPU/他プレイヤーを並べる
            if (localPlayerRankedIndex >= 0)
            {
                int slotForOthers = 0;
                for (int i = 0; i < displayedEntryCount; i++)
                {
                    if (i == localPlayerRankedIndex) continue;
                    displaySlot[slotForOthers] = i;
                    slotForOthers++;
                }
                localPlayerSlotIndex = displayedEntryCount - 1;
                displaySlot[localPlayerSlotIndex] = localPlayerRankedIndex;
            }
            else
            {
                localPlayerSlotIndex = -1;
                for (int i = 0; i < displayedEntryCount; i++)
                    displaySlot[i] = i;
            }

            for (int i = 0; i < resultRows.Length && i < 4; i++)
            {
                if (resultRows[i] == null) continue;

                if (i >= displayedEntryCount)
                {
                    resultRows[i].gameObject.SetActive(false);
                    continue;
                }

                finalRowPositions[i] = resultRows[i].anchoredPosition;
                resultRows[i].anchoredPosition = finalRowPositions[i] + new Vector2(0f, rowStartYOffset);
                resultRows[i].gameObject.SetActive(false);

                int entryIndex = displaySlot[i];
                BattleResultEntry entry = rankedEntries[entryIndex];

                if (rankTexts[i] != null)
                    rankTexts[i].text = GetRankLabel(i + 1);

                if (nameTexts[i] != null)
                    nameTexts[i].text = entry.PlayerName;

                // ローカルプレイヤーのスコアは0からスタート（カウントアップ演出のため）
                if (scoreTexts[i] != null)
                    scoreTexts[i].text = (i == localPlayerSlotIndex) ? "0" : entry.Score.ToString();
            }

            UpdateSideInfoPanels();
            UpdateRowColors();

            StartCoroutine(PlayFullAnimation());


        }

        private void ShowMissingResultData()
        {
            rankedEntries = new List<BattleResultEntry>();
            displayedEntryCount = 0;
            localPlayerRankedIndex = -1;
            localPlayerSlotIndex = -1;

            if (titleText != null)
                titleText.text = "リザルトデータがありません";

            if (winnerText != null)
                winnerText.gameObject.SetActive(false);

            for (int i = 0; i < resultRows.Length; i++)
            {
                if (resultRows[i] != null)
                    resultRows[i].gameObject.SetActive(false);
            }

            SetSideInfoVisible(false);
        }

        // ─────────────────────────────────────────────
        // ローカルプレイヤー識別
        // ─────────────────────────────────────────────

        private int FindLocalPlayerIndex(List<BattleResultEntry> ranked)
        {
            if (BattleResultData.WasOnline && NetworkManager.Singleton != null)
            {
                ulong localId = NetworkManager.Singleton.LocalClientId;
                for (int i = 0; i < ranked.Count; i++)
                {
                    if (!ranked[i].IsCpu && ranked[i].ClientId == localId)
                        return i;
                }
            }
            else
            {
                for (int i = 0; i < ranked.Count; i++)
                {
                    if (!ranked[i].IsCpu && ranked[i].ClientId == 0)
                        return i;
                }
            }

            return -1;
        }

        // ─────────────────────────────────────────────
        // メインアニメーション
        // ─────────────────────────────────────────────

        private IEnumerator PlayFullAnimation()
        {
            SetSideInfoVisible(false);
            SetSideInfoAlpha(0f);

            // Step1: 全行を下からスライドイン（4位→1位の順）
            for (int slotIndex = displayedEntryCount - 1; slotIndex >= 0; slotIndex--)
            {
                if (resultRows[slotIndex] != null)
                    yield return SlideInRow(resultRows[slotIndex], finalRowPositions[slotIndex]);

                yield return new WaitForSeconds(rowDelay);
            }

            yield return new WaitForSeconds(0.3f);

            // Step2: ローカルプレイヤーのスコアカウントアップ演出
            if (localPlayerRankedIndex >= 0)
                yield return PlayScoreCountUp();

            if (sideInfoGroup != null)
            {
                SetSideInfoVisible(true);
                yield return FadeSideInfo(0f, 1f, 0.35f);
            }

            UpdateRowColors();
        }

        // ─────────────────────────────────────────────
        // スコアカウントアップ + 順位入れ替え
        // ─────────────────────────────────────────────

        private IEnumerator PlayScoreCountUp()
        {
            int finalScore = rankedEntries[localPlayerRankedIndex].Score;
            float countUpDuration = CalculateScoreCountUpDuration(finalScore);

            float elapsed = 0f;
            int currentScore = 0;

            while (elapsed < countUpDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / countUpDuration);
                float eased = 1f - Mathf.Pow(1f - t, 2f);
                currentScore = Mathf.RoundToInt(eased * finalScore);

                if (scoreTexts[localPlayerSlotIndex] != null)
                    scoreTexts[localPlayerSlotIndex].text = currentScore.ToString();

                // 1つ上のスロットのスコアを超えたらスワップ
                int slotAbove = localPlayerSlotIndex - 1;
                if (slotAbove >= 0 &&
                    localPlayerSlotIndex > localPlayerRankedIndex)
                {
                    int aboveScore = rankedEntries[displaySlot[slotAbove]].Score;
                    if (currentScore >= aboveScore)
                    {
                        yield return SwapRows(localPlayerSlotIndex, slotAbove);
                        localPlayerSlotIndex = slotAbove;
                        yield return new WaitForSeconds(rankSwapPauseDuration);
                    }
                }

                yield return null;
            }

            if (scoreTexts[localPlayerSlotIndex] != null)
                scoreTexts[localPlayerSlotIndex].text = finalScore.ToString();
        }

        // ─────────────────────────────────────────────
        // 2行の入れ替えアニメーション
        // ─────────────────────────────────────────────

        private float CalculateScoreCountUpDuration(int finalScore)
        {
            float baseDuration = Mathf.Max(0.01f, scoreCountUpDuration);
            int referenceScore = Mathf.Max(1, scoreCountUpReferenceScore);
            int score = Mathf.Max(0, finalScore);

            if (score <= referenceScore)
                return baseDuration;

            float speedMultiplier = Mathf.Sqrt((float)score / referenceScore);
            float duration = baseDuration / Mathf.Max(1f, speedMultiplier);
            return Mathf.Max(scoreCountUpMinDuration, duration);
        }

        private IEnumerator SwapRows(int slotA, int slotB)
        {
            Vector2 posA = finalRowPositions[slotA];
            Vector2 posB = finalRowPositions[slotB];

            RectTransform rowA = resultRows[slotA];
            RectTransform rowB = resultRows[slotB];

            Vector2 startA = rowA.anchoredPosition;
            Vector2 startB = rowB.anchoredPosition;

            float elapsed = 0f;
            while (elapsed < rankSwapDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / rankSwapDuration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);

                if (rowA != null) rowA.anchoredPosition = Vector2.Lerp(startA, posB, eased);
                if (rowB != null) rowB.anchoredPosition = Vector2.Lerp(startB, posA, eased);

                yield return null;
            }

            if (rowA != null) rowA.anchoredPosition = posB;
            if (rowB != null) rowB.anchoredPosition = posA;

            int tmp = displaySlot[slotA];
            displaySlot[slotA] = displaySlot[slotB];
            displaySlot[slotB] = tmp;

            resultRows[slotA] = rowB;
            resultRows[slotB] = rowA;

            SwapTextRef(ref scoreTexts[slotA], ref scoreTexts[slotB]);
            SwapTextRef(ref rankTexts[slotA], ref rankTexts[slotB]);
            SwapTextRef(ref nameTexts[slotA], ref nameTexts[slotB]);

            if (resultRows[slotA] != null)
                resultRows[slotA].anchoredPosition = finalRowPositions[slotA];

            if (resultRows[slotB] != null)
                resultRows[slotB].anchoredPosition = finalRowPositions[slotB];

            for (int i = 0; i < displayedEntryCount; i++)
            {
                if (rankTexts[i] != null)
                    rankTexts[i].text = GetRankLabel(i + 1);
            }

            UpdateRowColors();
        }

        private void SwapTextRef(ref Text a, ref Text b)
        {
            Text tmp = a;
            a = b;
            b = tmp;
        }

        // ─────────────────────────────────────────────
        // スライドインアニメーション
        // ─────────────────────────────────────────────

        private IEnumerator SlideInRow(RectTransform row, Vector2 target)
        {
            row.gameObject.SetActive(true);

            Vector2 start = target + new Vector2(0f, rowStartYOffset);
            row.anchoredPosition = start;

            CanvasGroup group = row.GetComponent<CanvasGroup>();
            if (group == null)
                group = row.gameObject.AddComponent<CanvasGroup>();

            group.alpha = 0f;

            float elapsed = 0f;
            while (elapsed < rowSlideDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, rowSlideDuration));
                float eased = 1f - Mathf.Pow(1f - t, 3f);

                row.anchoredPosition = Vector2.Lerp(start, target, eased);
                group.alpha = eased;
                yield return null;
            }

            row.anchoredPosition = target;
            group.alpha = 1f;
        }

        // ─────────────────────────────────────────────
        // ボタン・ナビゲーション
        // ─────────────────────────────────────────────

        private void SetupButtons()
        {
            if (lobbyButton != null)
            {
                Text label = lobbyButton.GetComponentInChildren<Text>();
                if (label != null)
                    label.text = BattleResultData.WasOnline ? "ロビー" : "機体選択";

                lobbyButton.onClick.RemoveAllListeners();
                lobbyButton.onClick.AddListener(OnClickLobby);
                AddButtonHoverSound(lobbyButton);
            }

            if (titleButton != null)
            {
                titleButton.onClick.RemoveAllListeners();
                titleButton.onClick.AddListener(OnClickTitle);
                AddButtonHoverSound(titleButton);
            }
        }

        private void OnClickLobby()
        {
            if (m_isReturningToMenu)
                return;

            global::AudioManager.Instance?.PlayCue(global::AudioCue.UiSubmit);
            StartCoroutine(ReturnToLobbyOrTitle());
        }

        private void OnClickTitle()
        {
            if (m_isReturningToMenu)
                return;

            global::AudioManager.Instance?.PlayCue(global::AudioCue.UiSubmit);
            StartCoroutine(ReturnToTitle());
        }

        public void OnGamepadCancel()
        {
            if (m_isReturningToMenu)
                return;

            global::AudioManager.Instance?.PlayCue(global::AudioCue.UiSubmit);
            StartCoroutine(ReturnToTitle());
        }

        private IEnumerator ReturnToLobbyOrTitle()
        {
            m_isReturningToMenu = true;
            if (submitSoundWaitTime > 0f)
                yield return new WaitForSecondsRealtime(submitSoundWaitTime);

            bool wasOnline = BattleResultData.WasOnline;
            yield return PrepareNetworkForMenu(wasOnline, leaveLobby: false);
            BattleResultData.Clear();
            string sceneName = wasOnline ? lobbySceneName : vehicleSelectSceneName;
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }

        private IEnumerator SelectInitialButtonNextFrame()
        {
            yield return null;

            if (EventSystem.current == null)
                yield break;

            Selectable[] selectables = FindObjectsByType<Selectable>(FindObjectsInactive.Exclude);
            ConfigureAutomaticNavigation(selectables);
            for (int i = 0; i < selectables.Length; i++)
            {
                if (selectables[i] == null || !selectables[i].IsInteractable())
                    continue;

                EventSystem.current.SetSelectedGameObject(selectables[i].gameObject);
                yield break;
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

        private IEnumerator ReturnToTitle()
        {
            m_isReturningToMenu = true;
            if (submitSoundWaitTime > 0f)
                yield return new WaitForSecondsRealtime(submitSoundWaitTime);

            yield return PrepareNetworkForMenu(BattleResultData.WasOnline, leaveLobby: true);
            BattleResultData.Clear();
            SceneManager.LoadScene(titleSceneName, LoadSceneMode.Single);
        }

        private static void AddButtonHoverSound(Button button)
        {
            if (button == null)
                return;

            EventTrigger trigger = button.GetComponent<EventTrigger>();
            if (trigger == null)
                trigger = button.gameObject.AddComponent<EventTrigger>();

            EventTrigger.Entry entry = new EventTrigger.Entry
            {
                eventID = EventTriggerType.PointerEnter
            };
            entry.callback.AddListener(
                _ => global::AudioManager.Instance?.PlayCue(global::AudioCue.UiSelect));
            trigger.triggers.Add(entry);
        }

        private IEnumerator PrepareNetworkForMenu(bool wasOnline, bool leaveLobby)
        {
            if (!wasOnline) yield break;

            if (LobbyManager.Instance != null && LobbyManager.Instance.CurrentLobby != null)
            {
                if (!leaveLobby)
                {
                    bool isLobbyHost =
                        AuthenticationService.Instance != null &&
                        LobbyManager.Instance.CurrentLobby.HostId == AuthenticationService.Instance.PlayerId;

                    if (isLobbyHost)
                    {
                        var t = LobbyManager.Instance.SetLobbyStartedAsync(false);
                        yield return new WaitUntil(() => t.IsCompleted);
                        if (t.IsFaulted)
                            Debug.LogWarning("ロビーの試合状態を戻せませんでした: " + t.Exception);
                    }
                }
                else
                {
                    var t = LobbyManager.Instance.LeaveLobbyAsync();
                    yield return new WaitUntil(() => t.IsCompleted);
                    if (t.IsFaulted)
                        Debug.LogWarning("ロビー退出に失敗しました: " + t.Exception);
                }
            }

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown();
        }

        private string GetRankLabel(int rank)
        {
            switch (rank)
            {
                case 1: return "1st";
                case 2: return "2nd";
                case 3: return "3rd";
                default: return "4th";
            }
        }

        // ─────────────────────────────────────────────
        // UI構築
        // ─────────────────────────────────────────────

        private void EnsureUi()
        {
            if (resultRows != null && resultRows.Length >= 4 && resultRows[0] != null)
            {
                ApplyDefaultLayout();
                EnsureSideInfoPanels();
                return;
            }

            Debug.LogWarning("Result UI references are missing. Place the result UI in the scene hierarchy.");
        }

#if UNITY_EDITOR
        public void BuildDefaultUiForEditor() => BuildFallbackUi();
#endif

        private void BuildFallbackUi()
        {
            Font font = GetDefaultFont();

            Canvas canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObject = new GameObject("ResultCanvas");
                canvas = canvasObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasObject.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920f, 1080f);
                canvasObject.AddComponent<GraphicRaycaster>();
            }

            RectTransform canvasRect = canvas.GetComponent<RectTransform>();

            GameObject background = CreateUiObject("Background", canvasRect);
            Image backgroundImage = background.AddComponent<Image>();
            backgroundImage.color = new Color(0.06f, 0.07f, 0.09f, 1f);
            Stretch(background.GetComponent<RectTransform>());

            titleText = CreateText("TitleText", canvasRect, font, 76, TextAnchor.MiddleCenter, Color.white);
            SetRect(titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -92f), new Vector2(700f, 100f));

            winnerText = CreateText("WinnerText", canvasRect, font, 34, TextAnchor.MiddleCenter, new Color(1f, 0.86f, 0.32f, 1f));
            SetRect(winnerText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -168f), new Vector2(700f, 64f));

            resultRows = new RectTransform[4];
            rankTexts = new Text[4];
            nameTexts = new Text[4];
            scoreTexts = new Text[4];

            for (int i = 0; i < 4; i++)
                CreateResultRow(i, canvasRect, font);

            lobbyButton = CreateButton("LobbyButton", canvasRect, font, "LOBBY", new Vector2(-170f, 118f));
            titleButton = CreateButton("TitleButton", canvasRect, font, "タイトル", new Vector2(170f, 118f));

            ApplyDefaultLayout();
        }

        private void ApplyDefaultLayout()
        {
            for (int i = 0; i < 4; i++)
            {
                if (resultRows != null && i < resultRows.Length && resultRows[i] != null)
                    SetRect(resultRows[i], new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 185f - i * 118f), new Vector2(960f, 92f));

                if (rankTexts != null && i < rankTexts.Length && rankTexts[i] != null)
                {
                    rankTexts[i].alignment = TextAnchor.MiddleCenter;
                    rankTexts[i].resizeTextForBestFit = true;
                    rankTexts[i].resizeTextMinSize = 18;
                    rankTexts[i].resizeTextMaxSize = 34;
                    SetRect(rankTexts[i].rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(92f, 0f), new Vector2(150f, 64f));
                }

                if (nameTexts != null && i < nameTexts.Length && nameTexts[i] != null)
                {
                    nameTexts[i].alignment = TextAnchor.MiddleLeft;
                    nameTexts[i].resizeTextForBestFit = true;
                    nameTexts[i].resizeTextMinSize = 18;
                    nameTexts[i].resizeTextMaxSize = 30;
                    SetRect(nameTexts[i].rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(420f, 0f), new Vector2(520f, 64f));
                }

                if (scoreTexts != null && i < scoreTexts.Length && scoreTexts[i] != null)
                {
                    scoreTexts[i].alignment = TextAnchor.MiddleRight;
                    scoreTexts[i].resizeTextForBestFit = true;
                    scoreTexts[i].resizeTextMinSize = 18;
                    scoreTexts[i].resizeTextMaxSize = 30;
                    SetRect(scoreTexts[i].rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-132f, 0f), new Vector2(210f, 64f));
                }
            }

            if (lobbyButton != null)
                SetRect(lobbyButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-170f, 118f), new Vector2(280f, 72f));

            if (titleButton != null)
                SetRect(titleButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(170f, 118f), new Vector2(280f, 72f));

            if (leftInfoPanel != null)
                SetRect(leftInfoPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-690f, 20f), new Vector2(310f, 520f));

            if (rightInfoPanel != null)
                SetRect(rightInfoPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(690f, 20f), new Vector2(310f, 520f));
        }

        private void CreateResultRow(int rankIndex, RectTransform parent, Font font)
        {
            GameObject rowObject = CreateUiObject("ResultRow_" + (rankIndex + 1), parent);
            RectTransform rowRect = rowObject.GetComponent<RectTransform>();
            SetRect(rowRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 185f - rankIndex * 118f), new Vector2(960f, 92f));

            Image rowImage = rowObject.AddComponent<Image>();
            rowImage.color = normalPlaceColor;

            resultRows[rankIndex] = rowRect;

            rankTexts[rankIndex] = CreateText("RankText", rowRect, font, 34, TextAnchor.MiddleCenter, Color.white);
            SetRect(rankTexts[rankIndex].rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(92f, 0f), new Vector2(150f, 64f));

            nameTexts[rankIndex] = CreateText("NameText", rowRect, font, 30, TextAnchor.MiddleLeft, Color.white);
            SetRect(nameTexts[rankIndex].rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(420f, 0f), new Vector2(520f, 64f));

            scoreTexts[rankIndex] = CreateText("ScoreText", rowRect, font, 30, TextAnchor.MiddleRight, Color.white);
            SetRect(scoreTexts[rankIndex].rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-132f, 0f), new Vector2(210f, 64f));
        }

        private Button CreateButton(string objectName, RectTransform parent, Font font, string label, Vector2 anchoredPosition)
        {
            GameObject buttonObject = CreateUiObject(objectName, parent);
            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            SetRect(buttonRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), anchoredPosition, new Vector2(280f, 72f));

            Image buttonImage = buttonObject.AddComponent<Image>();
            buttonImage.color = new Color(0.92f, 0.95f, 1f, 1f);

            Button button = buttonObject.AddComponent<Button>();

            Text buttonText = CreateText("Text", buttonRect, font, 28, TextAnchor.MiddleCenter, new Color(0.06f, 0.07f, 0.09f, 1f));
            buttonText.text = label;
            Stretch(buttonText.rectTransform);

            return button;
        }

        private void EnsureSideInfoPanels()
        {
            Canvas canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null)
                return;

            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            Font font = GetDefaultFont();

            if (leftInfoPanel == null)
                leftInfoPanel = CreateSideInfoPanel(canvasRect, font, "ResultLeftInfoPanel", -690f, out leftInfoTitleText, out leftInfoBodyText);
            else if (leftInfoTitleText == null || leftInfoBodyText == null)
                ResolveSideInfoTexts(leftInfoPanel, out leftInfoTitleText, out leftInfoBodyText);

            if (rightInfoPanel == null)
                rightInfoPanel = CreateSideInfoPanel(canvasRect, font, "ResultRightInfoPanel", 690f, out rightInfoTitleText, out rightInfoBodyText);
            else if (rightInfoTitleText == null || rightInfoBodyText == null)
                ResolveSideInfoTexts(rightInfoPanel, out rightInfoTitleText, out rightInfoBodyText);

            PlaceSidePanel(leftInfoPanel);
            PlaceSidePanel(rightInfoPanel);
            sideInfoGroup = GetOrAddCanvasGroup(leftInfoPanel);
            GetOrAddCanvasGroup(rightInfoPanel);
            ApplyDefaultLayout();
        }

        private RectTransform CreateSideInfoPanel(
            RectTransform parent,
            Font font,
            string objectName,
            float x,
            out Text title,
            out Text body)
        {
            GameObject panelObject = CreateUiObject(objectName, parent);
            RectTransform panel = panelObject.GetComponent<RectTransform>();
            SetRect(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 20f), new Vector2(310f, 520f));
            PlaceSidePanel(panel);

            Image panelImage = panelObject.AddComponent<Image>();
            panelImage.color = new Color(0.025f, 0.035f, 0.055f, 0.72f);
            panelImage.raycastTarget = false;

            Outline outline = panelObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.18f, 0.78f, 1f, 0.34f);
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = false;

            CanvasGroup group = panelObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            CreateSideAccent(panel, "TopAccent", new Vector2(0f, 238f), new Vector2(230f, 3f), new Color(0.24f, 0.9f, 1f, 0.75f));
            CreateSideAccent(panel, "BottomAccent", new Vector2(0f, -238f), new Vector2(230f, 3f), new Color(0.93f, 0.75f, 0.22f, 0.55f));
            CreateSideAccent(panel, "VerticalAccent", new Vector2(-138f, 0f), new Vector2(3f, 420f), new Color(0.24f, 0.9f, 1f, 0.4f));

            title = CreateText("SideInfoTitle", panel, font, 24, TextAnchor.MiddleLeft, new Color(0.82f, 0.96f, 1f, 1f));
            title.fontStyle = FontStyle.Bold;
            SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -52f), new Vector2(-48f, 52f));

            body = CreateText("SideInfoBody", panel, font, 22, TextAnchor.UpperLeft, new Color(0.95f, 0.97f, 1f, 0.95f));
            body.lineSpacing = 1.25f;
            body.resizeTextForBestFit = true;
            body.resizeTextMinSize = 15;
            body.resizeTextMaxSize = 22;
            SetRect(body.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(18f, -18f), new Vector2(-52f, -126f));

            panelObject.SetActive(false);
            return panel;
        }

        private void PlaceSidePanel(RectTransform panel)
        {
            if (panel == null || panel.parent == null)
                return;

            int targetIndex = 1;
            Transform background = panel.parent.Find("Background");
            if (background != null)
                targetIndex = background.GetSiblingIndex() + 1;

            targetIndex = Mathf.Clamp(targetIndex, 0, panel.parent.childCount - 1);
            panel.SetSiblingIndex(targetIndex);
        }

        private void CreateSideAccent(RectTransform parent, string objectName, Vector2 position, Vector2 size, Color color)
        {
            GameObject accentObject = CreateUiObject(objectName, parent);
            RectTransform accent = accentObject.GetComponent<RectTransform>();
            SetRect(accent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);

            Image image = accentObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        private void ResolveSideInfoTexts(RectTransform panel, out Text title, out Text body)
        {
            title = null;
            body = null;

            Text[] texts = panel.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i].name == "SideInfoTitle")
                    title = texts[i];
                else if (texts[i].name == "SideInfoBody")
                    body = texts[i];
            }
        }

        private CanvasGroup GetOrAddCanvasGroup(RectTransform rect)
        {
            if (rect == null)
                return null;

            CanvasGroup group = rect.GetComponent<CanvasGroup>();
            if (group == null)
                group = rect.gameObject.AddComponent<CanvasGroup>();

            return group;
        }

        private void SetSideInfoVisible(bool visible)
        {
            if (leftInfoPanel != null)
                leftInfoPanel.gameObject.SetActive(visible);

            if (rightInfoPanel != null)
                rightInfoPanel.gameObject.SetActive(visible);
        }

        private void SetSideInfoAlpha(float alpha)
        {
            if (sideInfoGroup != null)
                sideInfoGroup.alpha = alpha;

            if (rightInfoPanel != null)
            {
                CanvasGroup rightGroup = rightInfoPanel.GetComponent<CanvasGroup>();
                if (rightGroup != null)
                    rightGroup.alpha = alpha;
            }
        }

        private void UpdateSideInfoPanels()
        {
            EnsureSideInfoPanels();

            if (rankedEntries == null || rankedEntries.Count == 0)
            {
                SetSideInfoVisible(false);
                return;
            }

            BattleResultEntry winner = rankedEntries[0];
            bool hasLocal = localPlayerRankedIndex >= 0 && localPlayerRankedIndex < rankedEntries.Count;
            BattleResultEntry local = hasLocal ? rankedEntries[localPlayerRankedIndex] : winner;

            if (leftInfoTitleText != null)
                leftInfoTitleText.text = "TOP PILOT";

            if (leftInfoBodyText != null)
            {
                string localLabel = hasLocal ? (localPlayerRankedIndex + 1).ToString() : "-";
                leftInfoBodyText.text =
                    "WINNER\n" +
                    winner.PlayerName + "\n\n" +
                    "YOUR RANK\n" +
                    localLabel + " / " + rankedEntries.Count + "\n\n" +
                    "YOUR SCORE\n" +
                    local.Score;
            }

            int totalScore = 0;
            int totalDefeats = 0;
            int totalDeaths = 0;
            float totalDamageTaken = 0f;
            for (int i = 0; i < rankedEntries.Count; i++)
            {
                totalScore += rankedEntries[i].Score;
                totalDefeats += rankedEntries[i].DefeatedEnemies;
                totalDeaths += rankedEntries[i].DeathCount;
                totalDamageTaken += rankedEntries[i].DamageTaken;
            }

            if (rightInfoTitleText != null)
                rightInfoTitleText.text = "BATTLE DATA";

            if (rightInfoBodyText != null)
            {
                rightInfoBodyText.text =
                    "TOTAL SCORE\n" +
                    totalScore + "\n\n" +
                    "ENEMY KILLS\n" +
                    totalDefeats + "\n\n" +
                    "TOTAL DEATHS\n" +
                    totalDeaths + "\n\n" +
                    "DAMAGE TAKEN\n" +
                    Mathf.RoundToInt(totalDamageTaken);
            }

            SetSideInfoAlpha(0f);
            SetSideInfoVisible(false);
        }

        private IEnumerator FadeSideInfo(float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, duration));
                SetSideInfoAlpha(Mathf.Lerp(from, to, t));
                yield return null;
            }

            SetSideInfoAlpha(to);
        }

        private Text CreateText(string objectName, RectTransform parent, Font font, int size, TextAnchor anchor, Color color)
        {
            GameObject textObject = CreateUiObject(objectName, parent);
            Text text = textObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private GameObject CreateUiObject(string objectName, RectTransform parent)
        {
            GameObject obj = new GameObject(objectName);
            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            return obj;
        }

        private void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private Font GetDefaultFont()
        {
            if (uiFont != null)
                return uiFont;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return font;
        }

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;

            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
        }
    }
}
