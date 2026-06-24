using UnityEngine;

// PlayerController의 현재 "상태"를 읽어서 거기에 맞는 애니메이션을 재생하는 다리 스크립트.
// 상태 기반(state-machine) 방식 — 속도가 아니라 FSM 상태로 판단한다.
// PlayerController가 붙은 같은 오브젝트(Player 루트)에 함께 붙이고,
// 자식 강아지의 Animator를 Inspector에서 연결한다.
[RequireComponent(typeof(PlayerController))]
public class PlayerAnimator : MonoBehaviour
{
    [Header("연결")]
    [Tooltip("자식 강아지 모델의 Animator를 드래그. 비워두면 자식에서 자동으로 찾음.")]
    [SerializeField] private Animator animator;

    [Header("애니메이션 상태 이름 (Animator 박스 이름과 정확히 일치)")]
    [SerializeField] private string idleState = "Idle";        // 출발선 대기
    [SerializeField] private string walkState = "Running";     // 평소 자동 전진
    [SerializeField] private string runState = "RunningFast"; // J 홀드 스프린트
    [SerializeField] private string dashState = "RunningFast"; // 대시
    [SerializeField] private string jumpState = "Jump";        // 점프키 점프
    [SerializeField] private string airborneState = "Jump";        // 점프대 묘기(공중)
    [Tooltip("넘어짐 동작이 없으면 비워두기 — idle로 대체됨.")]
    [SerializeField] private string fallenState = "";

    [Header("전환 부드러움")]
    [Tooltip("동작 전환 시 섞이는 시간(초). 0이면 즉시.")]
    [SerializeField] private float crossFadeTime = 0.12f;

    private PlayerController _player;
    private string _currentAnim;

    private void Awake()
    {
        _player = GetComponent<PlayerController>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator == null)
            Debug.LogError("[PlayerAnimator] Animator를 찾지 못했습니다. 강아지 모델의 Animator를 연결하세요.");
    }

    private void Update()
    {
        if (animator == null || _player == null) return;

        string target = DecideState();
        if (string.IsNullOrEmpty(target) || target == _currentAnim) return;

        _currentAnim = target;
        animator.CrossFade(target, crossFadeTime);
    }

    // 우선순위: 넘어짐 > 점프키점프 > 점프대공중 > 대시 > 스프린트 > 걷기 > 대기
    private string DecideState()
    {
        if (_player.IsFallen) return string.IsNullOrEmpty(fallenState) ? idleState : fallenState;
        if (_player.IsJumping) return jumpState;
        if (_player.IsAirborne) return airborneState;
        if (_player.IsDashing) return dashState;
        if (_player.IsSprinting) return runState;
        if (_player.IsWalking) return walkState;
        return idleState;
    }
}