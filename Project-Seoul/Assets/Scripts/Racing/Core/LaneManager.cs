using UnityEngine;

public class LaneManager : MonoBehaviour
{
    public static LaneManager Instance { get; private set; }

    // Z축 기준 라인 위치 (5=화면 위, 0=화면 아래)
    [SerializeField] private float[] laneZPositions = { 5f, 4f, 3f, 2f, 1f, 0f };

    public int   LaneCount   => laneZPositions.Length;
    public float LaneSpacing => laneZPositions.Length >= 2
        ? Mathf.Abs(laneZPositions[0] - laneZPositions[1]) : 1f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            if (GetPriority() <= Instance.GetPriority())
            {
                Destroy(this);
                return;
            }

            Destroy(Instance);
        }

        Instance = this;
    }

    private int GetPriority()
    {
        int priority = 0;
        if (gameObject.name == "GameManagers") priority += 100;
        if (GetComponent<StageManager>() != null) priority += 50;
        if (GetComponent<ScoreManager>() != null) priority += 25;
        if (transform.position.sqrMagnitude < 100f) priority += 10;
        return priority;
    }

    public float GetLaneZ(int index) =>
        laneZPositions[Mathf.Clamp(index, 0, laneZPositions.Length - 1)];

    // minLane~maxLane 범위의 Z 중심
    public float GetLaneCenterZ(int minLane, int maxLane) =>
        (GetLaneZ(minLane) + GetLaneZ(maxLane)) / 2f;

    // minLane~maxLane 범위의 Z 총 폭 (라인 간격 포함)
    public float GetLaneSpanZ(int minLane, int maxLane) =>
        Mathf.Abs(GetLaneZ(minLane) - GetLaneZ(maxLane)) + LaneSpacing;

    // 주어진 Transform을 minLane~maxLane 범위에 맞춰 Z 좌표 정렬하고,
    // BoxCollider가 있으면 그 Z 크기도 범위 폭에 맞춤. 기믹 4종이 동일하게 쓰던 보일러플레이트.
    public void FitToLaneRange(Transform target, int minLane, int maxLane)
    {
        if (target == null) return;

        var pos = target.position;
        pos.z = GetLaneCenterZ(minLane, maxLane);
        target.position = pos;

        if (target.TryGetComponent<BoxCollider>(out var col))
        {
            var size = col.size;
            size.z = GetLaneSpanZ(minLane, maxLane);
            col.size = size;
        }
    }

    public bool IsOverpassLane(int index)  => index <= 1;
    public bool IsCafeLane(int index)      => index >= 2 && index <= 3;
    public bool IsCrosswalkLane(int index) => index >= 2;
}
