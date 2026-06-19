using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float sprintSpeed = 10f;
    [SerializeField] private float dashSpeed = 15f;
    [SerializeField] private float acceleration = 20f;
    [SerializeField] private float deceleration = 15f;

    [Header("Stamina")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float sprintDrainRate = 20f;
    [SerializeField] private float staminaRegenRate = 10f;
    [SerializeField] private float minSprintStamina = 10f;
    [SerializeField] private float dashStaminaCost = 30f;

    [Header("Jump / Gravity")]
    [SerializeField] private float jumpForce = 8f;
    [SerializeField] private float gravity = 25f;
    [SerializeField] private float maxFallSpeed = 30f;
    [SerializeField] private float groundCheckDistance = 0.3f;
    [SerializeField] private LayerMask groundLayer = ~0;
    [SerializeField] private bool debugGround = false;

    [Header("Jump Buffer")]
    [SerializeField] private float jumpBufferTime = 0.15f;

    [Header("Lane (Z축)")]
    [SerializeField] private int startLane = 3;
    [SerializeField] private float laneSnapSpeed = 8f;
    [SerializeField] private float laneChangeCooldown = 0.3f;

    [Header("Fallen / Dash")]
    [SerializeField] private float fallenDuration = 1.2f;
    [SerializeField] private float recoveryTime = 0.8f;
    [SerializeField] private float knockbackForce = 6f;
    [SerializeField] private float dashDuration = 1.5f;

    [Header("Player Collision")]
    [SerializeField] private float playerCheckRadius = 0.6f;
    [SerializeField] private LayerMask playerLayer;

    public event Action OnItemUse;
    public event Action OnInteract;

    // 상태 인스턴스 (상태 클래스에서 ChangeState로 참조)
    public readonly PlayerIdleState IdleState = new PlayerIdleState();
    public readonly PlayerRunState RunState = new PlayerRunState();
    public readonly PlayerDashState DashState = new PlayerDashState();
    public readonly PlayerStunState StunState = new PlayerStunState();
       public readonly PlayerAirborneState AirborneState = new PlayerAirborneState();
    private IPlayerState _currentState;

    private Rigidbody _rb;
    private CapsuleCollider _col;
    private IInputProvider _input;
    private Vector3 _velocity;

    private float _stamina;
    private bool _isGrounded;
    private int _currentLane;
    private float _laneChangeCooldownTimer;
    private float _jumpBufferTimer;
    private float _recoveryTimer;
    private float _recoverySpeedMult = 1f;
    // source → multiplier. Overpass/Kickboard/Puddle 등 여러 효과가 동시에 곱해지도록.
    // 단일 슬롯이던 시절에는 한쪽이 끝나면 다른 효과가 사라지는 버그가 있었음.
    private readonly Dictionary<string, float> _speedModifiers = new();
    private Coroutine _slowCoroutine;

    // 외부/상태 접근용 프로퍼티
    public IInputProvider Input => _input;
    public float Stamina => _stamina;
    public float MaxStamina => maxStamina;
    public float MinSprintStamina => minSprintStamina;
    public float SprintDrainRate => sprintDrainRate;
    public float DashStaminaCost => dashStaminaCost;
    public float WalkSpeed => walkSpeed;
    public float SprintSpeed => sprintSpeed;
    public float DashSpeed => dashSpeed;
    public float DashDuration => dashDuration;
    public float FallenDuration => fallenDuration;
    public bool IsSprinting => _currentState == RunState;
    public bool IsFallen => _currentState == StunState;
    public bool IsDashing => _currentState == DashState;
    public int CurrentLane => _currentLane;

    public void Initialize(IInputProvider inputProvider) => _input = inputProvider;

    private void OnEnable()
    {
        StageEventManager.OnForceLaneChangeRequested += OnGimmickForceLaneChange;
    }

    private void OnDisable()
    {
        StageEventManager.OnForceLaneChangeRequested -= OnGimmickForceLaneChange;
    }

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _col = GetComponent<CapsuleCollider>();

        _rb.isKinematic = true;
        _rb.useGravity = false;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        _stamina = maxStamina;
    }

    private void Start()
    {
        if (_input == null) Initialize(new PlayerInputProvider());

        if (LaneManager.Instance == null)
        {
            Debug.LogError("[PlayerController] LaneManager not found in scene! Add a LaneManager GameObject.");
            _currentLane = startLane;
        }
        else
        {
            _currentLane = FindNearestLane(transform.position.z);
            var pos = transform.position;
            pos.z = LaneManager.Instance.GetLaneZ(_currentLane);
            transform.position = pos;
        }

        ChangeState(IdleState);
    }

    private int FindNearestLane(float z)
    {
        int nearest = 0;
        float minDist = float.MaxValue;
        int count = LaneManager.Instance.LaneCount;
        for (int i = 0; i < count; i++)
        {
            float d = Mathf.Abs(LaneManager.Instance.GetLaneZ(i) - z);
            if (d < minDist) { minDist = d; nearest = i; }
        }
        return nearest;
    }

    // 비-owner의 로컬 시뮬레이션 차단 — NetworkTransform 보간과 경쟁 방지.
    // AI 봇은 호스트가 서버 권한으로 시뮬레이션해야 하므로 예외로 허용.
    private bool IsLocallySimulated()
    {
        if (!TryGetComponent<Unity.Netcode.NetworkObject>(out var netObj)) return true;
        if (netObj.IsOwner) return true;
        return Unity.Netcode.NetworkManager.Singleton != null
            && Unity.Netcode.NetworkManager.Singleton.IsServer
            && !netObj.IsOwnedByServer
            && netObj.OwnerClientId == Unity.Netcode.NetworkManager.ServerClientId;
    }

    private void Update()
    {
        if (_input == null) return;
        if (!IsLocallySimulated()) return;

        CheckGrounded();

        HandleNaturalStaminaRegen();
        HandleLaneChange();
        HandleJumpInput();
        HandleItemAndInteract();
        HandleSlipstreamCheck();
        HandleItemAndInteract();
        UpdateRecoveryMultiplier();

        _currentState.UpdateState(this);
    }

    private void FixedUpdate()
    {
        if (_input == null) return;
        if (!IsLocallySimulated()) return;

        CheckGrounded();
        ApplyGravity();
        _currentState.FixedUpdateState(this);
        HandleLaneSnap();
        ApplyVelocity();
        ApplyVelocityInternal();
        CheckPlayerCollision();
    }

    // ── FSM 제어 ─────────────────────────────────────────

    public void ChangeState(IPlayerState newState)
    {
        _currentState?.ExitState(this);
        _currentState = newState;
        _currentState.EnterState(this);
    }

    public void CalculateForwardVelocity(float targetBaseSpeed)
    {
        float target = targetBaseSpeed * _recoverySpeedMult * ComputeSpeedMultiplier();
        float rate = (targetBaseSpeed > 0f) ? acceleration : deceleration;
        _velocity.x = Mathf.MoveTowards(_velocity.x, target, rate * Time.fixedDeltaTime);
    }

    private float ComputeSpeedMultiplier()
    {
        float m = 1f;
        foreach (var v in _speedModifiers.Values) m *= v;
        return m;
    }

    public void SetVelocityX(float newX) => _velocity.x = newX;
    public void ConsumeStamina(float amount) => _stamina = Mathf.Max(0f, _stamina - amount);
    public void StartRecoveryWindow() => _recoveryTimer = recoveryTime;
    public void TriggerFall() => ChangeState(StunState);

    public bool TryTriggerDash()
    {
        if (IsFallen || _currentState == DashState) return false;
        if (_stamina < dashStaminaCost) return false;
        ChangeState(DashState);
        return true;
    }

    // 충돌 감지 sphere(0.6) + 상대 capsule(~0.5) 합이 lane spacing(1.0)보다 커서
    // 옆 lane에서도 OverlapSphere가 잡힘. 같은 lane만 처리하도록 Z 좌표로 사후 필터.
    public bool IsInSameLane(Collider other)
    {
        float spacing = LaneManager.Instance != null ? LaneManager.Instance.LaneSpacing : 1f;
        return Mathf.Abs(transform.position.z - other.transform.position.z) < spacing * 0.5f;
    }

    // ── 스태미나/회복 ─────────────────────────────────────

    private void HandleNaturalStaminaRegen()
    {
        if (_currentState == RunState || _currentState == DashState) return;
        _stamina = Mathf.Min(maxStamina, _stamina + staminaRegenRate * Time.deltaTime);
    }

    private void UpdateRecoveryMultiplier()
    {
        if (_recoveryTimer > 0f)
        {
            _recoveryTimer -= Time.deltaTime;
            _recoverySpeedMult = Mathf.Clamp01(1f - _recoveryTimer / recoveryTime);
        }
        else
        {
            _recoverySpeedMult = 1f;
        }
    }


    private void ApplyKnockback(Vector3 dir)
    {
        dir.y = 0.4f;
        dir.z = 0f;
        _velocity += dir.normalized * knockbackForce;
    }

    // ── 중력 ──────────────────────────────────────────────

    private void ApplyGravity()
    {
        if (_isGrounded && _velocity.y <= 0f)
        {
            _velocity.y = 0f;
            return;
        }
        _velocity.y = Mathf.Max(_velocity.y - gravity * Time.fixedDeltaTime, -maxFallSpeed);
    }

    // ── Z축 스냅 (라인 이동) ──────────────────────────────

    private void HandleLaneSnap()
    {
        if (LaneManager.Instance == null) { _velocity.z = 0f; return; }

        float targetZ = LaneManager.Instance.GetLaneZ(_currentLane);
        float currentZ = _rb.position.z;
        if (Mathf.Abs(currentZ - targetZ) < 0.001f)
        {
            _velocity.z = 0f;
            return;
        }
        float dir = Mathf.Sign(targetZ - currentZ);
        _velocity.z = dir * laneSnapSpeed;
    }

    private void HandleLaneChange()
    {
        //if (_isFallen) return;
        if (_currentState == AirborneState) return;
        // 아이템 관련 수정
        // [추가] 택시 아이템 작동 중일 때 플레이어의 좌우 레인 변경 입력을 강제로 차단
        if (TryGetComponent<NetworkItemInventory>(out var inv) && inv.IsLaneLocked) return;

        _laneChangeCooldownTimer -= Time.deltaTime;
        if (_laneChangeCooldownTimer > 0f) return;

        int laneCount = LaneManager.Instance != null ? LaneManager.Instance.LaneCount : 6;

        float v = _input.GetLaneChange();
        // 자전거 도로 보행자 도로 분할
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "05_Stage_Bicycle" && transform.position.x > 125f)
        {
            // 조건 A: 자전거 도로(0~2)에서 보행자 도로(3~5)로 넘어가려는 변경 시도 원천 차단
            if (_currentLane == 2 && v < -0.5f) return;

            // 조건 B: 보행자 도로(3~5)에서 자전거 도로(0~2)로 가로지르려는 변경 시도 원천 차단
            if (_currentLane == 3 && v > 0.5f) return;
        }

        if (v > 0.5f && _currentLane > 0)
        {
            _currentLane--;
            _laneChangeCooldownTimer = laneChangeCooldown;
        }
        else if (v < -0.5f && _currentLane < laneCount - 1)
        {
            _currentLane++;
            _laneChangeCooldownTimer = laneChangeCooldown;
        }
    }


    private void ApplyVelocityInternal()
    {
        Vector3 newPos = _rb.position + _velocity * Time.fixedDeltaTime;

        if (LaneManager.Instance != null)
        {
            float targetZ = LaneManager.Instance.GetLaneZ(_currentLane);
            // 목표 라인 정렬 보정 로직
            if (Mathf.Sign(targetZ - _rb.position.z) != Mathf.Sign(targetZ - newPos.z)
                && Mathf.Abs(targetZ - newPos.z) < laneSnapSpeed * Time.fixedDeltaTime * 1.5f)
            {
                newPos.z = targetZ;
            }
        }

        // Kinematic 바디의 정석 이동 방식
        _rb.MovePosition(newPos);
    }
    // ── 적용 ──────────────────────────────────────────────

    private void ApplyVelocity()
    {
        Vector3 newPos = _rb.position + _velocity * Time.fixedDeltaTime;

        if (LaneManager.Instance != null)
        {
            float targetZ = LaneManager.Instance.GetLaneZ(_currentLane);
            if (Mathf.Sign(targetZ - _rb.position.z) != Mathf.Sign(targetZ - newPos.z)
                && Mathf.Abs(targetZ - newPos.z) < laneSnapSpeed * Time.fixedDeltaTime * 1.5f)
            {
                newPos.z = targetZ;
            }
        }

        _rb.MovePosition(newPos);
    }

    // ── 점프 입력 ─────────────────────────────────────────

    private void HandleJumpInput()
    {
        if (IsFallen) return;

        if (_input.GetJumpDown())
        {
            _jumpBufferTimer = jumpBufferTime;
        }
        else
        {
            _jumpBufferTimer -= Time.deltaTime;
        }

        if (_jumpBufferTimer > 0f && _isGrounded)
        {
            _velocity.y = jumpForce;
            _isGrounded = false;
            _jumpBufferTimer = 0f;
        }
    }


    // ── 지면 체크 ─────────────────────────────────────────

    private void CheckGrounded()
    {
        Vector3 origin = transform.position + Vector3.down * (_col.height * 0.5f - _col.radius);
        // 낙하 속도가 빠를 때 한 fixed step에 이동 거리(|vy|*dt)보다 cast 거리가 짧으면 바닥을 놓침 → 무한 추락.
        // groundCheckDistance를 baseline으로 두고, 필요 시 동적으로 늘려 tunneling 방지.
        float dist = Mathf.Max(groundCheckDistance, Mathf.Abs(_velocity.y) * Time.fixedDeltaTime + 0.05f);
        _isGrounded = Physics.SphereCast(
            origin, _col.radius * 0.9f,
            Vector3.down, out var hit,
            dist, groundLayer, QueryTriggerInteraction.Ignore);

        if (debugGround)
            Debug.Log($"[Grounded] origin={origin} pos.y={transform.position.y:F2} grounded={_isGrounded} dist={dist:F2} hit={(hit.collider != null ? hit.collider.name : "none")}");
    }

    // ── 아이템 / 상호작용 ─────────────────────────────────

    private void HandleItemAndInteract()
    {
        if (_input.GetDashDown())     TryTriggerDash();
        if (_input.GetItemUse())      OnItemUse?.Invoke();
        if (_input.GetInteractDown()) OnInteract?.Invoke();
    }

    // ── 플레이어 간 충돌 (FSM에 위임) ─────────────────────

    private void CheckPlayerCollision()
    {
        Vector3 center = transform.position + Vector3.up * (_col.height * 0.5f);
        var hits = Physics.OverlapSphere(center, playerCheckRadius, playerLayer);

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].transform == transform) continue;
            _currentState.OnCollisionCheck(this, hits[i]);
        }
    }

    // ── 장애물 충돌 (Trigger) ─────────────────────────────

    private void OnTriggerEnter(Collider col)
    {
        if (_currentState == StunState) return; // 스턴 상태 면역(무적) 유지

        // 아이템 관련 수정
        // [추가] 킥보드 및 택시 돌진(IsItemDashing) 중에는 일반 장애물 충돌 판정을 무시
        if (TryGetComponent<NetworkItemInventory>(out var inv) && inv.IsItemDashing) return;

        if (col.TryGetComponent<ObstacleBase>(out var obstacle) && obstacle.KnockDownOnCollision)
        {
            TriggerFall();
            ApplyKnockback(transform.position - col.transform.position);
        }
    }

    // ObstacleBase에서 호출. 충돌 지점을 받아 knockback 방향 계산.
    public void HitByObstacle(Vector3 obstaclePos)
    {
        TriggerFall();
        ApplyKnockback(transform.position - obstaclePos);
    }


    // ── 공개 메서드 ───────────────────────────────────────

    public void SetSpeedMultiplier(string source, float mult) => _speedModifiers[source] = mult;
    public void ClearSpeedMultiplier(string source) => _speedModifiers.Remove(source);
    public void RecoverStamina(float amount) => _stamina = Mathf.Min(maxStamina, _stamina + amount);

    private const string SlowSource = "puddle";
    public void ApplySlow(float speedRatio, float duration)
    {
        if (_slowCoroutine != null) StopCoroutine(_slowCoroutine);
        _slowCoroutine = StartCoroutine(SlowRoutine(speedRatio, duration));
    }

    private System.Collections.IEnumerator SlowRoutine(float ratio, float duration)
    {
        SetSpeedMultiplier(SlowSource, ratio);
        yield return new WaitForSeconds(duration);
        ClearSpeedMultiplier(SlowSource);
    }

    // ── 기믹: 강제 라인 변경 ──────────────────────────────

    private void OnGimmickForceLaneChange(int direction)
    {
        if (TryGetComponent<Unity.Netcode.NetworkObject>(out var netObj))
        {
            bool isMyCharacter = netObj.IsOwner;
            bool isServerSimulatedBot = Unity.Netcode.NetworkManager.Singleton != null &&
                                        Unity.Netcode.NetworkManager.Singleton.IsServer &&
                                        !netObj.IsOwnedByServer &&
                                        netObj.OwnerClientId == Unity.Netcode.NetworkManager.ServerClientId;

            if (!isMyCharacter && !isServerSimulatedBot) return;
        }

        if (IsFallen) return;

        int laneCount = LaneManager.Instance != null ? LaneManager.Instance.LaneCount : 6;
        int targetLane = Mathf.Clamp(_currentLane + direction, 0, laneCount - 1);

        if (targetLane != _currentLane)
        {
            _currentLane = targetLane;
            _laneChangeCooldownTimer = laneChangeCooldown;

            Debug.Log($"[{gameObject.name}] 글로벌 기믹 신호로 레인 강제 보정: {targetLane}");
        }
    }
    
    // 아이템 관련 수정
    // 택시 아이템 사용 시 중앙 레인으로 강제 이동
    public void ForceSetLane(int laneIndex)
    {
        int laneCount = LaneManager.Instance != null ? LaneManager.Instance.LaneCount : 6;
        _currentLane = Mathf.Clamp(laneIndex, 0, laneCount - 1);
    }

    private void HandleSlipstreamCheck()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "05_Stage_Bicycle") return;
        if (TryGetComponent<Unity.Netcode.NetworkObject>(out var netObj) && !netObj.IsOwner) return;
        if (_currentState != RunState) return; // 오직 달리기를 사용하는 상태에서만 소모 감면 유효

        bool isInSlipstreamZone = false;
        float maxTrackDistance = 8f; // 동등 차선 내 허용 최대 임계 거리 8m

        // 현재 씬에 생성된 모든 플레이어 컨트롤러 객체를 탐색하여 전방 추종 대상 비교
        PlayerController[] allPlayers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var other in allPlayers)
        {
            if (other == this) continue;
            if (other.IsFallen) continue;

            // 동일한 라인에 정렬되어 있는지 검증
            if (other.CurrentLane == this._currentLane)
            {
                float distanceX = other.transform.position.x - this.transform.position.x;

                // 상대방이 내 앞에 있고(distanceX > 0) 허용 기준 내에 들어온 경우 슬립 스트림 적용
                if (distanceX > 0f && distanceX <= maxTrackDistance)
                {
                    isInSlipstreamZone = true;
                    break;
                }
            }
        }

        if (isInSlipstreamZone)
        {
            // 슬립 스트림 영역 안에서는 달리기를 유지해도 스태미나가 소모되지 않도록 
            _stamina = Mathf.Min(maxStamina, _stamina + sprintDrainRate * Time.deltaTime);
        }
    }

    public void SetVelocityY(float newY) => _velocity.y = newY;

}
