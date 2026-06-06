using UnityEngine;
using Unity.Netcode;
using System.Collections;

public enum ItemType { None, Coffee, AlarmClock, Coin, Kickboard, Taxi }

public class NetworkItemInventory : NetworkBehaviour
{
    public NetworkVariable<ItemType> currentItem = new NetworkVariable<ItemType>(ItemType.None);

    private PlayerController _player;
    private Coroutine _itemBuffCoroutine;

    public bool IsItemDashing { get; private set; }
    public bool IsLaneLocked { get; private set; }

    private void Start()
    {
        _player = GetComponent<PlayerController>();

        if (_player != null && IsOwner)
        {
            _player.OnItemUse += RequestUseItem;
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        if (_player != null && IsOwner)
        {
            _player.OnItemUse -= RequestUseItem;
        }
    }

    public bool TryPickup(ItemType item)
    {
        if (!IsServer) return false;
        if (currentItem.Value != ItemType.None) return false;

        currentItem.Value = item;
        Debug.Log($"[SERVER] 플레이어 '{gameObject.name}'이(가) 아이템 '{item}'을(를) 획득했습니다.");
        NotifyPickupClientRpc(item);
        return true;
    }

    [ClientRpc]
    private void NotifyPickupClientRpc(ItemType item)
    {
        if (IsOwner)
        {
            Debug.Log($"[CLIENT] 아이템 획득 완료: {item} (인벤토리 보관됨)");
        }
    }

    private void RequestUseItem()
    {
        if (currentItem.Value == ItemType.None) return;
        if (_player.IsFallen) return;

        UseItemServerRpc();
    }

    [ServerRpc]
    private void UseItemServerRpc()
    {
        if (currentItem.Value == ItemType.None) return;
        ItemType usedItem = currentItem.Value;
        currentItem.Value = ItemType.None;

        Debug.Log($"[SERVER] 플레이어 '{gameObject.name}'이(가) 아이템 '{usedItem}'을(를) 사용했습니다.");

        if (usedItem == ItemType.Coin)
        {
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.AddScore(_player, 10);
            }
        }

        ApplyItemEffectClientRpc(usedItem);
    }

    [ClientRpc]
    private void ApplyItemEffectClientRpc(ItemType usedItem)
    {
        if (!IsOwner) return;

        switch (usedItem)
        {
            case ItemType.Coffee:
                _player.RecoverStamina(_player.MaxStamina);
                break;

            case ItemType.Coin:
                break;

            case ItemType.AlarmClock:
                if (_itemBuffCoroutine != null) StopCoroutine(_itemBuffCoroutine);
                _itemBuffCoroutine = StartCoroutine(AlarmClockRoutine(3f));
                break;

            case ItemType.Kickboard:
                if (_itemBuffCoroutine != null) StopCoroutine(_itemBuffCoroutine);
                _itemBuffCoroutine = StartCoroutine(DashItemRoutine(3f, false));
                break;

            case ItemType.Taxi:
                if (_itemBuffCoroutine != null) StopCoroutine(_itemBuffCoroutine);
                _itemBuffCoroutine = StartCoroutine(DashItemRoutine(4f, true));
                break;
        }
    }

    private IEnumerator AlarmClockRoutine(float duration)
    {
        _player.SetSpeedMultiplier(1.5f);
        yield return new WaitForSeconds(duration);
        _player.SetSpeedMultiplier(1.0f);
    }

    private IEnumerator DashItemRoutine(float duration, bool isTaxi)
    {
        IsItemDashing = true;
        _player.SetSpeedMultiplier(2.0f);

        if (isTaxi)
        {
            IsLaneLocked = true;
            int centerLane = (LaneManager.Instance != null) ? LaneManager.Instance.LaneCount / 2 : 3;
            _player.ForceSetLane(centerLane);
        }

        float timer = 0f;
        while (timer < duration)
        {
            if (_player.IsFallen) break;

            timer += Time.deltaTime;

            if (isTaxi)
            {
                int centerLane = (LaneManager.Instance != null) ? LaneManager.Instance.LaneCount / 2 : 3;
                _player.ForceSetLane(centerLane);
            }

            CheckDashCollision();
            yield return null;
        }

        IsItemDashing = false;
        IsLaneLocked = false;
        _player.SetSpeedMultiplier(1.0f);
    }

    private void CheckDashCollision()
    {
        Vector3 center = transform.position + Vector3.up * 1f;
        var hits = Physics.OverlapSphere(center, 1.2f);
        foreach (var hit in hits)
        {
            if (hit.transform == transform) continue;
            if (hit.CompareTag("Player"))
            {
                var otherPlayer = hit.GetComponent<PlayerController>();
                if (otherPlayer != null && !otherPlayer.IsFallen)
                {
                    if (otherPlayer.TryGetComponent<NetworkObject>(out var targetNetObj))
                    {
                        RequestStunPlayerServerRpc(targetNetObj.NetworkObjectId);
                    }
                }
            }
        }
    }

    // 피격 대상의 기절 연산을 모든 네트워크 클라이언트에 동기화하기 위한 RPC 구조
    [ServerRpc]
    private void RequestStunPlayerServerRpc(ulong targetNetObjectId)
    {
        NotifyStunPlayerClientRpc(targetNetObjectId);
    }

    [ClientRpc]
    private void NotifyStunPlayerClientRpc(ulong targetNetObjectId)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetNetObjectId, out var netObj))
        {
            var targetPlayer = netObj.GetComponent<PlayerController>();
            if (targetPlayer != null && !targetPlayer.IsFallen)
            {
                targetPlayer.TriggerFall(); // 대상 플레이어가 자신의 화면을 포함한 전체 화면에서 정상 기절함
                Debug.Log($"[NETWORK-STUN] 플레이어 '{targetPlayer.name}'이(가) 아이템 돌진 충돌로 인해 스턴 상태로 진입했습니다.");
            }
        }
    }

    // [자전거 스테이지 기믹용 추가]: 클라이언트(Owner)가 QTE 성공 시 서버 공간에서 점수를 합산하도록 지시하는 RPC
    [ServerRpc]
    public void RequestAddScoreServerRpc(int amount)
    {
        if (ScoreManager.Instance != null && _player != null)
        {
            ScoreManager.Instance.AddScore(_player, amount);
            Debug.Log($"[SERVER] QTE 성공 신호 접수 완료 -> '{gameObject.name}'에게 {amount}pt 동기화 복사 진행");
        }
    }
}