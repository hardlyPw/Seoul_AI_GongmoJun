using UnityEngine;

namespace Seoul.Network.Game
{
    public class SubwayquakeGimmick : IStageGimmick
    {
        private float _timer;
        private float _interval;
        private NetworkRaceManager _raceManager;

        // 서버 권위 + ClientRpc 동기화 모드
        public SubwayquakeGimmick(NetworkRaceManager raceManager, float interval = 5f)
        {
            _raceManager = raceManager;
            _interval = interval;
        }

        // 로컬 모드 (싱글 플레이 테스트용)
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
            // NetworkRaceManager.cs의 Update에서 이미 if (!IsServer) return; 처리를 해주지만,
            // 방어적 프로그래밍 차원에서 혹시 모를 로컬 모드와의 호환성을 위해 유지합니다.
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
            float shakeIntensity = 0.5f;

            if (_raceManager != null)
            {
                // [서버] 모든 클라이언트에게 RPC를 전송하여 카메라 흔들림과 레인 변경을 일제히 지시합니다.
                _raceManager.SendGimmickEventClientRpc(GimmickType.Subwayquake, randomDirection, shakeIntensity);
            }
            else
            {
                // [로컬 모드] 즉시 자기 화면 연출 발생
                StageEventManager.TriggerCameraShake(0.4f, shakeIntensity);
                StageEventManager.TriggerForceLaneChange(randomDirection);
            }
        }

        public void OnStageEnd() { }
    }
}