using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
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
        [SerializeField] private Image[]    playerSlotReadyIndicators = new Image[4];

        [Header("Buttons")]
        [SerializeField] private Button startGameButton;
        [SerializeField] private Button readyButton;
        [SerializeField] private Button leaveButton;

        [Header("Scene")]
        [SerializeField] private string titleSceneName = "01_Title";
        [SerializeField] private string raceSceneName  = "03_Stage_Running";

        private float _refreshTimer;
        private const float RefreshInterval = 0.5f;
        private const string ReadyMessageName = "LobbyReady";
        private const string ReadySyncMessageName = "LobbyReadySync";
        private bool _isHost;
        private bool _localReady;
        private static Sprite s_readyDotSprite;
        private static readonly HashSet<ulong> ReadyClientIds = new();

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

            if (startGameButton == null) startGameButton = FindButton("StartGameButton");
            if (readyButton == null) readyButton = FindButton("ReadyButton");
            if (leaveButton == null) leaveButton = FindButton("LeaveButton");

            if (startGameButton != null)
                startGameButton.onClick.AddListener(() => { SoundManager.Instance.PlaySFX("Ui_button_click1"); OnStartClicked(); });
            if (leaveButton != null)
                leaveButton.onClick.AddListener(() => { SoundManager.Instance.PlaySFX("Ui_button_click1"); OnLeaveClicked(); });
            if (readyButton != null)
                readyButton.onClick.AddListener(() => { SoundManager.Instance.PlaySFX("Ui_button_click1"); OnReadyClicked(); });

            _isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
            RegisterReadyMessageHandler();
            EnsureReadyIndicators();
            if (startGameButton != null) startGameButton.gameObject.SetActive(false);
            if (readyButton != null) readyButton.gameObject.SetActive(!_isHost);

            RefreshUI();
        }

        private void OnDestroy()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;

            if (nm.CustomMessagingManager != null && nm.IsServer)
                nm.CustomMessagingManager.UnregisterNamedMessageHandler(ReadyMessageName);
            if (nm.CustomMessagingManager != null)
                nm.CustomMessagingManager.UnregisterNamedMessageHandler(ReadySyncMessageName);
            nm.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        private void Update()
        {
            _refreshTimer += Time.deltaTime;
            if (_refreshTimer < RefreshInterval) return;
            _refreshTimer = 0f;
            RefreshUI();
        }

        private void RegisterReadyMessageHandler()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.CustomMessagingManager == null) return;

            nm.CustomMessagingManager.UnregisterNamedMessageHandler(ReadySyncMessageName);
            nm.CustomMessagingManager.RegisterNamedMessageHandler(ReadySyncMessageName, OnReadySyncMessageReceived);

            if (!nm.IsServer) return;

            ReadyClientIds.Clear();
            nm.CustomMessagingManager.UnregisterNamedMessageHandler(ReadyMessageName);
            nm.CustomMessagingManager.RegisterNamedMessageHandler(ReadyMessageName, OnReadyMessageReceived);
            nm.OnClientDisconnectCallback += OnClientDisconnected;
        }

        private void OnClientDisconnected(ulong clientId)
        {
            ReadyClientIds.Remove(clientId);
            BroadcastReadyState();
        }

        private void OnReadyMessageReceived(ulong senderClientId, FastBufferReader reader)
        {
            if (senderClientId == NetworkManager.ServerClientId) return;

            reader.ReadValueSafe(out bool isReady);
            if (isReady) ReadyClientIds.Add(senderClientId);
            else ReadyClientIds.Remove(senderClientId);

            BroadcastReadyState();
            RefreshUI();
        }

        private void OnReadySyncMessageReceived(ulong senderClientId, FastBufferReader reader)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || senderClientId != NetworkManager.ServerClientId) return;

            reader.ReadValueSafe(out int count);
            ReadyClientIds.Clear();
            for (int i = 0; i < count; i++)
            {
                reader.ReadValueSafe(out ulong clientId);
                ReadyClientIds.Add(clientId);
            }

            RefreshUI();
        }

        private void BroadcastReadyState()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.CustomMessagingManager == null || !nm.IsServer) return;

            int byteSize = sizeof(int) + ReadyClientIds.Count * sizeof(ulong);
            using var writer = new FastBufferWriter(byteSize, Allocator.Temp);
            writer.WriteValueSafe(ReadyClientIds.Count);
            foreach (ulong clientId in ReadyClientIds)
                writer.WriteValueSafe(clientId);

            nm.CustomMessagingManager.SendNamedMessageToAll(ReadySyncMessageName, writer);
        }

        private void RefreshUI()
        {
            var session = LobbyManager.Instance?.CurrentSession;
            if (session == null) return;

            if (roomCodeLabel != null) roomCodeLabel.text = $"초대코드: {session.Code}";

            var players = session.Players;
            for (int i = 0; i < playerSlotLabels.Length; i++)
            {
                if (playerSlotLabels[i] != null)
                    playerSlotLabels[i].text = i < players.Count
                        ? $"Player {i + 1}: {players[i].Id.Substring(0, 6)}..."
                        : "(empty)";
            }

            RefreshReadyIndicators(players.Count);

            if (_isHost && startGameButton != null)
                startGameButton.gameObject.SetActive(AreAllNonHostPlayersReady(players.Count));
        }

        private Button FindButton(string objectName)
        {
            var buttons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null && buttons[i].name == objectName) return buttons[i];
            }
            return null;
        }

        private void RefreshReadyIndicators(int activePlayerCount)
        {
            EnsureReadyIndicators();
            if (playerSlotReadyIndicators == null) return;

            for (int i = 0; i < playerSlotReadyIndicators.Length; i++)
            {
                if (playerSlotReadyIndicators[i] == null) continue;
                playerSlotReadyIndicators[i].enabled = false;
            }

            ulong hostId = NetworkManager.ServerClientId;
            var nm = NetworkManager.Singleton;
            if (nm == null) return;

            foreach (ulong clientId in nm.ConnectedClientsIds)
            {
                bool isReady = clientId == hostId || ReadyClientIds.Contains(clientId);
                if (!isReady) continue;

                int slot = (int)clientId;
                if (slot < 0 || slot >= playerSlotReadyIndicators.Length) continue;
                if (slot >= activePlayerCount) continue;
                if (playerSlotReadyIndicators[slot] == null) continue;
                playerSlotReadyIndicators[slot].enabled = true;
            }
        }

        private void EnsureReadyIndicators()
        {
            if (playerSlotLabels == null) return;

            if (playerSlotReadyIndicators == null || playerSlotReadyIndicators.Length != playerSlotLabels.Length)
                playerSlotReadyIndicators = new Image[playerSlotLabels.Length];

            for (int i = 0; i < playerSlotLabels.Length; i++)
            {
                if (playerSlotReadyIndicators[i] != null || playerSlotLabels[i] == null) continue;

                var go = new GameObject($"ReadyIndicator_{i + 1}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(playerSlotLabels[i].transform, false);

                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(1f, 0.5f);
                rt.anchorMax = new Vector2(1f, 0.5f);
                rt.pivot = new Vector2(1f, 0.5f);
                rt.anchoredPosition = new Vector2(-18f, 0f);
                rt.sizeDelta = new Vector2(28f, 28f);

                var image = go.GetComponent<Image>();
                image.sprite = GetReadyDotSprite();
                image.color = new Color(0.25f, 1f, 0.42f, 0.95f);
                image.raycastTarget = false;
                image.enabled = false;
                playerSlotReadyIndicators[i] = image;
            }
        }

        private static Sprite GetReadyDotSprite()
        {
            if (s_readyDotSprite != null) return s_readyDotSprite;

            const int size = 64;
            const float radius = size * 0.43f;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "LobbyReadyDot",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = Mathf.Clamp01(radius - dist + 1f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            s_readyDotSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            s_readyDotSprite.hideFlags = HideFlags.HideAndDontSave;
            return s_readyDotSprite;
        }

        private bool AreAllNonHostPlayersReady(int sessionPlayerCount = -1)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening) return false;

            if (sessionPlayerCount >= 0 && nm.ConnectedClientsIds.Count < sessionPlayerCount)
                return false;

            ulong hostId = NetworkManager.ServerClientId;
            foreach (ulong clientId in nm.ConnectedClientsIds)
            {
                if (clientId == hostId) continue;
                if (!ReadyClientIds.Contains(clientId)) return false;
            }
            return true;
        }

        private void OnReadyClicked()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.CustomMessagingManager == null || !nm.IsClient)
            {
                Debug.LogWarning("[LobbyRoom] Network client is not ready — ready ignored.");
                return;
            }

            _localReady = !_localReady;
            using var writer = new FastBufferWriter(sizeof(bool), Allocator.Temp);
            writer.WriteValueSafe(_localReady);
            nm.CustomMessagingManager.SendNamedMessage(ReadyMessageName, NetworkManager.ServerClientId, writer);
        }

        private void OnStartClicked()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsHost) return;
            var session = LobbyManager.Instance?.CurrentSession;
            if (!AreAllNonHostPlayersReady(session?.Players.Count ?? -1)) return;
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
