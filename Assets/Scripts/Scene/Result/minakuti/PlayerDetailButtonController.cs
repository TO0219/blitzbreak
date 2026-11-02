using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MyGame
{
    public class PlayerDetailButtonController : MonoBehaviour
    {
        [SerializeField] private string playerDetailSceneName = "PlayerDetailScene";
        [SerializeField] private Button[] playerButtons = new Button[4];

        private List<BattleResultEntry> _ranked;
        private ResultUIController _resultUIController;

        public static BattleResultEntry SelectedEntry { get; private set; }
        public static bool HasSelectedEntry { get; private set; }

        private void Start()
        {
            SelectedEntry = default;
            HasSelectedEntry = false;

            if (!BattleResultData.HasResult)
            {
                BattleResultData.TryRestoreSavedResults();
            }

            _ranked = BattleResultData.HasResult
                ? BattleResultData.GetRankedEntries()
                : new List<BattleResultEntry>();
            _resultUIController = FindAnyObjectByType<ResultUIController>();

            for (int i = 0; i < playerButtons.Length; i++)
            {
                if (playerButtons[i] == null)
                    continue;

                bool hasEntry = i < _ranked.Count;
                if (!hasEntry)
                {
                    playerButtons[i].gameObject.SetActive(false);
                    continue;
                }

                Button button = playerButtons[i];
                int fallbackIndex = i;
                button.onClick.AddListener(() => OnClickPlayer(button, fallbackIndex));
            }
        }

        private void OnClickPlayer(Button button, int fallbackIndex)
        {
            BattleResultEntry selectedEntry;

            if (_resultUIController != null &&
                (_resultUIController.TryGetDisplayedEntry(button.transform, out BattleResultEntry displayedEntry) ||
                 _resultUIController.TryGetDisplayedEntryBySlot(fallbackIndex, out displayedEntry)))
            {
                selectedEntry = displayedEntry;
            }
            else
            {
                if (fallbackIndex < 0 || fallbackIndex >= _ranked.Count)
                    return;

                selectedEntry = _ranked[fallbackIndex];
            }

            SelectedEntry = selectedEntry;
            HasSelectedEntry = true;
            MultiSceneManager.Instance.Load(playerDetailSceneName);
        }
    }
}
