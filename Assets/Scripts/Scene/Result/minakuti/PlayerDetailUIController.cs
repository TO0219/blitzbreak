using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MyGame
{
    public class PlayerDetailUIController : MonoBehaviour
    {
        [SerializeField] private Text nameText;
        [SerializeField] private Text scoreText;
        [SerializeField] private Text defeatedEnemiesText;
        [SerializeField] private Text damageTakenText;
        [SerializeField] private Text deathCountText;

        private void Start()
        {
            BattleResultEntry entry = PlayerDetailButtonController.SelectedEntry;
            if (!PlayerDetailButtonController.HasSelectedEntry)
            {
                if (!BattleResultData.HasResult)
                    BattleResultData.TryRestoreSavedResults();

                if (BattleResultData.HasResult && BattleResultData.Entries.Count > 0)
                    entry = BattleResultData.GetRankedEntries()[0];
            }

            if (nameText != null)
                nameText.text = entry.PlayerName;

            if (scoreText != null)
                scoreText.text = entry.Score.ToString();

            if (defeatedEnemiesText != null)
                defeatedEnemiesText.text = entry.DefeatedEnemies.ToString();

            if (damageTakenText != null)
                damageTakenText.text = Mathf.RoundToInt(entry.DamageTaken).ToString();

            if (deathCountText != null)
                deathCountText.text = entry.DeathCount.ToString();

            SelectFirstInteractable();
        }

        private static void SelectFirstInteractable()
        {
            if (EventSystem.current == null)
                return;

            Selectable[] selectables = FindObjectsByType<Selectable>(FindObjectsInactive.Exclude);
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
    }
}
