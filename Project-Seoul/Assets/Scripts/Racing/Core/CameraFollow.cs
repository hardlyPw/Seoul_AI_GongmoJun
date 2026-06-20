using UnityEngine;

// 쿼터뷰 카메라. X축은 항상 플레이어 따라감. Y는 평소 고정, 지하차도 깊이 이하일 때만 따라감. Z는 고정.
// Inspector에서 카메라 Rotation을 (35, 0, 0)으로 설정.
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float     xOffset    = 0f;
    [SerializeField] private float     smoothTime = 0.2f;

    [Header("Y Follow (지하차도용)")]
    [Tooltip("target.y가 이 값 이하로 내려가면 카메라도 따라 내려감. 그 위에서는 baseY 고정 → 점프해도 카메라 안 흔들림.")]
    [SerializeField] private float yFollowThreshold = -1f;

    private Vector3 _velocity;
    private float   _baseY;

    public void SetTarget(Transform t) => target = t;

    private void Start()
    {
        _baseY = transform.position.y;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // 캐릭터가 threshold 이하로 내려가면 그만큼 카메라도 따라 내려감 (지상 기준 오프셋 유지).
        // 평소(점프 포함)에는 _baseY 고정.
        float desiredY = (target.position.y < yFollowThreshold)
            ? _baseY + target.position.y
            : _baseY;

        Vector3 desired = new Vector3(
            target.position.x + xOffset,
            desiredY,
            transform.position.z);

        transform.position = Vector3.SmoothDamp(
            transform.position, desired, ref _velocity, smoothTime);
    }
}
