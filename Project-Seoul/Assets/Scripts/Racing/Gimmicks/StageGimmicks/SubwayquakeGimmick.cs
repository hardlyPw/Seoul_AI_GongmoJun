using UnityEngine;

namespace Seoul.Network.Game
{
    public class SubwayquakeGimmick : IStageGimmick
    {
        private float _timer;
        private float _interval;
        private NetworkRaceManager _raceManager;

        // 서버 권위 + ClientRpc 동기화 모드 (NetworkRaceManager가 살아있는 스테이지에서 사용)
        public SubwayquakeGimmick(NetworkRaceManager raceManager, float interval = 5f)
        {
            _raceManager = raceManager;
            _interval = interval;
        }

        // 로컬 모드: 각 클라가 자기 화면에 독립적으로 지진 효과 발동 (NetworkRaceManager 없을 때)
        public SubwayquakeGimmick(float interval = 5f)
        {
            _raceManager = null;
            _interval = interval;
        }

        public void OnStageStart()
        {
            _timer = _interval;
            string mode = _raceManager != null ? "서버 권위" : "로컬";
            Debug.Log($"[SubwayquakeGimmick] 지하철 지진 기믹 루프 시작 ({mode} 모드)");
        }

        public void OnStageUpdate()
        {
            _timer -= Time.deltaTime;
            if (_timer <= 0f)
            {
                TriggerEarthquake();
                _timer = _interval;
            }
        }

        private void TriggerEarthquake()
        {
            int randomDirection = Random.Range(0, 2) == 0 ? -1 : 1;
            float shakeIntensity = 1.2f;

            if (_raceManager != null)
            {
                _raceManager.SendGimmickEventClientRpc(GimmickType.Subwayquake, randomDirection, shakeIntensity);
            }
            else
            {
                // 로컬 모드: StageEventManager에 직접 발행 (이 클라이언트에서만 발동)
                StageEventManager.TriggerCameraShake(0.6f, shakeIntensity);
                StageEventManager.TriggerForceLaneChange(randomDirection);
            }
        }

        public void OnStageEnd() { }
    }
}