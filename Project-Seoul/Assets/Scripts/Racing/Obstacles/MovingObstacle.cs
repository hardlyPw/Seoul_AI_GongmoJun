using UnityEngine;
using Unity.Netcode;

public class MovingObstacle : ObstacleBase
{
    public enum MoveAxis { X, Y, Z }

    [Header("이동 설정")]
    [SerializeField] private MoveAxis axis = MoveAxis.Z; // 기본값 Z축
    [SerializeField] private float moveDistance = 3f;
    [SerializeField] private float moveSpeed = 2f;

    private Rigidbody _rb;
    private Vector3 _startPos;
    private float _randomPhase;

    protected override void Awake()
    {
        base.Awake();
        _rb = GetComponent<Rigidbody>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        _startPos = transform.position;

        if (IsServer)
        {
            _randomPhase = Random.Range(0f, Mathf.PI * 2f);
        }
    }

    private void FixedUpdate()
    {
        if (!IsServer) return;

        float offset = Mathf.Sin(Time.fixedTime * moveSpeed + _randomPhase) * moveDistance;
        Vector3 targetPos = _startPos;
        switch (axis)
        {
            case MoveAxis.X: targetPos.x += offset; break;
            case MoveAxis.Y: targetPos.y += offset; break;
            case MoveAxis.Z: targetPos.z += offset; break;
        }

        _rb.MovePosition(targetPos);
    }
}