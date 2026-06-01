using System;
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

    [Header("Fallen / Dash Spec")]
    [SerializeField] private float fallenDuration = 1.2f;
    [SerializeField] private float recoveryTime = 0.8f;
    [SerializeField] private float knockbackForce = 6f;
    [SerializeField] private float dashDuration = 1.5f;

    [Header("Player Collision")]
    [SerializeField] private float playerCheckRadius = 0.6f;
    [SerializeField] private LayerMask playerLayer;

    public event Action OnItemUse;
    public event Action OnInteract;

    // 상태 인스턴스    
    public readonly PlayerIdleState IdleState = new PlayerIdleState();
    public readonly PlayerRunState RunState = new PlayerRunState();
    public readonly PlayerDashState DashState = new PlayerDashState();
    public readonly PlayerStunState StunState = new PlayerStunState();
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
    private float _externalSpeedMult = 1f;
    private Coroutine _slowCoroutine;

    // 없애야할 변수
    private bool _isSprinting;
    private bool _isFallen;
    private float _fallenTimer;


    // 프로퍼티
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

        // 초기 상태
        ChangeState(IdleState);

        if (LaneManager.Instance == null)
        {
            Debug.LogError("[PlayerController] LaneManager not found in scene! Add a LaneManager GameObject.");
            _currentLane = startLane;
            return;
        }

        _currentLane = FindNearestLane(transform.position.z);

        var pos = transform.position;
        pos.z = LaneManager.Instance.GetLaneZ(_currentLane);
        transform.position = pos;
    }

    private void Update()
    {
        if (_input == null) return;

        CheckGrounded();
        //HandleStamina();
        HandleNaturalStaminaRegen();
        HandleLaneChange();
        HandleJumpInput();
        HandleItemAndInteract();
        //UpdateFallenState();
        HandleItemAndInteract();
        UpdateRecoveryMultiplier();

        _currentState.UpdateState(this);
    }

    private void FixedUpdate()
    {
        if (_input == null) return;

        ApplyGravity();

        _currentState.FixedUpdateState(this);

        //HandleMovement();
        HandleLaneSnap();
        //ApplyVelocity();
        ApplyVelocityInternal();
        CheckPlayerCollision();
    }

    public void ChangeState(IPlayerState newState)
    {
        _currentState?.ExitState(this);
        _currentState = newState;
        _currentState.EnterState(this);
    }

    // ── 상태 전용 제어 API ──
    public void CalculateForwardVelocity(float targetBaseSpeed)
    {
        float targetSpeed = targetBaseSpeed * _recoverySpeedMult * _externalSpeedMult;
        float accelRate = (targetBaseSpeed > 0f) ? acceleration : deceleration;
        _velocity.x = Mathf.MoveTowards(_velocity.x, targetSpeed, accelRate * Time.fixedDeltaTime);
    }

    public void SetVelocityX(float newX) => _velocity.x = newX;
    public void ConsumeStamina(float amount) => _stamina = Mathf.Max(0f, _stamina - amount);
    public void StartRecoveryWindow() => _recoveryTimer = recoveryTime;
    public void TriggerFall() => ChangeState(StunState);

    public void TryTriggerDash()
    {
        if (IsFallen || _currentState == DashState) return;
        if (_stamina >= dashStaminaCost)
        {
            ChangeState(DashState);
        }
    }

    private void HandleNaturalStaminaRegen()
    {
        if (_currentState != RunState && _currentState != DashState)
        {
            _stamina = Mathf.Min(maxStamina, _stamina + staminaRegenRate * Time.deltaTime);
        }
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

    private void OnTriggerEnter(Collider col)
    {
        if (_currentState == StunState) return; // 스턴 상태 면역(무적) 유지

        if (col.TryGetComponent<ObstacleBase>(out var obstacle) && obstacle.KnockDownOnCollision)
        {
            TriggerFall();
            ApplyKnockback(transform.position - col.transform.position);
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

    /*
    // ── X축 이동 (달리기) ──────────────────────────────────

    private void HandleMovement()
    {
        // 카운트다운/관전/스폰 직후처럼 입력이 비활성화된 상태에선 자동 전진도 안 함.
        if (_isFallen || _input is NullInputProvider)
        {
            _velocity.x = Mathf.MoveTowards(_velocity.x, 0f, deceleration * Time.fixedDeltaTime);
            return;
        }

        // 자동 전진: 입력 없이 항상 앞으로. 스프린트(J 홀드)로 속도 부스트.
        float speed = (_isSprinting ? sprintSpeed : walkSpeed) * _recoverySpeedMult * _externalSpeedMult;
        _velocity.x = Mathf.MoveTowards(_velocity.x, speed, acceleration * Time.fixedDeltaTime);
    }
    */

    // ── 라인 변경 ─────────────────────────────────────────
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
        if (_isFallen) return;
        _laneChangeCooldownTimer -= Time.deltaTime;
        if (_laneChangeCooldownTimer > 0f) return;

        int laneCount = LaneManager.Instance != null ? LaneManager.Instance.LaneCount : 6;

        float v = _input.GetLaneChange();
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

    // ── 지면 체크 ─────────────────────────────────────────
    private void CheckGrounded()
    {
        Vector3 origin = transform.position + Vector3.down * (_col.height * 0.5f - _col.radius);
        _isGrounded = Physics.SphereCast(
            origin, _col.radius * 0.9f,
            Vector3.down, out var hit,
            groundCheckDistance, groundLayer, QueryTriggerInteraction.Ignore);

        if (debugGround)
            Debug.Log($"[Grounded] origin={origin} pos.y={transform.position.y:F2} grounded={_isGrounded} hit={(hit.collider != null ? hit.collider.name : "none")}");
    }

    // ── 점프 입력 ─────────────────────────────────────────
    private void HandleJumpInput()
    {
        if (_isFallen) return;

        if (_input.GetJumpDown())
        {
            _jumpBufferTimer = jumpBufferTime;
        }
        else
        {
            // 키 입력이 없는 프레임에는 타이머 차감
            _jumpBufferTimer -= Time.deltaTime;
        }

        if (_jumpBufferTimer > 0f && _isGrounded)
        {
            _velocity.y = jumpForce;
            _isGrounded = false;
            _jumpBufferTimer = 0f;
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
    /*
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



        // ── 스태미나 ──────────────────────────────────────────

        private void HandleStamina()
        {
            if (_input.GetSprint() && _stamina >= minSprintStamina && !_isFallen)
            {
                _isSprinting = true;
                _stamina = Mathf.Max(0f, _stamina - sprintDrainRate * Time.deltaTime);
                if (_stamina <= 0f) _isSprinting = false;
            }
            else
            {
                _isSprinting = false;
                _stamina = Mathf.Min(maxStamina, _stamina + staminaRegenRate * Time.deltaTime);
            }
        }

        // ── 넘어짐 ────────────────────────────────────────────

        public void TriggerFall()
        {
            if (_isFallen) return;
            _isFallen = true;
            _fallenTimer = fallenDuration;
            _recoverySpeedMult = 0f;
            _velocity.x = 0f;
        }

        private void UpdateFallenState()
        {
            if (_isFallen)
            {
                _fallenTimer -= Time.deltaTime;
                if (_fallenTimer <= 0f)
                {
                    _isFallen = false;
                    _recoveryTimer = recoveryTime;
                }
                return;
            }
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
    */

    // ── 아이템 / 상호작용 ─────────────────────────────────

    private void HandleItemAndInteract()
    {
        if (_input.GetItemUse()) OnItemUse?.Invoke();
        if (_input.GetInteractDown()) OnInteract?.Invoke();
    }

    // ── 공개 메서드 ───────────────────────────────────────

    public void SetSpeedMultiplier(float mult) => _externalSpeedMult = mult;
    public void RecoverStamina(float amount) => _stamina = Mathf.Min(maxStamina, _stamina + amount);


    // ㅡㅡ 감속 장애물 처리 ㅡㅡ
    public void ApplySlow(float speedRatio, float duration)
    {
        if (_slowCoroutine != null)
        {
            StopCoroutine(_slowCoroutine);
        }
        _slowCoroutine = StartCoroutine(SlowRoutine(speedRatio, duration));
    }

    private System.Collections.IEnumerator SlowRoutine(float ratio, float duration)
    {
        _externalSpeedMult = ratio;
        yield return new WaitForSeconds(duration);

        _externalSpeedMult = 1.0f;
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

    private void OnGimmickForceLaneChange(int direction)
    {
        // 1. NGO 멀티플레이 환경 소유권 필터링
        if (TryGetComponent<Unity.Netcode.NetworkObject>(out var netObj))
        {
            // 로컬 유저 캐릭, AI 봇(호스트 경우)에만 수정 가능
            bool isMyCharacter = netObj.IsOwner;
            bool isServerSimulatedBot = Unity.Netcode.NetworkManager.Singleton != null &&
                                        Unity.Netcode.NetworkManager.Singleton.IsServer &&
                                        !netObj.IsOwnedByServer &&
                                        netObj.OwnerClientId == Unity.Netcode.NetworkManager.ServerClientId;

            if (!isMyCharacter && !isServerSimulatedBot) return;
        }

        // 예외 상태 처리
        //if (_isFallen) return;

        // 6레인 한계 연산 및 내부 스냅 인덱스 변경
        int laneCount = LaneManager.Instance != null ? LaneManager.Instance.LaneCount : 6;
        int targetLane = Mathf.Clamp(_currentLane + direction, 0, laneCount - 1);

        if (targetLane != _currentLane)
        {
            _currentLane = targetLane;
            _laneChangeCooldownTimer = laneChangeCooldown; // 변경 직후 쿨타임 동기화

            Debug.Log($"[{gameObject.name}] 글로벌 기믹 신호로 레인 강제 보정: {targetLane}");
        }
    }


}
