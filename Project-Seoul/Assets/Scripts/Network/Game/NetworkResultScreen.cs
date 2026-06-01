using System.Text;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Seoul.Network.Lobby;

namespace Seoul.Network.Game
{
    public class NetworkResultScreen : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private GameObject      panel;
        [SerializeField] private TextMeshProUGUI resultText;
        [SerializeField] private Button          backToTitleButton;

        [Header("Scene")]
        [SerializeField] private string titleSceneName = "01_Title";

        private NetworkResultBroadcaster _broadcaster;

        private void Start()
        {
            if (panel != null) panel.SetActive(true);
            if (backToTitleButton != null) backToTitleButton.onClick.AddListener(OnBackToTitleClicked);

            TryBind();
            Refresh();
        }

        private void OnDestroy()
        {
            if (_broadcaster != null && _broadcaster.Entries != null)
                _broadcaster.Entries.OnListChanged -= OnEntriesChanged;
            if (backToTitleButton != null) backToTitleButton.onClick.RemoveListener(OnBackToTitleClicked);
        }

        private void Update()
        {
            if (_broadcaster == null) TryBind();
        }

        private void TryBind()
        {
            if (_broadcaster != null) return;
            _broadcaster = NetworkResultBroadcaster.Instance;
            if (_broadcaster == null) return;

            _broadcaster.Entries.OnListChanged += OnEntriesChanged;
            Refresh();
        }

        private void OnEntriesChanged(NetworkListEvent<ResultEntry> change) => Refresh();

        private void Refresh()
        {
            if (resultText == null) return;
            if (_broadcaster == null || _broadcaster.Entries == null)
            {
                resultText.text = "결과 집계 중...";
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("=== 최종 결과 ===");

            ulong localId = NetworkManager.Singleton != null
                ? NetworkManager.Singleton.LocalClientId
                : ulong.MaxValue;

            string[] medals = { "🥇 1위", "🥈 2위", "🥉 3위" };

            for (int i = 0; i < _broadcaster.Entries.Count; i++)
            {
                var e        = _broadcaster.Entries[i];
                string rank  = i < medals.Length ? medals[i] : $"{e.FinalRank}위";
                string who   = e.ClientId == localId ? $"P{e.ClientId} (You)" : $"P{e.ClientId}";
                sb.AppendLine($"{rank}  {who}  -  {e.Score}점");
            }

            resultText.text = sb.ToString();
        }

        private async void OnBackToTitleClicked()
        {
            if (backToTitleButton != null) backToTitleButton.interactable = false;

            try
            {
                if (LobbyManager.Instance != null)
                    await LobbyManager.Instance.LeaveRoomAsync();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[NetworkResultScreen] LeaveRoom failed: {e.Message}");
            }

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown();

            if (SessionScoreStore.Instance != null)
                SessionScoreStore.Instance.ResetAll();

            SceneTransition.Load(titleSceneName);
        }
    }
}
