using UnityEngine;

public class LaneManager : MonoBehaviour
{
    public static LaneManager Instance { get; private set; }

    [Header("Line Settings")]
    public int totalLanes = 6;
    public float laneSpacing = 1.5f;     // 라인 간의 간격 (X축 거리)
    public float leftMostLaneX = -3.75f; // 가장 위쪽(0번) 라인의 X 좌표

    [Header("Dynamic Access Settings")]
    [Tooltip("현재 플레이어가 진입할 수 있는 최소 라인 인덱스 (기본값: 0)")]
    public int minAccessibleLane = 0;
    [Tooltip("현재 플레이어가 진입할 수 있는 최대 라인 인덱스 (기본값: 5)")]
    public int maxAccessibleLane = 5;

    private float[] laneXPositions;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        CalculateLanePositions();
        
        // 시작할 때는 모든 라인 개방
        ResetLanes();
    }

    private void CalculateLanePositions()
    {
        laneXPositions = new float[totalLanes];
        for (int i = 0; i < totalLanes; i++)
        {
            laneXPositions[i] = leftMostLaneX + (i * laneSpacing);
        }
    }

    public float GetLaneXPosition(int laneIndex)
    {
        // 범위를 벗어나면 안전하게 한계치 좌표 리턴
        laneIndex = Mathf.Clamp(laneIndex, 0, totalLanes - 1);
        return laneXPositions[laneIndex];
    }

    // [추가] 외부 구역 센서에서 호출하여 진입 허용 라인을 강제로 가두는 메서드
    public void SetLaneRange(int minLane, int maxLane)
    {
        minAccessibleLane = Mathf.Clamp(minLane, 0, totalLanes - 1);
        maxAccessibleLane = Mathf.Clamp(maxLane, minAccessibleLane, totalLanes - 1);
        Debug.Log($"? 라인 제한 변경됨: {minAccessibleLane}번 칸 ~ {maxAccessibleLane}번 칸만 사용 가능");
    }

    // [추가] 모든 라인을 다시 열어주는 리셋 메서드 (예: 계단, 출구 구역)
    public void ResetLanes()
    {
        minAccessibleLane = 0;
        maxAccessibleLane = totalLanes - 1;
    }
}