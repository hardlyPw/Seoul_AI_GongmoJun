using UnityEngine;
using Unity.Netcode;

public class PuddleObstacle : ObstacleBase
{
    [Header("감속 설정")]
    [SerializeField] private float slowRatio = 0.5f;
    [SerializeField] private float slowDuration = 0.5f;

    protected override void Awake()
    {
        base.Awake();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // 멀티플레이어 둔화
            var netObj = other.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsOwner)
            {
                var player = other.GetComponent<PlayerController>();
                if (player != null)
                {
                    player.ApplySlow(slowRatio, slowDuration);
                }
            }
        }
    }
}