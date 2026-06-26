using System.Collections;
using UnityEngine;

// CameraFollow 와 동일한 world-space 추적 + dash dynamic offset + 화면 흔들림.
// CameraFollow 의 로직을 그대로 가져왔고, 여기에 StageEventManager 의 흔들림 이벤트 처리만 추가됨.
// 04_Stage_Subway 같이 카메라 흔들림이 필요한 씬엔 CameraFollow 대신 이 컴포넌트를 붙임.
public class CameraFollowAndShake : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float     xOffset    = 0f;
    [SerializeField] private float     smoothTime = 0.2f;

    [Header("Dash Offset (대시 중 카메라가 뒤에 머물러 플레이어가 화면 우측으로 밀리게)")]
    [Tooltip("IsDashing == true 일 때 사용할 xOffset. 음수면 카메라가 플레이어 뒤로 가서 플레이어가 화면 우측으로 이동.")]
    [SerializeField] private float dashXOffset = 0f;

    [Header("Y Follow (지하차도용)")]
    [SerializeField] private float yFollowThreshold = -1f;

    private Vector3 _velocity;
    private float   _baseY;
    private float   _baseZ;
    private PlayerController _targetPlayer;

    // 흔들림을 빼고 보관하는 카메라 본체 위치. SmoothDamp 가 shake offset 누적 영향을 안 받게 분리.
    private Vector3 _basePos;
    private Vector3 _shakeOffset = Vector3.zero;
    private Coroutine _shakeCoroutine;

    public void SetTarget(Transform t)
    {
        target = t;
        _targetPlayer = t != null ? t.GetComponent<PlayerController>() : null;
    }

    private void Start()
    {
        _baseY = transform.position.y;
        _baseZ = transform.position.z;
        _basePos = transform.position;
        if (target != null && _targetPlayer == null)
            _targetPlayer = target.GetComponent<PlayerController>();
    }

    private void OnEnable()  => StageEventManager.OnCameraShakeRequested += PlayShake;
    private void OnDisable() => StageEventManager.OnCameraShakeRequested -= PlayShake;

    private void LateUpdate()
    {
        if (target == null) return;

        float effectiveXOffset = (_targetPlayer != null && _targetPlayer.IsDashing)
            ? dashXOffset : xOffset;

        float desiredY = (target.position.y < yFollowThreshold)
            ? _baseY + target.position.y
            : _baseY;

        Vector3 desired = new Vector3(
            target.position.x + effectiveXOffset,
            desiredY,
            _baseZ);

        _basePos = Vector3.SmoothDamp(_basePos, desired, ref _velocity, smoothTime);
        transform.position = _basePos + _shakeOffset;
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
            float shakeX = Random.Range(-1f, 1f) * intensity;
            float shakeY = Random.Range(-0.2f, 0.2f) * intensity;
            _shakeOffset = new Vector3(shakeX, shakeY, 0f);
            elapsed += Time.deltaTime;
            yield return null;
        }
        _shakeOffset = Vector3.zero;
        _shakeCoroutine = null;
    }
}
