using UnityEngine;

// 고정형 장애물 (소화전, 뚜껑 없는 맨홀, 전봇대)
public class StaticObstacle : ObstacleBase
{
    [SerializeField] private int laneIndex = 0;

    private void Start()
    {
        var pos = transform.position;
        pos.z              = LaneManager.Instance.GetLaneZ(laneIndex);
        transform.position = pos;
    }
}
