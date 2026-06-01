using UnityEngine;

// ── 1. 일반 상태 (IDLE) ──────────────────────────────────
public class PlayerIdleState : IPlayerState
{
    public void EnterState(PlayerController player) { }

    public void UpdateState(PlayerController player)
    {
        if (player.Input is NullInputProvider) return;

        // 일반 상태에서 J 키를 누르고 있으면 RUN 상태로 전환
        if (player.Input.GetSprint() && player.Stamina >= player.MinSprintStamina)
        {
            player.ChangeState(player.RunState);
        }
    }

    public void FixedUpdateState(PlayerController player)
    {
        if (player.Input is NullInputProvider)
        {
            player.CalculateForwardVelocity(0f);
        }
        else
        {
            player.CalculateForwardVelocity(player.WalkSpeed);
        }
    }

    public void OnCollisionCheck(PlayerController player, UnityEngine.Collider other)
    {
        if (other.TryGetComponent<PlayerController>(out var otherPlayer) && !otherPlayer.IsFallen)
        {
            // 앞 사람에게 가로막힘 (X축 기준 상대방이 앞에 있을 때 전진 속도 차단)
            if (other.transform.position.x > player.transform.position.x)
            {
                player.SetVelocityX(0f);
            }
        }
    }

    public void ExitState(PlayerController player) { }
}

// ── 2. 달리기 상태 (RUN) ──────────────────────────────────
public class PlayerRunState : IPlayerState
{
    public void EnterState(PlayerController player) { }

    public void UpdateState(PlayerController player)
    {
        // 키를 떼거나 스태미나가 바닥나면 IDLE 복귀
        if (!player.Input.GetSprint() || player.Stamina <= 0f)
        {
            player.ChangeState(player.IdleState);
            return;
        }

        // 스태미나 지속 소모
        player.ConsumeStamina(player.SprintDrainRate * UnityEngine.Time.deltaTime);
    }

    public void FixedUpdateState(PlayerController player)
    {
        // 달리기 속도 (sprintSpeed)
        player.CalculateForwardVelocity(player.SprintSpeed);
    }

    public void OnCollisionCheck(PlayerController player, UnityEngine.Collider other)
    {
        // RUN 상태에서도 추월은 불가능하므로 IDLE과 동일하게 가로막힘 처리
        if (other.TryGetComponent<PlayerController>(out var otherPlayer) && !otherPlayer.IsFallen)
        {
            if (other.transform.position.x > player.transform.position.x)
            {
                player.SetVelocityX(0f);
            }
        }
    }

    public void ExitState(PlayerController player) { }
}

// ── 3. 대시 상태 (DASH) ──────────────────────────────────
public class PlayerDashState : IPlayerState
{
    private float _timer;

    public void EnterState(PlayerController player)
    {
        _timer = player.DashDuration;
        player.ConsumeStamina(player.DashStaminaCost); // 즉시 스태미나 소모
        UnityEngine.Debug.Log("⚡ 광속 질주 시작 (추월 가능)");
    }

    public void UpdateState(PlayerController player)
    {
        _timer -= UnityEngine.Time.deltaTime;
        if (_timer <= 0f)
        {
            player.ChangeState(player.IdleState);
        }
    }

    public void FixedUpdateState(PlayerController player)
    {
        // 폭발적인 대시 속도 (dashSpeed)
        player.CalculateForwardVelocity(player.DashSpeed);
    }

    public void OnCollisionCheck(PlayerController player, UnityEngine.Collider other)
    {
        // ⚡ DASH 상태에서는 앞에 있는 상대를 들이받아 넘어뜨리고 추월함!
        if (other.TryGetComponent<PlayerController>(out var otherPlayer))
        {
            if (other.transform.position.x > player.transform.position.x && !otherPlayer.IsFallen)
            {
                otherPlayer.TriggerFall();
            }
        }
    }

    public void ExitState(PlayerController player) { }
}

// ── 4. 넘어짐 상태 (STUN) ──────────────────────────────────
public class PlayerStunState : IPlayerState
{
    private float _timer;

    public void EnterState(PlayerController player)
    {
        _timer = player.FallenDuration;
        player.SetVelocityX(0f); //  멈춤
    }

    public void UpdateState(PlayerController player)
    {
        _timer -= UnityEngine.Time.deltaTime;
        if (_timer <= 0f)
        {
            player.ChangeState(player.IdleState); // 기상 후 일반 상태로 복귀
        }
    }

    public void FixedUpdateState(PlayerController player)
    {
        // 스턴 상태일 때는 전진 가속 연산을 하지 않고 감속 마찰력 등만 적용되도록 유도
        player.CalculateForwardVelocity(0f);
    }

    public void OnCollisionCheck(PlayerController player, UnityEngine.Collider other)
    {
        // 스턴 상태(무적 타임)일 때는 추가 장애물 충돌이나 플레이어 충돌 모두 면역 처리
    }

    public void ExitState(PlayerController player)
    {
        player.StartRecoveryWindow(); // 일어서면서 서서히 속도가 복구되는 타이머 시작
    }
}