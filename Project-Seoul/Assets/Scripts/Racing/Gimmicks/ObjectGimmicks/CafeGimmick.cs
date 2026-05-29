using System;
using System.Collections.Generic;
using UnityEngine;

// 카페 기믹. IsTrigger BoxCollider 필요. minLane~maxLane 범위를 자동으로 커버.
// 플레이어가 구역 안에서 Q키 → 커피 주문 → 스코어 + 커피 아이템 획득
public class CafeGimmick : MonoBehaviour
{
    [SerializeField] private int scoreReward = 50;
    [SerializeField] private int minLane     = 0;
    [SerializeField] private int maxLane     = 2;

    private void Start()
    {
        var lm  = LaneManager.Instance;
        var pos = transform.position;
        pos.z              = lm.GetLaneCenterZ(minLane, maxLane);
        transform.position = pos;

        if (TryGetComponent<BoxCollider>(out var col))
        {
            var size = col.size;
            size.z   = lm.GetLaneSpanZ(minLane, maxLane);
            col.size = size;
        }
    }

    private readonly Dictionary<PlayerController, Action> _subscriptions = new();
    private readonly HashSet<PlayerController>            _servedPlayers = new();

    private void OnTriggerEnter(Collider other)
    {
        if (!other.TryGetComponent<PlayerController>(out var player)) return;

        Action action = () => TryOrderCoffee(player);
        _subscriptions[player] = action;
        player.OnInteract += action;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.TryGetComponent<PlayerController>(out var player)) return;
        if (!_subscriptions.TryGetValue(player, out var action)) return;

        player.OnInteract -= action;
        _subscriptions.Remove(player);
    }

    private void TryOrderCoffee(PlayerController player)
    {
        if (_servedPlayers.Contains(player)) return;
        _servedPlayers.Add(player);

        ScoreManager.Instance?.AddScore(player, scoreReward);
        player.GetComponent<ItemInventory>()?.TryPickup(new CoffeeItemEffect());
        Debug.Log($"{player.name}: 커피 주문 완료! (+{scoreReward}점 + 커피 아이템)");
    }
}
