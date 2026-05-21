using UnityEngine;

// PlayerController와 같은 GameObject에 부착
public class ItemInventory : MonoBehaviour
{
    private ItemEffectBase  _heldItem;
    private PlayerController _player;

    public bool            HasItem  => _heldItem != null;
    public ItemEffectBase  HeldItem => _heldItem;

    private void Start()
    {
        _player = GetComponent<PlayerController>();
        if (_player != null) _player.OnItemUse += UseItem;
    }

    private void OnDestroy()
    {
        if (_player != null) _player.OnItemUse -= UseItem;
    }

    // 아이템 획득 - 이미 보유 중이면 기존 아이템 유지
    public bool TryPickup(ItemEffectBase item)
    {
        if (_heldItem != null) return false;
        _heldItem = item;
        Debug.Log($"아이템 획득: {item.ItemName}");
        return true;
    }

    // L키 → PlayerController.OnItemUse 이벤트 → 자동 호출
    private void UseItem()
    {
        if (_heldItem == null) return;
        _heldItem.Apply(_player);
        _heldItem = null;
    }
}
