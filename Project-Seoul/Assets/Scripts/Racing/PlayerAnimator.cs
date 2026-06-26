using Unity.Netcode;
using UnityEngine;

// PlayerController의 현재 "상태"를 읽어 애니메이션을 재생하되,
// 멀티플레이어에서 다른 클라이언트에도 보이도록 상태를 네트워크 동기화한다.
//
//  - 소유자(IsOwner): PlayerController에서 상태를 계산해 재생하고, NetworkVariable에 기록.
//  - 리모트(비소유자): 동기화된 NetworkVariable 값을 읽어 같은 애니메이션을 재생.
//  - 스폰 전/비네트워크 테스트: 로컬 단독으로 동작.
//
// 플레이어별 모델 교체를 지원하기 위해, 활성 모델의 Animator를 SetAnimator로 갈아끼울 수 있다.
// NetworkBehaviour이므로 반드시 NetworkObject가 있는 Player 루트에 붙여야 한다.
[RequireComponent(typeof(PlayerController))]
public class PlayerAnimator : NetworkBehaviour
{
    [Header("연결")]
    [Tooltip("자식 모델의 Animator. 비우면 자식에서 자동으로 찾음. (모델 교체 시 런타임에 갱신됨)")]
    [SerializeField] private Animator animator;

    [Header("애니메이션 상태 이름 (Animator 박스 이름과 정확히 일치)")]
    [SerializeField] private string idleState = "Idle";
    [SerializeField] private string walkState = "Running";
    [SerializeField] private string runState = "RunningFast";
    [SerializeField] private string dashState = "RunningFast";
    [SerializeField] private string jumpState = "Jump";
    [SerializeField] private string airborneState = "Jump";
    [Tooltip("넘어짐 동작이 없으면 비워두기 — idle로 대체됨.")]
    [SerializeField] private string fallenState = "";

    [Header("전환 부드러움")]
    [SerializeField] private float crossFadeTime = 0.12f;

    private enum AnimKind { Idle = 0, Walk = 1, Run = 2, Dash = 3, Jump = 4, Airborne = 5, Fallen = 6 }

    private readonly NetworkVariable<int> _netState = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private PlayerController _player;
    private string _currentAnim;

    private void Awake()
    {
        _player = GetComponent<PlayerController>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    // 플레이어별 모델 교체 시, 활성 모델의 Animator로 갈아끼운다. (NetworkPlayer가 호출)
    public void SetAnimator(Animator a)
    {
        animator = a;
        _currentAnim = null; // 다음 프레임에 현재 상태를 다시 재생하도록 초기화
    }

    private void Update()
    {
        if (animator == null || _player == null) return;

        if (!IsSpawned || IsOwner) {
            AnimKind kind = DecideKind();
            PlayByKind(kind);

            if (IsSpawned && IsOwner && _netState.Value != (int)kind)
                _netState.Value = (int)kind;

            return;
        }

        PlayByKind((AnimKind)_netState.Value);
    }

    private AnimKind DecideKind()
    {
        if (_player.IsFallen) return AnimKind.Fallen;
        if (_player.IsJumping) return AnimKind.Jump;
        if (_player.IsAirborne) return AnimKind.Airborne;
        if (_player.IsDashing) return AnimKind.Dash;
        if (_player.IsSprinting) return AnimKind.Run;
        if (_player.IsWalking) return AnimKind.Walk;
        return AnimKind.Idle;
    }

    private void PlayByKind(AnimKind kind)
    {
        string target = NameFor(kind);
        if (string.IsNullOrEmpty(target) || target == _currentAnim) return;
        _currentAnim = target;
        animator.CrossFade(target, crossFadeTime);
    }

    private string NameFor(AnimKind kind)
    {
        switch (kind) {
            case AnimKind.Fallen: return string.IsNullOrEmpty(fallenState) ? idleState : fallenState;
            case AnimKind.Jump: return jumpState;
            case AnimKind.Airborne: return airborneState;
            case AnimKind.Dash: return dashState;
            case AnimKind.Run: return runState;
            case AnimKind.Walk: return walkState;
            default: return idleState;
        }
    }
}