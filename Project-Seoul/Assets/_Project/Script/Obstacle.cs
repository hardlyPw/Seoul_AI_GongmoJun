using UnityEngine;

public class Obstacle : MonoBehaviour
{
    [Header("Penalty Settings")]
    [Tooltip("장애물에 부딪혔을 때 일시적으로 저하될 속도 배율입니다. (0.4면 원래 속도의 40%로 감소)")]
    public float speedMultiplier = 0.4f;
    [Tooltip("속도 저하 페널티가 지속될 시간(초)입니다.")]
    public float penaltyDuration = 1.5f;

    private void OnTriggerEnter(Collider other)
    {
        // 부딪힌 대상이 플레이어인지 확인합니다.
        PlayerController player = other.GetComponent<PlayerController>();
        if (player != null)
        {
            Debug.Log($"[장애물 충돌] {penaltyDuration}초간 감속");
            
            // 플레이어에게 감속 페널티 명령 전달
            player.ApplySpeedPenalty(speedMultiplier, penaltyDuration);

        }
    }
}