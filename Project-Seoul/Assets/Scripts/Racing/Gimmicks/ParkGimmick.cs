using Unity.Netcode;
using UnityEngine;

namespace Seoul.Network.Game
{
    // 공원 정문 기믹 (멀티 동기화).
    // - 연속된 두 lane(gateMinLane ~ gateMaxLane)을 통로로 비워두고, 그 양옆 영역을 ParkWall로 막아 정문 유도.
    // - 한 줄짜리. 입구/출구 구분 없음 — 동일 prefab을 두 x좌표에 배치하면 됨.
    // - 서버가 ParkWall 두 개(좌/우)를 Spawn → NGO가 모든 클라에 복제 + ParkWall이 자기 측에서 lane 정렬.
    public class ParkGimmick : NetworkBehaviour
    {
        [Header("Gate Lanes (정문 — 연속된 두 lane)")]
        [Tooltip("정문 시작 lane (이 lane은 통로)")]
        [SerializeField] private int gateMinLane = 2;
        [Tooltip("정문 끝 lane (이 lane까지 통로)")]
        [SerializeField] private int gateMaxLane = 3;

        [Header("Wall Prefab")]
        [Tooltip("ParkWall + BoxCollider + NetworkObject prefab. DefaultNetworkPrefabs 등록 필수.")]
        [SerializeField] private ParkWall wallPrefab;

        private ParkWall _leftWall;
        private ParkWall _rightWall;

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;

            var lm = LaneManager.Instance;
            if (lm == null || wallPrefab == null) return;

            // 정문 왼쪽 영역(lane 0 ~ gateMinLane-1)
            _leftWall  = SpawnWall(0, gateMinLane - 1);
            // 정문 오른쪽 영역(lane gateMaxLane+1 ~ laneCount-1)
            _rightWall = SpawnWall(gateMaxLane + 1, lm.LaneCount - 1);
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer) return;
            // 셧다운 중에는 NGO가 모든 NetworkObject를 알아서 정리.
            // 여기서 추가 Despawn을 호출하면 NetworkManagerSession.OnStopCompleted에서 NullRef.
            if (NetworkManager == null || NetworkManager.ShutdownInProgress) return;
            DespawnWall(ref _leftWall);
            DespawnWall(ref _rightWall);
        }

        private ParkWall SpawnWall(int minLane, int maxLane)
        {
            // 정문이 트랙 끝에 닿으면 한쪽 벽은 불필요
            if (minLane > maxLane) return null;

            var wall = Instantiate(wallPrefab, transform.position, transform.rotation);
            wall.NetworkObject.Spawn();
            wall.MinLane.Value = minLane;
            wall.MaxLane.Value = maxLane;
            return wall;
        }

        private void DespawnWall(ref ParkWall wall)
        {
            if (wall != null && wall.NetworkObject.IsSpawned)
                wall.NetworkObject.Despawn();
            wall = null;
        }
    }
}
