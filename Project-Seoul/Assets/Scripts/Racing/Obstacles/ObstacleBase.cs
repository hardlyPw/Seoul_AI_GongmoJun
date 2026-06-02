using UnityEngine;
using Unity.Netcode;

public abstract class ObstacleBase : NetworkBehaviour
{
    [SerializeField] private bool knockDownOnCollision = true;

    protected virtual void Awake()
    {
        if (!TryGetComponent<Rigidbody>(out var rb))
            rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        if (TryGetComponent<Collider>(out var col))
            col.isTrigger = true;
    }

    // 플레이어가 트리거에 진입했을 때 이 장애물이 가하는 효과.
    // default: KnockDown 플래그가 켜져 있으면 fall + knockback.
    // Puddle 등 다른 효과를 가진 파생은 override.
    public virtual void HandlePlayerEnter(PlayerController player)
    {
        if (knockDownOnCollision) player.HitByObstacle(transform.position);
    }
}