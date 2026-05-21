using UnityEngine;

// 결승선 오브젝트에 부착. IsTrigger 콜라이더 필요.
public class GoalTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<PlayerController>(out var player))
            StageManager.Instance?.PlayerReachedGoal(player);
    }
}
