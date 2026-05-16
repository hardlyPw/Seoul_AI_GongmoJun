using UnityEngine;

public class SubwayEnvironment : MonoBehaviour
{
    [Header("Target Connections")]
    public PlayerController playerController;
    public CameraFollow cameraFollow; // [추가] 연결할 카메라 스크립트

    [Header("Subway Shake Settings")]
    public float minShakeInterval = 3f;
    public float maxShakeInterval = 6f;

    [Header("Subway Shake Intensity")]
    [Tooltip("지하철이 덜컹거리는 지속 시간(초)입니다.")]
    public float shakeDuration = 0.4f;
    [Tooltip("지하철 진동의 세기입니다. 값이 클수록 카메라가 격렬하게 흔들립니다.")]
    public float shakeMagnitude = 0.3f;

    private float shakeTimer;
    private float currentShakeInterval;

    private void Start()
    {
        ResetShakeTimer();

        // 컴포넌트 자동 찾기 방어 코드
        if (playerController == null) playerController = FindFirstObjectByType<PlayerController>();
        if (cameraFollow == null) cameraFollow = FindFirstObjectByType<CameraFollow>();
    }

    private void Update()
    {
        shakeTimer += Time.deltaTime;

        if (shakeTimer >= currentShakeInterval)
        {
            TriggerSubwayShake();
            ResetShakeTimer();
        }
    }

    private void TriggerSubwayShake()
    {
        Debug.Log("쿠구궁! 지하철 진동 및 라인 밀림 발생!");
        
        // 플레이어 랜덤 밀기
        if (playerController != null)
        {
            int randomDirection = Random.Range(0, 2) == 0 ? -1 : 1;
            playerController.PushPlayerRandomly(randomDirection);
        }

        // 카메라 흔들기 작동
        if (cameraFollow != null)
        {
            cameraFollow.TriggerShake(shakeDuration, shakeMagnitude);
        }
    }

    private void ResetShakeTimer()
    {
        shakeTimer = 0f;
        currentShakeInterval = Random.Range(minShakeInterval, maxShakeInterval);
    }
}