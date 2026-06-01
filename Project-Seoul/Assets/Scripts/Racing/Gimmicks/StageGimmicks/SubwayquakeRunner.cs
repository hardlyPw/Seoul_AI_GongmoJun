using UnityEngine;

namespace Seoul.Network.Game
{
    // 04_Stage_Subway 씬 루트에 빈 GameObject로 배치.
    // 각 클라이언트가 로컬로 subway 스테이지에 진입했을 때 본인 화면에 지진 효과를 굴림.
    // SubwayquakeGimmick의 로컬 생성자 모드를 래핑해 MonoBehaviour 수명에 맞춰 호출.
    public class SubwayquakeRunner : MonoBehaviour
    {
        [Tooltip("지진 발동 간격(초)")]
        [SerializeField] private float interval = 6f;

        private SubwayquakeGimmick _gimmick;

        private void Start()
        {
            _gimmick = new SubwayquakeGimmick(interval);
            _gimmick.OnStageStart();
        }

        private void Update()
        {
            _gimmick?.OnStageUpdate();
        }

        private void OnDestroy()
        {
            _gimmick?.OnStageEnd();
        }
    }
}
