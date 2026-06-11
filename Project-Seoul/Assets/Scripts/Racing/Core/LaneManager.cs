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
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public float GetLaneZ(int index) =>
        laneZPositions[Mathf.Clamp(index, 0, laneZPositions.Length - 1)];

    // minLane~maxLane 범위의 Z 중심
    public float GetLaneCenterZ(int minLane, int maxLane) =>
        (GetLaneZ(minLane) + GetLaneZ(maxLane)) / 2f;

    // minLane~maxLane 범위의 Z 총 폭 (라인 간격 포함)
    public float GetLaneSpanZ(int minLane, int maxLane) =>
        Mathf.Abs(GetLaneZ(minLane) - GetLaneZ(maxLane)) + LaneSpacing;

    public bool IsOverpassLane(int index)  => index <= 1;
    public bool IsCafeLane(int index)      => index >= 2 && index <= 3;
    public bool IsCrosswalkLane(int index) => index >= 2;
}
