using Unity.Netcode;
using UnityEngine;

namespace Seoul.Network.Game
{
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(Collider))]
    public class NetworkScoreItem : NetworkBehaviour
    {
        [SerializeField] private int  scoreValue   = 10;
        [SerializeField] private int  laneIndex    = 0;
        [SerializeField] private bool alignToLane  = true;

        private void Start()
        {
            if (alignToLane && LaneManager.Instance != null)
            {
                var pos = transform.position;
                pos.z              = LaneManager.Instance.GetLaneZ(laneIndex);
                transform.position = pos;
            }

            if (TryGetComponent<Collider>(out var col)) col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsServer) return;
            if (!IsSpawned) return;

            var netPlayer = other.GetComponentInParent<NetworkPlayer>();
            if (netPlayer == null) return;

            netPlayer.AddScore(scoreValue);
            NetworkObject.Despawn(true);
        }
    }
}
