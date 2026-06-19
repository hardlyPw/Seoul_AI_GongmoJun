using UnityEngine;

namespace Seoul.Network.Game
{
    // 횡단보도 페널티 트리거. Collider(IsTrigger=true) 필요.
    // 빨간불일 때 RUN/DASH 상태로 진입하면 서버 권위로 TriggerFall.
    [RequireComponent(typeof(Collider))]
    public class CrosswalkPenaltyZone : MonoBehaviour
    {
        [SerializeField] private CrosswalkGimmick crosswalk;

        private void Reset()
        {
            crosswalk = GetComponentInParent<CrosswalkGimmick>();
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other) => Evaluate(other);
        // 영역 안에서 도중에 J(달리기/대시)를 시작하는 경우도 잡기 위해 Stay에서도 평가.
        // 이미 IsFallen이면 Evaluate가 조기 종료하므로 중복 처리 안전.
        private void OnTriggerStay(Collider other)  => Evaluate(other);

        private void Evaluate(Collider other)
        {
            if (crosswalk == null) return;
            if (crosswalk.IsGreen) return;

            // 서버가 모든 페널티를 권위적으로 처리. 클라가 보낸 이벤트는 무시.
            if (!crosswalk.IsServer) return;

            if (!other.TryGetComponent<PlayerController>(out var player)) return;
            if (player.IsFallen) return;
            if (!IsSprintingOrDashing(player)) return;

            player.TriggerFall();
        }

        private static bool IsSprintingOrDashing(PlayerController player)
        {
            return player.IsSprinting || player.IsDashing;
        }
    }
}
