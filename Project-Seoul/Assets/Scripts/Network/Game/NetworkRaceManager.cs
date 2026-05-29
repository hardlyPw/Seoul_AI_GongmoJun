using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Seoul.Network.Game
{
    public class NetworkRaceManager : NetworkBehaviour
    {
        public static NetworkRaceManager Instance { get; private set; }

        [Header("Spawn")]
        [SerializeField] private float spawnX = 0f;
        [SerializeField] private float spawnY = 1f;

        [Header("Countdown")]
        [SerializeField] private float countdownDuration = 3f;

        public NetworkVariable<RaceState> State = new(
            RaceState.WaitingForPlayers,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<float> CountdownRemaining = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly Dictionary<ulong, NetworkObject> _spawnedPlayers  = new();
        private readonly HashSet<ulong>                  _finishedClients = new();
        private int _nextRank = 1;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public override void OnNetworkSpawn()
        {
            Debug.Log($"[NetworkRaceManager] OnNetworkSpawn IsServer={IsServer} IsClient={IsClient} IsHost={IsHost} LocalClientId={NetworkManager.Singleton.LocalClientId}");

            if (!IsServer) return;

            if (SessionScoreStore.Instance == null)
            {
                var go = new GameObject("SessionScoreStore");
                go.AddComponent<SessionScoreStore>();
            }

            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnLoadEventCompleted;
            NetworkManager.Singleton.OnClientConnectedCallback         += OnLateClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback        += DespawnPlayerForClient;
        }

        public override void OnNetworkDespawn()
        {
            if (NetworkManager.Singleton == null) return;
            if (NetworkManager.Singleton.SceneManager != null)
                NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;
            NetworkManager.Singleton.OnClientConnectedCallback         -= OnLateClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback        -= DespawnPlayerForClient;
        }

        private void OnLoadEventCompleted(string sceneName, LoadSceneMode mode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
        {
            if (sceneName != gameObject.scene.name) return;

            Debug.Log($"[NetworkRaceManager] All clients loaded '{sceneName}'. Completed=[{string.Join(",", clientsCompleted)}]");

            foreach (var clientId in clientsCompleted)
            {
                SpawnPlayerForClient(clientId);
            }

            StartCoroutine(CountdownRoutine());
        }

        private IEnumerator CountdownRoutine()
        {
            State.Value              = RaceState.Countdown;
            CountdownRemaining.Value = countdownDuration;

            Debug.Log($"[NetworkRaceManager] Countdown started ({countdownDuration}s)");

            while (CountdownRemaining.Value > 0f)
            {
                yield return null;
                CountdownRemaining.Value -= Time.deltaTime;
            }

            CountdownRemaining.Value = 0f;
            State.Value              = RaceState.Racing;

            Debug.Log("[NetworkRaceManager] Race started!");
        }

        public void ReportGoal(ulong clientId)
        {
            if (!IsServer) return;
            if (_finishedClients.Contains(clientId)) return;

            _finishedClients.Add(clientId);
            int rank = _nextRank++;

            if (_spawnedPlayers.TryGetValue(clientId, out var netObj) && netObj != null)
            {
                var np = netObj.GetComponent<NetworkPlayer>();
                if (np != null) np.MarkFinished(rank);
            }

            Debug.Log($"[NetworkRaceManager] clientId={clientId} reached goal — rank {rank} (this stage: {_finishedClients.Count} finished)");
        }

        private void OnLateClientConnected(ulong clientId)
        {
            if (!IsServer) return;
            if (_spawnedPlayers.ContainsKey(clientId)) return;
            SpawnPlayerForClient(clientId);
        }

        private void SpawnPlayerForClient(ulong clientId)
        {
            if (!IsServer) return;
            if (_spawnedPlayers.ContainsKey(clientId)) return;

            // 이미 살아있는 NetworkPlayer가 있으면 (DontDestroyOnLoad로 이전 스테이지에서 넘어옴) 다시 안 만듦
            foreach (var existing in NetworkPlayer.All)
            {
                if (existing != null && existing.OwnerClientId == clientId)
                {
                    _spawnedPlayers[clientId] = existing.GetComponent<NetworkObject>();
                    Debug.Log($"[NetworkRaceManager] Reusing existing NetworkPlayer for clientId={clientId}");
                    return;
                }
            }

            var prefab = NetworkManager.Singleton.NetworkConfig.PlayerPrefab;
            if (prefab == null)
            {
                Debug.LogError("[NetworkRaceManager] PlayerPrefab is not set on NetworkManager.");
                return;
            }

            int laneIndex = GetLaneForClient(clientId);
            float z = LaneManager.Instance != null
                ? LaneManager.Instance.GetLaneZ(laneIndex)
                : laneIndex * 1.5f;

            Vector3 spawnPos = new Vector3(spawnX, spawnY, z);
            var go     = Instantiate(prefab, spawnPos, Quaternion.identity);
            var netObj = go.GetComponent<NetworkObject>();
            // destroyWithScene=false: 씬 언로드 시 NGO가 자동으로 despawn 하지 않도록.
            // DontDestroyOnLoad와 함께 쓰려면 false여야 함 — true면 NGO가
            // 스폰 시점 씬 핸들을 캐싱해서 그 씬 언로드시 DDOL 무시하고 despawn함.
            netObj.SpawnAsPlayerObject(clientId, false);

            _spawnedPlayers[clientId] = netObj;
            Debug.Log($"[NetworkRaceManager] Spawned player for clientId={clientId} at lane {laneIndex} (pos {spawnPos})");
        }

        private void DespawnPlayerForClient(ulong clientId)
        {
            if (!IsServer) return;
            if (!_spawnedPlayers.TryGetValue(clientId, out var netObj)) return;

            if (netObj != null && netObj.IsSpawned) netObj.Despawn(true);
            _spawnedPlayers.Remove(clientId);
            Debug.Log($"[NetworkRaceManager] Despawned player for clientId={clientId}");
        }

        private int GetLaneForClient(ulong clientId)
        {
            int laneCount = LaneManager.Instance != null ? LaneManager.Instance.LaneCount : 6;
            return (int)(clientId % (ulong)laneCount);
        }
    }
}
