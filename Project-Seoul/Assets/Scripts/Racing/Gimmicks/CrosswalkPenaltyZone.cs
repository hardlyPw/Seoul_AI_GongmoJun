using Unity.Netcode;
using UnityEngine;

namespace Seoul.Network.Game
{
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
        private void OnTriggerStay(Collider other) => Evaluate(other);

        private void Evaluate(Collider other)
        {
            if (crosswalk == null) return;
            if (crosswalk.IsGreen) return;

            if (!other.TryGetComponent<PlayerController>(out var player)) return;
            if (player.IsFallen) return;
            if (!IsSprintingOrDashing(player)) return;

            if (crosswalk.IsServer)
            {
                crosswalk.PenalizePlayer(player);
                return;
            }

            if (!player.TryGetComponent<NetworkObject>(out var playerNetObj)) return;
            if (!playerNetObj.IsOwner) return;

            crosswalk.RequestPenalty(playerNetObj.NetworkObjectId);
        }

        private static bool IsSprintingOrDashing(PlayerController player)
        {
            return player.IsSprinting || player.IsDashing;
        }
    }
}
