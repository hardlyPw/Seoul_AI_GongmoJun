using UnityEngine;

namespace Seoul.Network.Game
{
    // 출구 safe zone의 뒤쪽(상류 -x) 벽 마커.
    // - 안에 있는 동안 PlayerController가 _velocity.x를 0 이하로 클램프 → 더 이상 앞으로 못 감.
    // - lane change는 자유 (z 이동 막지 않음).
    // - 모든 lane을 가로지르는 얇은 trigger로 배치 → 어느 lane에서 와도 막힘.
    [RequireComponent(typeof(BoxCollider))]
    public class BackWall : MonoBehaviour
    {
        [Tooltip("If enabled, this wall only blocks while the player's world Y is at or below MaxActiveWorldY.")]
        public bool UseMaxActiveWorldY;
        [Tooltip("World Y threshold used when UseMaxActiveWorldY is enabled.")]
        public float MaxActiveWorldY;

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
            if (!UseMaxActiveWorldY) return true;
            return player.transform.position.y <= MaxActiveWorldY;
        }
    }
}
