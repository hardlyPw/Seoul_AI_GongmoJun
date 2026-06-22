using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float     xOffset    = 0f;
    [SerializeField] private float     smoothTime = 0.2f;

    [Header("Y Follow (지하차도용)")]
    [SerializeField] private float yFollowThreshold = -1f;

    private Vector3 _velocity;
    private float   _baseY;
    private float   _baseZ; // 처음 시작할 때의 Z축 위치 보관

    public void SetTarget(Transform t) => target = t;

    private void Start()
    {
        _baseY = transform.position.y;
        _baseZ = transform.position.z; // Z값 저장
    }

    private void LateUpdate()
    {
        if (target == null) return;

        float desiredY = (target.position.y < yFollowThreshold)
            ? _baseY + target.position.y
            : _baseY;

        // 부모(Rig)의 위치만 플레이어를 스무스하게 쫓아갑니다.
        Vector3 desired = new Vector3(
            target.position.x + xOffset,
            desiredY,
            _baseZ); // transform.position.z 대신 고정된 _baseZ 사용

        transform.position = Vector3.SmoothDamp(
            transform.position, desired, ref _velocity, smoothTime);
    }
}