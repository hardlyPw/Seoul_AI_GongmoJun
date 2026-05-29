using UnityEngine;

namespace Seoul.Network.Game
{
    public class SubwayquakeGimmick : IStageGimmick
    {
        private float _timer;
        private float _interval;
        private NetworkRaceManager _raceManager;

        public SubwayquakeGimmick(NetworkRaceManager raceManager, float interval = 5f)
        {
            _raceManager = raceManager;
            _interval = interval;
        }

        public void OnStageStart()
        {
            _timer = _interval;
            Debug.Log("[SubwayquakeGimmick] 지하철 지진 기믹 루프가 서버에서 시작되었습니다.");
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

            _raceManager.SendGimmickEventClientRpc(GimmickType.Subwayquake, randomDirection, shakeIntensity);
        }

        public void OnStageEnd() { }
    }
}