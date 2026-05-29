using UnityEngine;
using UnityEngine.SceneManagement;
using Seoul.Network.Game;

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

            netPlayer.ReportGoalServerRpc();

            if (!string.IsNullOrEmpty(nextSceneName))
            {
                Debug.Log($"[GoalTrigger] Owner goaled — local-loading '{nextSceneName}'");
                SceneManager.LoadScene(nextSceneName, LoadSceneMode.Single);
            }
            else
            {
                Debug.LogWarning("[GoalTrigger] nextSceneName is empty — no scene load.");
            }
            return;
        }

        if (other.TryGetComponent<PlayerController>(out var player))
            StageManager.Instance?.PlayerReachedGoal(player);
    }
}
