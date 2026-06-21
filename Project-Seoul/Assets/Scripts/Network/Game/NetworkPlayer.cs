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

        [Header("Camera")]
        [SerializeField] private bool attachCameraOnSpawn = true;


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
        // LoadNextStageClientRpc 또는 OnNetworkSpawn으로 true, EnableStageInputClientRpc 또는 EnableInputDirect로 false.
        // ⚠️ static: 같은 클라이언트의 모든 NetworkPlayer 인스턴스가 공유.
        //    (LoadNextStageClientRpc는 특정 인스턴스에서fb8호스되지만, 플래그는 클라이언트 전체에 적용되어야 함)
        private static bool _waitingForStageStart = false;

        // [서버 전용] 현 스테이지에서 준비 완료 신호를 보낸 클라이언트 ID 집합.
        // static으로 모든 NetworkPlayer 인스턴스가 공유 (서버에 하나만 존재).
        private static readonly HashSet<ulong> _stageReadyClients = new();

        public void MarkFinished(int rank)
        {
            if (!IsServer) return;
            HasFinished.Value = true;
            FinishRank.Value = rank;
        }

        [ServerRpc(RequireOwnership = false)]
        public void ReportGoalServerRpc(FixedString64Bytes nextScene, ServerRpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            if (senderId != OwnerClientId)
            {
                Debug.LogWarning($"[NetworkPlayer] ReportGoalServerRpc rejected: sender={senderId} owner={OwnerClientId}");
                return;
            }
            if (HasFinished.Value) return;

            if (TryGetComponent<PlayerScore>(out var playerScore))
            {
                if (SessionScoreStore.Instance != null)
                    SessionScoreStore.Instance.SetScore(OwnerClientId, playerScore.Score.Value);
            }

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
                // 최종 스테이지 골인 — IsFullyFinished 처리 후 결과 화면으로
                IsFullyFinished.Value = true;
                TryAdvanceToResult();
            }
            else
            {
                // 중간 스테이지 골인 — 현재 씬 전체 플레이어 골인 시 ClientRpc로 일괄 전환
                TryAdvanceToNextStage(nextScene.ToString());
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
            
            Debug.Log("[NetworkPlayer] All players fully finished — Sorting scores via PlayerScore and filling Broadcaster.");

            // [수정] 결과 창 집계 시 PlayerScore 기반 정렬 후 Broadcaster에 바인딩
            var broadcaster = NetworkResultBroadcaster.Instance;
            if (broadcaster != null && broadcaster.Entries != null)
            {
                broadcaster.Entries.Clear();

                var sortedPlayers = new List<NetworkPlayer>(All);
                sortedPlayers.Sort((a, b) =>
                {
                    int scoreA = a.TryGetComponent<PlayerScore>(out var sA) ? sA.Score.Value : 0;
                    int scoreB = b.TryGetComponent<PlayerScore>(out var sB) ? sB.Score.Value : 0;
                    return scoreB.CompareTo(scoreA); // 내림차순 정렬
                });

                for (int i = 0; i < sortedPlayers.Count; i++)
                {
                    var p = sortedPlayers[i];
                    if (p == null) continue;

                    int finalScore = p.TryGetComponent<PlayerScore>(out var s) ? s.Score.Value : 0;

                    ResultEntry entry = new ResultEntry
                    {
                        ClientId = p.OwnerClientId,
                        Score = finalScore,
                        FinalRank = i + 1
                    };

                    broadcaster.Entries.Add(entry);
                }
            }

            Debug.Log("[NetworkPlayer] Loading Result scene via NGO.");
            NetworkManager.Singleton.SceneManager.LoadScene(ResultSceneName, LoadSceneMode.Single);
        }

        /// <summary>
        /// 중간 스테이지 전환: 현재 씬의 모든 활성 플레이어가 골인했으면
        /// 모든 클라이언트에 ClientRpc로 다음 씬 로드 신호를 보냅니다.
        /// </summary>
        private void TryAdvanceToNextStage(string nextScene)
        {
            if (!IsServer) return;
            if (string.IsNullOrEmpty(nextScene)) return;

            string myScene = CurrentScene.Value.ToString();

            // 현재 씬에서 플레이 중인 모든 플레이어(관전자 제외)가 골인했는지 확인
            foreach (var p in All)
            {
                if (p == null) continue;
                if (p.IsFullyFinished.Value) continue;  // 최종 완료 관전자는 제외
                if (p.CurrentScene.Value.ToString() != myScene) continue;
                if (!p.HasFinished.Value) return;       // 아직 골인 못 한 플레이어 존재
            }

            Debug.Log($"[NetworkPlayer] All players finished '{myScene}' → loading '{nextScene}'");
            LoadNextStageClientRpc(new FixedString64Bytes(nextScene));
        }

        /// <summary>
        /// 모든 클라이언트에서 다음 스테이지를 로컬 로드합니다.
        /// SceneTransition의 페이드 처리가 자동으로 이루어집니다.
        /// </summary>
        [ClientRpc]
        private void LoadNextStageClientRpc(FixedString64Bytes nextScene)
        {
            Debug.Log($"[NetworkPlayer] LoadNextStageClientRpc — loading '{nextScene}'");
            if (IsServer) _stageReadyClients.Clear();
            _waitingForStageStart = true;
            StopLocalMovementForStageWait();
            
            // 모든 클라이언트가 로딩을 마칠 때까지 화면이 까만 상태로 대기하도록 설정
            if (SceneTransition.Instance != null)
            {
                SceneTransition.Instance.ManualFadeIn = true;
            }
            SceneTransition.Load(nextScene.ToString());
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestStageResetServerRpc(ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
            if (IsFullyFinished.Value) return; // 스펙테이터는 리셋하지 않음
            HasFinished.Value = false;
            FinishRank.Value = 0;
        }

        /// <summary>
        /// 스테이지 2/3 전환 시 LocalStageEntry가 호출.
        /// HasFinished 리셋 후 준비 완료를 서버에 통보하며,
        /// 모든 플레이어가 준비되면 서버가 EnableStageInputClientRpc를 일괄 발송합니다.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void RequestReadyServerRpc(ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
            if (IsFullyFinished.Value) return; // 스펙테이터는 참여하지 않음

            // HasFinished / FinishRank 리셋 (RequestStageResetServerRpc와 동일)
            HasFinished.Value = false;
            FinishRank.Value = 0;

            _stageReadyClients.Add(OwnerClientId);
            Debug.Log($"[NetworkPlayer] Client {OwnerClientId} ready for next stage. ({_stageReadyClients.Count} total)");

            TryBroadcastStageStart();
        }

        /// <summary>
        /// 모든 활성 플레이어가 준비됐으면 EnableStageInputClientRpc를 전송합니다.
        /// </summary>
        private void TryBroadcastStageStart()
        {
            if (!IsServer) return;

            // 관전자(IsFullyFinished)를 제외한 모든 플레이어가 준비됐는지 확인
            foreach (var p in All)
            {
                if (p == null) continue;
                if (p.IsFullyFinished.Value) continue;
                if (!_stageReadyClients.Contains(p.OwnerClientId)) return; // 아직 준비 안 됨
            }

            Debug.Log("[NetworkPlayer] All players ready — broadcasting stage start.");
            _stageReadyClients.Clear();
            
            // 네트워크 지연을 극복하기 위해 정확히 0.5초 뒤의 서버 시간을 동시 출발 시간으로 지정합니다.
            double exactStartTime = NetworkManager.Singleton.ServerTime.Time + 0.5;
            
            // ⚠️ this.EnableStageInputClientRpc() 가 아니라 각 NetworkPlayer 인스턴스에서 호출.
            foreach (var p in All)
            {
                if (p != null) p.ScheduleExactStartClientRpc(exactStartTime);
            }
        }

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
            // 완벽한 동시 출발을 위해 서버 시간이 일치할 때까지 대기합니다.
            while (NetworkManager.Singleton.ServerTime.Time < exactStartTime)
            {
                yield return null;
            }

            _waitingForStageStart = false;
            
            if (SceneTransition.Instance != null && SceneTransition.Instance.ManualFadeIn)
            {
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


        // ─── 로컬 로드 씬용 아이템 소비 동기화 ─────────────────

        /// <summary>
        /// NetworkRaceManager.ForceStartRaceClientRpc에서 호출.
        /// 지정된 지연 시간(delay)에 맞춰 직접 입력을 활성화합니다.
        /// </summary>
        public void ScheduleInputEnable(float delay)
        {
            if (!IsOwner) return;
            if (IsFullyFinished.Value || HasFinished.Value) return;

            if (_startCoroutine != null) StopCoroutine(_startCoroutine);
            StopLocalMovementForStageWait();
            _startCoroutine = StartCoroutine(WaitAndEnableInput(delay));
        }

        private System.Collections.IEnumerator WaitAndEnableInput(float delay)
        {
            if (delay > 0f)
            {
                // 로컬 클럭(Time.time)을 사용하여 오차 없이 매끄럽게 대기합니다.
                yield return new WaitForSeconds(delay);
            }

            // static 플래그 해제 (서버-클라이언트 동시 시작을 위한 차단 해제)
            _waitingForStageStart = false;
            Debug.Log($"[NetworkPlayer] Stage start synchronized exact time reached (delay: {delay}) — enabling input.");
            controller.SetMovementLocked(false);
            controller.Initialize(new PlayerInputProvider());
        }

        [ServerRpc(RequireOwnership = false)]
        public void ReportConsumedItemServerRpc(FixedString64Bytes sceneName, FixedString64Bytes itemId, ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
            if (SessionScoreStore.Instance == null) return;

            string s = sceneName.ToString();
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


            if (IsOwner)
            {
                controller.Initialize(new NullInputProvider());
                controller.ResetMovementState();
                if (ownerVisualMarker != null) ownerVisualMarker.SetActive(true);
                if (attachCameraOnSpawn) AttachCameraTo(transform);
                ReportActiveSceneToServer();
            }
            else
            {
                controller.Initialize(new NullInputProvider());
                if (ownerVisualMarker != null) ownerVisualMarker.SetActive(false);
            }

            int currentScore = TryGetComponent<PlayerScore>(out var s) ? s.Score.Value : 0;
            Debug.Log($"[NetworkPlayer] Spawned. OwnerClientId={OwnerClientId} IsOwner={IsOwner} LocalClientId={NetworkManager.Singleton.LocalClientId} pos={transform.position} restoredScore={currentScore}");
            
            if (NetworkRaceManager.Instance != null)
                NetworkRaceManager.Instance.State.OnValueChanged += OnRaceStateChanged;

            HasFinished.OnValueChanged += OnHasFinishedChanged;
            CurrentScene.OnValueChanged += OnCurrentSceneChanged;
            IsFullyFinished.OnValueChanged += OnIsFullyFinishedChanged;
            SceneManager.sceneLoaded += OnSceneLoadedLocal;

            // 스테이지 시작 신호(ForceStartRaceClientRpc 또는 EnableStageInputClientRpc)를
            // 받기 전까지 입력을 차단. 서버가 State=Racing으로 먼저 시작하는 것을 방지.
            _waitingForStageStart = true;
            StopLocalMovementForStageWait();

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

            HasFinished.OnValueChanged -= OnHasFinishedChanged;
            CurrentScene.OnValueChanged -= OnCurrentSceneChanged;
            IsFullyFinished.OnValueChanged -= OnIsFullyFinishedChanged;
            SceneManager.sceneLoaded -= OnSceneLoadedLocal;
        }

        private void Update()
        {
            if (!IsOwner) return;

            if (_isSpectating)
            {
                _spectatePollTimer -= Time.deltaTime;
                if (_spectatePollTimer <= 0f)
                {
                    _spectatePollTimer = SpectatePollInterval;
                    UpdateSpectateTarget();
                }
            }

            if (_isIntermediateSpectating)
            {
                _intermediatePollTimer -= Time.deltaTime;
                if (_intermediatePollTimer <= 0f)
                {
                    _intermediatePollTimer = SpectatePollInterval;
                    UpdateIntermediateSpectateTarget();
                }
            }
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
                else
                {
                    // 새 씬이 로드되면 중간 관전을 즉시 해제하고 카메라를 자신에게 복귀
                    // (LocalStageEntry가 HasFinished를 reset하기 전에 해제하기 위해 여기서도 처리)
                    if (_isIntermediateSpectating)
                        ExitIntermediateSpectateMode();
                    else if (attachCameraOnSpawn)
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
        {
            RefreshInputForLocalState();
            if (!IsOwner) return;
            if (current && !IsFullyFinished.Value)
                EnterIntermediateSpectateMode();  // 중간 스테이지 골인 → 관전 시작
            else if (!current)
                ExitIntermediateSpectateMode();   // 새 스테이지 시작(HasFinished 리셋) → 관전 해제
        }

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
            foreach (var p in All)
            {
                if (p == null || p == this) continue;
                if (p.IsFullyFinished.Value) continue;
                if (p.CurrentScene.Value.ToString() != myScene) continue;
                if (p.HasFinished.Value) continue;  // 이미 골인한 플레이어는 건너뜀
                target = p;
                break;
            }

            if (target != null)
            {
                Debug.Log($"[NetworkPlayer] Intermediate spectating clientId={target.OwnerClientId}");
                AttachCameraTo(target.transform);
            }
            // target이 없으면 카메라 유지 (곧 씬 전환 예정)
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
                controller.SetMovementLocked(true);
                return;
            }

            bool racingOrFreeRun = NetworkRaceManager.Instance == null
                                   || !NetworkRaceManager.Instance.IsSpawned
                                   || NetworkRaceManager.Instance.State.Value == RaceState.Racing;

            // 대기 중이면 입력 차단 (이제 ExactStartRoutine에서 대기 플래그를 해제합니다)
            if (_waitingForStageStart)
            {
                controller.Initialize(new NullInputProvider());
                controller.SetMovementLocked(true);
                return;
            }

            if (racingOrFreeRun && !HasFinished.Value)
            {
                controller.SetMovementLocked(false);
                controller.Initialize(new PlayerInputProvider());
                // 개발자님의 로컬 환경(듀얼 모니터) 완벽 동시 출발 시각 효과를 위해
                // 출발 직후 1초간 모든 NetworkTransform의 보간(버퍼)을 강제로 꺼서 즉시 반응하게 만듭니다.
                StartCoroutine(TemporaryDisableInterpolationRoutine());
            }
            else
            {
                controller.Initialize(new NullInputProvider());
                controller.SetMovementLocked(true);
            }
        }

        private void StopLocalMovementForStageWait()
        {
            if (!IsOwner || controller == null) return;
            controller.Initialize(new NullInputProvider());
            controller.SetMovementLocked(true);
        }

        private System.Collections.IEnumerator TemporaryDisableInterpolationRoutine()
        {
            // 모든 플레이어의 NetworkTransform 보간을 끕니다. (100ms 지연 제거)
            var netTransforms = FindObjectsOfType<Unity.Netcode.Components.NetworkTransform>();
            foreach (var nt in netTransforms)
            {
                nt.Interpolate = false;
            }

            // 1초 뒤에 다시 부드러운 움직임(보간)을 켭니다.
            yield return new WaitForSeconds(1.0f);

            foreach (var nt in netTransforms)
            {
                if (nt != null) nt.Interpolate = true;
            }
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

        // ─── NetworkPlayer 내부의 QTE 처리 세션 ──────────────────────────────────

        [ServerRpc(RequireOwnership = false)]
        public void RequestQTEResultServerRpc(bool isSuccess, int scoreToAdd, BaseQTE.QteActionType actionType, ServerRpcParams rpcParams = default)
        {
            // 🌟 구버전 방식인 Receive.SenderClientId로 보안 검증
            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
            if (IsFullyFinished.Value) return;

            if (isSuccess)
            {
                if (TryGetComponent<PlayerScore>(out var playerScore))
                {
                    playerScore.AddScore(scoreToAdd); // ⭕ PlayerScore를 통해 추가
                }
                ExecuteQteSuccessAction(actionType);
            }
            else
            {
                NotifyPlayerQTEFailureClientRpc(actionType);
            }
        }

        private void ExecuteQteSuccessAction(BaseQTE.QteActionType actionType)
        {
            // 성공 시 필요한 기믹별 액션 처리 (가속 등)
        }

        [ClientRpc] // 🌟 클라이언트 RPC는 기존 규격 유지
        private void NotifyPlayerQTEFailureClientRpc(BaseQTE.QteActionType actionType)
        {
            if (controller == null) return;

            Debug.Log($"[QTE 실패] Client {OwnerClientId}가 {actionType} 기믹 실패로 스턴 상태에 진입합니다.");

            // 실패 시 확실하게 스턴(넘어짐) 처리
            controller.TriggerFall();
        }
    }
}
