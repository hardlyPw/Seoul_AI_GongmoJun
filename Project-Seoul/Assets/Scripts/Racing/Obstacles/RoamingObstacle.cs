using UnityEngine;
using Unity.Netcode;

public class RoamingObstacle : ObstacleBase
{
    [Header("배회(Roaming) 설정")]
    [SerializeField] private float roamRadiusX = 5f;
    [SerializeField] private float roamRadiusZ = 5f;
    [SerializeField] private float moveSpeed = 3f;

    private Rigidbody _rb;
    private Vector3 _originPos;
    private Vector3 _targetPos;

    protected override void Awake()
    {
        base.Awake();
        _rb = GetComponent<Rigidbody>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        _originPos = transform.position;

        if (IsServer)
        {
            SetNewTarget();
        }
    }

    private void FixedUpdate()
    {
        if (!IsServer) return;

        Vector3 newPos = Vector3.MoveTowards(_rb.position, _targetPos, moveSpeed * Time.fixedDeltaTime);
        _rb.MovePosition(newPos);

        if (Vector3.Distance(_rb.position, _targetPos) < 0.1f)
        {
            SetNewTarget();
        }
    }

    private void SetNewTarget()
    {
        float randomX = Random.Range(-roamRadiusX, roamRadiusX);
        float randomZ = Random.Range(-roamRadiusZ, roamRadiusZ);

        _targetPos = _originPos + new Vector3(randomX, 0f, randomZ);
    }
}