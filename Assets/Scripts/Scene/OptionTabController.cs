using UnityEngine;
using UnityEngine.UI;

namespace MyGame
{
    public class OptionTabController : MonoBehaviour
    {
        [Header("Tab Buttons")]
        [SerializeField] private Button soundTabButton;
        [SerializeField] private Button controlTabButton;
        [SerializeField] private Button graphicsTabButton;

        [Header("Tab Panels")]
        [SerializeField] private GameObject soundPanel;
        [SerializeField] private GameObject controlPanel;
        [SerializeField] private GameObject graphicsPanel;

        [Header("Tab Colors")]
        [SerializeField] private Color activeColor = new Color(0.2f, 0.8f, 0.4f, 1f);
        [SerializeField] private Color inactiveColor = new Color(0.15f, 0.15f, 0.15f, 1f);

        private Button[] tabButtons;
        private GameObject[] tabPanels;

        public Transform ControlPanelTransform => controlPanel != null ? controlPanel.transform : null;

        private void Awake()
        {
            InitializeTabs();
        }

        private void Start()
        {
            SwitchTab(0);
        }

        public void SwitchTab(int index)
        {
            InitializeTabs();

            for (int i = 0; i < tabPanels.Length; i++)
            {
                if (tabPanels[i] == null || tabButtons[i] == null)
                    continue;

                tabPanels[i].SetActive(i == index);

                ColorBlock colors = tabButtons[i].colors;
                colors.normalColor = i == index ? activeColor : inactiveColor;
                colors.selectedColor = colors.normalColor;
                colors.highlightedColor = i == index
                    ? activeColor
                    : new Color(inactiveColor.r + 0.05f, inactiveColor.g + 0.05f, inactiveColor.b + 0.05f);
                tabButtons[i].colors = colors;
            }
        }

        private void InitializeTabs()
        {
            if (tabButtons != null && tabPanels != null)
                return;

            tabButtons = new[] { soundTabButton, controlTabButton, graphicsTabButton };
            tabPanels = new[] { soundPanel, controlPanel, graphicsPanel };

            if (soundTabButton != null) soundTabButton.onClick.AddListener(() => SwitchTab(0));
            if (controlTabButton != null) controlTabButton.onClick.AddListener(() => SwitchTab(1));
            if (graphicsTabButton != null) graphicsTabButton.onClick.AddListener(() => SwitchTab(2));
        }
    }
}
