using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Seoul.Network.Game
{
    [RequireComponent(typeof(PlayerController))]
    public class NetworkPlayer : NetworkBehaviour
    {
        public static readonly List<NetworkPlayer> All = new();

        // 최종 스테이지에 도달한 뒤의 골인이 "완전 종료"로 인정됨.
        private const string FinalStageName  = "05_Stage_Bicycle";
        private const string ResultSceneName = "06_Result";

        [Header("References")]
        [SerializeField] private PlayerController controller;
        [SerializeField] private GameObject ownerVisualMarker;

        [Header("Camera")]
        [SerializeField] private bool attachCameraOnSpawn = true;

        public NetworkVariable<int> Score = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<bool> HasFinished = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<int> FinishRank = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        // 각 플레이어가 현재 어느 씬(스테이지)에 있는지. 다른 씬에 있는 플레이어는 가시화 안 함.
        public NetworkVariable<FixedString64Bytes> CurrentScene = new(
            new FixedString64Bytes(""),
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        // 최종 스테이지까지 완전히 끝낸 상태. 한 번 true가 되면 다시 false로 돌아오지 않음.
        // 스펙테이트(관전) 진입 및 결과 화면 진행의 신호로 쓰임.
        public NetworkVariable<bool> IsFullyFinished = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private Renderer[] _cachedRenderers;
        private Collider[] _cachedColliders;
        private bool       _visualEnabled = true;

        // 스펙테이트 상태
        private bool          _isSpectating       = false;
        private NetworkPlayer _spectateTarget     = null;
        private float         _spectatePollTimer  = 0f;
        private const float   SpectatePollInterval = 0.5f;

        public void AddScore(int amount)
        {
            if (!IsServer) return;
            Score.Value += amount;
            Debug.Log($"[NetworkPlayer] clientId={OwnerClientId} score={Score.Value} (+{amount})");
        }

        public void MarkFinished(int rank)
        {
            if (!IsServer) return;
            HasFinished.Value = true;
            FinishRank.Value  = rank;
        }

        [ServerRpc(RequireOwnership = false)]
        public void ReportGoalServerRpc(ServerRpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            if (senderId != OwnerClientId)
            {
                Debug.LogWarning($"[NetworkPlayer] ReportGoalServerRpc rejected: sender={senderId} owner={OwnerClientId}");
                return;
            }
            if (HasFinished.Value) return;

            if (SessionScoreStore.Instance != null)
                SessionScoreStore.Instance.SetScore(OwnerClientId, Score.Value);

            // NetworkRaceManager가 살아있고 NGO-spawn된 경우에만 위임 (스테이지 1)
            bool useRaceManager = NetworkRaceManager.Instance != null
                                  && NetworkRaceManager.Instance.IsSpawned;
            if (useRaceManager)
            {
                NetworkRaceManager.Instance.ReportGoal(OwnerClientId);
            }
            else
            {
                // 스테이지 2/3에는 NetworkRaceManager가 없음 — 직접 마무리
                MarkFinished(0);
            }

            if (CurrentScene.Value.ToString() == FinalStageName)
            {
                IsFullyFinished.Value = true;
                TryAdvanceToResult();
            }
        }

        private static void TryAdvanceToResult()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
            if (All.Count == 0) return;
            foreach (var p in All)
            {
                if (p == null) continue;
                if (!p.IsFullyFinished.Value) return;
            }
            Debug.Log("[NetworkPlayer] All players fully finished — loading Result scene via NGO.");
            NetworkManager.Singleton.SceneManager.LoadScene(ResultSceneName, LoadSceneMode.Single);
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestStageResetServerRpc(ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
            if (IsFullyFinished.Value) return; // 스펙테이터는 리셋하지 않음
            HasFinished.Value = false;
            FinishRank.Value  = 0;
        }

        [ServerRpc(RequireOwnership = false)]
        public void SetCurrentSceneServerRpc(FixedString64Bytes scene, ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
            CurrentScene.Value = scene;
        }

        [ServerRpc(RequireOwnership = false)]
        public void ReportLocalScorePickupServerRpc(int amount, ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
            if (amount <= 0) return;
            AddScore(amount);
        }

        // ─── 로컬 로드 씬용 아이템 소비 동기화 ─────────────────

        [ServerRpc(RequireOwnership = false)]
        public void ReportConsumedItemServerRpc(FixedString64Bytes sceneName, FixedString64Bytes itemId, ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
            if (SessionScoreStore.Instance == null) return;

            string s  = sceneName.ToString();
            string id = itemId.ToString();
            bool added = SessionScoreStore.Instance.MarkItemConsumed(s, id);
            if (!added) return; // 이미 기록된 거면 broadcast 안 함

            BroadcastItemConsumedClientRpc(sceneName, itemId);
        }

        [ClientRpc]
        private void BroadcastItemConsumedClientRpc(FixedString64Bytes sceneName, FixedString64Bytes itemId)
        {
            StageItemSync.RaiseItemConsumed(sceneName.ToString(), itemId.ToString());
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestConsumedItemListServerRpc(FixedString64Bytes sceneName, ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
            if (SessionScoreStore.Instance == null) return;

            string s = sceneName.ToString();
            var consumed = SessionScoreStore.Instance.GetConsumedItems(s);

            var list = new List<FixedString64Bytes>();
            foreach (var id in consumed) list.Add(new FixedString64Bytes(id));

            var rpcSend = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { rpcParams.Receive.SenderClientId }
                }
            };
            ReceiveConsumedItemListClientRpc(sceneName, list.ToArray(), rpcSend);
        }

        [ClientRpc]
        private void ReceiveConsumedItemListClientRpc(FixedString64Bytes sceneName, FixedString64Bytes[] ids, ClientRpcParams rpcParams = default)
        {
            string s = sceneName.ToString();
            for (int i = 0; i < ids.Length; i++)
            {
                StageItemSync.RaiseItemConsumed(s, ids[i].ToString());
            }
        }

        // ─── 라이프사이클 ──────────────────────────────────────

        public override void OnNetworkSpawn()
        {
            if (!All.Contains(this)) All.Add(this);

            if (controller == null) controller = GetComponent<PlayerController>();

            DontDestroyOnLoad(gameObject);
            CacheVisuals();

            if (IsServer)
            {
                var store = SessionScoreStore.Instance;
                if (store != null) Score.Value = store.GetScore(OwnerClientId);
            }

            if (IsOwner)
            {
                controller.Initialize(new NullInputProvider());
                if (ownerVisualMarker != null) ownerVisualMarker.SetActive(true);
                if (attachCameraOnSpawn) AttachCameraTo(transform);
                ReportActiveSceneToServer();
            }
            else
            {
                controller.Initialize(new NullInputProvider());
                if (ownerVisualMarker != null) ownerVisualMarker.SetActive(false);
            }

            Debug.Log($"[NetworkPlayer] Spawned. OwnerClientId={OwnerClientId} IsOwner={IsOwner} LocalClientId={NetworkManager.Singleton.LocalClientId} pos={transform.position} restoredScore={Score.Value}");

            if (NetworkRaceManager.Instance != null)
                NetworkRaceManager.Instance.State.OnValueChanged += OnRaceStateChanged;

            HasFinished.OnValueChanged     += OnHasFinishedChanged;
            CurrentScene.OnValueChanged    += OnCurrentSceneChanged;
            IsFullyFinished.OnValueChanged += OnIsFullyFinishedChanged;
            SceneManager.sceneLoaded       += OnSceneLoadedLocal;

            RefreshInputForLocalState();
            RefreshAllVisibility();

            // 이미 종료 상태로 스폰됐다면(재접속 등) 즉시 스펙테이트
            if (IsOwner && IsFullyFinished.Value)
            {
                EnterSpectateMode();
            }
        }

        public override void OnNetworkDespawn()
        {
            All.Remove(this);

            if (NetworkRaceManager.Instance != null)
                NetworkRaceManager.Instance.State.OnValueChanged -= OnRaceStateChanged;

            HasFinished.OnValueChanged     -= OnHasFinishedChanged;
            CurrentScene.OnValueChanged    -= OnCurrentSceneChanged;
            IsFullyFinished.OnValueChanged -= OnIsFullyFinishedChanged;
            SceneManager.sceneLoaded       -= OnSceneLoadedLocal;
        }

        private void Update()
        {
            if (!_isSpectating || !IsOwner) return;
            _spectatePollTimer -= Time.deltaTime;
            if (_spectatePollTimer > 0f) return;
            _spectatePollTimer = SpectatePollInterval;
            UpdateSpectateTarget();
        }

        private void OnSceneLoadedLocal(Scene scene, LoadSceneMode mode)
        {
            if (IsOwner)
            {
                ReportActiveSceneToServer();

                if (_isSpectating)
                {
                    // 새 씬 로드 직후 잠시 기다렸다가 스펙테이트 갱신
                    _spectatePollTimer = 0.2f;
                }
                else if (attachCameraOnSpawn)
                {
                    AttachCameraTo(transform);
                }
            }

            if (NetworkRaceManager.Instance != null)
            {
                NetworkRaceManager.Instance.State.OnValueChanged -= OnRaceStateChanged;
                NetworkRaceManager.Instance.State.OnValueChanged += OnRaceStateChanged;
            }

            RefreshInputForLocalState();
            RefreshAllVisibility();
        }

        private void ReportActiveSceneToServer()
        {
            if (!IsOwner) return;
            var name = SceneManager.GetActiveScene().name;
            SetCurrentSceneServerRpc(new FixedString64Bytes(name));
        }

        private void OnRaceStateChanged(RaceState previous, RaceState current)
            => RefreshInputForLocalState();

        private void OnHasFinishedChanged(bool previous, bool current)
            => RefreshInputForLocalState();

        private void OnCurrentSceneChanged(FixedString64Bytes previous, FixedString64Bytes current)
        {
            if (IsOwner)
            {
                foreach (var p in All)
                {
                    if (p != null && p != this) p.UpdateVisibilityVsOwner();
                }
            }
            else
            {
                UpdateVisibilityVsOwner();
            }
        }

        private void OnIsFullyFinishedChanged(bool previous, bool current)
        {
            RefreshAllVisibility();
            RefreshInputForLocalState();
            if (current && IsOwner)
            {
                EnterSpectateMode();
            }
        }

        // ─── 스펙테이트 ────────────────────────────────────────

        private void EnterSpectateMode()
        {
            if (_isSpectating) return;
            _isSpectating = true;
            Debug.Log("[NetworkPlayer] Entering spectate mode.");
            _spectatePollTimer = 0f;
            UpdateSpectateTarget();
        }

        private void UpdateSpectateTarget()
        {
            // 아직 완전히 끝나지 않은 다른 플레이어를 찾는다
            NetworkPlayer target = null;
            foreach (var p in All)
            {
                if (p == null || p == this) continue;
                if (p.IsFullyFinished.Value) continue;
                target = p;
                break;
            }

            if (target == null)
            {
                _spectateTarget = null;
                return;
            }

            if (target != _spectateTarget)
            {
                _spectateTarget = target;
                Debug.Log($"[NetworkPlayer] Spectating clientId={target.OwnerClientId}");
            }

            string targetScene = target.CurrentScene.Value.ToString();
            if (string.IsNullOrEmpty(targetScene)) return;

            string myScene = SceneManager.GetActiveScene().name;
            if (targetScene != myScene)
            {
                Debug.Log($"[NetworkPlayer] Following target into scene '{targetScene}'");
                SceneTransition.Load(targetScene);
                return;
            }

            AttachCameraTo(target.transform);
        }

        // ─── 입력 / 가시성 ─────────────────────────────────────

        private void RefreshInputForLocalState()
        {
            if (!IsOwner) return;

            if (IsFullyFinished.Value)
            {
                controller.Initialize(new NullInputProvider());
                return;
            }

            bool racingOrFreeRun = NetworkRaceManager.Instance == null
                                   || !NetworkRaceManager.Instance.IsSpawned
                                   || NetworkRaceManager.Instance.State.Value == RaceState.Racing;

            if (racingOrFreeRun && !HasFinished.Value)
                controller.Initialize(new PlayerInputProvider());
            else
                controller.Initialize(new NullInputProvider());
        }

        private void RefreshAllVisibility()
        {
            foreach (var p in All)
            {
                if (p != null) p.UpdateVisibilityVsOwner();
            }
        }

        private void UpdateVisibilityVsOwner()
        {
            // 완전 종료자는 어디서도 안 보임 (관전 중인 유령). 본인 시점에서도 숨김.
            if (IsFullyFinished.Value)
            {
                SetVisualEnabled(false);
                return;
            }

            if (IsOwner)
            {
                SetVisualEnabled(true);
                return;
            }

            NetworkPlayer localOwner = null;
            foreach (var p in All)
            {
                if (p != null && p.IsOwner) { localOwner = p; break; }
            }

            bool sameScene = localOwner != null
                          && localOwner.CurrentScene.Value.Equals(CurrentScene.Value)
                          && CurrentScene.Value.Length > 0;

            SetVisualEnabled(sameScene);
        }

        private void CacheVisuals()
        {
            _cachedRenderers = GetComponentsInChildren<Renderer>(true);
            _cachedColliders = GetComponentsInChildren<Collider>(true);
        }

        private void SetVisualEnabled(bool enabled)
        {
            if (_visualEnabled == enabled) return;
            _visualEnabled = enabled;

            if (_cachedRenderers != null)
            {
                foreach (var r in _cachedRenderers)
                    if (r != null) r.enabled = enabled;
            }
            if (_cachedColliders != null)
            {
                foreach (var c in _cachedColliders)
                    if (c != null) c.enabled = enabled;
            }
        }

        // ─── 카메라 ────────────────────────────────────────────

        private void AttachCameraTo(Transform t)
        {
            var mainCam = Camera.main;
            if (mainCam == null) return;

            var follow = mainCam.GetComponent<CameraFollow>();
            if (follow != null)
            {
                follow.SetTarget(t);
            }
            else
            {
                mainCam.transform.SetParent(t);
                mainCam.transform.localPosition = new Vector3(0f, 3f, -6f);
                mainCam.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);
            }
        }
    }
}
