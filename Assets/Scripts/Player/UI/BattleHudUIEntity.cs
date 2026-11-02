using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MyGame
{
    [ExecuteAlways]
    public class BattleHudUIEntity : MonoBehaviour
    {
        [SerializeField] private TMP_Text timeText;
        [SerializeField] private TMP_Text rankText;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text hpText;
        [SerializeField] private TMP_Text gaugeText;
        [SerializeField] private TMP_Text weaponText;
        [SerializeField] private TMP_Text ammoText;
        [SerializeField] private TMP_Text lockText;
        [SerializeField] private TMP_Text airTimeText;
        [SerializeField] private TMP_Text buffTitleText;
        [SerializeField] private TMP_Text buffText;
        [SerializeField] private Image attackPowerBuffImage;
        [SerializeField] private Image attackRangeBuffImage;
        [SerializeField] private Image attackSpeedBuffImage;
        [SerializeField] private Image DurabilityBuffImage;
        [SerializeField] private Image moveSpeedBuffImage;
        [SerializeField] private Sprite attackPowerBuffSprite;
        [SerializeField] private Sprite attackRangeBuffSprite;
        [SerializeField] private Sprite attackSpeedBuffSprite;
        [SerializeField] private Sprite durabilityBuffSprite;
        [SerializeField] private Sprite moveSpeedBuffSprite;
        [SerializeField] private CanvasGroup gaugeStockNoticeGroup;
        [SerializeField] private TMP_Text gaugeStockNoticeText;
        [SerializeField] private RectTransform gaugeStockNoticeRoot;
        [SerializeField] private CanvasGroup defeatNoticeGroup;
        [SerializeField] private TMP_Text defeatNoticeText;
        [SerializeField] private RectTransform defeatNoticeRoot;
        [SerializeField] private CanvasGroup pickupNoticeGroup;
        [SerializeField] private TMP_Text pickupNoticeText;
        [SerializeField] private RectTransform pickupNoticeRoot;
        [SerializeField] private CanvasGroup controlGuideGroup;
        [SerializeField] private RectTransform controlGuideRoot;
        [SerializeField] private CanvasGroup lowHealthWarningGroup;
        [SerializeField] private LowHealthEdgeGraphic lowHealthEdgeGraphic;
        [SerializeField] private Image hpFill;
        [SerializeField] private Image airTimeFill;
        [SerializeField] private Image gaugeFill1;
        [SerializeField] private Image gaugeFill2;
        [SerializeField] private Image gaugeFill3;
        [SerializeField] private GameObject airTimePanel;
        [SerializeField] private Color normalTimeColor = Color.white;
        [SerializeField] private Color warningTimeColor = new Color(1f, 0.24f, 0.18f, 1f);
        [SerializeField] private int gaugeStockCount = 3;
        [SerializeField] private float gaugeStockNoticeDuration = 1.45f;
        [SerializeField, Range(0.05f, 0.8f)] private float lowHealthWarningThreshold = 0.35f;
        [SerializeField, Range(0f, 1f)] private float lowHealthWarningMaxAlpha = 0.48f;

        private readonly Dictionary<string, int> m_buffStacks = new();
        private readonly StringBuilder m_buffBuilder = new();
        private readonly List<TMP_Text> m_buffStackTexts = new();
        private int m_previousGaugeStock;
        private bool m_hasGaugeSnapshot;
        private float m_gaugeStockNoticeRemaining;
        private float m_defeatNoticeRemaining;
        private float m_pickupNoticeRemaining;
        private Vector2 m_gaugeStockNoticeBasePosition;
        private Vector2 m_defeatNoticeBasePosition;
        private Vector2 m_pickupNoticeBasePosition;
        private bool m_hasGaugeStockNoticeBasePosition;
        private bool m_hasDefeatNoticeBasePosition;
        private bool m_hasPickupNoticeBasePosition;

#if UNITY_EDITOR
        public void BuildDefaultLayout(Transform parent)
        {
            if (parent == null) return;

            transform.SetParent(parent, false);
            ClearLayoutChildren();

            RectTransform rootRect = GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            GameObject topBar = CreatePanel("TopStatusBar", rootRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(820f, 76f), new Vector2(0f, -58f), new Color(0.04f, 0.05f, 0.08f, 0.82f));
            timeText = CreateText("TimeText", topBar.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(250f, 56f), new Vector2(150f, 0f), 32f, TextAlignmentOptions.Left, Color.white);
            rankText = CreateText("RankText", topBar.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(220f, 56f), Vector2.zero, 30f, TextAlignmentOptions.Center, new Color(1f, 0.86f, 0.18f));
            scoreText = CreateText("ScoreText", topBar.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(270f, 56f), new Vector2(-155f, 0f), 32f, TextAlignmentOptions.Right, Color.white);

            CreateAirTimePanel(rootRect);

            GameObject vitalsPanel = CreatePanel("VitalsPanel", rootRect, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(420f, 150f), new Vector2(236f, -126f), new Color(0.05f, 0.06f, 0.09f, 0.82f));

            hpText = CreateText("HpText", vitalsPanel.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(360f, 30f), new Vector2(210f, -28f), 24f, TextAlignmentOptions.Left, Color.white);
            hpFill = CreateBar("HpBar", vitalsPanel.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(360f, 16f), new Vector2(210f, -56f), new Color(0.15f, 0.86f, 0.36f));

            gaugeText = CreateText("GaugeText", vitalsPanel.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(360f, 30f), new Vector2(210f, -92f), 22f, TextAlignmentOptions.Left, Color.white);
            CreateStockGauge(vitalsPanel.transform, new Vector2(210f, -122f));

            GameObject combatPanel = CreatePanel("CombatStatusPanel", rootRect, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(360f, 124f), new Vector2(-196f, 88f), new Color(0.05f, 0.06f, 0.09f, 0.82f));
            weaponText = CreateText("WeaponText", combatPanel.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(320f, 30f), new Vector2(0f, -28f), 18f, TextAlignmentOptions.Left, Color.white);
            ammoText = CreateText("AmmoText", combatPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(320f, 30f), Vector2.zero, 18f, TextAlignmentOptions.Left, Color.white);
            lockText = CreateText("LockText", combatPanel.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(320f, 30f), new Vector2(0f, 28f), 18f, TextAlignmentOptions.Left, new Color(1f, 0.86f, 0.18f));

            CreateBuffPanel(rootRect);
            CreateLowHealthWarning(rootRect);
            CreateGaugeStockNotice(rootRect);
            CreateDefeatNotice(rootRect);
            CreatePickupNotice(rootRect);
            CreateControlGuide(rootRect);
        }
#endif

        public void EnsureDefaultLayout()
        {
            ResolveReferences();
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
        }

        private void OnValidate()
        {
            if (!Application.isPlaying)
                ResolveReferences();
        }

        private void Update()
        {
            UpdateGaugeStockNotice();
            UpdateDefeatNotice();
            UpdatePickupNotice();
            UpdateControlGuideVisibility();
        }

        private void ClearLayoutChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = transform.GetChild(i).gameObject;

                if (Application.isPlaying)
                    Destroy(child);
                else
                    DestroyImmediate(child);
            }
        }

        public void SetCore(float timeRemaining, int score, int rank, int participantCount)
        {
            ResolveReferences();

            if (timeText != null)
            {
                timeText.text = "残り時間 " + FormatTime(timeRemaining);
                bool warning = timeRemaining > 0f && timeRemaining <= 30f;
                timeText.color = warning ? warningTimeColor : normalTimeColor;
                float pulse = warning && timeRemaining <= 10f
                    ? 1f + Mathf.Sin(Time.unscaledTime * 8f) * 0.08f
                    : 1f;
                timeText.rectTransform.localScale = Vector3.one * pulse;
            }

            if (rankText != null)
                rankText.text = "順位 " + rank + "/" + participantCount;

            if (scoreText != null)
                scoreText.text = "スコア " + score.ToString("0000");
        }

        public void SetVitals(float currentHp, float maxHp, float currentGauge, float maxGauge, bool isAirMode)
        {
            ResolveReferences();

            float hpRate = maxHp > 0f ? Mathf.Clamp01(currentHp / maxHp) : 0f;
            float gaugeRate = maxGauge > 0f ? Mathf.Clamp01(currentGauge / maxGauge) : 0f;

            if (hpText != null)
                hpText.text = "HP " + Mathf.CeilToInt(currentHp) + "/" + Mathf.CeilToInt(maxHp);

            if (gaugeText != null)
                gaugeText.text = (isAirMode ? "飛行中 " : "飛行ゲージ ") + Mathf.RoundToInt(gaugeRate * 100f) + "%";

            if (hpFill != null)
                SetRectFill(hpFill, hpRate);

            SetStockFill(gaugeFill1, currentGauge, 0f);
            SetStockFill(gaugeFill2, currentGauge, 100f);
            SetStockFill(gaugeFill3, currentGauge, 200f);
            UpdateGaugeStockSnapshot(currentGauge, maxGauge);
            UpdateLowHealthWarning(hpRate);
        }

        public void SetAirTime(float remainingTime, float maxTime, bool isAirMode)
        {
            ResolveReferences();

            bool shouldShow = isAirMode && maxTime > 0f;
            if (airTimePanel != null && airTimePanel.activeSelf != shouldShow)
                airTimePanel.SetActive(shouldShow);

            if (!shouldShow)
                return;

            float rate = Mathf.Clamp01(remainingTime / maxTime);

            if (airTimeText != null)
                airTimeText.text = "飛行時間  " + Mathf.CeilToInt(Mathf.Max(0f, remainingTime)).ToString() + "秒";

            if (airTimeFill != null)
                SetRectFill(airTimeFill, rate);
        }

        public void SetCombat(string weaponName, string weaponTime, bool hasLockTarget)
        {
            ResolveReferences();

            if (weaponText != null)
                weaponText.text = "武器  " + weaponName;

            if (ammoText != null)
                ammoText.text = "残り  " + weaponTime;

            if (lockText != null)
            {
                lockText.text = hasLockTarget ? "ロック中" : "未ロック";
                lockText.color = hasLockTarget ? new Color(1f, 0.24f, 0.18f) : new Color(0.78f, 0.83f, 0.92f);
            }
        }

        public void SetBuffs(BuffManagerEntity buffManager)
        {
            ResolveReferences();
            EnsureBuffIconLayout();

            if (buffText == null) return;

            if (buffManager == null || buffManager.GetTotalStackCount() <= 0)
            {
                buffText.text = "なし";
                buffText.color = new Color(0.78f, 0.83f, 0.92f);
                SetKnownBuffIconStacks(0, 0, 0, 0, 0);
                return;
            }

            buffManager.GetAllBuffStackCounts(m_buffStacks);
            SetKnownBuffIconStacks(
                GetBuffStack("moveSpeed"),
                GetBuffStack("attackPower"),
                GetBuffStack("attackRange"),
                GetBuffStack("attackSpeed"),
                GetBuffStack("durability")
            );
            m_buffBuilder.Clear();

            AppendBuffLine("moveSpeed", "速度", "#4DDCFF");
            AppendBuffLine("attackPower", "火力", "#FF5A54");
            AppendBuffLine("attackRange", "射程", "#FFD64A");
            AppendBuffLine("attackSpeed", "連射", "#B98BFF");
            AppendBuffLine("durability", "耐久", "#6AF08A");

            foreach (KeyValuePair<string, int> stack in m_buffStacks)
            {
                if (IsKnownBuff(stack.Key)) continue;
                AppendRawBuffLine(stack.Key, stack.Value, "#DCE6F5");
            }

            buffText.text = m_buffBuilder.Length > 0 ? m_buffBuilder.ToString() : "なし";
            buffText.color = Color.white;
        }

        private int GetBuffStack(string buffId)
        {
            return m_buffStacks.TryGetValue(buffId, out int stackCount) ? stackCount : 0;
        }

        private void SetKnownBuffIconStacks(int moveSpeed, int attackPower, int attackRange, int attackSpeed, int durability)
        {
            SetBuffIconStack(moveSpeedBuffImage, GetBuffStackText(0), moveSpeed);
            SetBuffIconStack(attackPowerBuffImage, GetBuffStackText(1), attackPower);
            SetBuffIconStack(attackRangeBuffImage, GetBuffStackText(2), attackRange);
            SetBuffIconStack(attackSpeedBuffImage, GetBuffStackText(3), attackSpeed);
            SetBuffIconStack(DurabilityBuffImage, GetBuffStackText(4), durability);
        }

        private TMP_Text GetBuffStackText(int index)
        {
            return index >= 0 && index < m_buffStackTexts.Count ? m_buffStackTexts[index] : null;
        }

        private void SetBuffIconStack(Image image, TMP_Text stackText, int stackCount)
        {
            if (image == null) return;

            image.gameObject.SetActive(stackCount > 0);

            if (stackText != null)
                stackText.text = "x" + stackCount;
         }
        public void ShowDefeatNotice(int count = 1)
        {
            ResolveReferences();

            string message = count > 1
                ? "TARGET DOWN x" + count
                : "TARGET DOWN";
            ShowNotice(
                defeatNoticeGroup,
                defeatNoticeText,
                defeatNoticeRoot,
                ref m_defeatNoticeRemaining,
                ref m_defeatNoticeBasePosition,
                ref m_hasDefeatNoticeBasePosition,
                message,
                new Color(1f, 0.22f, 0.12f, 1f));
        }

        public void ShowNoGaugeNotice()
        {
            ResolveReferences();

            ShowNotice(
                gaugeStockNoticeGroup,
                gaugeStockNoticeText,
                gaugeStockNoticeRoot,
                ref m_gaugeStockNoticeRemaining,
                ref m_gaugeStockNoticeBasePosition,
                ref m_hasGaugeStockNoticeBasePosition,
                "NO GAUGE",
                new Color(1f, 0.24f, 0.16f, 1f));
        }

        public static void ShowPickupNoticeFor(PlayerEntity player, string category, string itemName)
        {
            if (player == null || !player.CanControl)
                return;

            BattleHudUIEntity hud = FindAnyObjectByType<BattleHudUIEntity>();
            hud?.ShowPickupNotice(category, itemName);
        }

        public void ShowPickupNotice(string category, string itemName)
        {
            ResolveReferences();

            string safeCategory = string.IsNullOrWhiteSpace(category) ? "アイテム" : category;
            string safeItemName = string.IsNullOrWhiteSpace(itemName) ? "取得" : itemName;
            ShowNotice(
                pickupNoticeGroup,
                pickupNoticeText,
                pickupNoticeRoot,
                ref m_pickupNoticeRemaining,
                ref m_pickupNoticeBasePosition,
                ref m_hasPickupNoticeBasePosition,
                safeCategory + "取得  " + safeItemName,
                new Color(0.58f, 1f, 0.68f, 1f));
        }

        private string FormatTime(float time)
        {
            int totalSeconds = Mathf.CeilToInt(Mathf.Max(0f, time));
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            return minutes.ToString("00") + ":" + seconds.ToString("00");
        }

        private void ResolveReferences()
        {
            RectTransform rootRect = GetComponent<RectTransform>();

            if (timeText == null) timeText = FindChildComponent<TMP_Text>("TimeText");
            if (rankText == null) rankText = FindChildComponent<TMP_Text>("RankText");
            if (scoreText == null) scoreText = FindChildComponent<TMP_Text>("ScoreText");
            if (hpText == null) hpText = FindChildComponent<TMP_Text>("HpText");
            if (gaugeText == null) gaugeText = FindChildComponent<TMP_Text>("GaugeText");
            if (weaponText == null) weaponText = FindChildComponent<TMP_Text>("WeaponText");
            if (ammoText == null) ammoText = FindChildComponent<TMP_Text>("AmmoText");
            if (lockText == null) lockText = FindChildComponent<TMP_Text>("LockText");
            if (airTimeText == null) airTimeText = FindChildComponent<TMP_Text>("AirTimeText");
            if (buffTitleText == null) buffTitleText = FindChildComponent<TMP_Text>("BuffTitleText");
            if (buffText == null) buffText = FindChildComponent<TMP_Text>("BuffListText");
            if (attackPowerBuffImage == null) attackPowerBuffImage = FindChildComponent<Image>("AttackPowerBuffImage");
            if (attackRangeBuffImage == null) attackRangeBuffImage = FindChildComponent<Image>("AttackRangeBuffImage");
            if (attackSpeedBuffImage == null) attackSpeedBuffImage = FindChildComponent<Image>("AttackSpeedBuffImage");
            if (DurabilityBuffImage == null) DurabilityBuffImage = FindChildComponent<Image>("DurabilityBuffImage");
            if (moveSpeedBuffImage == null) moveSpeedBuffImage = FindChildComponent<Image>("MoveSpeedBuffImage");
            if (hpFill == null) hpFill = FindChildComponent<Image>("HpBarFill");
            if (airTimeFill == null) airTimeFill = FindChildComponent<Image>("AirTimeBarFill");
            if (gaugeFill1 == null) gaugeFill1 = FindChildComponent<Image>("AirGaugeStock1Fill");
            if (gaugeFill2 == null) gaugeFill2 = FindChildComponent<Image>("AirGaugeStock2Fill");
            if (gaugeFill3 == null) gaugeFill3 = FindChildComponent<Image>("AirGaugeStock3Fill");

            if (airTimePanel == null)
            {
                RectTransform airTimePanelRect = FindChildComponent<RectTransform>("AirTimePanel");
                if (airTimePanelRect != null)
                    airTimePanel = airTimePanelRect.gameObject;
            }

            if (gaugeStockNoticeRoot == null)
                gaugeStockNoticeRoot = FindChildComponent<RectTransform>("GaugeStockNotice");
            if (gaugeStockNoticeGroup == null && gaugeStockNoticeRoot != null)
                gaugeStockNoticeGroup = gaugeStockNoticeRoot.GetComponent<CanvasGroup>();
            if (gaugeStockNoticeText == null)
                gaugeStockNoticeText = FindChildComponent<TMP_Text>("GaugeStockNoticeText");
            if (gaugeStockNoticeRoot == null && rootRect != null)
                CreateGaugeStockNotice(rootRect);
            if (defeatNoticeRoot == null)
                defeatNoticeRoot = FindChildComponent<RectTransform>("DefeatNotice");
            if (defeatNoticeRoot == null && rootRect != null)
                CreateDefeatNotice(rootRect);
            if (defeatNoticeGroup == null && defeatNoticeRoot != null)
                defeatNoticeGroup = defeatNoticeRoot.GetComponent<CanvasGroup>();
            if (defeatNoticeText == null)
                defeatNoticeText = FindChildComponent<TMP_Text>("DefeatNoticeText");
            if (pickupNoticeRoot == null)
                pickupNoticeRoot = FindChildComponent<RectTransform>("PickupNotice");
            if (pickupNoticeRoot == null && rootRect != null)
                CreatePickupNotice(rootRect);
            if (pickupNoticeGroup == null && pickupNoticeRoot != null)
                pickupNoticeGroup = pickupNoticeRoot.GetComponent<CanvasGroup>();
            if (pickupNoticeText == null)
                pickupNoticeText = FindChildComponent<TMP_Text>("PickupNoticeText");
            if (controlGuideRoot == null)
                controlGuideRoot = FindChildComponent<RectTransform>("ControlGuidePanel");
            if (controlGuideRoot == null && rootRect != null)
                CreateControlGuide(rootRect);
            if (controlGuideGroup == null && controlGuideRoot != null)
                controlGuideGroup = controlGuideRoot.GetComponent<CanvasGroup>();
            if (lowHealthWarningGroup == null)
            {
                RectTransform lowHealthRoot = FindChildComponent<RectTransform>("LowHealthWarning");
                if (lowHealthRoot != null)
                    lowHealthWarningGroup = lowHealthRoot.GetComponent<CanvasGroup>();
            }
            if (lowHealthWarningGroup == null && rootRect != null)
                CreateLowHealthWarning(rootRect);
            if (lowHealthEdgeGraphic == null)
                lowHealthEdgeGraphic = FindChildComponent<LowHealthEdgeGraphic>("LowHealthEdge");

            ConfigureNoticePanel(gaugeStockNoticeRoot, new Color(0.02f, 0.04f, 0.08f, 0.58f), 52f);
            ConfigureNoticePanel(defeatNoticeRoot, new Color(0.08f, 0.02f, 0.02f, 0.58f), 52f);
            ConfigureNoticePanel(pickupNoticeRoot, new Color(0.02f, 0.08f, 0.04f, 0.58f), 52f);
            ConfigureControlGuide();
            ConfigureLowHealthWarning();

            if (gaugeStockNoticeRoot != null && !m_hasGaugeStockNoticeBasePosition)
            {
                m_gaugeStockNoticeBasePosition = gaugeStockNoticeRoot.anchoredPosition;
                m_hasGaugeStockNoticeBasePosition = true;
            }
            if (defeatNoticeRoot != null && !m_hasDefeatNoticeBasePosition)
            {
                m_defeatNoticeBasePosition = defeatNoticeRoot.anchoredPosition;
                m_hasDefeatNoticeBasePosition = true;
            }
            if (pickupNoticeRoot != null && !m_hasPickupNoticeBasePosition)
            {
                m_pickupNoticeBasePosition = pickupNoticeRoot.anchoredPosition;
                m_hasPickupNoticeBasePosition = true;
            }

        }

        private void EnsureBuffIconLayout()
        {
            if (attackPowerBuffImage != null
                && attackRangeBuffImage != null
                && attackSpeedBuffImage != null
                && DurabilityBuffImage != null
                && moveSpeedBuffImage != null)
            {
                ConfigureBuffIcon(attackPowerBuffImage, attackPowerBuffSprite);
                ConfigureBuffIcon(attackRangeBuffImage, attackRangeBuffSprite);
                ConfigureBuffIcon(attackSpeedBuffImage, attackSpeedBuffSprite);
                ConfigureBuffIcon(DurabilityBuffImage, durabilityBuffSprite);
                ConfigureBuffIcon(moveSpeedBuffImage, moveSpeedBuffSprite);
                CacheBuffStackTexts();
                return;
            }

            RectTransform buffPanel = FindChildComponent<RectTransform>("BuffStatusPanel");
            if (buffPanel == null) return;

            if (buffText != null)
                buffText.gameObject.SetActive(false);

            float startX = -128f;
            float gap = 64f;
            float y = -76f;

            moveSpeedBuffImage = CreateBuffIcon("MoveSpeedBuffImage", buffPanel, moveSpeedBuffSprite, new Vector2(startX + gap * 0f, y));
            attackPowerBuffImage = CreateBuffIcon("AttackPowerBuffImage", buffPanel, attackPowerBuffSprite, new Vector2(startX + gap * 1f, y));
            attackRangeBuffImage = CreateBuffIcon("AttackRangeBuffImage", buffPanel, attackRangeBuffSprite, new Vector2(startX + gap * 2f, y));
            attackSpeedBuffImage = CreateBuffIcon("AttackSpeedBuffImage", buffPanel, attackSpeedBuffSprite, new Vector2(startX + gap * 3f, y));
            DurabilityBuffImage = CreateBuffIcon("DurabilityBuffImage", buffPanel, durabilityBuffSprite, new Vector2(startX + gap * 4f, y));

            CacheBuffStackTexts();
        }

        private Image CreateBuffIcon(string name, Transform parent, Sprite sprite, Vector2 position)
        {
            GameObject iconObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(parent, false);

            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 1f);
            iconRect.anchorMax = new Vector2(0.5f, 1f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(44f, 44f);
            iconRect.anchoredPosition = position;

            Image image = iconObject.GetComponent<Image>();
            ConfigureBuffIcon(image, sprite);

            TMP_Text stackText = CreateText(name + "StackText", iconObject.transform, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(34f, 20f), new Vector2(10f, -2f), 16f, TextAlignmentOptions.Center, Color.white);
            stackText.fontStyle = FontStyles.Bold;

            iconObject.SetActive(false);
            return image;
        }

        private void ConfigureBuffIcon(Image image, Sprite sprite)
        {
            if (image == null) return;

            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        private void CacheBuffStackTexts()
        {
            m_buffStackTexts.Clear();
            AddBuffStackText(moveSpeedBuffImage);
            AddBuffStackText(attackPowerBuffImage);
            AddBuffStackText(attackRangeBuffImage);
            AddBuffStackText(attackSpeedBuffImage);
            AddBuffStackText(DurabilityBuffImage);
        }

        private void AddBuffStackText(Image image)
        {
            if (image == null)
            {
                m_buffStackTexts.Add(null);
                return;
            }

            TMP_Text text = image.GetComponentInChildren<TMP_Text>(true);
            m_buffStackTexts.Add(text);
        }

        private T FindChildComponent<T>(string objectName) where T : UnityEngine.Component
        {
            T[] components = GetComponentsInChildren<T>(true);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null && components[i].gameObject.name == objectName)
                    return components[i];
            }

            return null;
        }

        private GameObject CreatePanel(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 size,
            Vector2 position,
            Color color)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);

            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            Image image = panel.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            return panel;
        }

        private TMP_Text CreateText(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 size,
            Vector2 position,
            float fontSize,
            TextAlignmentOptions alignment,
            Color color)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);

            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.alignment = alignment;
            text.fontSize = fontSize;
            text.enableAutoSizing = true;
            text.fontSizeMin = 14f;
            text.fontSizeMax = fontSize;
            text.color = color;
            text.fontStyle = FontStyles.Bold;
            text.raycastTarget = false;

            return text;
        }

        private Image CreateBar(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 size,
            Vector2 position,
            Color fillColor)
        {
            GameObject bg = CreatePanel(name + "BG", parent, anchorMin, anchorMax, size, position, new Color(1f, 1f, 1f, 0.15f));
            GameObject fill = new GameObject(name + "Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(bg.transform, false);

            RectTransform fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            Image image = fill.GetComponent<Image>();
            image.color = fillColor;
            image.type = Image.Type.Simple;
            image.raycastTarget = false;

            return image;
        }

        private void CreateStockGauge(Transform parent, Vector2 position)
        {
            GameObject gaugeBar = CreatePanel(
                "AirGaugeBar",
                parent,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(360f, 18f),
                position,
                new Color(0.04f, 0.09f, 0.14f, 0.92f)
            );

            gaugeFill1 = CreateOverlayStockFill("AirGaugeStock1", gaugeBar.transform, new Color(1f, 0.18f, 0.16f, 1f));
            gaugeFill2 = CreateOverlayStockFill("AirGaugeStock2", gaugeBar.transform, new Color(1f, 0.78f, 0.16f, 1f));
            gaugeFill3 = CreateOverlayStockFill("AirGaugeStock3", gaugeBar.transform, new Color(0.24f, 0.72f, 1f, 1f));
        }

        private void CreateAirTimePanel(RectTransform rootRect)
        {
            if (rootRect == null) return;

            RectTransform existing = FindChildComponent<RectTransform>("AirTimePanel");
            if (existing != null)
            {
                airTimePanel = existing.gameObject;
                return;
            }

            airTimePanel = CreatePanel("AirTimePanel", rootRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(360f, 42f), new Vector2(0f, -112f), new Color(0.05f, 0.06f, 0.09f, 0.82f));

            airTimeText = CreateText("AirTimeText", airTimePanel.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(320f, 20f), new Vector2(0f, -13f), 16f, TextAlignmentOptions.Center, Color.white);

            airTimeFill = CreateBar("AirTimeBar", airTimePanel.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(320f, 10f), new Vector2(0f, 11f), new Color(0.24f, 0.72f, 1f, 1f));

            airTimeText.text = "飛行時間  --";
            airTimePanel.SetActive(false);
        }

        private void CreateBuffPanel(RectTransform rootRect)
        {
            if (rootRect == null) return;
            if (FindChildComponent<RectTransform>("BuffStatusPanel") != null) return;

            GameObject buffPanel = CreatePanel("BuffStatusPanel", rootRect, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(360f, 128f), new Vector2(-196f, -126f), new Color(0.05f, 0.06f, 0.09f, 0.82f));

            buffTitleText = CreateText("BuffTitleText", buffPanel.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(320f, 28f), new Vector2(0f, -26f), 18f, TextAlignmentOptions.Left, new Color(1f, 0.86f, 0.18f));
            buffText = CreateText("BuffListText", buffPanel.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(320f, 78f), new Vector2(0f, 46f), 18f, TextAlignmentOptions.TopLeft, new Color(0.78f, 0.83f, 0.92f));

            buffTitleText.text = "バフ";
            buffText.text = "なし";
        }

        private void CreateGaugeStockNotice(RectTransform rootRect)
        {
            if (rootRect == null) return;
            if (FindChildComponent<RectTransform>("GaugeStockNotice") != null) return;

            GameObject notice = CreatePanel("GaugeStockNotice", rootRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(500f, 88f), new Vector2(0f, 246f), new Color(0.02f, 0.04f, 0.08f, 0.58f));
            gaugeStockNoticeRoot = notice.GetComponent<RectTransform>();
            gaugeStockNoticeGroup = notice.AddComponent<CanvasGroup>();
            gaugeStockNoticeGroup.alpha = 0f;
            gaugeStockNoticeGroup.blocksRaycasts = false;
            gaugeStockNoticeGroup.interactable = false;
            ConfigureNoticePanel(gaugeStockNoticeRoot, new Color(0.02f, 0.04f, 0.08f, 0.58f), 52f);

            gaugeStockNoticeText = CreateText("GaugeStockNoticeText", notice.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(460f, 64f), Vector2.zero, 38f, TextAlignmentOptions.Center, new Color(0.24f, 0.92f, 1f, 1f));
            gaugeStockNoticeText.text = "GAUGE READY";
        }

        private void CreateDefeatNotice(RectTransform rootRect)
        {
            if (rootRect == null) return;
            if (FindChildComponent<RectTransform>("DefeatNotice") != null) return;

            GameObject notice = CreatePanel("DefeatNotice", rootRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(540f, 92f), new Vector2(0f, 144f), new Color(0.08f, 0.02f, 0.02f, 0.58f));
            defeatNoticeRoot = notice.GetComponent<RectTransform>();
            defeatNoticeGroup = notice.AddComponent<CanvasGroup>();
            defeatNoticeGroup.alpha = 0f;
            defeatNoticeGroup.blocksRaycasts = false;
            defeatNoticeGroup.interactable = false;
            ConfigureNoticePanel(defeatNoticeRoot, new Color(0.08f, 0.02f, 0.02f, 0.58f), 52f);

            defeatNoticeText = CreateText("DefeatNoticeText", notice.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(500f, 68f), Vector2.zero, 40f, TextAlignmentOptions.Center, new Color(1f, 0.22f, 0.12f, 1f));
            defeatNoticeText.text = "TARGET DOWN";
        }

        private void CreatePickupNotice(RectTransform rootRect)
        {
            if (rootRect == null) return;
            if (FindChildComponent<RectTransform>("PickupNotice") != null) return;

            GameObject notice = CreatePanel("PickupNotice", rootRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(520f, 78f), new Vector2(0f, 72f), new Color(0.02f, 0.08f, 0.04f, 0.58f));
            pickupNoticeRoot = notice.GetComponent<RectTransform>();
            pickupNoticeGroup = notice.AddComponent<CanvasGroup>();
            pickupNoticeGroup.alpha = 0f;
            pickupNoticeGroup.blocksRaycasts = false;
            pickupNoticeGroup.interactable = false;
            ConfigureNoticePanel(pickupNoticeRoot, new Color(0.02f, 0.08f, 0.04f, 0.58f), 52f);

            pickupNoticeText = CreateText("PickupNoticeText", notice.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(480f, 58f), Vector2.zero, 32f, TextAlignmentOptions.Center, new Color(0.58f, 1f, 0.68f, 1f));
            pickupNoticeText.text = "アイテム取得";
        }

        private void CreateControlGuide(RectTransform rootRect)
        {
            if (rootRect == null) return;
            if (FindChildComponent<RectTransform>("ControlGuidePanel") != null) return;

            GameObject panel = CreatePanel(
                "ControlGuidePanel",
                rootRect,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(320f, 220f),
                new Vector2(-178f, -30f),
                new Color(0.03f, 0.04f, 0.07f, 0.72f));
            controlGuideRoot = panel.GetComponent<RectTransform>();
            controlGuideGroup = panel.AddComponent<CanvasGroup>();
            controlGuideGroup.blocksRaycasts = false;
            controlGuideGroup.interactable = false;

            CreateText(
                "ControlGuideTitle",
                panel.transform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(280f, 30f),
                new Vector2(0f, -24f),
                20f,
                TextAlignmentOptions.Left,
                new Color(0.78f, 0.94f, 1f, 1f)).text = "操作";

            CreateKeyBox(panel.transform, "KeyW", "W", new Vector2(-92f, 54f), new Vector2(46f, 36f));
            CreateKeyBox(panel.transform, "KeyA", "A", new Vector2(-140f, 14f), new Vector2(46f, 36f));
            CreateKeyBox(panel.transform, "KeyS", "S", new Vector2(-92f, 14f), new Vector2(46f, 36f));
            CreateKeyBox(panel.transform, "KeyD", "D", new Vector2(-44f, 14f), new Vector2(46f, 36f));
            CreateText(
                "MoveGuideText",
                panel.transform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(118f, 38f),
                new Vector2(82f, 34f),
                20f,
                TextAlignmentOptions.Left,
                Color.white).text = "移動";

            CreateMouseButton(panel.transform, "LeftClickKey", "左", new Vector2(-105f, -38f));
            CreateText(
                "ShootGuideText",
                panel.transform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(118f, 38f),
                new Vector2(82f, -38f),
                20f,
                TextAlignmentOptions.Left,
                Color.white).text = "射撃";

            CreateKeyBox(panel.transform, "SpaceKey", "SPACE", new Vector2(-92f, -82f), new Vector2(142f, 36f));
            CreateText(
                "AirModeGuideText",
                panel.transform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(118f, 38f),
                new Vector2(82f, -82f),
                20f,
                TextAlignmentOptions.Left,
                Color.white).text = "空モード";

            ConfigureControlGuide();
        }

        private void CreateKeyBox(
            Transform parent,
            string name,
            string label,
            Vector2 position,
            Vector2 size)
        {
            GameObject key = CreatePanel(
                name,
                parent,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                size,
                position,
                new Color(0.92f, 0.96f, 1f, 0.16f));

            Outline outline = key.AddComponent<Outline>();
            outline.effectColor = new Color(0.65f, 0.9f, 1f, 0.72f);
            outline.effectDistance = new Vector2(1.4f, -1.4f);

            CreateText(
                name + "Text",
                key.transform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                size,
                Vector2.zero,
                18f,
                TextAlignmentOptions.Center,
                Color.white).text = label;
        }

        private void CreateMouseButton(
            Transform parent,
            string name,
            string label,
            Vector2 position)
        {
            GameObject mouse = CreatePanel(
                name,
                parent,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(72f, 38f),
                position,
                new Color(0.92f, 0.96f, 1f, 0.16f));

            Outline outline = mouse.AddComponent<Outline>();
            outline.effectColor = new Color(0.65f, 0.9f, 1f, 0.72f);
            outline.effectDistance = new Vector2(1.4f, -1.4f);

            Image divider = CreateBar(
                name + "Divider",
                mouse.transform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(2f, 30f),
                new Vector2(0f, -20f),
                new Color(0.65f, 0.9f, 1f, 0.8f));
            if (divider != null && divider.transform.parent != null)
                divider.transform.parent.GetComponent<Image>().color = Color.clear;

            CreateText(
                name + "Text",
                mouse.transform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(35f, 34f),
                new Vector2(18f, 0f),
                16f,
                TextAlignmentOptions.Center,
                Color.white).text = label;
        }

        private void ConfigureControlGuide()
        {
            if (controlGuideRoot == null)
                return;

            Image image = controlGuideRoot.GetComponent<Image>();
            if (image != null)
            {
                image.color = new Color(0.03f, 0.04f, 0.07f, 0.72f);
                image.raycastTarget = false;
            }

            controlGuideRoot.anchorMin = new Vector2(1f, 0.5f);
            controlGuideRoot.anchorMax = new Vector2(1f, 0.5f);
            controlGuideRoot.pivot = new Vector2(0.5f, 0.5f);
            controlGuideRoot.sizeDelta = new Vector2(320f, 220f);
            controlGuideRoot.anchoredPosition = new Vector2(-178f, -30f);
        }

        private void UpdateControlGuideVisibility()
        {
            if (controlGuideGroup == null)
                return;

            bool show = OptionManager.IsControlGuideEnabled;
            controlGuideGroup.alpha = show ? 1f : 0f;
        }


        private void CreateLowHealthWarning(RectTransform rootRect)
        {
            if (rootRect == null) return;
            if (FindChildComponent<RectTransform>("LowHealthWarning") != null) return;

            GameObject root = new GameObject("LowHealthWarning", typeof(RectTransform), typeof(CanvasGroup));
            root.transform.SetParent(rootRect, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            lowHealthWarningGroup = root.GetComponent<CanvasGroup>();
            lowHealthWarningGroup.alpha = 0f;
            lowHealthWarningGroup.blocksRaycasts = false;
            lowHealthWarningGroup.interactable = false;

            GameObject edge = new GameObject("LowHealthEdge", typeof(RectTransform), typeof(LowHealthEdgeGraphic));
            edge.transform.SetParent(rect, false);
            RectTransform edgeRect = edge.GetComponent<RectTransform>();
            edgeRect.anchorMin = Vector2.zero;
            edgeRect.anchorMax = Vector2.one;
            edgeRect.offsetMin = Vector2.zero;
            edgeRect.offsetMax = Vector2.zero;

            lowHealthEdgeGraphic = edge.GetComponent<LowHealthEdgeGraphic>();
            lowHealthEdgeGraphic.color = new Color(1f, 0.02f, 0.02f, 0.78f);
            lowHealthEdgeGraphic.raycastTarget = false;
        }

        private void UpdateLowHealthWarning(float hpRate)
        {
            ResolveReferences();
            if (lowHealthWarningGroup == null)
                return;

            float safeThreshold = Mathf.Max(0.45f, lowHealthWarningThreshold);
            float dangerRate = Mathf.Clamp01((safeThreshold - hpRate) / safeThreshold);
            if (dangerRate <= 0f)
            {
                lowHealthWarningGroup.alpha = 0f;
                return;
            }

            float pulse = 0.78f + Mathf.Sin(Time.unscaledTime * 5.5f) * 0.22f;
            float maxAlpha = Mathf.Max(0.78f, lowHealthWarningMaxAlpha);
            lowHealthWarningGroup.alpha =
                Mathf.Clamp01(dangerRate * maxAlpha * pulse);
        }

        private void UpdateGaugeStockSnapshot(float currentGauge, float maxGauge)
        {
            int stock = CalculateGaugeStock(currentGauge, maxGauge);

            if (!m_hasGaugeSnapshot)
            {
                m_previousGaugeStock = stock;
                m_hasGaugeSnapshot = true;
                return;
            }

            if (stock > m_previousGaugeStock)
                ShowGaugeStockNotice(stock);

            m_previousGaugeStock = stock;
        }

        private int CalculateGaugeStock(float currentGauge, float maxGauge)
        {
            int stockCount = Mathf.Max(1, gaugeStockCount);
            float perStock = maxGauge > 0f ? maxGauge / stockCount : 100f;
            if (perStock <= 0f) return 0;
            return Mathf.Clamp(Mathf.FloorToInt(currentGauge / perStock), 0, stockCount);
        }

        private void ShowGaugeStockNotice(int stock)
        {
            ResolveReferences();

            int clampedStock = Mathf.Clamp(stock, 1, Mathf.Max(1, gaugeStockCount));
            ShowNotice(
                gaugeStockNoticeGroup,
                gaugeStockNoticeText,
                gaugeStockNoticeRoot,
                ref m_gaugeStockNoticeRemaining,
                ref m_gaugeStockNoticeBasePosition,
                ref m_hasGaugeStockNoticeBasePosition,
                "GAUGE " + clampedStock + " READY",
                new Color(0.24f, 0.92f, 1f, 1f));
        }

        private void ShowNotice(
            CanvasGroup group,
            TMP_Text text,
            RectTransform root,
            ref float remaining,
            ref Vector2 basePosition,
            ref bool hasBasePosition,
            string message,
            Color color)
        {
            ResolveReferences();
            if (group == null || text == null)
                return;

            text.text = message;
            text.color = color;
            remaining = Mathf.Max(0.1f, gaugeStockNoticeDuration);

            if (root != null)
            {
                if (!hasBasePosition)
                {
                    basePosition = root.anchoredPosition;
                    hasBasePosition = true;
                }

                root.anchoredPosition = basePosition;
                root.localScale = Vector3.one * 1.08f;
            }

            group.alpha = 1f;
        }

        private void UpdateGaugeStockNotice()
        {
            UpdateNotice(
                gaugeStockNoticeGroup,
                gaugeStockNoticeRoot,
                ref m_gaugeStockNoticeRemaining,
                m_gaugeStockNoticeBasePosition,
                gaugeStockNoticeDuration,
                22f);
        }

        private void UpdateDefeatNotice()
        {
            UpdateNotice(
                defeatNoticeGroup,
                defeatNoticeRoot,
                ref m_defeatNoticeRemaining,
                m_defeatNoticeBasePosition,
                gaugeStockNoticeDuration,
                30f);
        }

        private void UpdatePickupNotice()
        {
            UpdateNotice(
                pickupNoticeGroup,
                pickupNoticeRoot,
                ref m_pickupNoticeRemaining,
                m_pickupNoticeBasePosition,
                gaugeStockNoticeDuration,
                24f);
        }

        private void UpdateNotice(
            CanvasGroup group,
            RectTransform root,
            ref float remaining,
            Vector2 basePosition,
            float noticeDuration,
            float riseAmount)
        {
            if (group == null)
                return;

            if (remaining <= 0f)
            {
                group.alpha = 0f;
                return;
            }

            remaining = Mathf.Max(0f, remaining - Time.unscaledDeltaTime);
            float duration = Mathf.Max(0.1f, noticeDuration);
            float normalized = 1f - Mathf.Clamp01(remaining / duration);

            float fadeIn = Mathf.Clamp01(normalized / 0.12f);
            float fadeOut = Mathf.Clamp01(remaining / 0.35f);
            group.alpha = Mathf.Min(fadeIn, fadeOut);

            if (root != null)
            {
                float rise = Mathf.SmoothStep(0f, riseAmount, normalized);
                float scale = Mathf.Lerp(1.08f, 1f, Mathf.Clamp01(normalized / 0.24f));
                root.anchoredPosition = basePosition + new Vector2(0f, rise);
                root.localScale = Vector3.one * scale;
            }
        }

        private void ConfigureNoticePanel(RectTransform root, Color panelColor, float slant)
        {
            if (root == null)
                return;

            Image image = root.GetComponent<Image>();
            if (image != null)
            {
                image.enabled = false;
                image.raycastTarget = false;
            }

            RectTransform panelRoot = FindDirectChildRect(root, "SlantedNoticePanel");
            if (panelRoot == null)
            {
                GameObject panelObject = new GameObject(
                    "SlantedNoticePanel",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(SlantedPanelGraphic));
                panelObject.transform.SetParent(root, false);
                panelRoot = panelObject.GetComponent<RectTransform>();
            }

            panelRoot.gameObject.SetActive(true);
            panelRoot.anchorMin = Vector2.zero;
            panelRoot.anchorMax = Vector2.one;
            panelRoot.offsetMin = Vector2.zero;
            panelRoot.offsetMax = Vector2.zero;
            panelRoot.localScale = Vector3.one;
            panelRoot.SetAsFirstSibling();

            CanvasRenderer canvasRenderer = panelRoot.GetComponent<CanvasRenderer>();
            if (canvasRenderer == null)
                panelRoot.gameObject.AddComponent<CanvasRenderer>();

            SlantedPanelGraphic panel = panelRoot.GetComponent<SlantedPanelGraphic>();
            if (panel == null)
                panel = panelRoot.gameObject.AddComponent<SlantedPanelGraphic>();

            if (panel == null)
            {
                if (image != null)
                {
                    image.enabled = true;
                    image.color = new Color(panelColor.r, panelColor.g, panelColor.b, Mathf.Max(panelColor.a * 0.55f, 0.28f));
                    image.raycastTarget = false;
                }

                return;
            }

            panel.color = panelColor;
            panel.Slant = slant;
            panel.enabled = true;
            panel.raycastTarget = false;
            panel.SetAllDirty();
        }

        private RectTransform FindDirectChildRect(RectTransform parent, string childName)
        {
            if (parent == null)
                return null;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child != null && child.name == childName)
                    return child as RectTransform;
            }

            return null;
        }

        private void ConfigureLowHealthWarning()
        {
            if (lowHealthWarningGroup == null)
                return;

            RectTransform root = lowHealthWarningGroup.GetComponent<RectTransform>();
            if (root == null)
                return;

            root.SetAsLastSibling();

            Image[] oldImages = root.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < oldImages.Length; i++)
            {
                if (oldImages[i] != null && oldImages[i].GetComponent<LowHealthEdgeGraphic>() == null)
                {
                    oldImages[i].enabled = false;
                    oldImages[i].raycastTarget = false;
                }
            }

            if (lowHealthEdgeGraphic == null)
            {
                GameObject edge = new GameObject(
                    "LowHealthEdge",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(LowHealthEdgeGraphic));
                edge.transform.SetParent(root, false);
                RectTransform edgeRect = edge.GetComponent<RectTransform>();
                edgeRect.anchorMin = Vector2.zero;
                edgeRect.anchorMax = Vector2.one;
                edgeRect.offsetMin = Vector2.zero;
                edgeRect.offsetMax = Vector2.zero;
                lowHealthEdgeGraphic = edge.GetComponent<LowHealthEdgeGraphic>();
            }

            if (lowHealthEdgeGraphic != null)
            {
                lowHealthEdgeGraphic.color = new Color(1f, 0.02f, 0.02f, 0.86f);
                lowHealthEdgeGraphic.enabled = true;
                lowHealthEdgeGraphic.raycastTarget = false;
                lowHealthEdgeGraphic.SetAllDirty();
            }
        }

        private void EnsureLowHealthFallbackPanel(
            RectTransform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 size,
            Vector2 position)
        {
            RectTransform panelRect = FindDirectChildRect(parent, name);
            Image image;
            if (panelRect == null)
            {
                GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
                panel.transform.SetParent(parent, false);
                panelRect = panel.GetComponent<RectTransform>();
                image = panel.GetComponent<Image>();
            }
            else
            {
                image = panelRect.GetComponent<Image>();
                if (image == null)
                    image = panelRect.gameObject.AddComponent<Image>();
            }

            panelRect.anchorMin = anchorMin;
            panelRect.anchorMax = anchorMax;
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = size;
            panelRect.anchoredPosition = position;
            panelRect.SetAsFirstSibling();

            image.enabled = true;
            image.color = new Color(1f, 0.02f, 0.02f, 0.18f);
            image.raycastTarget = false;
        }

        private Image CreateOverlayStockFill(string name, Transform parent, Color fillColor)
        {
            GameObject slot = new GameObject(name + "Track", typeof(RectTransform), typeof(Image));
            slot.transform.SetParent(parent, false);

            RectTransform slotRect = slot.GetComponent<RectTransform>();
            slotRect.anchorMin = Vector2.zero;
            slotRect.anchorMax = Vector2.one;
            slotRect.pivot = new Vector2(0.5f, 0.5f);
            slotRect.offsetMin = new Vector2(3f, 3f);
            slotRect.offsetMax = new Vector2(-3f, -3f);

            Image slotImage = slot.GetComponent<Image>();
            slotImage.color = new Color(1f, 1f, 1f, 0.05f);
            slotImage.raycastTarget = false;

            GameObject fill = new GameObject(name + "Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(slot.transform, false);

            RectTransform fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            Image image = fill.GetComponent<Image>();
            image.color = fillColor;
            image.raycastTarget = false;

            return image;
        }

        private void SetStockFill(Image targetImage, float currentValue, float stockStart)
        {
            if (targetImage == null) return;

            float stockValue = Mathf.Clamp(currentValue - stockStart, 0f, 100f);
            SetRectFill(targetImage, stockValue / 100f);
        }

        private void SetRectFill(Image targetImage, float rate)
        {
            if (targetImage == null) return;

            RectTransform rect = targetImage.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(Mathf.Clamp01(rate), 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void AppendBuffLine(string buffId, string label, string color)
        {
            if (!m_buffStacks.TryGetValue(buffId, out int stackCount)) return;
            AppendRawBuffLine(label, stackCount, color);
        }

        private void AppendRawBuffLine(string label, int stackCount, string color)
        {
            if (stackCount <= 0) return;
            if (m_buffBuilder.Length > 0)
                m_buffBuilder.Append('\n');

            m_buffBuilder
                .Append("<color=")
                .Append(color)
                .Append('>')
                .Append(label)
                .Append("</color>  x")
                .Append(stackCount);
        }

        private bool IsKnownBuff(string buffId)
        {
            return buffId == "moveSpeed"
                || buffId == "attackPower"
                || buffId == "attackRange"
                || buffId == "attackSpeed"
                || buffId == "durability";
        }
    }

}
