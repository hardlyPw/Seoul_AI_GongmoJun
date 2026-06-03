using UnityEngine;

// 육교 구역 - 진입 시 속도 감소, 이탈 시 원상복구.
// IsTrigger BoxCollider 필요. minLane~maxLane 범위를 자동으로 커버.
public class OverpassZone : MonoBehaviour
{
    [SerializeField] private float speedMultiplier = 0.6f;
    [SerializeField] private int   minLane         = 0;
    [SerializeField] private int   maxLane         = 1;

    private void Start()
    {
        LaneManager.Instance.FitToLaneRange(transform, minLane, maxLane);
    }

    private const string Source = "overpass";

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<PlayerController>(out var player))
            player.SetSpeedMultiplier(Source, speedMultiplier);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent<PlayerController>(out var player))
            player.ClearSpeedMultiplier(Source);
    }
}
