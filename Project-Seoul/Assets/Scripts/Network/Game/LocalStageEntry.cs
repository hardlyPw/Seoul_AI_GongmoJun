using System.Collections;
using Unity.Collections;
using UnityEngine;

namespace Seoul.Network.Game
{
    // Stage 2 / Stage 3 씬에 배치. 클라이언트가 단독으로 로컬 LoadScene 한 후,
    // 자기 NetworkPlayer를 스폰지점으로 옮기고 골인 상태 + 아이템 소비 동기화 요청.
    public class LocalStageEntry : MonoBehaviour
    {
        [Tooltip("플레이어가 이 스테이지에서 시작할 위치/회전")]
        [SerializeField] private Transform spawnPoint;

        [Tooltip("NetworkPlayer가 활성화될 때까지 기다리는 최대 시간(초)")]
        [SerializeField] private float ownerWaitTimeout = 3f;

        private void Awake()
        {
            // 이 씬의 로컬 소비 캐시를 비움 — 서버 응답으로 다시 채워질 것.
            // Awake에서 처리해야 NetworkScoreItem.Start가 stale 캐시를 안 본다.
            StageItemSync.ClearScene(gameObject.scene.name);
        }

        private void Start()
        {
            StartCoroutine(InitWhenOwnerReady());
        }

        private IEnumerator InitWhenOwnerReady()
        {
            NetworkPlayer owner = null;
            float elapsed = 0f;

            while (owner == null && elapsed < ownerWaitTimeout)
            {
                foreach (var p in NetworkPlayer.All)
                {
                    if (p != null && p.IsOwner) { owner = p; break; }
                }
                if (owner != null) break;
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }

            if (owner == null)
            {
                Debug.LogWarning("[LocalStageEntry] Owner NetworkPlayer not found within timeout.");
                yield break;
            }

            // 서버로부터 이 씬의 기존 소비 목록을 받아옴 (스펙테이터든 정상 플레이어든 둘 다 필요)
            owner.RequestConsumedItemListServerRpc(new FixedString64Bytes(gameObject.scene.name));

            // 스펙테이터(완전 종료자)는 텔레포트 + 리셋 안 함 — 그냥 따라온 것뿐
            if (owner.IsFullyFinished.Value)
            {
                Debug.Log("[LocalStageEntry] Owner is fully finished (spectating). Skipping teleport/reset.");
                yield break;
            }

            if (spawnPoint != null)
            {
                // kinematic Rigidbody + Interpolation은 transform.position만 바꾸면
                // 다음 FixedUpdate에서 rb.position(이전 값) 기준으로 움직여 원위치로 돌아감.
                // Rigidbody.position도 같이 세팅해서 물리 상태까지 강제 동기화.
                var rb = owner.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.position = spawnPoint.position;
                    rb.rotation = spawnPoint.rotation;
                }
                owner.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
                Debug.Log($"[LocalStageEntry] Teleported owner to {spawnPoint.position} (rb synced)");
            }
            else
            {
                Debug.LogWarning("[LocalStageEntry] spawnPoint is not set.");
            }

            owner.RequestStageResetServerRpc();
        }
    }
}
