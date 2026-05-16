using UnityEngine;
using System.Collections; // 코루틴

public class PlayerController : MonoBehaviour
{
    [Header("Manual Movement (A/D)")]
    public float moveSpeed = 7f;
    private float currentPenaltyMultiplier = 2f;

    [Header("Fixed Lane Movement (W/S)")]
    public int totalLanes = 6;              
    public float laneSpacing = 1.5f;        
    public float baseLaneX = -3.75f;        
    public float laneChangeSpeed = 15f;     
    
    private int currentLane = 2;            

    [Header("Actions")]
    public float dashSpeedMultiplier = 1.8f; 
    public float jumpForce = 8f;            
    
    private bool isDashing = false;
    private bool isJumping = false;
    private float currentYVelocity = 0f;    
    private float targetYPosition;          

    private void Start()
    {
        targetYPosition = transform.position.y;
        
        Vector3 startPos = transform.position;
        startPos.x = GetTargetXPosition(currentLane);
        transform.position = startPos;
    }

    private void Update()
    {
        HandleInput();
        MovePlayer();
    }

    private void HandleInput()
    {
        // 1. 고정 라인 변경 입력 (W / S)
        if (Input.GetKeyDown(KeyCode.W))
        {
            if (currentLane > LaneManager.Instance.minAccessibleLane) currentLane--;
        }
        if (Input.GetKeyDown(KeyCode.S))
        {
            if (currentLane < LaneManager.Instance.maxAccessibleLane) currentLane++;
        }

        // 2. 대시 입력 (J 키)
        if (Input.GetKeyDown(KeyCode.J)) isDashing = true;
        if (Input.GetKeyUp(KeyCode.J)) isDashing = false;

        // 3. 점프 입력 (K 키)
        if (Input.GetKeyDown(KeyCode.K) && !isJumping)
        {
            isJumping = true;
            currentYVelocity = jumpForce;
        }
    }

    // 외부 장애물(Obstacle)이 플레이어를 감속시킬 때 호출하는 함수
    public void ApplySpeedPenalty(float multiplier, float duration)
    {
        // 이미 다른 감속 코루틴이 돌고 있다면 겹치지 않게 중지
        StopAllCoroutines();
        // 새롭게 감속 및 타이머 코루틴을 시작
        StartCoroutine(SpeedPenaltyCoroutine(multiplier, duration));
    }

    // 일정 시간 동안만 속도를 깎았다가 돌려놓는 타이머 코루틴
    private IEnumerator SpeedPenaltyCoroutine(float multiplier, float duration)
    {
        currentPenaltyMultiplier = multiplier; // 속도 반토막 내기

        // 지정된 초만큼 대기
        yield return new WaitForSeconds(duration);

        // 페널티 시간이 끝나면 부드럽게 복구하기 위해 서서히 1f로 되돌립니다.
        while (currentPenaltyMultiplier < 1f)
        {
            currentPenaltyMultiplier += Time.deltaTime * 2f; // 복구 속도 조절
            yield return null;
        }

        currentPenaltyMultiplier = 1f; // 완벽히 정상 속도로 복구
        Debug.Log("? 정상 속도로 회복되었습니다!");
    }

    public void PushPlayerRandomly(int direction)
    {
        int nextLane = currentLane + direction;

        if (nextLane < LaneManager.Instance.minAccessibleLane) nextLane = LaneManager.Instance.minAccessibleLane + 1;
        else if (nextLane >= LaneManager.Instance.maxAccessibleLane) nextLane = LaneManager.Instance.maxAccessibleLane - 1;

        currentLane = Mathf.Clamp(nextLane, LaneManager.Instance.minAccessibleLane, LaneManager.Instance.maxAccessibleLane);
    }

    private void MovePlayer()
    {
        // --- Z축 이동 (A/D) ---
        float inputZ = Input.GetAxis("Horizontal");
        
        // 대시 배율뿐만 아니라 장애물 페널티 배율도 함께 곱해서 최종 속도를 계산합니다.
        float baseSpeed = moveSpeed * (isDashing ? dashSpeedMultiplier : 1f);
        float finalSpeed = baseSpeed * currentPenaltyMultiplier; 
        
        float moveZ = inputZ * finalSpeed * Time.deltaTime;

        // --- X축 이동 (Lerp) ---
        float targetX = GetTargetXPosition(currentLane);
        float nextX = Mathf.Lerp(transform.position.x, targetX, Time.deltaTime * laneChangeSpeed);

        // --- Y축 이동 (점프) ---
        float nextY = transform.position.y;
        if (isJumping)
        {
            currentYVelocity += Physics.gravity.y * Time.deltaTime;
            nextY += currentYVelocity * Time.deltaTime;

            if (nextY <= targetYPosition)
            {
                nextY = targetYPosition;
                isJumping = false;
                currentYVelocity = 0f;
            }
        }

        transform.position = new Vector3(nextX, nextY, transform.position.z + moveZ);
    }

    private float GetTargetXPosition(int laneIndex)
    {
        return baseLaneX + (laneIndex * laneSpacing);
    }
}