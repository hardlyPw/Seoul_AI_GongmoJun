using UnityEngine;

public class SubwayZoneTrigger : MonoBehaviour
{
    public enum ZoneType { FullOpen, SubwayInside, DoorConnection }
    
    [Header("구역 설정")]
    [Tooltip("이 센서를 밟았을 때 어떤 구역으로 취급할지 선택하세요.")]
    public ZoneType zoneType;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") || other.GetComponent<PlayerController>() != null)
        {
            switch (zoneType)
            {
                case ZoneType.FullOpen: // 계단, 개찰구 (6개 전체 라인 사용)
                    LaneManager.Instance.ResetLanes();
                    break;

                case ZoneType.SubwayInside: // 지하철 내부 (중앙 4라인: 1, 2, 3, 4번 라인 사용)
                    LaneManager.Instance.SetLaneRange(1, 4);
                    break;

                case ZoneType.DoorConnection: // 연결문/문 (중앙 2라인: 2, 3번 라인 사용)
                    LaneManager.Instance.SetLaneRange(2, 3);
                    break;
            }
        }
    }
}