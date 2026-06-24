using UnityEngine;

// ── IDLE: 출발선 대기 (게임 시작 전, 진짜 정지) ─────────────
// 이동이 잠겨있는 동안 머무는 상태. 잠금이 풀려 Update가 처음 도는 순간
// (= 게임 시작) WalkState로 전환된다.
public class PlayerIdleState : IPlayerState
{
    public void EnterState(PlayerController player)
    {
        player.SetVelocityX(0f);
    }

    public void UpdateState(PlayerController player)
    {
        // 네트워크 비-소유 플레이어는 입력 기반 전환을 하지 않음
        if (player.Input is NullInputProvider) return;

        // 이 시점 = 이동 잠금이 풀린 시점. 출발선 대기 → 자동 전진.
        player.ChangeState(player.WalkState);
    }

    public void FixedUpdateState(PlayerController player)
    {
        player.CalculateForwardVelocity(0f); // 대기 중엔 전진 안 함
    }

    public void OnCollisionCheck(PlayerController player, Collider other) { }

    public void ExitState(PlayerController player) { }
}

// ── WALK: 자동 전진 (기존 Idle 로직을 그대로 이어받음) ───────
public class PlayerWalkState : IPlayerState
{
    public void EnterState(PlayerController player) { }

    public void UpdateState(PlayerController player)
    {
        if (player.Input is NullInputProvider) return;

        if (player.Input.GetSprint() && player.Stamina >= player.MinSprintStamina) {
            player.ChangeState(player.RunState);
        }
    }

    public void FixedUpdateState(PlayerController player)
    {
        float target = (player.Input is NullInputProvider) ? 0f : player.WalkSpeed;
        player.CalculateForwardVelocity(target);
    }

    public void OnCollisionCheck(PlayerController player, Collider other)
    {
        if (!other.TryGetComponent<PlayerController>(out var otherPlayer)) return;
        if (otherPlayer.IsFallen) return;
        if (!player.IsInSameLane(other)) return;

        // 앞에 있는 사람에게 가로막힘 (X축 기준)
        if (other.transform.position.x > player.transform.position.x) {
            player.SetVelocityX(0f);
        }
    }

    public void ExitState(PlayerController player) { }
}

// ── RUN: J 홀드 sprint ────────────────────────────────────
public class PlayerRunState : IPlayerState
{
    public void EnterState(PlayerController player) { }

    public void UpdateState(PlayerController player)
    {
        if (!player.Input.GetSprint() || player.Stamina <= 0f) {
            player.ChangeState(player.WalkState); // 스프린트 해제 → 걷기로 복귀
            return;
        }
        player.ConsumeStamina(player.SprintDrainRate * Time.deltaTime);
    }

    public void FixedUpdateState(PlayerController player)
    {
        player.CalculateForwardVelocity(player.SprintSpeed);
    }

    public void OnCollisionCheck(PlayerController player, Collider other)
    {
        if (!other.TryGetComponent<PlayerController>(out var otherPlayer)) return;
        if (otherPlayer.IsFallen) return;
        if (!player.IsInSameLane(other)) return;

        if (other.transform.position.x > player.transform.position.x) {
            player.SetVelocityX(0f);
        }
    }

    public void ExitState(PlayerController player) { }
}

// ── DASH: 시간 기반 광속, 앞 사람 추월(넘어뜨림) ──────────
public class PlayerDashState : IPlayerState
{
    private float _timer;

    public void EnterState(PlayerController player)
    {
        _timer = player.DashDuration;
        player.ConsumeStamina(player.DashStaminaCost);
    }

    public void UpdateState(PlayerController player)
    {
        _timer -= Time.deltaTime;
        if (_timer <= 0f) player.ChangeState(player.WalkState); // 대시 종료 → 걷기로 복귀
    }

    public void FixedUpdateState(PlayerController player)
    {
        player.CalculateForwardVelocity(player.DashSpeed);
    }

    public void OnCollisionCheck(PlayerController player, Collider other)
    {
        if (!other.TryGetComponent<PlayerController>(out var otherPlayer)) return;
        if (otherPlayer.IsFallen) return;
        if (!player.IsInSameLane(other)) return;

        otherPlayer.TriggerFall();
    }

    public void ExitState(PlayerController player) { }
}

// ── STUN: 넘어짐, 시간 기반 ───────────────────────────────
public class PlayerStunState : IPlayerState
{
    private float _timer;

    public void EnterState(PlayerController player)
    {
        _timer = player.FallenDuration;
        player.SetVelocityX(0f);
    }

    public void UpdateState(PlayerController player)
    {
        _timer -= Time.deltaTime;
        if (_timer <= 0f) player.ChangeState(player.WalkState); // 회복 → 걷기로 복귀
    }

    public void FixedUpdateState(PlayerController player)
    {
        player.CalculateForwardVelocity(0f);
    }

    public void OnCollisionCheck(PlayerController player, Collider other) { }

    public void ExitState(PlayerController player)
    {
        player.StartRecoveryWindow();
    }
}

// ── JUMP: 점프키 점프 (애니메이션은 Airborne과 동일한 Jump 클립) ──
// velocity.y는 PlayerController.HandleJumpInput에서 이미 설정됨.
// 중력도 PlayerController.ApplyGravity가 처리하므로, 여기선 전진 속도 유지 + 착지 판정만.
public class PlayerJumpState : IPlayerState
{
    private float _entrySpeed;
    private bool _leftGround;

    public void EnterState(PlayerController player)
    {
        _entrySpeed = Mathf.Max(player.CurrentSpeedX, 0f); // 점프 직전 전진 속도 유지
        _leftGround = false;
    }

    public void UpdateState(PlayerController player)
    {
        // 한 번 공중에 뜬 뒤에야 착지 판정 시작 (점프 직후 즉시 종료되는 것 방지)
        if (!player.IsGrounded) _leftGround = true;

        if (_leftGround && player.IsGrounded && player.VerticalSpeed <= 0f) {
            player.ChangeState(player.WalkState); // 착지 → 걷기로 복귀
        }
    }

    public void FixedUpdateState(PlayerController player)
    {
        player.CalculateForwardVelocity(_entrySpeed);
    }

    public void OnCollisionCheck(PlayerController player, Collider other)
    {
        if (!other.TryGetComponent<PlayerController>(out var otherPlayer)) return;
        if (otherPlayer.IsFallen) return;
        if (!player.IsInSameLane(other)) return;

        if (other.transform.position.x > player.transform.position.x) {
            player.SetVelocityX(0f);
        }
    }

    public void ExitState(PlayerController player) { }
}