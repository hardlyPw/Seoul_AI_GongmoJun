using UnityEngine;

// ── IDLE: 자동 전진(walk) ─────────────────────────────────
public class PlayerIdleState : IPlayerState
{
    public void EnterState(PlayerController player) { }

    public void UpdateState(PlayerController player)
    {
        if (player.Input is NullInputProvider) return;

        if (player.Input.GetSprint() && player.Stamina >= player.MinSprintStamina)
        {
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

        // 앞에 있는 사람에게 가로막힘 (X축 기준)
        if (other.transform.position.x > player.transform.position.x)
        {
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
        if (!player.Input.GetSprint() || player.Stamina <= 0f)
        {
            player.ChangeState(player.IdleState);
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

        if (other.transform.position.x > player.transform.position.x)
        {
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
        Debug.Log($"[Dash] 진입 — duration={_timer:F2}s, stamina={player.Stamina:F1}");
    }

    public void UpdateState(PlayerController player)
    {
        _timer -= Time.deltaTime;
        if (_timer <= 0f) player.ChangeState(player.IdleState);
    }

    public void FixedUpdateState(PlayerController player)
    {
        player.CalculateForwardVelocity(player.DashSpeed);
    }

    public void OnCollisionCheck(PlayerController player, Collider other)
    {
        if (!other.TryGetComponent<PlayerController>(out var otherPlayer)) return;
        if (otherPlayer.IsFallen) return;

        // dash 중에 닿은 다른 플레이어는 모두 추월(넘어뜨림).
        // x 비교 안 함 — dash 진입 시 본인이 가속하면서 곧장 상대보다 앞서버려 조건 false가 되는 케이스를 피함.
        Debug.Log($"[Dash] hit {otherPlayer.name} — myX={player.transform.position.x:F2}, otherX={other.transform.position.x:F2}");
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
        if (_timer <= 0f) player.ChangeState(player.IdleState);
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
