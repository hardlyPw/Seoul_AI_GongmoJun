using UnityEngine;

using UnityEngine;

// 이동형 장애물 (조깅하는 사람, 어르신, 자전거 등)
public class MovingObstacle : ObstacleBase
{
    public enum MoveAxis { X, Y, Z }

    [SerializeField] private MoveAxis axis      = MoveAxis.Z;
    [SerializeField] private float    moveSpeed = 2f;

    [Header("Z축 레인 이동 (axis = Z일 때)")]
    [SerializeField] private int minLane = 0;
    [SerializeField] private int maxLane = 2;

    [Header("X/Y축 이동 (axis = X or Y일 때)")]
    [SerializeField] private int   laneIndex = 0;
    [SerializeField] private float moveRange = 3f;

    private Rigidbody _rb;
    private Vector3   _origin;
    private float     _phase;
    private float     _minZ;
    private float     _maxZ;

    protected override void Awake()
    {
        base.Awake();
        _rb    = GetComponent<Rigidbody>();
        _phase = Random.Range(0f, Mathf.PI * 2f);
    }

    private void Start()
    {
        var lm  = LaneManager.Instance;
        var pos = transform.position;

        if (axis == MoveAxis.Z)
        {
            _minZ = lm.GetLaneZ(maxLane);
            _maxZ = lm.GetLaneZ(minLane);
            pos.z = _maxZ;
        }
        else
        {
            pos.z = lm.GetLaneZ(laneIndex);
        }

        transform.position = pos;
        _origin            = transform.position;
    }

    private void FixedUpdate()
    {
        float t   = (Mathf.Sin(Time.time * moveSpeed + _phase) + 1f) * 0.5f; // 0~1
        Vector3 pos = _origin;

        switch (axis)
        {
            case MoveAxis.Z:
                pos.z = Mathf.Lerp(_minZ, _maxZ, t);
                break;
            case MoveAxis.X:
                pos.x = _origin.x + (Mathf.Sin(Time.time * moveSpeed + _phase) * moveRange);
                break;
            case MoveAxis.Y:
                pos.y = _origin.y + (Mathf.Sin(Time.time * moveSpeed + _phase) * moveRange);
                break;
        }

        _rb.MovePosition(pos);
    }
}
