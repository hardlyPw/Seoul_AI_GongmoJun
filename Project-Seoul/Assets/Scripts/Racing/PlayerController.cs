using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float walkSpeed    = 5f;
    [SerializeField] private float sprintSpeed  = 10f;
    [SerializeField] private float acceleration = 20f;
    [SerializeField] private float deceleration = 15f;

    [Header("Stamina")]
    [SerializeField] private float maxStamina       = 100f;
    [SerializeField] private float sprintDrainRate  = 20f;
    [SerializeField] private float staminaRegenRate = 10f;
    [SerializeField] private float minSprintStamina = 10f;

    [Header("Jump / Gravity")]
    [SerializeField] private float     jumpForce           = 8f;
    [SerializeField] private float     gravity             = 25f;
    [SerializeField] private float     maxFallSpeed        = 30f;
    [SerializeField] private float     groundCheckDistance = 0.3f;
    [SerializeField] private LayerMask groundLayer = ~0;
    [SerializeField] private bool      debugGround = false;

    [Header("Lane (Z축)")]
    [SerializeField] private int   startLane          = 3;
    [SerializeField] private float laneSnapSpeed      = 8f;
    [SerializeField] private float laneChangeCooldown = 0.3f;

    [Header("Fallen")]
    [SerializeField] private float fallenDuration = 1.2f;
    [SerializeField] private float recoveryTime   = 0.8f;
    [SerializeField] private float knockbackForce = 6f;

    [Header("Player Collision")]
    [SerializeField] private float playerCheckRadius = 0.6f;
    [SerializeField] private LayerMask playerLayer;

    public event Action OnItemUse;
    public event Action OnInteract;

    private Rigidbody       _rb;
    private CapsuleCollider _col;
    private IInputProvider  _input;

    private Vector3 _velocity;

    private float _stamina;
    private bool  _isSprinting;
    private bool  _isGrounded;

    private int   _currentLane;
    private float _laneChangeCooldownTimer;

    private bool  _isFallen;
    private float _fallenTimer;
    private float _recoveryTimer;
    private float _recoverySpeedMult = 1f;
    private float _externalSpeedMult = 1f;

    // 감속 코루틴 추가했습니다
    private Coroutine _slowCoroutine;

    public float Stamina     => _stamina;
    public float MaxStamina  => maxStamina;
    public bool  IsSprinting => _isSprinting;
    public bool  IsFallen    => _isFallen;
    public int   CurrentLane => _currentLane;

    public void Initialize(IInputProvider inputProvider) => _input = inputProvider;

    private void Awake()
    {
        _rb  = GetComponent<Rigidbody>();
        _col = GetComponent<CapsuleCollider>();

        _rb.isKinematic = true;
        _rb.useGravity  = false;
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
            return;
        }

        _currentLane = FindNearestLane(transform.position.z);

        var pos = transform.position;
        pos.z              = LaneManager.Instance.GetLaneZ(_currentLane);
        transform.position = pos;
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

    private void Update()
    {
        if (_input == null) return;
        CheckGrounded();
        HandleStamina();
        HandleLaneChange();
        HandleJumpInput();
        HandleItemAndInteract();
        UpdateFallenState();
    }

    private void FixedUpdate()
    {
        if (_input == null) return;

        ApplyGravity();
        HandleMovement();
        HandleLaneSnap();
        ApplyVelocity();
        CheckPlayerCollision();
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

    // ── X축 이동 (달리기) ──────────────────────────────────

    private void HandleMovement()
    {
        if (_isFallen)
        {
            _velocity.x = Mathf.MoveTowards(_velocity.x, 0f, deceleration * Time.fixedDeltaTime);
            return;
        }

        float h     = _input.GetHorizontal();
        float speed = (_isSprinting ? sprintSpeed : walkSpeed) * _recoverySpeedMult * _externalSpeedMult;

        _velocity.x = Mathf.Abs(h) > 0.01f
            ? Mathf.MoveTowards(_velocity.x, h * speed, acceleration * Time.fixedDeltaTime)
            : Mathf.MoveTowards(_velocity.x, 0f,        deceleration * Time.fixedDeltaTime);
    }

    // ── Z축 스냅 (라인 이동) ──────────────────────────────

    private void HandleLaneSnap()
    {
        if (LaneManager.Instance == null) { _velocity.z = 0f; return; }

        float targetZ  = LaneManager.Instance.GetLaneZ(_currentLane);
        float currentZ = _rb.position.z;
        if (Mathf.Abs(currentZ - targetZ) < 0.001f)
        {
            _velocity.z = 0f;
            return;
        }
        float dir = Mathf.Sign(targetZ - currentZ);
        _velocity.z = dir * laneSnapSpeed;
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
        if (_isFallen) return;
        if (_input.GetJumpDown() && _isGrounded)
        {
            _velocity.y = jumpForce;
            _isGrounded = false;
        }
    }

    // ── 라인 변경 ─────────────────────────────────────────

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

    // ── 스태미나 ──────────────────────────────────────────

    private void HandleStamina()
    {
        if (_input.GetSprint() && _stamina >= minSprintStamina && !_isFallen)
        {
            _isSprinting = true;
            _stamina     = Mathf.Max(0f, _stamina - sprintDrainRate * Time.deltaTime);
            if (_stamina <= 0f) _isSprinting = false;
        }
        else
        {
            _isSprinting = false;
            _stamina     = Mathf.Min(maxStamina, _stamina + staminaRegenRate * Time.deltaTime);
        }
    }

    // ── 넘어짐 ────────────────────────────────────────────

    public void TriggerFall()
    {
        if (_isFallen) return;
        _isFallen    = true;
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
                _isFallen      = false;
                _recoveryTimer = recoveryTime;
            }
            return;
        }
        if (_recoveryTimer > 0f)
        {
            _recoveryTimer    -= Time.deltaTime;
            _recoverySpeedMult = Mathf.Clamp01(1f - _recoveryTimer / recoveryTime);
        }
        else
        {
            _recoverySpeedMult = 1f;
        }
    }

    // ── 아이템 / 상호작용 ─────────────────────────────────

    private void HandleItemAndInteract()
    {
        if (_input.GetItemUse())      OnItemUse?.Invoke();
        if (_input.GetInteractDown()) OnInteract?.Invoke();
    }

    // ── 공개 메서드 ───────────────────────────────────────

    public void SetSpeedMultiplier(float mult) => _externalSpeedMult = mult;
    public void RecoverStamina(float amount)   => _stamina = Mathf.Min(maxStamina, _stamina + amount);

    // ── 플레이어 간 충돌 (OverlapSphere) ─────────────────

    private void CheckPlayerCollision()
    {
        if (_isFallen) return;

        Vector3 center = transform.position + Vector3.up * (_col.height * 0.5f);
        var hits = Physics.OverlapSphere(center, playerCheckRadius, playerLayer);

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].transform == transform) continue;
            if (!hits[i].TryGetComponent<PlayerController>(out var other)) continue;
            if (other.IsSprinting) TriggerFall();
        }
    }

    // ── 장애물 충돌 (Trigger 권장) ────────────────────────

    private void OnTriggerEnter(Collider col)
    {
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
}
