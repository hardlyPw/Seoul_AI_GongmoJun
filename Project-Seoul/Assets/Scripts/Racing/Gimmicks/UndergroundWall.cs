using UnityEngine;

namespace Seoul.Network.Game
{
    // 지하차도 hole 옆 lane change 차단 영역 마커.
    // - 캐릭터가 이 trigger 안에 있는 동안 lane change를 시도하면 PlayerController가 fall + knockback 처리.
    // - 정상 진행(입력 없음)은 영향 없음 → capsule radius와 lane boundary가 겹쳐도 무관.
    // - 점프 중에는 어차피 HandleLaneChange가 AirborneState에서 차단되니 자동 면제.
    [RequireComponent(typeof(BoxCollider))]
    public class UndergroundWall : MonoBehaviour
    {
        private void Reset() => EnsureTrigger();
        private void Awake() => EnsureTrigger();

        private void EnsureTrigger()
        {
            if (TryGetComponent<BoxCollider>(out var box))
                box.isTrigger = true;
        }
    }
}
