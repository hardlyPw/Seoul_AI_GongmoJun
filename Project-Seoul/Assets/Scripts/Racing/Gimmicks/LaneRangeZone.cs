using UnityEngine;

namespace Seoul.Network.Game
{
    // 진입한 캐릭터에게 lane 범위 제한 또는 페널티를 주는 trigger 마커.
    // - HardBlock: 현재 lane이 [Min, Max] 안일 땐 밖으로 이탈하는 lane change 차단 (안→밖 차단). 밖→안 진입은 허용.
    // - Penalty: 닿는 순간(정면/측면 무관) 감속 + 무적 + 깜빡임. lane change 자체는 자유.
    // - NoEntry: HardBlock의 반대. 현재 lane이 [Min, Max] 밖일 때 안으로 들어오는 lane change 차단 (밖→안 차단). 안에서의 이동은 자유.
    // 사용처: Underground/Overpass 통로(HardBlock), 위험 영역(Penalty), 출구 safe zone 측벽(NoEntry).
    [RequireComponent(typeof(BoxCollider))]
    public class LaneRangeZone : MonoBehaviour
    {
        public enum BlockMode { HardBlock, Penalty, NoEntry, BoundaryLock }

        [Tooltip("HardBlock = 안→밖 차단. Penalty = 닿으면 감속+무적+깜빡임. NoEntry = 밖→안 차단. BoundaryLock = 경계 양방향 차단.")]
        public BlockMode Mode = BlockMode.HardBlock;

        [Header("Lane Range (HardBlock 모드만 사용)")]
        public int MinLane;
        public int MaxLane;

        [Header("Vertical Activation")]
        [Tooltip("If enabled, this zone only applies while the player's world Y is at or above MinActiveWorldY.")]
        public bool UseMinActiveWorldY;
        [Tooltip("World Y threshold used when UseMinActiveWorldY is enabled.")]
        public float MinActiveWorldY;
        [Tooltip("If enabled, this zone only applies while the player's world Y is at or below MaxActiveWorldY.")]
        public bool UseMaxActiveWorldY;
        [Tooltip("World Y threshold used when UseMaxActiveWorldY is enabled.")]
        public float MaxActiveWorldY;

        [Header("Penalty 효과 (Penalty 모드만 사용)")]
        [Tooltip("감속 비율 (0.5 = 절반 속도). PlayerController.ApplySlow로 전달.")]
        public float PenaltySpeedRatio = 0.5f;
        [Tooltip("감속 지속 시간 (초).")]
        public float PenaltySlowDuration = 1.5f;
        [Tooltip("무적/깜빡임 지속 시간 (초). 이 동안 같은 zone 재진입 + 일반 장애물(KnockDown) 충돌 무시.")]
        public float InvincibilityDuration = 1.5f;
        [Tooltip("깜빡임 1회 on/off 주기 (초). 작을수록 빠르게 깜빡.")]
        public float BlinkInterval = 0.1f;

        private void Reset() => EnsureTrigger();
        private void Awake() => EnsureTrigger();

        private void EnsureTrigger()
        {
            if (TryGetComponent<BoxCollider>(out var box))
                box.isTrigger = true;
        }

        public bool IsActiveFor(PlayerController player)
        {
            if (player == null) return false;
            if (UseMinActiveWorldY && player.transform.position.y < MinActiveWorldY) return false;
            if (UseMaxActiveWorldY && player.transform.position.y > MaxActiveWorldY) return false;
            return true;
        }
    }
}
