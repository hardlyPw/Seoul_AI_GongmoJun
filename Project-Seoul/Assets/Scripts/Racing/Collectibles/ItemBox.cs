using UnityEngine;

// 아이템 박스. IsTrigger 콜라이더 필요.
public class ItemBox : MonoBehaviour
{
    [SerializeField] [Range(0f, 1f)] private float coffeeChance = 0.5f;
    [SerializeField] private int laneIndex = 0;

    private void Start()
    {
        ApplyLanePosition();
    }

    // 태풍 기믹: WeatherGimmick이 런타임에 lane을 재배정할 때 사용.
    public void SetLaneIndex(int idx)
    {
        laneIndex = idx;
        ApplyLanePosition();
    }

    private void ApplyLanePosition()
    {
        if (LaneManager.Instance == null) return;
        var pos = transform.position;
        pos.z = LaneManager.Instance.GetLaneZ(laneIndex);
        transform.position = pos;
    }

    private void OnTriggerEnter(Collider other) => TryGiveItem(other);
    private void OnTriggerStay(Collider other)  => TryGiveItem(other);

    private void TryGiveItem(Collider other)
    {
        if (!other.TryGetComponent<ItemInventory>(out var inventory)) return;
        if (inventory.HasItem) return;

        var item = Random.value < coffeeChance
            ? (ItemEffectBase)new CoffeeItemEffect()
            : new KickboardItemEffect();

        if (inventory.TryPickup(item))
            gameObject.SetActive(false);
    }
}
