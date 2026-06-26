using UnityEngine;

// 본인 플레이어 캐릭터 머리 위에 떠다니는 시각 마커.
// NetworkPlayer.ownerVisualMarker 에 연결되어 IsOwner 일 때만 SetActive(true) 됨.
// 위아래 살짝 둥둥 + Y축 회전으로 본인 캐릭터를 한눈에 알아볼 수 있게.
public class OwnerMarker : MonoBehaviour
{
    [Tooltip("위아래 둥둥 진폭(미터)")]
    [SerializeField] private float bobAmplitude = 0.15f;
    [Tooltip("위아래 둥둥 주기(Hz)")]
    [SerializeField] private float bobFrequency = 1.2f;
    [Tooltip("Y축 회전 속도(도/초)")]
    [SerializeField] private float rotationSpeed = 90f;

    private Vector3 _baseLocalPos;

    private void Awake()
    {
        _baseLocalPos = transform.localPosition;
    }

    private void Update()
    {
        float bob = Mathf.Sin(Time.time * bobFrequency * Mathf.PI * 2f) * bobAmplitude;
        var pos = _baseLocalPos;
        pos.y += bob;
        transform.localPosition = pos;

        transform.Rotate(0f, rotationSpeed * Time.deltaTime, 0f, Space.Self);
    }
}
