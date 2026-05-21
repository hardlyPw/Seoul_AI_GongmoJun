using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Seoul.Network.Lobby
{
    public class TitleSceneController : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button createRoomButton;
        [SerializeField] private Button joinRoomButton;

        [Header("Join Input")]
        [SerializeField] private TMP_InputField joinCodeInput;

        [Header("Status")]
        [SerializeField] private TMP_Text statusText;

        [Header("Scene")]
        [SerializeField] private string lobbyRoomSceneName = "02_LobbyRoom";

        private void Awake()
        {
            createRoomButton.onClick.AddListener(OnCreateClicked);
            joinRoomButton.onClick.AddListener(OnJoinClicked);
            SetStatus("");
        }

        private async void OnCreateClicked()
        {
            SetInteractable(false);
            SetStatus("Creating room...");

            var session = await LobbyManager.Instance.CreateRoomAsync();
            if (session == null)
            {
                SetStatus("Failed to create room.");
                SetInteractable(true);
                return;
            }

            SetStatus($"Created. Code: {session.Code}");

            // Host loads scene via NGO so all clients sync follow
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.SceneManager.LoadScene(lobbyRoomSceneName, LoadSceneMode.Single);
            }
            else
            {
                SceneManager.LoadScene(lobbyRoomSceneName);
            }
        }

        private async void OnJoinClicked()
        {
            string code = joinCodeInput != null ? joinCodeInput.text.Trim() : "";
            if (string.IsNullOrEmpty(code))
            {
                SetStatus("Enter a room code.");
                return;
            }

            SetInteractable(false);
            SetStatus("Joining...");

            var session = await LobbyManager.Instance.JoinRoomByCodeAsync(code);
            if (session == null)
            {
                SetStatus("Failed to join.");
                SetInteractable(true);
                return;
            }

            SetStatus($"Joined: {session.Code}. Waiting for host scene...");
            // Client does NOT call LoadScene — NGO auto-syncs to host's current scene
        }

        private void SetInteractable(bool value)
        {
            createRoomButton.interactable = value;
            joinRoomButton.interactable   = value;
            if (joinCodeInput != null) joinCodeInput.interactable = value;
        }

        private void SetStatus(string msg)
        {
            if (statusText != null) statusText.text = msg;
        }
    }
}
