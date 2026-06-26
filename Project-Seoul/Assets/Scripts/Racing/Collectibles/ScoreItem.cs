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
        if (other.TryGetComponent<Unity.Netcode.NetworkObject>(out var no))
        {
            if (no.IsOwner) Seoul.SoundManager.Instance.PlaySFX("get_coin");
        }
        else
        {
            Seoul.SoundManager.Instance.PlaySFX("get_coin");
        }
        ScoreManager.Instance?.AddScore(player, scoreValue);
        gameObject.SetActive(false);
    }
}
