using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Seoul.Network.Game
{
    public class PlayerScore : NetworkBehaviour
    {
        // 서버 권한의 점수 동기화 변수
        public NetworkVariable<int> Score = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public override void OnNetworkSpawn()
        {
            // 스폰 시점에 SessionScoreStore에 백업된 데이터가 있다면 복구 (서버 전용)
            if (IsServer && SessionScoreStore.Instance != null)
            {
                Score.Value = SessionScoreStore.Instance.GetScore(OwnerClientId);
            }
        }

        /// <summary>
        /// ? 다른 작업자분들은 이 메서드 하나만 호출하시면 됩니다!
        /// 씬 종류와 권한을 자동으로 체크하여 점수를 안전하게 추가합니다.
        /// </summary>
        public void AddScore(int amount)
        {
            if (amount <= 0) return;

            // 이미 골인해서 완전히 끝난 상태라면 점수 처리를 무시합니다.
            if (TryGetComponent<NetworkPlayer>(out var player) && player.IsFullyFinished.Value) return;

            // [대원칙 1] NGO 동기화 씬 (Stage 1)인 경우
            if (IsSpawned)
            {
                // 실시간 동기화 씬에서는 '서버'만 직접 점수를 올릴 수 있습니다.
                if (IsServer)
                {
                    Score.Value += amount;
                    Debug.Log($"[PlayerScore] Stage1 - clientId={OwnerClientId} score={Score.Value} (+{amount})");
                }
            }
            // [대원칙 2] 로컬 로드 씬 (Stage 2/3)인 경우
            else
            {
                // 로컬 씬에서는 '내 화면(Owner)'에서 아이템을 먹었을 때만 서버에 RPC로 요청합니다.
                if (TryGetComponent<NetworkPlayer>(out var netPlayer) && netPlayer.IsOwner)
                {
                    ReportLocalScorePickupServerRpc(amount);
                }
            }
        }

        /// <summary>
        /// 로컬 로드 씬(Stage 2/3)에서 클라이언트가 서버에 점수 반영을 요청하는 RPC
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void ReportLocalScorePickupServerRpc(int amount, ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
            if (amount <= 0) return;

            Score.Value += amount;
            Debug.Log($"[PlayerScore] Stage2/3 RPC - clientId={OwnerClientId} score={Score.Value} (+{amount})");
        }
    }
}