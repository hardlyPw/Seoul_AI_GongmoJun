using UnityEngine;

/// <summary>
/// PlayerController의 현재 상태를 읽어서, 거기에 맞는 애니메이션을 재생하는 "다리" 스크립트.
/// PlayerController가 붙어있는 같은 오브젝트(Player 루트)에 함께 붙입니다.
/// Animator는 자식 강아지 모델에 있는 것을 Inspector에서 연결해줍니다.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class PlayerAnimator : MonoBehaviour
{
    [Header("연결")]
    [Tooltip("자식 강아지 모델에 붙어있는 Animator를 여기에 드래그하세요. 비워두면 자식에서 자동으로 찾습니다.")]
    [SerializeField] private Animator animator;

    [Header("애니메이션 상태 이름")]
    [Tooltip("Animator에 넣은 각 상태(박스)의 이름과 똑같이 적어주세요. 대소문자까지 정확히.")]
    [SerializeField] private string idleState = "Idle";
    [SerializeField] private string runState = "Running";
    [SerializeField] private string fastRunState = "FastRun";
    [SerializeField] private string jumpState = "Jump";
    [Tooltip("넘어짐(Stun) 동작이 따로 없으면 비워두세요. 비어있으면 idle로 대체됩니다.")]
    [SerializeField] private string fallenState = "";

    [Header("전환 부드러움")]
    [Tooltip("동작이 바뀔 때 섞이는 시간(초). 0이면 즉시 전환.")]
    [SerializeField] private float crossFadeTime = 0.12f;

    private PlayerController _player;
    private string _currentAnim;

    private void Awake()
    {
        _player = GetComponent<PlayerController>();

        // Animator를 안 넣었으면 자식에서 자동으로 찾아본다.
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (animator == null)
            Debug.LogError("[PlayerAnimator] Animator를 찾지 못했습니다. 강아지 모델의 Animator를 Inspector에 연결해주세요.");
    }

    private void Update()
    {
        if (animator == null || _player == null) return;

        string target = DecideState();

        // 이미 그 동작 중이면 다시 재생하지 않는다 (애니메이션이 처음으로 튀는 것 방지).
        if (target == _currentAnim) return;

        _currentAnim = target;
        animator.CrossFade(target, crossFadeTime);
    }

    /// <summary>
    /// 우선순위대로 검사해서 지금 재생할 애니메이션 이름을 고른다.
    /// (넘어짐 > 점프 > 대시 > 달리기 > 가만히)
    /// </summary>
    private string DecideState()
    {
        if (_player.IsFallen)
            return string.IsNullOrEmpty(fallenState) ? idleState : fallenState;

        if (_player.IsAirborne)
            return jumpState;

        if (_player.IsDashing)
            return fastRunState;

        if (_player.IsSprinting)
            return runState;

        return idleState;
    }
}