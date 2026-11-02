using TMPro;
using UnityEngine;

namespace MyGame
{
    public class PlayerListItemUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text playerNameText;
        [SerializeField] private TMP_Text hostLabel;

        public void Setup(string playerName, bool isHost)
        {
            if (playerNameText != null)
                playerNameText.text = playerName;

            if (hostLabel != null)
                hostLabel.text = isHost ? "ホスト" : "";
        }
    }
}
