using UnityEngine;

// 모든 장애물의 기반 클래스.
// Kinematic Rigidbody를 자동 추가하여 OnCollisionEnter가 작동하도록 함.
public abstract class ObstacleBase : MonoBehaviour
{
    [SerializeField] private bool knockDownOnCollision = true;
    public bool KnockDownOnCollision => knockDownOnCollision;

    protected virtual void Awake()
    {
        if (!TryGetComponent<Rigidbody>(out var rb))
            rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        if (TryGetComponent<Collider>(out var col))
            col.isTrigger = true;
    }
}
