using Unity.Netcode;
using UnityEngine;

public class BicycleJumpPad : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // 05_Stage_Bicycle 씬에 강착된 스폰 객체일 때만 트리거 유효 처리
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "05_Stage_Bicycle") return;

        if (other.CompareTag("Player"))
        {

            if (other.TryGetComponent<NetworkObject>(out var netObj) && netObj.IsOwner)
            {
                var player = other.GetComponent<PlayerController>();
                if (player != null && !player.IsFallen)
                {
                    Debug.Log($"[{other.name}] 로컬 점프대 트리거 판정 통과 -> AirborneState 진입");
                    player.ChangeState(player.AirborneState);
                }
            }
        }
    }
}