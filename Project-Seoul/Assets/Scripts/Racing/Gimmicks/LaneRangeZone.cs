using UnityEngine;

namespace Seoul.Network.Game
{
    // 안에 들어온 동안 lane change를 [MinLane, MaxLane] 범위로 제한하는 trigger 마커.
    // - PlayerController가 zone 안의 lane change 시도를 검사: target이 범위 밖이면 차단.
    // - 이미 범위 밖이어도 안쪽으로 좁히는 변경은 허용 (예: lane 4 → 3).
    // - 사용처: Underground(지하차도 통로 + 계단), Overpass(다리 위 평평한 구간).
    [RequireComponent(typeof(BoxCollider))]
    public class LaneRangeZone : MonoBehaviour
    {
        public int MinLane;
        public int MaxLane;

        private void Reset() => EnsureTrigger();
        private void Awake() => EnsureTrigger();

        private void EnsureTrigger()
        {
            if (TryGetComponent<BoxCollider>(out var box))
                box.isTrigger = true;
        }
    }
}
