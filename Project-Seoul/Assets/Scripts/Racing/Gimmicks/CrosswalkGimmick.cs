using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Seoul.Network.Game
{
    // 횡단보도 신호등 기믹 (멀티 동기화).
    // - 서버가 페이즈를 결정하고 NetworkVariable로 전파
    // - 모든 클라가 신호등 시각을 자기 측에서 토글
    // - 진입은 항상 가능. 빨간불 동안 PenaltyZone 안에서 달리기/대시 시 서버가 TriggerFall.
    public class CrosswalkGimmick : NetworkBehaviour
    {
        [Header("Cycle (server-authoritative)")]
        [SerializeField] private float initialRedDuration = 30f;
        [SerializeField] private float greenDuration = 3f;
        [SerializeField] private float redDuration   = 4f;

        [Header("Visuals")]
        [SerializeField] private GameObject greenLight;
        [SerializeField] private GameObject redLight;

        public NetworkVariable<bool> IsGreenNet = new(
            value: false,
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);

        public bool IsGreen => IsGreenNet.Value;

        private Coroutine _cycleRoutine;

        public override void OnNetworkSpawn()
        {
            IsGreenNet.OnValueChanged += OnPhaseChanged;

            // 늦게 접속한 클라이언트도 현재 페이즈를 즉시 반영
            ApplyVisuals(IsGreenNet.Value);

            if (!IsServer) return;
            _cycleRoutine = StartCoroutine(TrafficCycle());
        }

        public override void OnNetworkDespawn()
        {
            IsGreenNet.OnValueChanged -= OnPhaseChanged;

            if (_cycleRoutine != null)
            {
                StopCoroutine(_cycleRoutine);
                _cycleRoutine = null;
            }
        }

        private IEnumerator TrafficCycle()
        {
            // 게임 시작 직후엔 빨간불을 길게 유지 (디버그/테스트 용이)
            IsGreenNet.Value = false;
            yield return new WaitForSeconds(initialRedDuration);

            while (true)
            {
                IsGreenNet.Value = true;
                yield return new WaitForSeconds(greenDuration);
                IsGreenNet.Value = false;
                yield return new WaitForSeconds(redDuration);
            }
        }

        private void OnPhaseChanged(bool _, bool next) => ApplyVisuals(next);

        private void ApplyVisuals(bool green)
        {
            if (greenLight) greenLight.SetActive(green);
            if (redLight)   redLight.SetActive(!green);
        }

        public void PenalizePlayer(PlayerController player)
        {
            if (!IsServer || player == null || player.IsFallen) return;

            player.TriggerFall();

            if (!player.TryGetComponent<NetworkObject>(out var playerNetObj)) return;
            var ownerClientId = playerNetObj.OwnerClientId;
            if (ownerClientId == NetworkManager.ServerClientId) return;

            var rpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { ownerClientId }
                }
            };
            TriggerPlayerFallClientRpc(playerNetObj.NetworkObjectId, rpcParams);
        }

        public void RequestPenalty(ulong playerNetworkObjectId)
        {
            if (!IsClient || IsServer) return;
            RequestPenaltyServerRpc(playerNetworkObjectId);
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestPenaltyServerRpc(ulong playerNetworkObjectId, ServerRpcParams rpcParams = default)
        {
            if (IsGreenNet.Value) return;
            if (NetworkManager == null) return;
            if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(playerNetworkObjectId, out var playerNetObj)) return;
            if (playerNetObj.OwnerClientId != rpcParams.Receive.SenderClientId) return;
            if (!playerNetObj.TryGetComponent<PlayerController>(out var player)) return;

            PenalizePlayer(player);
        }

        [ClientRpc]
        private void TriggerPlayerFallClientRpc(ulong playerNetworkObjectId, ClientRpcParams rpcParams = default)
        {
            if (NetworkManager == null) return;
            if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(playerNetworkObjectId, out var playerNetObj)) return;
            if (!playerNetObj.IsOwner) return;
            if (!playerNetObj.TryGetComponent<PlayerController>(out var player)) return;

            player.TriggerFall();
        }
    }
}
