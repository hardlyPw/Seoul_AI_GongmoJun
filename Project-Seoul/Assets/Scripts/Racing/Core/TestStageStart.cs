using UnityEngine;

// 테스트용 - GameManagers 오브젝트에 부착
// Play 시 자동으로 스테이지 시작 + 플레이어 등록
public class TestStageStart : MonoBehaviour
{
    [SerializeField] private PlayerController[] players;

    private void Start()
    {
        foreach (var p in players)
            StageManager.Instance.RegisterPlayer(p);

        StageManager.Instance.StartStage();
    }
}
