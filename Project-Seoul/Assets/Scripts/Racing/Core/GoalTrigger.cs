using UnityEngine;
using UnityEngine.SceneManagement;
using Seoul.Network.Game;
using Unity.Collections;

// 결승선 오브젝트에 부착. IsTrigger 콜라이더 필요.
public class GoalTrigger : MonoBehaviour
{
    [Tooltip("이 스테이지의 골인 후 로컬에서 로드할 다음 씬 이름.")]
    [SerializeField] private string nextSceneName = "";

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<NetworkPlayer>(out var netPlayer))
        {
            if (!netPlayer.IsOwner) return;
            if (netPlayer.HasFinished.Value) return;

            // nextSceneName을 서버 RPC로 전달 — 모든 플레이어 골인 시 서버가 일괄 전환함
            Debug.Log($"[GoalTrigger] Owner goaled — reporting to server with nextScene='{nextSceneName}'");
            netPlayer.ReportGoalServerRpc(new FixedString64Bytes(nextSceneName ?? ""));
            return;
        }

        if (other.TryGetComponent<PlayerController>(out var player))
            StageManager.Instance?.PlayerReachedGoal(player);
    }
}
