using UnityEngine;
using System.Collections;

public enum ItemType { None, Coffee, Scooter }

public class BikeController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 15f;
    public float sprintMultiplier = 2f;
    public float laneChangeSpeed = 10f;
    public float jumpForce = 4f;
    public float padJumpForce = 6f;

    [Header("Lanes")]
    public float[] laneZPositions = { -5f, -3f, -1f, 1f, 3f, 5f };
    private int currentLaneIndex = 3;

    [Header("Physics Setup")]
    public LayerMask groundLayer;
    public bool isGrounded;

    [Header("Game State")]
    public int currentScore = 0;
    public int currentStage = 1;    // 디버깅용 스테이지 번호         

    [Header("Obstacle / Stun System")]
    public float stunDuration = 1.0f;
    public float speedRecoveryTime = 2.0f;
    public float puddleSlowRatio = 0.4f;
    private bool isStunned = false;
    private float speedModifier = 1.0f;

    [Header("Stamina System")]
    public float maxStamina = 100f;
    public float staminaDrain = 25f;
    public float staminaRecover = 15f;
    public float currentStamina;
    public bool isSprinting = false;

    [Header("Item System")]
    public ItemType currentInventory = ItemType.None;
    public float scooterSpeedMultiplier = 1.5f;
    public float scooterDuration = 3.0f;
    private bool isScooterActive = false;

    [Header("UI System")]
    public UIManager uiManager;

    private Rigidbody rb;
    private float targetXVelocity;
    private bool jumpRequested;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        currentStamina = maxStamina;
    }

    void Update()
    {
        CheckGround();
        HandleInput();
        HandleSpeedRecovery();
        ManageStamina();

        if (uiManager != null)
        {
            uiManager.UpdateUI(currentScore, currentStamina, currentInventory);
        }
    }

    void FixedUpdate()
    {
        ApplyMovement();
    }

    private void CheckGround()
    {
        isGrounded = Physics.Raycast(transform.position, Vector3.down, 1.1f, groundLayer);
    }

    private void HandleInput()
    {
        if (isStunned)
        {
            targetXVelocity = 0f;
            jumpRequested = false;
            isSprinting = false;
            return;
        }

        targetXVelocity = Input.GetAxisRaw("Vertical") * moveSpeed;

        if (Input.GetKeyDown(KeyCode.A) && currentLaneIndex < 5) currentLaneIndex++;
        if (Input.GetKeyDown(KeyCode.D) && currentLaneIndex > 0) currentLaneIndex--;

        if (Input.GetKeyDown(KeyCode.K) && isGrounded)
        {
            jumpRequested = true;
        }

        isSprinting = Input.GetKey(KeyCode.J) && currentStamina > 0f && targetXVelocity > 0f;

        if (Input.GetKeyDown(KeyCode.L) && currentInventory != ItemType.None)
        {
            UseItem();
        }
    }

    private void HandleSpeedRecovery()
    {
        if (!isStunned && speedModifier < 1.0f)
        {
            speedModifier += Time.deltaTime / speedRecoveryTime;
            speedModifier = Mathf.Clamp01(speedModifier);
        }
    }

    private void ManageStamina()
    {
        if (isSprinting)
        {
            currentStamina -= staminaDrain * Time.deltaTime;
        }
        else
        {
            currentStamina += staminaRecover * Time.deltaTime;
        }
        currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);
    }

    private bool autoJumpRequested = false;

    private void ApplyMovement()
    {
        float targetZ = laneZPositions[currentLaneIndex];
        float zVelocity = (targetZ - transform.position.z) * laneChangeSpeed;
        float finalBaseSpeed = isSprinting ? targetXVelocity * sprintMultiplier : targetXVelocity;

        if (isScooterActive && targetXVelocity > 0f) finalBaseSpeed *= scooterSpeedMultiplier;
        float finalXVelocity = finalBaseSpeed * speedModifier;

        float finalYVelocity = rb.linearVelocity.y;

        if (autoJumpRequested)
        {
            finalYVelocity = padJumpForce; // 점프대용
            autoJumpRequested = false;
        }
        else if (jumpRequested)
        {
            finalYVelocity = jumpForce;    // 일반 점프
            jumpRequested = false;
        }

        rb.linearVelocity = new Vector3(finalXVelocity, finalYVelocity, zVelocity);
    }

    private void UseItem()
    {
        switch (currentInventory)
        {
            case ItemType.Coffee:
                currentStamina = maxStamina;
                break;
            case ItemType.Scooter:
                StartCoroutine(ScooterRoutine());
                break;
        }
        currentInventory = ItemType.None;
    }

    private IEnumerator ScooterRoutine()
    {
        isScooterActive = true;
        yield return new WaitForSeconds(scooterDuration);
        isScooterActive = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("ScoreItem"))
        {
            currentScore += 10;
            Destroy(other.gameObject);
        }

        if (other.CompareTag("Obstacle") && !isStunned)
        {
            StartCoroutine(StunRoutine());
        }

        if (other.CompareTag("Slowdown") && !isStunned)
        {
            speedModifier = puddleSlowRatio;
        }

        if (other.CompareTag("ItemBox"))
        {
            Destroy(other.gameObject);
            if (currentInventory == ItemType.None)
            {
                currentInventory = (ItemType)Random.Range(1, 3);
            }
        }
        if (other.CompareTag("JumpPad"))
        {
            autoJumpRequested = true;
        }
        // 엔드포인트
        if (other.CompareTag("Goal"))
        {
            NextStageLevel();
        }
    }

    // 골 지점 도달시 맵 초기화
    private void NextStageLevel()
    {
        currentStage++;
        Debug.Log($"다음 스테이지: {currentStage}");

        transform.position = new Vector3(0f, 3f, laneZPositions[3]);
        currentLaneIndex = 3;
        rb.linearVelocity = Vector3.zero;

        LevelSpawner spawner = FindFirstObjectByType<LevelSpawner>();
        if (spawner != null)
        {
            spawner.ReconstructLevel();
        }
    }

    private IEnumerator StunRoutine()
    {
        isStunned = true;
        speedModifier = 0f;
        isSprinting = false;
        isScooterActive = false;
        yield return new WaitForSeconds(stunDuration);
        isStunned = false;
    }

    // 디버깅용 레이캐스트 시각화
    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.down * 1.1f);
    }

}