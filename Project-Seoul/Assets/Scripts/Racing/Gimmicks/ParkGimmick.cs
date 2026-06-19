using UnityEngine;

namespace Seoul.Network.Game
{
    // 공원 기믹.
    // 기획서 1.3.0: "스테이지 중에 존재하며 정문과 벽으로 입출구가 제한되어 있음"
    //              + "특정 라인(입구)으로 유도하는 장치"
    //
    // 구조: 트랙을 가로지르는 사각형 영역. 진입측/출구측 벽이 정문 lane만 비워두고 나머지를 차단.
    // 결과: 캐릭터가 공원 구간을 통과하려면 정문 lane으로 강제 이동해야 함 → "유도" 효과.
    //
    // 멀티: 정적 collider라 별도 동기화 불필요. 모든 클라가 자기 측에서 동일하게 충돌.
    public class ParkGimmick : MonoBehaviour
    {
        [Header("Gate Lanes (입구/출구 공통)")]
        [Tooltip("정문 시작 lane (이 lane은 통로)")]
        [SerializeField] private int gateMinLane = 2;
        [Tooltip("정문 끝 lane (이 lane까지 통로)")]
        [SerializeField] private int gateMaxLane = 3;

        [Header("Walls — 정문 양옆 (각 4개)")]
        [Tooltip("입구측: 정문보다 낮은 lane 영역")]
        [SerializeField] private Collider entryLeftWall;
        [Tooltip("입구측: 정문보다 높은 lane 영역")]
        [SerializeField] private Collider entryRightWall;
        [Tooltip("출구측: 정문보다 낮은 lane 영역")]
        [SerializeField] private Collider exitLeftWall;
        [Tooltip("출구측: 정문보다 높은 lane 영역")]
        [SerializeField] private Collider exitRightWall;

        private void Start()
        {
            var lm = LaneManager.Instance;
            if (lm == null) return;

            int laneCount = lm.LaneCount;

            // 정문 왼쪽 영역 (lane 0 ~ gateMinLane-1) 막기
            ConfigureWall(entryLeftWall, 0, gateMinLane - 1, lm);
            ConfigureWall(exitLeftWall, 0, gateMinLane - 1, lm);

            // 정문 오른쪽 영역 (lane gateMaxLane+1 ~ laneCount-1) 막기
            ConfigureWall(entryRightWall, gateMaxLane + 1, laneCount - 1, lm);
            ConfigureWall(exitRightWall, gateMaxLane + 1, laneCount - 1, lm);
        }

        private static void ConfigureWall(Collider wall, int minLane, int maxLane, LaneManager lm)
        {
            if (wall == null) return;

            // 정문이 트랙 끝에 닿아 있으면 한쪽 벽 자동 비활성화
            if (minLane > maxLane)
            {
                wall.gameObject.SetActive(false);
                return;
            }

            var pos = wall.transform.position;
            pos.z = lm.GetLaneCenterZ(minLane, maxLane);
            wall.transform.position = pos;

            if (wall is BoxCollider box)
            {
                var size = box.size;
                size.z = lm.GetLaneSpanZ(minLane, maxLane);
                box.size = size;
            }
        }
    }
}
