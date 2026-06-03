using UnityEngine;

public class PuddleObstacle : ObstacleBase
{
    [Header("감속 설정")]
    [SerializeField] private float slowRatio = 0.5f;
    [SerializeField] private float slowDuration = 0.5f;

    // base의 knockDown은 무시 — puddle은 감속만.
    // owner/server 가드는 PlayerController.OnTriggerEnter가 이미 보장.
    public override void HandlePlayerEnter(PlayerController player)
    {
        player.ApplySlow(slowRatio, slowDuration);
    }
}