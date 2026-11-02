using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MyGame
{
    public class RoomListItemUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text roomNameText;
        [SerializeField] private TMP_Text playerCountText;
        [SerializeField] private Button joinButton;

        private string m_lobbyId;
        private LobbyUIController m_lobbyUIController;

        public void Setup(string lobbyId, string roomName, int currentPlayers, int maxPlayers, LobbyUIController controller)
        {
            m_lobbyId = lobbyId;
            m_lobbyUIController = controller;

            if (roomNameText != null)
                roomNameText.text = roomName;

            if (playerCountText != null)
                playerCountText.text = currentPlayers + " / " + maxPlayers;

            if (joinButton != null)
            {
                joinButton.onClick.RemoveAllListeners();
                joinButton.onClick.AddListener(OnClickJoin);
            }
        }

        private void OnClickJoin()
        {
            if (m_lobbyUIController != null)
                m_lobbyUIController.JoinLobbyById(m_lobbyId);
        }
    }
}