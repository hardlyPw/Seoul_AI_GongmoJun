using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;

    [Header("Z-Forward Quarter View Settings")]
    [Tooltip("X축 회전")]
    public float pitchAngle = 50f;
    [Tooltip("Y축 회전")]
    public float yawAngle = 270f; 

    [Header("Distance Settings")]
    [Tooltip("플레이어와 카메라 사이의 대각선 거리")]
    public float cameraDistance = 12f;
    [Tooltip("플레이어보다 얼마나 화면 앞쪽을 미리 보여줄지 결정")]
    public float zOffset = 2f;   

    [Header("Follow Speed")]
    public float followSpeed = 10f; 

    [Header("Camera Shake Settings")]
    private float shakeDuration = 0f;    // 흔들림이 지속될 남은 시간
    private float shakeMagnitude = 0f;   // 흔들림의 세기
    private Vector3 shakeOffset = Vector3.zero;
private void LateUpdate()
    {
        if (target == null) return;

        // 매 프레임 타이머를 깎고 shakeOffset을 계산하는 함수 실행
        HandleShakeUpdate();

        // x축 회전 radian 값으로 변환
        float radians = pitchAngle * Mathf.Deg2Rad;
        
        // 카메라와 플레이어 사이의 대각선 거리 기반 X, Y 오프셋 계산
        float calculatedXOffset = Mathf.Abs(cameraDistance * Mathf.Cos(radians)); 
        float calculatedYOffset = Mathf.Abs(cameraDistance * Mathf.Sin(radians));

        // 좌표계 기준에 맞춘 카메라 목표 위치 계산
        Vector3 desiredPosition = new Vector3(
            target.position.x + calculatedXOffset,
            target.position.y + calculatedYOffset,
            target.position.z + zOffset
        );

        // 부드럽게 카메라 이동
        Vector3 nextPosition = Vector3.Lerp(transform.position, desiredPosition, followSpeed * Time.deltaTime);

        // 흔들림 오프셋을 더함.
        transform.position = nextPosition + shakeOffset;

        // 카메라 회전 (X축은 pitch, Y축은 yaw)
        transform.rotation = Quaternion.Euler(pitchAngle, yawAngle, 0f);
    }

    // 외부(SubwayEnvironment 등)에서 카메라를 흔들고 싶을 때 호출하는 메서드
    public void TriggerShake(float duration, float magnitude)
    {
        shakeDuration = duration;
        shakeMagnitude = magnitude;
    }

    // 매 프레임 타이머를 깎으며 무작위 진동 오프셋을 생성하는 헬퍼 메서드
    private void HandleShakeUpdate()
    {
        if (shakeDuration > 0)
        {
            // 타이머 감소
            shakeDuration -= Time.deltaTime;

            // InsideUnitSphere를 통해 구형 범위 내의 무작위 3D 좌표를 뽑아 세기를 곱해줍니다.
            shakeOffset = Random.insideUnitSphere * shakeMagnitude;

            // 흔들림이 끝나가는 프레임에서는 자연스럽게 진동을 감쇄시킵니다.
            if (shakeDuration <= 0)
            {
                shakeOffset = Vector3.zero;
            }
        }
    }
}
