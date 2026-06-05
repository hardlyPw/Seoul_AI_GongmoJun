using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using System;

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
        /// 서버에서 직접 점수를 추가할 때 사용
        /// </summary>
        public void AddScore(int amount)
        {
            if (!IsServer) return;
            Score.Value += amount;
            Debug.Log($"[PlayerScore] clientId={OwnerClientId} score={Score.Value} (+{amount})");
        }

        /// <summary>
        /// 클라이언트(아이템 픽업 등)에서 점수 추가를 요청할 때 사용
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void ReportLocalScorePickupServerRpc(int amount, ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
            if (amount <= 0) return;

            // 이미 골인해서 완전히 끝난 상태인지 체크하고 싶다면 NetworkPlayer를 참조할 수 있습니다.
            if (TryGetComponent<NetworkPlayer>(out var player) && player.IsFullyFinished.Value) return;

            AddScore(amount);
        }
    }
}