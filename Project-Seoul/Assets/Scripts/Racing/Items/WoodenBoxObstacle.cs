using UnityEngine;
using Unity.Netcode;

public class WoodenBoxObstacle : NetworkBehaviour
{
    [Header("판정 설정")]
    [SerializeField] private float jumpOverThreshold = 1.2f; // 상자 Y축 기준 점프 통과 최소 고도 인정 수치

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            var inventory = other.GetComponent<NetworkItemInventory>();
            var player = other.GetComponent<PlayerController>();
            if (inventory == null || player == null) return;

            // 분기 1: 플레이어 고도가 임계점을 넘었을 때 (점프로 정상 통과, 아무 효과 없음)
            if (other.transform.position.y >= transform.position.y + jumpOverThreshold)
            {
                return;
            }

            // 분기 2: 아이템 대시(킥보드, 택시) 상태로 상자에 정면 부딪혔을 때 (상자 파괴 및 아이템 즉시 지급)
            if (inventory.IsItemDashing)
            {
                if (IsServer)
                {
                    // 내 인벤토리가 완전히 비어있을 때만 순위 기반 보상 즉시 지급
                    if (inventory.currentItem.Value == ItemType.None)
                    {
                        ItemType item = NetworkItemBox.GetRandomItemByRank(other.transform.position.x);
                        inventory.TryPickup(item);
                    }
                    GetComponent<NetworkObject>().Despawn(); // 상자 파괴 처리
                }
            }
            // 분기 3: 대시 상태가 아닌데 정면 충돌했을 때 (일반 장애물 충돌과 동일하게 강제 기절)
            else
            {
                player.TriggerFall();
            }
        }
    }
}