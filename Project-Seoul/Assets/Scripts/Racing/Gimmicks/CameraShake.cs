using System.Collections;
using UnityEngine;

public class CameraFollowAndShake : MonoBehaviour
{
    // [기존 변수 이름과 역할 100% 동일 유지]
    [SerializeField] private Transform target;
    [SerializeField] private float     xOffset    = 0f;
    [SerializeField] private float     smoothTime = 0.2f;

    [Header("Y Follow (지하차도용)")]
    [SerializeField] private float yFollowThreshold = -1f;

    // 인스펙터에 적어두셨던 이쁜 오프셋 높이와 깊이 (Y: 7, Z: -8)
    private float _defaultOffsetY;
    private float _defaultOffsetZ;

    private Vector3 _velocity;
    private Vector3 _shakeOffset = Vector3.zero;
    private Coroutine _shakeCoroutine;

    public void SetTarget(Transform t) => target = t;

    private void Start()
    {
        if (target == null) target = transform.parent;

        // [보정] 인스펙터 값을 믿지 말고, 우리가 원하는 완벽한 값을 강제로 하드코딩해 둡니다.
        _defaultOffsetY = 7f;   // (0, 2, -6)으로 꼬이는 걸 원천 차단
        _defaultOffsetZ = -8f;

        // 에디터 실행 시 각도가 15도로 꺾이는 걸 방지하기 위해 로컬 각도를 35도로 강제 고정합니다.
        transform.localRotation = Quaternion.Euler(35f, 0f, 0f);
    }

    private void OnEnable()
    {
        StageEventManager.OnCameraShakeRequested += PlayShake;
    }

    private void OnDisable()
    {
        StageEventManager.OnCameraShakeRequested -= PlayShake;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // 기존 수식 동일
        float desiredLocalY = (target.position.y < yFollowThreshold)
            ? _defaultOffsetY
            : _defaultOffsetY - target.position.y;

        float targetLocalX = Mathf.SmoothDamp(transform.localPosition.x, xOffset, ref _velocity.x, smoothTime);
        
        Vector3 desiredLocalPos = new Vector3(targetLocalX, desiredLocalY, _defaultOffsetZ);

        // [핵심 추가] 누군가 매 프레임 내 각도를 15도로 꺾으려고 간섭하므로, 
        // LateUpdate 최하단에서 내 로컬 각도를 강제로 다시 35도로 덮어써 버립니다!
        transform.localRotation = Quaternion.Euler(35f, 0f, 0f);

        // 최종 위치 적용 (흔들림 포함)
        transform.localPosition = desiredLocalPos + _shakeOffset;
    }

    public void PlayShake(float duration, float intensity)
    {
        if (_shakeCoroutine != null) StopCoroutine(_shakeCoroutine);
        _shakeCoroutine = StartCoroutine(ShakeRoutine(duration, intensity));
    }

    private IEnumerator ShakeRoutine(float duration, float intensity)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            // 카메라 시선 방향 기준 좌우/위아래 흔들림 분리
            float shakeX = Random.Range(-1f, 1f) * intensity;
            float shakeY = Random.Range(-0.2f, 0.2f) * intensity;

            // 로컬 좌표계 기준으로 흔들림 오프셋 생성
            _shakeOffset = (Vector3.right * shakeX) + (Vector3.up * shakeY);

            elapsed += Time.deltaTime;
            yield return null;
        }
        _shakeOffset = Vector3.zero;
        _shakeCoroutine = null;
    }
}