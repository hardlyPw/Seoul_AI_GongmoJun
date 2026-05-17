using UnityEngine;

public class CameraController : MonoBehaviour
{
    public Transform target;            // 추적 대상 
    public Vector3 offset = new Vector3(0, 10, -15); // 플레이어와의 거리 오프셋
    public float smoothSpeed = 5f;      // 카메라 이동의 부드러운 정도

    void LateUpdate()
    {
        if (target == null) return;

        // X축 위치만 플레이어를 추적
        Vector3 desiredPosition = new Vector3(target.position.x + offset.x, offset.y, offset.z);

        // 부드러운 이동을 위해 Lerp 적용
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
    }
}