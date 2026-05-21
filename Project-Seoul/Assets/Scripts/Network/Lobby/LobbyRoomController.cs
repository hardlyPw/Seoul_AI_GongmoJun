using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Seoul.Network.Lobby
{
    public class LobbyRoomController : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField] private TMP_Text roomCodeLabel;
        [SerializeField] private TMP_Text[] playerSlotLabels = new TMP_Text[4];

        [Header("Buttons")]
        [SerializeField] private Button startGameButton;
        [SerializeField] private Button leaveButton;

        [Header("Scene")]
        [SerializeField] private string titleSceneName = "01_Title";
        [SerializeField] private string raceSceneName  = "03_Race";

        private float _refreshTimer;
        private const float RefreshInterval = 1f;

        private IEnumerator Start()
        {
            float timeout = 5f;
            while (timeout > 0f && (LobbyManager.Instance == null || LobbyManager.Instance.CurrentSession == null))
            {
                yield return null;
                timeout -= Time.unscaledDeltaTime;
            }

            if (LobbyManager.Instance == null || LobbyManager.Instance.CurrentSession == null)
            {
                Debug.LogWarning("[LobbyRoom] Session never became ready, returning to title.");
                SceneManager.LoadScene(titleSceneName);
                yield break;
            }

            startGameButton.onClick.AddListener(OnStartClicked);
            leaveButton.onClick.AddListener(OnLeaveClicked);

            bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
            startGameButton.gameObject.SetActive(isHost);

            RefreshUI();
        }

        private void Update()
        {
            _refreshTimer += Time.deltaTime;
            if (_refreshTimer < RefreshInterval) return;
            _refreshTimer = 0f;
            RefreshUI();
        }

        private void RefreshUI()
        {
            var session = LobbyManager.Instance?.CurrentSession;
            if (session == null) return;

            if (roomCodeLabel != null) roomCodeLabel.text = $"Code: {session.Code}";

            var players = session.Players;
            for (int i = 0; i < playerSlotLabels.Length; i++)
            {
                if (playerSlotLabels[i] == null) continue;
                playerSlotLabels[i].text = i < players.Count
                    ? $"Player {i + 1}: {players[i].Id.Substring(0, 6)}..."
                    : "(empty)";
            }
        }

        private void OnStartClicked()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsHost) return;
            NetworkManager.Singleton.SceneManager.LoadScene(raceSceneName, LoadSceneMode.Single);
        }

        private async void OnLeaveClicked()
        {
            await LobbyManager.Instance.LeaveRoomAsync();
            if (NetworkManager.Singleton.IsListening) NetworkManager.Singleton.Shutdown();
            SceneManager.LoadScene(titleSceneName);
        }
    }
}
