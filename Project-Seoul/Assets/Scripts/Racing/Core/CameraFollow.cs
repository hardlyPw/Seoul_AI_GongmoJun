using UnityEngine;

// 쿼터뷰 카메라. X축만 플레이어 따라감 (Y,Z는 고정).
// Inspector에서 카메라 Rotation을 (35, 0, 0)으로 설정.
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float     xOffset    = 0f;    // 플레이어 기준 X 오프셋
    [SerializeField] private float     smoothTime = 0.2f;

    private Vector3 _velocity;

    public void SetTarget(Transform t) => target = t;

    private void LateUpdate()
    {
        if (target == null) return;

        // X축만 추적, Y/Z는 씬에서 설정한 위치 고정
        Vector3 desired = new Vector3(
            target.position.x + xOffset,
            transform.position.y,
            transform.position.z);

        transform.position = Vector3.SmoothDamp(
            transform.position, desired, ref _velocity, smoothTime);
    }
}
