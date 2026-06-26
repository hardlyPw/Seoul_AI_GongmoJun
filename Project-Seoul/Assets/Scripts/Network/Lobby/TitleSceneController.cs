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
            createRoomButton.onClick.AddListener(() => { SoundManager.Instance.PlaySFX("Ui_button_click1"); OnCreateClicked(); });
            joinRoomButton.onClick.AddListener(() => { SoundManager.Instance.PlaySFX("Ui_button_click1"); OnJoinClicked(); });
            SetStatus("");
        }

        private async void OnCreateClicked()
        {
            SetInteractable(false);
            SetStatus("방 생성 중...");

            var session = await LobbyManager.Instance.CreateRoomAsync();
            if (session == null)
            {
                SetStatus("방 생성에 실패했습니다.");
                SetInteractable(true);
                return;
            }

            SetStatus($"생성 완료. 코드: {session.Code}");
            SoundManager.Instance.PlaySFX("lobby_enter");

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
                SetStatus("방 코드를 입력하세요.");
                return;
            }

            SetInteractable(false);
            SetStatus("참가 중...");

            var session = await LobbyManager.Instance.JoinRoomByCodeAsync(code);
            if (session == null)
            {
                SetStatus("참가에 실패했습니다.");
                SetInteractable(true);
                return;
            }

            SetStatus($"참가 완료: {session.Code}. 호스트 장면을 기다리는 중...");
            SoundManager.Instance.PlaySFX("lobby_enter");
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
