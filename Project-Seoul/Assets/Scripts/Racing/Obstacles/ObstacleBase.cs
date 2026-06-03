using UnityEngine;
using Unity.Netcode;

public abstract class ObstacleBase : NetworkBehaviour
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