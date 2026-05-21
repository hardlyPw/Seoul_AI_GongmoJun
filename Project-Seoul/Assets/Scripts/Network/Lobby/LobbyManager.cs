using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace Seoul.Network.Lobby
{
    public class LobbyManager : MonoBehaviour
    {
        public static LobbyManager Instance { get; private set; }

        [SerializeField] private int maxPlayers = 4;

        public ISession CurrentSession { get; private set; }
        public string   JoinCode       => CurrentSession?.Code;
        public bool     IsHost         => CurrentSession?.IsHost ?? false;

        public event Action<ISession> OnSessionJoined;
        public event Action            OnSessionLeft;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public async Task<ISession> CreateRoomAsync(string roomName = null)
        {
            try
            {
                var options = new SessionOptions
                {
                    Name       = roomName ?? $"Room-{UnityEngine.Random.Range(1000, 9999)}",
                    MaxPlayers = maxPlayers,
                    IsPrivate  = false
                }.WithRelayNetwork();

                CurrentSession = await MultiplayerService.Instance.CreateSessionAsync(options);
                Debug.Log($"[Lobby] Created. Code={CurrentSession.Code}, PlayerId={AuthenticationService.Instance.PlayerId}");

                OnSessionJoined?.Invoke(CurrentSession);
                return CurrentSession;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Lobby] Create failed: {e.Message}");
                return null;
            }
        }

        public async Task<ISession> JoinRoomByCodeAsync(string code)
        {
            try
            {
                var options = new JoinSessionOptions { };
                CurrentSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(code, options);
                Debug.Log($"[Lobby] Joined. Code={CurrentSession.Code}");

                OnSessionJoined?.Invoke(CurrentSession);
                return CurrentSession;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Lobby] Join failed: {e.Message}");
                return null;
            }
        }

        public async Task LeaveRoomAsync()
        {
            if (CurrentSession == null) return;
            try
            {
                await CurrentSession.LeaveAsync();
                Debug.Log("[Lobby] Left session.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Lobby] Leave failed: {e.Message}");
            }
            finally
            {
                CurrentSession = null;
                OnSessionLeft?.Invoke();
            }
        }
    }
}
