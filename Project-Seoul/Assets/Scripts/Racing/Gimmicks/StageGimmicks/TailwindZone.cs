using Unity.Netcode;
using UnityEngine;

public class TailwindZone : MonoBehaviour
{
    [Header("순풍 가속 설정")]
    [SerializeField] private float speedMultiplier = 1.3f; // 순풍 구역 진입 시 1.3배 가속

    private void OnTriggerEnter(Collider other)
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "05_Stage_Bicycle") return;

        if (other.CompareTag("Player"))
        {
            if (other.TryGetComponent<NetworkObject>(out var netObj) && netObj.IsOwner)
            {
                var player = other.GetComponent<PlayerController>();
                if (player != null)
                {
                    player.SetSpeedMultiplier(speedMultiplier);
                    Debug.Log($"[{other.name}] 로컬 순풍 가속 배율 {speedMultiplier}배 적용 완료");
                }
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            var player = other.GetComponent<PlayerController>();
            if (player != null)
            {
                player.SetSpeedMultiplier(1.0f); // 일반 기본 배율로 정상 원복
                Debug.Log($"[{other.name}] 순풍 구역 이탈: 가속 버프 해제");
            }
        }
    }
}