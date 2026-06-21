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

    [Header("Camera Occlusion Fade")]
    [Tooltip("카메라-플레이어 사이를 가로막는 오브젝트의 material로 swap할 반투명 material. 비우면 occlusionFadeAlpha 값으로 런타임 생성.")]
    [SerializeField] private Material occlusionFadeMaterial;
    [Tooltip("occlusionFadeMaterial이 비었을 때 자동 생성되는 fade material의 알파(0~1). 낮을수록 더 투명.")]
    [SerializeField, Range(0f, 1f)] private float occlusionFadeAlpha = 0.3f;
    [Tooltip("occlusion 체크 주기 (프레임). 1=매 프레임. 클수록 가벼움.")]
    [SerializeField] private int occlusionCheckFrameInterval = 3;
    [Tooltip("occlusion raycast가 검사할 layer mask. ~0 = 모든 layer.")]
    [SerializeField] private LayerMask occlusionMask = ~0;
    [Tooltip("occlusion raycast 시작 카메라. 비우면 자동으로 Camera.main 사용.")]
    [SerializeField] private Camera occlusionCamera;

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
    // 현재 캡슐이 trigger 안에 들어있는 모든 UndergroundHole. 매 프레임 _currentLane이 hole 범위 안인지 재검사 →
    // capsule이 살짝 옆 lane trigger를 침범해도 _currentLane이 hole 차선이 아니면 안 빠짐.
    private readonly System.Collections.Generic.HashSet<Seoul.Network.Game.UndergroundHole> _hoveringHoles
        = new System.Collections.Generic.HashSet<Seoul.Network.Game.UndergroundHole>();
    // 지하차도 옆 벽(UndergroundWall) trigger 중첩 카운트.
    // > 0 인 상태에서 lane change가 실제로 발생하려는 순간 → fall + knockback.
    private int _underWallOverlap;
    // 현재 캐릭터가 위에 있는 출구 ramp (없으면 null). FixedUpdate에서 캐릭터 y를 ramp surface로 자동 보정.
    private Seoul.Network.Game.UndergroundExitRamp _currentExitRamp;
    // 현재 캐릭터가 들어있는 lane 범위 제한 zone (없으면 null). 안에서 lane change target이 범위 밖이면 차단.
    private readonly System.Collections.Generic.HashSet<Seoul.Network.Game.LaneRangeZone> _overlappingLaneRangeZones
        = new System.Collections.Generic.HashSet<Seoul.Network.Game.LaneRangeZone>();
    // Penalty zone 진입 시 부여되는 무적 시간. > 0인 동안: Penalty zone 재진입 + KnockDown 장애물 충돌 무시.
    private float _invincibilityTimer;
    private Coroutine _blinkCoroutine;
    // 현재 겹치는 BackWall들. 활성 Y 조건을 만족하는 벽이 있으면 _velocity.x를 0 이하로 클램프(전진 금지).
    private readonly System.Collections.Generic.HashSet<Seoul.Network.Game.BackWall> _overlappingBackWalls
        = new System.Collections.Generic.HashSet<Seoul.Network.Game.BackWall>();
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
        if (_invincibilityTimer > 0f) _invincibilityTimer -= Time.deltaTime;
        UpdateCameraOcclusion();

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
        // 출구 BackWall 안에 있으면 전진(+x) 차단. lane change(z), 중력(y)은 그대로.
        // 단, 출구 ramp를 올라오는 중인 플레이어는 BackWall 예외 (안 그러면 ramp 위에서 멈춤).
        if (IsInActiveBackWall() && _currentExitRamp == null && _velocity.x > 0f) _velocity.x = 0f;
        ApplyVelocity();
        ApplyVelocityInternal();
        SnapToExitRamp();
        CheckPlayerCollision();
    }

    // Ramp 영역에 있는 동안 캐릭터 발을 ramp surface로 자동 보정 (올라가기/내려가기/추락 모두).
    // - 위로 가는 중(점프, vy>0.1)에만 skip → 점프 곡선 자유.
    // - 추락(vy<0)에도 snap 작동 → 입구 계단을 부드럽게 따라 내려옴.
    // - surface와의 갭이 1m를 넘으면 skip (점프 정점 등 — 캐릭터 곡선이 다시 surface 가까이 오면 다시 snap).
    // 주의: ApplyVelocity가 이미 _rb.MovePosition으로 (oldPos + vel*dt) target을 설정해뒀지만
    //       _rb.position은 다음 물리 step까지 OLD 값. 여기서 _rb.position을 그대로 쓰면
    //       방금 설정한 전진 target을 덮어써서 x 이동이 통째로 사라짐. velocity를 다시 적용한 뒤 y만 보정.
    private void SnapToExitRamp()
    {
        if (_currentExitRamp == null) return;
        if (!_currentExitRamp.IsActiveForLane(_currentLane)) return;
        if (_velocity.y > 0.1f) return;

        Vector3 newPos = _rb.position + _velocity * Time.fixedDeltaTime;

        // ApplyVelocity가 적용하던 lane z snap을 여기서도 동일하게 보존 (없으면 ramp 중 lane change overshoot).
        if (LaneManager.Instance != null)
        {
            float targetZ = LaneManager.Instance.GetLaneZ(_currentLane);
            if (Mathf.Sign(targetZ - _rb.position.z) != Mathf.Sign(targetZ - newPos.z)
                && Mathf.Abs(targetZ - newPos.z) < laneSnapSpeed * Time.fixedDeltaTime * 1.5f)
            {
                newPos.z = targetZ;
            }
        }

        float surfaceY = _currentExitRamp.SurfaceYAt(newPos.x);
        float targetY  = surfaceY + _col.height * 0.5f; // capsule pivot center 가정
        if (Mathf.Abs(targetY - newPos.y) > 1f) return;

        newPos.y = targetY;
        _rb.MovePosition(newPos);
        _velocity.y = 0f;
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
            // 지하차도 영역에서 lane change 시도 → fall (옆에서 hole로 진입 방지).
            if (_underWallOverlap > 0) return; // 지하차도 옆 벽 — lane change 자체 차단 (페널티 없이 막힘)
            int target = _currentLane - 1;
            // HardBlock 모드 zone만 차단 — Penalty 모드는 lane change 그대로 진행 (페널티는 OnTriggerEnter에서 처리).
            if (IsHardBlockedByZone(target)) return;
            _currentLane = target;
            _laneChangeCooldownTimer = laneChangeCooldown;
        }
        else if (v < -0.5f && _currentLane < laneCount - 1)
        {
            if (_underWallOverlap > 0) return; // 지하차도 옆 벽 — lane change 자체 차단 (페널티 없이 막힘)
            int target = _currentLane + 1;
            if (IsHardBlockedByZone(target)) return;
            _currentLane = target;
            _laneChangeCooldownTimer = laneChangeCooldown;
        }
    }

    private bool IsInActiveHole()
    {
        foreach (var hole in _hoveringHoles)
        {
            if (hole == null) continue;
            if (_currentLane >= hole.MinLane && _currentLane <= hole.MaxLane) return true;
        }
        return false;
    }

    private bool IsInActiveBackWall()
    {
        foreach (var wall in _overlappingBackWalls)
        {
            if (wall == null) continue;
            if (wall.IsActiveFor(this)) return true;
        }
        return false;
    }

    private bool IsHardBlockedByZone(int targetLane)
    {
        foreach (var zone in _overlappingLaneRangeZones)
        {
            if (zone == null) continue;
            if (!zone.IsActiveFor(this)) continue;

            int min = zone.MinLane;
            int max = zone.MaxLane;
            bool currentInside = _currentLane >= min && _currentLane <= max;
            bool targetInside = targetLane >= min && targetLane <= max;

            switch (zone.Mode)
            {
                case Seoul.Network.Game.LaneRangeZone.BlockMode.HardBlock:
                    if (IsOutsideRangeAndAwayFromCurrent(zone, targetLane)) return true;
                    break;
                case Seoul.Network.Game.LaneRangeZone.BlockMode.NoEntry:
                // 현재 lane이 범위 밖일 때 안으로 들어오는 lane change 차단. 안에서의 이동은 자유.
                    if (!currentInside && targetInside) return true;
                    break;
                case Seoul.Network.Game.LaneRangeZone.BlockMode.BoundaryLock:
                    if (currentInside != targetInside) return true;
                    break;
            }
        }

        return false;
    }

    // zone 범위 밖으로 "벗어나는" 방향인지 판정. 현재 lane이 이미 밖이어도 안쪽으로 좁히는 변경(예: 4 → 3)은 허용.
    private bool IsOutsideRangeAndAwayFromCurrent(Seoul.Network.Game.LaneRangeZone zone, int targetLane)
    {
        int min = zone.MinLane;
        int max = zone.MaxLane;
        if (targetLane >= min && targetLane <= max) return false; // target이 범위 안 → 허용

        // target이 범위 밖 — 현재 lane보다 더 멀어지는 경우만 차단.
        if (targetLane < min && targetLane < _currentLane) return true; // 더 작은 쪽으로 벗어남
        if (targetLane > max && targetLane > _currentLane) return true; // 더 큰 쪽으로 벗어남
        return false;
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

        // hole trigger 안 + _currentLane이 hole의 lane 범위 안일 때만 도로 ground 무시 → 추락.
        // 옆 lane에서 trigger 살짝 침범해도 lane 검사로 걸러짐.
        if (IsInActiveHole()) _isGrounded = false;

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
        // 지하차도 입구 — 스턴 중에도 등록해야 exit와 짝이 맞음.
        // 추락 여부는 CheckGrounded에서 _currentLane을 hole 범위와 매 프레임 비교해서 결정.
        var holeEnter = col.GetComponentInParent<Seoul.Network.Game.UndergroundHole>();
        if (holeEnter != null) _hoveringHoles.Add(holeEnter);

        if (col.GetComponentInParent<Seoul.Network.Game.UndergroundWall>() != null)
            _underWallOverlap++;

        var backWallEnter = col.GetComponentInParent<Seoul.Network.Game.BackWall>();
        if (backWallEnter != null) _overlappingBackWalls.Add(backWallEnter);

        var ramp = col.GetComponentInParent<Seoul.Network.Game.UndergroundExitRamp>();
        if (ramp != null) _currentExitRamp = ramp;

        var laneZone = col.GetComponentInParent<Seoul.Network.Game.LaneRangeZone>();
        if (laneZone != null)
        {
            _overlappingLaneRangeZones.Add(laneZone);
            // Penalty 모드 zone 진입 — 방향 무관(정면 충돌/측면 진입 모두) 감속 + 무적 + 깜빡임.
            // 스턴/무적 중이면 무시. 같은 zone에 머무르며 lane만 바꿔도 OnTriggerEnter는 한 번뿐이라 중복 적용 X.
            if (laneZone.Mode == Seoul.Network.Game.LaneRangeZone.BlockMode.Penalty
                && laneZone.IsActiveFor(this)
                && _currentState != StunState
                && _invincibilityTimer <= 0f)
            {
                ApplyLaneZonePenalty(laneZone);
            }
        }

        if (_currentState == StunState) return; // 스턴 상태 면역(무적) 유지

        // 아이템 관련 수정
        // [추가] 킥보드 및 택시 돌진(IsItemDashing) 중에는 일반 장애물 충돌 판정을 무시
        if (TryGetComponent<NetworkItemInventory>(out var inv) && inv.IsItemDashing) return;

        // Penalty zone에서 받은 무적 동안 KnockDown 장애물도 면역.
        if (_invincibilityTimer > 0f) return;

        if (col.TryGetComponent<ObstacleBase>(out var obstacle) && obstacle.KnockDownOnCollision)
        {
            TriggerFall();
            ApplyKnockback(transform.position - col.transform.position);
        }
    }

    private void OnTriggerExit(Collider col)
    {
        var holeExit = col.GetComponentInParent<Seoul.Network.Game.UndergroundHole>();
        if (holeExit != null) _hoveringHoles.Remove(holeExit);

        if (col.GetComponentInParent<Seoul.Network.Game.UndergroundWall>() != null)
            _underWallOverlap = Mathf.Max(0, _underWallOverlap - 1);

        var backWallExit = col.GetComponentInParent<Seoul.Network.Game.BackWall>();
        if (backWallExit != null) _overlappingBackWalls.Remove(backWallExit);

        var ramp = col.GetComponentInParent<Seoul.Network.Game.UndergroundExitRamp>();
        if (ramp != null && _currentExitRamp == ramp) _currentExitRamp = null;

        var laneZone = col.GetComponentInParent<Seoul.Network.Game.LaneRangeZone>();
        if (laneZone != null) _overlappingLaneRangeZones.Remove(laneZone);
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

    // Penalty zone 효과: 감속(ApplySlow 재사용) + 무적 타이머 + 깜빡임 코루틴.
    private void ApplyLaneZonePenalty(Seoul.Network.Game.LaneRangeZone zone)
    {
        ApplySlow(zone.PenaltySpeedRatio, zone.PenaltySlowDuration);
        _invincibilityTimer = zone.InvincibilityDuration;
        if (_blinkCoroutine != null) StopCoroutine(_blinkCoroutine);
        _blinkCoroutine = StartCoroutine(BlinkRoutine(zone.InvincibilityDuration, zone.BlinkInterval));
    }

    // 캐릭터 자식 Renderer들을 interval 주기로 on/off 토글. 끝나면 모두 enabled=true로 복원.
    private System.Collections.IEnumerator BlinkRoutine(float duration, float interval)
    {
        var renderers = GetComponentsInChildren<Renderer>(includeInactive: false);
        float t = 0f;
        bool visible = true;
        while (t < duration)
        {
            visible = !visible;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null) renderers[i].enabled = visible;
            }
            float wait = Mathf.Max(0.02f, interval);
            yield return new WaitForSeconds(wait);
            t += wait;
        }
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null) renderers[i].enabled = true;
        }
        _blinkCoroutine = null;
    }

    // ── 카메라 occlusion fade ───────────────────────────────
    // 카메라 → 플레이어 ray 사이를 가로막는 Renderer들의 material을 fade material로 swap (없으면 hide).
    // 더 이상 가리지 않으면 원본 material 복원. 로컬 owner만 수행 (AI/원격 플레이어는 카메라 없음).
    // N 프레임마다 한 번씩 검사 (occlusionCheckFrameInterval).
    private readonly System.Collections.Generic.Dictionary<Renderer, Material[]> _occlusionState
        = new System.Collections.Generic.Dictionary<Renderer, Material[]>();
    private readonly System.Collections.Generic.HashSet<Renderer> _occlusionCurrent
        = new System.Collections.Generic.HashSet<Renderer>();
    private static readonly System.Collections.Generic.List<Renderer> _occlusionScratch
        = new System.Collections.Generic.List<Renderer>();
    private RaycastHit[] _occlusionHitBuffer;
    private int _occlusionTick;

    private void UpdateCameraOcclusion()
    {
        // 로컬 owner만 (AI 봇/원격 플레이어는 카메라 없음).
        if (TryGetComponent<Unity.Netcode.NetworkObject>(out var no) && !no.IsOwner) return;

        if (++_occlusionTick < Mathf.Max(1, occlusionCheckFrameInterval)) return;
        _occlusionTick = 0;

        if (occlusionCamera == null) occlusionCamera = Camera.main;
        if (occlusionCamera == null) return;

        Vector3 camPos = occlusionCamera.transform.position;
        Vector3 playerPos = transform.position + Vector3.up * (_col.height * 0.5f);
        Vector3 delta = playerPos - camPos;
        float dist = delta.magnitude;
        if (dist < 0.01f) return;
        Vector3 dir = delta / dist;

        if (_occlusionHitBuffer == null || _occlusionHitBuffer.Length < 32) _occlusionHitBuffer = new RaycastHit[32];
        int hitCount = Physics.RaycastNonAlloc(camPos, dir, _occlusionHitBuffer, dist, occlusionMask, QueryTriggerInteraction.Collide);

        _occlusionCurrent.Clear();
        for (int i = 0; i < hitCount; i++)
        {
            var hitT = _occlusionHitBuffer[i].collider != null ? _occlusionHitBuffer[i].collider.transform : null;
            if (hitT == null) continue;
            if (hitT == transform || hitT.IsChildOf(transform)) continue;

            AddOcclusionRenderers(hitT);
        }

        // 더 이상 가리지 않는 것 복원.
        _occlusionScratch.Clear();
        foreach (var kv in _occlusionState)
        {
            if (!_occlusionCurrent.Contains(kv.Key)) _occlusionScratch.Add(kv.Key);
        }
        for (int i = 0; i < _occlusionScratch.Count; i++) RestoreOcclusion(_occlusionScratch[i]);
    }

    private void AddOcclusionRenderers(Transform hitT)
    {
        Transform t = hitT;
        while (t != null)
        {
            if (t == transform || t.IsChildOf(transform)) return;

            var rs = t.GetComponentsInChildren<Renderer>(includeInactive: false);
            bool foundAny = false;
            for (int i = 0; i < rs.Length; i++)
            {
                var r = rs[i];
                if (r == null) continue;
                if (r.transform == transform || r.transform.IsChildOf(transform)) continue;

                foundAny = true;
                _occlusionCurrent.Add(r);
                if (!_occlusionState.ContainsKey(r)) ApplyOcclusion(r);
            }

            if (foundAny) return;
            t = t.parent;
        }
    }

    // 원본 material을 instance로 복사한 뒤 transparent mode로 변환 + alpha 낮춤 → 텍스처/색은 그대로 유지하며 반투명화.
    // occlusionFadeMaterial이 명시적으로 설정돼 있으면 그 material로 swap (사용자 의도 존중).
    private void ApplyOcclusion(Renderer r)
    {
        var originals = r.sharedMaterials;
        _occlusionState[r] = originals;

        var faded = new Material[originals.Length];
        for (int i = 0; i < faded.Length; i++)
        {
            if (originals[i] == null) { faded[i] = null; continue; }
            if (occlusionFadeMaterial != null)
            {
                faded[i] = occlusionFadeMaterial;
            }
            else
            {
                // 원본을 복사해서 transparent로 변환 → 원본 텍스처/색 유지.
                var copy = new Material(originals[i]);
                ConfigureTransparent(copy, occlusionFadeAlpha);
                faded[i] = copy;
            }
        }
        r.sharedMaterials = faded;
    }

    private void RestoreOcclusion(Renderer r)
    {
        if (_occlusionState.TryGetValue(r, out var originals))
        {
            if (r != null)
            {
                // ApplyOcclusion에서 만든 임시 material instance 해제 (메모리 누수 방지).
                if (occlusionFadeMaterial == null)
                {
                    var current = r.sharedMaterials;
                    for (int i = 0; i < current.Length; i++)
                    {
                        // 원본 배열에 없는 것 = 우리가 만든 instance → 파괴.
                        bool isOriginal = false;
                        for (int j = 0; j < originals.Length; j++)
                        {
                            if (current[i] == originals[j]) { isOriginal = true; break; }
                        }
                        if (!isOriginal && current[i] != null) Destroy(current[i]);
                    }
                }
                r.sharedMaterials = originals;
            }
            _occlusionState.Remove(r);
        }
    }

    // 주어진 material을 in-place로 transparent 모드 + alpha 적용. URP Lit / Standard / 그 외(_Color, _BaseColor 직접 수정) 처리.
    private static void ConfigureTransparent(Material m, float alpha)
    {
        if (m == null || m.shader == null) return;
        string shaderName = m.shader.name;

        if (shaderName.Contains("Universal") || shaderName.Contains("URP"))
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
            m.renderQueue = 3000;
        }
        else if (shaderName == "Standard")
        {
            m.SetFloat("_Mode", 3f); // Transparent
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = 3000;
        }
        else
        {
            // 알 수 없는 shader — 알파만 적용 (Transparent shader라면 반영됨).
            m.renderQueue = 3000;
        }

        // 색상 알파 적용 (URP/Built-in 양쪽 모두 시도).
        if (m.HasProperty("_BaseColor"))
        {
            var c = m.GetColor("_BaseColor");
            c.a = alpha;
            m.SetColor("_BaseColor", c);
        }
        if (m.HasProperty("_Color"))
        {
            var c = m.GetColor("_Color");
            c.a = alpha;
            m.SetColor("_Color", c);
        }
    }

    private void OnDisable()
    {
        StageEventManager.OnForceLaneChangeRequested -= OnGimmickForceLaneChange;
        // 모든 가린 오브젝트 원복.
        _occlusionScratch.Clear();
        foreach (var kv in _occlusionState) _occlusionScratch.Add(kv.Key);
        for (int i = 0; i < _occlusionScratch.Count; i++) RestoreOcclusion(_occlusionScratch[i]);
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
