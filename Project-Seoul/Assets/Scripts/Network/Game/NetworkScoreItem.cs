using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Seoul.Network.Game
{
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(Collider))]
    public class NetworkScoreItem : NetworkBehaviour
    {
        [SerializeField] private int   scoreValue   = 10;
        [SerializeField] private int   laneIndex    = 0;
        [SerializeField] private bool  alignToLane  = true;

        private bool   _localConsumed     = false;
        private string _itemId            = "";
        private string _sceneName         = "";
        private bool   _itemSyncSubscribed = false;

        private void Start()
        {
            if (alignToLane && LaneManager.Instance != null)
            {
                var pos = transform.position;
                pos.z              = LaneManager.Instance.GetLaneZ(laneIndex);
                transform.position = pos;
            }

            if (TryGetComponent<Collider>(out var col)) col.isTrigger = true;

            if (!IsSpawned)
            {
                // 로컬 로드된 씬 (Stage 2/3): 서버와 동기화된 소비 상태에 따라 표시 결정
                _sceneName = gameObject.scene.name;
                _itemId    = ComputeItemId();

                if (StageItemSync.IsConsumed(_sceneName, _itemId))
                {
                    gameObject.SetActive(false);
                    return;
                }

                StageItemSync.OnItemConsumed += OnAnyItemConsumed;
                _itemSyncSubscribed = true;
            }
        }

        private void OnDisable()
        {
            if (_itemSyncSubscribed)
            {
                StageItemSync.OnItemConsumed -= OnAnyItemConsumed;
                _itemSyncSubscribed = false;
            }
        }

        // 씬 내 (이름 + 좌표) 기반 결정적 ID. 같은 씬을 다시 로드해도 같은 ID.
        private string ComputeItemId()
        {
            var p = transform.position;
            return $"{gameObject.name}@{Mathf.RoundToInt(p.x * 10)}_{Mathf.RoundToInt(p.y * 10)}_{Mathf.RoundToInt(p.z * 10)}";
        }

        private void OnAnyItemConsumed(string sceneName, string itemId)
        {
            if (sceneName == _sceneName && itemId == _itemId)
            {
                gameObject.SetActive(false);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // 충돌 감지 자체는 여전히 NetworkPlayer(부모 또는 본인)가 맞는지 체크용으로 사용합니다.
            var netPlayer = other.GetComponentInParent<NetworkPlayer>();
            if (netPlayer == null) return;

            if (IsSpawned)
            {
                // NGO 동기화된 씬 (Stage 1): 서버 권한 처리
                if (!IsServer) return;

                // [수정 완료] NetworkPlayer가 아닌 분리된 PlayerScore 컴포넌트를 가져와 점수 추가
                if (netPlayer.TryGetComponent<PlayerScore>(out var playerScore))
                {
                    playerScore.AddScore(scoreValue);
                }
                
                HideClientRpc();
                NetworkObject.Despawn(false);
            }
            else
            {
                // 로컬 로드된 씬 (Stage 2/3): owner가 자기 점수 + 소비 보고
                if (!netPlayer.IsOwner) return;
                if (_localConsumed) return;
                _localConsumed = true;

                // [수정 완료] PlayerScore 컴포넌트를 찾아 이사 간 ServerRpc를 안전하게 요청
                if (netPlayer.TryGetComponent<PlayerScore>(out var playerScore))
                {
                    playerScore.ReportLocalScorePickupServerRpc(scoreValue);
                }

                // 아이템 소비 장부 기록은 원래 설계대로 NetworkPlayer의 RPC 유지
                netPlayer.ReportConsumedItemServerRpc(
                    new FixedString64Bytes(_sceneName),
                    new FixedString64Bytes(_itemId));

                gameObject.SetActive(false);
            }
        }

        [ClientRpc]
        private void HideClientRpc() => gameObject.SetActive(false);
    }
}