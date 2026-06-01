using UnityEngine;

// 접촉 시 점수 획득. IsTrigger 콜라이더 필요.
public class ScoreItem : MonoBehaviour
{
    [SerializeField] private int scoreValue = 10;
    [SerializeField] private int laneIndex  = 0;

    private void Start()
    {
        var pos = transform.position;
        pos.z              = LaneManager.Instance.GetLaneZ(laneIndex);
        transform.position = pos;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.TryGetComponent<PlayerController>(out var player)) return;
        ScoreManager.Instance?.AddScore(player, scoreValue);
        gameObject.SetActive(false);
    }
}
