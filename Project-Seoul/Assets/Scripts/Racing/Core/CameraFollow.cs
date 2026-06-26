using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float     xOffset    = 0f;
    [SerializeField] private float     smoothTime = 0.2f;

    [Header("Dash Offset (대시 중 카메라가 뒤에 머물러 플레이어가 화면 우측으로 밀리게)")]
    [Tooltip("IsDashing == true 일 때 사용할 xOffset. 음수면 카메라가 플레이어 뒤로 가서 플레이어가 화면 우측으로 이동.")]
    [SerializeField] private float dashXOffset = -1.5f;

    [Header("Y Follow (지하차도용)")]
    [SerializeField] private float yFollowThreshold = -1f;

    private Vector3 _velocity;
    private float   _baseY;
    private float   _baseZ; // 처음 시작할 때의 Z축 위치 보관
    private PlayerController _targetPlayer;

    public void SetTarget(Transform t)
    {
        target = t;
        _targetPlayer = t != null ? t.GetComponent<PlayerController>() : null;
    }

    private void Start()
    {
        _baseY = transform.position.y;
        _baseZ = transform.position.z; // Z값 저장
        if (target != null && _targetPlayer == null)
            _targetPlayer = target.GetComponent<PlayerController>();
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // 대시 중엔 dashXOffset 으로 hard switch. SmoothDamp(smoothTime) 이 카메라 이동을 부드럽게 보간해줌.
        float effectiveXOffset = (_targetPlayer != null && _targetPlayer.IsDashing)
            ? dashXOffset : xOffset;

        float desiredY = (target.position.y < yFollowThreshold)
            ? _baseY + target.position.y
            : _baseY;

        // 부모(Rig)의 위치만 플레이어를 스무스하게 쫓아갑니다.
        Vector3 desired = new Vector3(
            target.position.x + effectiveXOffset,
            desiredY,
            _baseZ); // transform.position.z 대신 고정된 _baseZ 사용

        transform.position = Vector3.SmoothDamp(
            transform.position, desired, ref _velocity, smoothTime);
    }
}