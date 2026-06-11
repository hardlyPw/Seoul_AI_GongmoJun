using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class NetworkItemBox : NetworkBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (NetworkObject.IsSpawned)
        {
            if (!IsServer) return;

            if (other.CompareTag("Player"))
            {
                var inventory = other.GetComponent<NetworkItemInventory>();
                // 인벤토리가 존재하고 슬롯이 완전히 비어있을 때만 아이템 획득 승인
                if (inventory != null && inventory.currentItem.Value == ItemType.None)
                {
                    ItemType droppedItem = GetRandomItemByRank(other.transform.position.x);
                    if (inventory.TryPickup(droppedItem))
                    {
                        GetComponent<NetworkObject>().Despawn(); // 재생성 없이 즉시 파괴
                    }
                }
            }

        }
        else
        {
            // 로컬 씬 (2~3스테이지): 클라이언트에서 직접 처리
            if (!other.CompareTag("Player")) return;
            var inventory = other.GetComponent<NetworkItemInventory>();
            if (inventory != null && inventory.currentItem.Value == ItemType.None)
            {
                ItemType item = GetRandomItemByRank(other.transform.position.x);
                if (inventory.TryPickupLocal(item))
                {
                    gameObject.SetActive(false);
                }
            }
        }
    }

    public static ItemType GetRandomItemByRank(float playerX)
    {
        List<float> xPositions = new List<float>();
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject != null)
                xPositions.Add(client.PlayerObject.transform.position.x);
        }

        // X축 좌표 기준 내림차순 정렬 (X값이 가장 클수록 1위)
        xPositions.Sort((a, b) => b.CompareTo(a));
        int rankIndex = xPositions.IndexOf(playerX);
        if (rankIndex == -1) rankIndex = 0;

        float rand = Random.value;

        if (rankIndex == 0) // 1위 확률 테이블 (코인 가중치 부여)
        {
            if (rand < 0.60f) return ItemType.Coin;
            if (rand < 0.80f) return ItemType.Coffee;
            return ItemType.AlarmClock;
        }
        else if (rankIndex == 1 || rankIndex == 2) // 2~3위 확률 테이블
        {
            if (rand < 0.40f) return ItemType.Coffee;
            if (rand < 0.80f) return ItemType.AlarmClock;
            return ItemType.Coin;
        }
        else // 4위 확률 테이블 (택시, 킥보드 돌진형 전용 등장 권한 활성화)
        {
            if (rand < 0.30f) return ItemType.Taxi;
            if (rand < 0.60f) return ItemType.Kickboard;
            if (rand < 0.75f) return ItemType.Coffee;
            if (rand < 0.90f) return ItemType.AlarmClock;
            return ItemType.Coin;
        }
    }
}