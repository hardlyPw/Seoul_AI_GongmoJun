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
        var lm  = LaneManager.Instance;
        var pos = transform.position;
        pos.z              = lm.GetLaneCenterZ(minLane, maxLane);
        transform.position = pos;

        if (TryGetComponent<BoxCollider>(out var col))
        {
            var size = col.size;
            size.z   = lm.GetLaneSpanZ(minLane, maxLane);
            col.size = size;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<PlayerController>(out var player))
            player.SetSpeedMultiplier(speedMultiplier);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent<PlayerController>(out var player))
            player.SetSpeedMultiplier(1f);
    }
}
