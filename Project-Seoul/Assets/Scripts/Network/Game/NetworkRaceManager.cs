using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Seoul.Network.Game
{
    public class NetworkRaceManager : NetworkBehaviour
    {
        [Header("Spawn")]
        [SerializeField] private float spawnX = 0f;
        [SerializeField] private float spawnY = 1f;

        private readonly Dictionary<ulong, NetworkObject> _spawnedPlayers = new();

        public override void OnNetworkSpawn()
        {
            Debug.Log($"[NetworkRaceManager] OnNetworkSpawn IsServer={IsServer} IsClient={IsClient} IsHost={IsHost} LocalClientId={NetworkManager.Singleton.LocalClientId}");

            if (!IsServer) return;

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

            Debug.Log($"[NetworkRaceManager] All clients loaded '{sceneName}'. Completed=[{string.Join(",", clientsCompleted)}] TimedOut=[{string.Join(",", clientsTimedOut)}]");

            foreach (var clientId in clientsCompleted)
            {
                SpawnPlayerForClient(clientId);
            }
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
            netObj.SpawnAsPlayerObject(clientId, true);

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
