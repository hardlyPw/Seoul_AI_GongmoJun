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
        private const string FinalStageName = "05_Stage_Bicycle";
        private const string ResultSceneName = "06_Result";

        [Header("References")]
        [SerializeField] private PlayerController controller;
        [SerializeField] private GameObject ownerVisualMarker;

        [Header("Character Models")]
        // 인덱스 순서 = 캐릭터 번호. 0번,1번... 순으로 모델을 넣으세요.
        [SerializeField] private GameObject[] characterModels;

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

        // 플레이어별 고정 캐릭터 번호. 서버가 스폰 시 한 번 정함. 모두가 같은 모델을 보도록 동기화.
        public NetworkVariable<int> CharacterIndex = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private Renderer[] _cachedRenderers;
        private Collider[] _cachedColliders;
        private bool _visualEnabled = true;

        // 스펙테이트 상태
        private bool _isSpectating = false;
        private NetworkPlayer _spectateTarget = null;
        private float _spectatePollTimer = 0f;
        private const float SpectatePollInterval = 0.5f;

        private Coroutine _startCoroutine;

        // 중간 관전 상태 (현재 스테이지 골인 후, 서버 전환 신호 대기 중)
        private bool _isIntermediateSpectating = false;
        private float _intermediatePollTimer = 0f;

        // 스테이지 진입 시 모든 플레이어 준비 완료 신호를 기다리는 동안 입력을 차단하는 플래그.
        private static bool _waitingForStageStart = false;

        // [서버 전용] 현 스테이지에서 준비 완료 신호를 보낸 클라이언트 ID 집합.
        private static readonly HashSet<ulong> _stageReadyClients = new();

        public void AddScore(int amount)
        {
            if (!IsServer) return;

            if (TryGetComponent<PlayerScore>(out var playerScore))
            {
                playerScore.AddScore(amount);
                Score.Value = playerScore.Score.Value;
                return;
            }

            Score.Value += amount;
            Debug.Log($"[NetworkPlayer] clientId={OwnerClientId} score={Score.Value} (+{amount})");
        }

        public void MarkFinished(int rank)
        {
            if (!IsServer) return;
            HasFinished.Value = true;
            FinishRank.Value = rank;
        }

        // 🌟 [CS1503 해결] GoalTrigger.cs에서 정상적으로 호출할 수 있도록 매개변수 구조 원복
        [ServerRpc(RequireOwnership = false)]
        public void ReportGoalServerRpc(FixedString64Bytes nextScene, ServerRpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            if (senderId != OwnerClientId) {
                Debug.LogWarning($"[NetworkPlayer] ReportGoalServerRpc rejected: sender={senderId} owner={OwnerClientId}");
                return;
            }
            if (HasFinished.Value) return;

            // 같은 씬에서 골인한 순서 기준 rank (1-based). 본인 HasFinished는 아직 false.
            int rank = ComputeFinishRankForCurrentScene();
            int rankBonus = GetRankBonus(rank);
            if (rankBonus > 0) AddScore(rankBonus);

            if (SessionScoreStore.Instance != null)
                SessionScoreStore.Instance.SetScore(OwnerClientId, GetCurrentScore());

            // NetworkRaceManager가 살아있고 NGO-spawn된 경우에만 위임 (스테이지 1)
            bool useRaceManager = NetworkRaceManager.Instance != null && NetworkRaceManager.Instance.IsSpawned;
            if (useRaceManager) {
                NetworkRaceManager.Instance.ReportGoal(OwnerClientId);
            }
            else {
                // 스테이지 2/3에는 NetworkRaceManager가 없음 — 직접 마무리. rank 보존.
                MarkFinished(rank);
            }

            if (CurrentScene.Value.ToString() == FinalStageName) {
                // 최종 스테이지 골인 — IsFullyFinished 처리 후 결과 화면으로
                IsFullyFinished.Value = true;
                TryAdvanceToResult();
            }
            else {
                // 중간 스테이지 골인 — 현재 씬 전체 플레이어 골인 시 ClientRpc로 일괄 전환
                TryAdvanceToNextStage(nextScene.ToString());
            }
        }

        private static void TryAdvanceToResult()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
            if (All.Count == 0) return;
            foreach (var p in All) {
                if (p == null) continue;
                if (!p.IsFullyFinished.Value) return;
            }

            Debug.Log("[NetworkPlayer] All players fully finished — Loading Result scene via NGO.");
            NetworkManager.Singleton.SceneManager.LoadScene(ResultSceneName, LoadSceneMode.Single);
        }

        /// <summary>
        /// 중간 스테이지 전환: 현재 씬의 모든 활성 플레이어가 골인했으면 다음 씬 로드
        /// </summary>
        private void TryAdvanceToNextStage(string nextScene)
        {
            if (!IsServer) return;
            if (string.IsNullOrEmpty(nextScene)) return;

            string myScene = CurrentScene.Value.ToString();

            foreach (var p in All) {
                if (p == null) continue;
                if (p.IsFullyFinished.Value) continue;
                if (p.CurrentScene.Value.ToString() != myScene) continue;
                if (!p.HasFinished.Value) return;
            }

            Debug.Log($"[NetworkPlayer] All players finished '{myScene}' → loading '{nextScene}'");
            LoadNextStageClientRpc(new FixedString64Bytes(nextScene));
        }

        [ClientRpc]
        private void LoadNextStageClientRpc(FixedString64Bytes nextScene)
        {
            Debug.Log($"[NetworkPlayer] LoadNextStageClientRpc — loading '{nextScene}'");
            if (IsServer) _stageReadyClients.Clear();
            _waitingForStageStart = true;
            StopLocalMovementForStageWait();

            if (SceneTransition.Instance != null) {
                SceneTransition.Instance.ManualFadeIn = true;
            }
            SceneTransition.Load(nextScene.ToString());
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestStageResetServerRpc(ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
            if (IsFullyFinished.Value) return;
            HasFinished.Value = false;
            FinishRank.Value = 0;
        }

        // 🌟 [CS1061 해결] LocalStageEntry.cs에서 다음 스테이지 진입 시 호출하는 서버 알림 메서드 원복
        [ServerRpc(RequireOwnership = false)]
        public void RequestReadyServerRpc(ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
            if (IsFullyFinished.Value) return;

            HasFinished.Value = false;
            FinishRank.Value = 0;

            _stageReadyClients.Add(OwnerClientId);
            Debug.Log($"[NetworkPlayer] Client {OwnerClientId} ready for next stage. ({_stageReadyClients.Count} total)");

            TryBroadcastStageStart();
        }

        private void TryBroadcastStageStart()
        {
            if (!IsServer) return;

            foreach (var p in All) {
                if (p == null) continue;
                if (p.IsFullyFinished.Value) continue;
                if (!_stageReadyClients.Contains(p.OwnerClientId)) return;
            }

            Debug.Log("[NetworkPlayer] All players ready — broadcasting stage start.");
            _stageReadyClients.Clear();

            double exactStartTime = NetworkManager.Singleton.ServerTime.Time + 0.5;

            foreach (var p in All) {
                if (p != null) p.ScheduleExactStartClientRpc(exactStartTime);
            }
        }

        // 🌟 [CS1061 해결] NetworkRaceManager.cs에서 완벽 동시 출발 스케줄링 시 사용하는 RPC 메서드 원복
        [ClientRpc]
        public void ScheduleExactStartClientRpc(double exactStartTime)
        {
            if (!IsOwner) return;
            if (IsFullyFinished.Value) return;

            Debug.Log($"[NetworkPlayer] Stage start scheduled at ServerTime: {exactStartTime}");
            StartCoroutine(ExactStartRoutine(exactStartTime));
        }

        private System.Collections.IEnumerator ExactStartRoutine(double exactStartTime)
        {
            while (NetworkManager.Singleton.ServerTime.Time < exactStartTime) {
                yield return null;
            }

            _waitingForStageStart = false;

            if (SceneTransition.Instance != null && SceneTransition.Instance.ManualFadeIn) {
                SceneTransition.Instance.TriggerFadeIn();
            }

            Debug.Log($"[NetworkPlayer] PERFECT SYNC START at ServerTime: {NetworkManager.Singleton.ServerTime.Time}");
            controller.SetMovementLocked(false);
            RefreshInputForLocalState();
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

            string s = sceneName.ToString();
            string id = itemId.ToString();
            bool added = SessionScoreStore.Instance.MarkItemConsumed(s, id);
            if (!added) return;

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
                Send = new ClientRpcSendParams { TargetClientIds = new[] { rpcParams.Receive.SenderClientId } }
            };
            ReceiveConsumedItemListClientRpc(sceneName, list.ToArray(), rpcSend);
        }

        [ClientRpc]
        private void ReceiveConsumedItemListClientRpc(FixedString64Bytes sceneName, FixedString64Bytes[] ids, ClientRpcParams rpcParams = default)
        {
            string s = sceneName.ToString();
            for (int i = 0; i < ids.Length; i++) {
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

            CharacterIndex.OnValueChanged += OnCharacterIndexChanged;
            ApplyCharacterModel(CharacterIndex.Value);

            if (IsServer) {
                var store = SessionScoreStore.Instance;
                if (store != null)
                {
                    Score.Value = store.GetScore(OwnerClientId);
                    if (TryGetComponent<PlayerScore>(out var playerScore))
                        playerScore.Score.Value = Score.Value;
                }
            }

            if (IsOwner) {
                controller.Initialize(new NullInputProvider());
                controller.ResetMovementState();
                if (ownerVisualMarker != null) ownerVisualMarker.SetActive(true);
                if (attachCameraOnSpawn) AttachCameraTo(transform);
                ReportActiveSceneToServer();
            }
            else {
                controller.Initialize(new NullInputProvider());
                if (ownerVisualMarker != null) ownerVisualMarker.SetActive(false);
            }

            Debug.Log($"[NetworkPlayer] Spawned. OwnerClientId={OwnerClientId} IsOwner={IsOwner} LocalClientId={NetworkManager.Singleton.LocalClientId} pos={transform.position} restoredScore={Score.Value}");

            if (NetworkRaceManager.Instance != null)
                NetworkRaceManager.Instance.State.OnValueChanged += OnRaceStateChanged;

            HasFinished.OnValueChanged += OnHasFinishedChanged;
            CurrentScene.OnValueChanged += OnCurrentSceneChanged;
            IsFullyFinished.OnValueChanged += OnIsFullyFinishedChanged;
            SceneManager.sceneLoaded += OnSceneLoadedLocal;

            _waitingForStageStart = true;
            StopLocalMovementForStageWait();

            RefreshInputForLocalState();
            RefreshAllVisibility();

            if (IsOwner && IsFullyFinished.Value) {
                EnterSpectateMode();
            }
        }

        public override void OnNetworkDespawn()
        {
            All.Remove(this);

            CharacterIndex.OnValueChanged -= OnCharacterIndexChanged;

            if (NetworkRaceManager.Instance != null)
                NetworkRaceManager.Instance.State.OnValueChanged -= OnRaceStateChanged;

            HasFinished.OnValueChanged -= OnHasFinishedChanged;
            CurrentScene.OnValueChanged -= OnCurrentSceneChanged;
            IsFullyFinished.OnValueChanged -= OnIsFullyFinishedChanged;
            SceneManager.sceneLoaded -= OnSceneLoadedLocal;
        }

        private void Update()
        {
            if (!IsOwner) return;

            if (_isSpectating) {
                _spectatePollTimer -= Time.deltaTime;
                if (_spectatePollTimer <= 0f) {
                    _spectatePollTimer = SpectatePollInterval;
                    UpdateSpectateTarget();
                }
            }

            if (_isIntermediateSpectating) {
                _intermediatePollTimer -= Time.deltaTime;
                if (_intermediatePollTimer <= 0f) {
                    _intermediatePollTimer = SpectatePollInterval;
                    UpdateIntermediateSpectateTarget();
                }
            }
        }

        private int GetCurrentScore()
        {
            return TryGetComponent<PlayerScore>(out var playerScore)
                ? playerScore.Score.Value
                : Score.Value;
        }

        // 현재 씬에서 본인이 몇 번째 골인인지(1-based). 본인 HasFinished가 set 되기 전에 호출해야 함.
        private int ComputeFinishRankForCurrentScene()
        {
            string myScene = CurrentScene.Value.ToString();
            int rank = 1;
            foreach (var p in All)
            {
                if (p == null || p == this) continue;
                if (p.CurrentScene.Value.ToString() != myScene) continue;
                if (p.HasFinished.Value) rank++;
            }
            return rank;
        }

        // 스테이지별 골인 순위 보상 점수. 1위 100 → 4위 40, 5위 이하 0.
        private static int GetRankBonus(int rank) => rank switch
        {
            1 => 100,
            2 => 80,
            3 => 60,
            4 => 40,
            _ => 0,
        };

        private void OnSceneLoadedLocal(Scene scene, LoadSceneMode mode)
        {
            if (IsOwner) {
                ReportActiveSceneToServer();

                if (_isSpectating) {
                    _spectatePollTimer = 0.2f;
                }
                else {
                    if (_isIntermediateSpectating)
                        ExitIntermediateSpectateMode();

                    // 🌟 핵심 카메라 주권 제어: 자전거 등 3스테이지 진입 시 정밀하게 현재 활성화 씬 메인카메라 할당
                    else if (attachCameraOnSpawn)
                        AttachCameraTo(transform);
                }
            }

            if (NetworkRaceManager.Instance != null) {
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
        {
            RefreshInputForLocalState();
            if (!IsOwner) return;
            if (current && !IsFullyFinished.Value)
                EnterIntermediateSpectateMode();
            else if (!current)
                ExitIntermediateSpectateMode();
        }

        private void OnCurrentSceneChanged(FixedString64Bytes previous, FixedString64Bytes current)
        {
            if (IsOwner) {
                foreach (var p in All) {
                    if (p != null && p != this) p.UpdateVisibilityVsOwner();
                }
            }
            else {
                UpdateVisibilityVsOwner();
            }
        }

        private void OnIsFullyFinishedChanged(bool previous, bool current)
        {
            RefreshAllVisibility();
            RefreshInputForLocalState();
            if (current && IsOwner) {
                EnterSpectateMode();
            }
        }

        // ─── 중간 관전 (스테이지 골인 후 대기) ───────────────────

        private void EnterIntermediateSpectateMode()
        {
            if (_isIntermediateSpectating) return;
            _isIntermediateSpectating = true;
            _intermediatePollTimer = 0f;
            Debug.Log("[NetworkPlayer] Entering intermediate spectate mode.");
            UpdateIntermediateSpectateTarget();
        }

        private void ExitIntermediateSpectateMode()
        {
            if (!_isIntermediateSpectating) return;
            _isIntermediateSpectating = false;
            Debug.Log("[NetworkPlayer] Exiting intermediate spectate mode.");
            if (attachCameraOnSpawn) AttachCameraTo(transform);
        }

        private void UpdateIntermediateSpectateTarget()
        {
            string myScene = SceneManager.GetActiveScene().name;
            NetworkPlayer target = null;
            foreach (var p in All) {
                if (p == null || p == this) continue;
                if (p.IsFullyFinished.Value) continue;
                if (p.CurrentScene.Value.ToString() != myScene) continue;
                if (p.HasFinished.Value) continue;
                target = p;
                break;
            }

            if (target != null) {
                Debug.Log($"[NetworkPlayer] Intermediate spectating clientId={target.OwnerClientId}");
                AttachCameraTo(target.transform);
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
            NetworkPlayer target = null;
            foreach (var p in All) {
                if (p == null || p == this) continue;
                if (p.IsFullyFinished.Value) continue;
                target = p;
                break;
            }

            if (target == null) {
                _spectateTarget = null;
                return;
            }

            if (target != _spectateTarget) {
                _spectateTarget = target;
                Debug.Log($"[NetworkPlayer] Spectating clientId={target.OwnerClientId}");
            }

            string targetScene = target.CurrentScene.Value.ToString();
            if (string.IsNullOrEmpty(targetScene)) return;

            string myScene = SceneManager.GetActiveScene().name;
            if (targetScene != myScene) {
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

            if (IsFullyFinished.Value) {
                controller.Initialize(new NullInputProvider());
                return;
            }

            bool racingOrFreeRun = NetworkRaceManager.Instance == null
                                   || !NetworkRaceManager.Instance.IsSpawned
                                   || NetworkRaceManager.Instance.State.Value == RaceState.Racing;

            if (_waitingForStageStart) {
                controller.Initialize(new NullInputProvider());
                return;
            }

            if (racingOrFreeRun && !HasFinished.Value)
                controller.Initialize(new PlayerInputProvider());
            else
                controller.Initialize(new NullInputProvider());
        }

        private void StopLocalMovementForStageWait()
        {
            if (!IsOwner || controller == null) return;
            controller.Initialize(new NullInputProvider());
        }

        private void RefreshAllVisibility()
        {
            foreach (var p in All) {
                if (p != null) p.UpdateVisibilityVsOwner();
            }
        }

        private void UpdateVisibilityVsOwner()
        {
            if (IsFullyFinished.Value) {
                SetVisualEnabled(false);
                return;
            }

            if (IsOwner) {
                SetVisualEnabled(true);
                return;
            }

            NetworkPlayer localOwner = null;
            foreach (var p in All) {
                if (p != null && p.IsOwner) { localOwner = p; break; }
            }

            bool sameScene = localOwner != null
                          && localOwner.CurrentScene.Value.Equals(CurrentScene.Value)
                          && CurrentScene.Value.Length > 0;

            SetVisualEnabled(sameScene);
        }

        private void OnCharacterIndexChanged(int previous, int current)
        {
            ApplyCharacterModel(current);
        }

        // 자기 인덱스에 맞는 모델만 켜고, 그 모델의 Animator를 PlayerAnimator에 연결한다.
        private void ApplyCharacterModel(int index)
        {
            if (characterModels == null || characterModels.Length == 0) return;

            GameObject active = null;
            for (int i = 0; i < characterModels.Length; i++) {
                if (characterModels[i] == null) continue;
                bool on = (i == index);
                characterModels[i].SetActive(on);
                if (on) active = characterModels[i];
            }

            if (active != null) {
                var pa = GetComponent<PlayerAnimator>();
                var anim = active.GetComponentInChildren<Animator>();
                if (pa != null && anim != null) pa.SetAnimator(anim);
            }

            // 보이는 메시가 바뀌었으니 가시성 캐시 갱신
            CacheVisuals();
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

            if (_cachedRenderers != null) {
                foreach (var r in _cachedRenderers)
                    if (r != null) r.enabled = enabled;
            }
            if (_cachedColliders != null) {
                foreach (var c in _cachedColliders)
                    if (c != null) c.enabled = enabled;
            }
        }

        // ─── 카메라 주권 제어 시스템 ────────────────────────────────────────────

        private void AttachCameraTo(Transform t)
        {
            var currentCamera = FindCurrentSceneCamera();
            if (currentCamera == null) return;

            DestroyStaleMainCameras(currentCamera);

            var follow = currentCamera.GetComponent<CameraFollow>();
            if (follow != null) {
                follow.SetTarget(t);
                return;
            }

            var followShake = currentCamera.GetComponent<CameraFollowAndShake>();
            if (followShake != null) {
                followShake.SetTarget(t);
                return;
            }

            currentCamera.transform.SetParent(t);
            currentCamera.transform.localPosition = new Vector3(0f, 3f, -6f);
            currentCamera.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);
        }

        private static Camera FindCurrentSceneCamera()
        {
            var activeScene = SceneManager.GetActiveScene();
            foreach (var camera in Camera.allCameras) {
                if (!camera.gameObject.activeInHierarchy) continue;
                if (!camera.CompareTag("MainCamera")) continue;
                if (camera.gameObject.scene == activeScene)
                    return camera;
            }
            return Camera.main;
        }

        private static void DestroyStaleMainCameras(Camera keepCamera)
        {
            var activeScene = SceneManager.GetActiveScene();
            foreach (var camera in Camera.allCameras) {
                if (camera == keepCamera) continue;
                if (!camera.CompareTag("MainCamera")) continue;
                if (camera.gameObject.scene != activeScene) {
                    Debug.Log($"[NetworkPlayer] Destroying stale MainCamera from scene '{camera.gameObject.scene.name}'.");
                    Object.Destroy(camera.gameObject);
                }
            }
        }

        // ─── QTE 처리 세션 ──────────────────────────────────

        [ServerRpc(RequireOwnership = false)]
        public void RequestQTEResultServerRpc(bool isSuccess, int scoreToAdd, BaseQTE.QteActionType actionType, ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
            if (IsFullyFinished.Value) return;

            if (isSuccess) {
                AddScore(scoreToAdd);
            }
            else {
                NotifyPlayerQTEFailureClientRpc(actionType);
            }
        }

        [ClientRpc]
        private void NotifyPlayerQTEFailureClientRpc(BaseQTE.QteActionType actionType)
        {
            if (controller == null) return;
            Debug.Log($"[QTE 실패] Client {OwnerClientId}가 {actionType} 기믹 실패로 스턴 상태에 진입합니다.");
            controller.TriggerFall();
        }
    }
}
