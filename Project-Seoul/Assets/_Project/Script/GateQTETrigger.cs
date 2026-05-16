using UnityEngine;
using UnityEngine.UI;

public class GateQTETrigger : MonoBehaviour
{
    [Header("UI 연결")]
    [Tooltip("화면 중앙에 'Press SPACE!'를 띄울 Text 컴포넌트")]
    public Text qteText;

    [Header("QTE 상세 세팅")]
    [Tooltip("QTE 입력 제한 시간 (초 단위)")]
    public float qteTimeLimit = 1.5f;
    [Tooltip("QTE 발동 시 게임이 얼마나 느려질지 결정 (0에 가까울수록 슬로우모션)")]
    public float slowMotionScale = 0.15f;

    private bool isQteActive = false;
    private float qteTimer = 0f;
    private PlayerController activePlayer;
    private bool isQteProcessed = false; // 성공/실패가 이미 결정되었는지 체크

    private void Start()
    {
        // 시작할 때는 텍스트를 숨겨둡니다.
        if (qteText != null) qteText.gameObject.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        // 이미 처리 중이거나 플레이어가 아니면 무시
        if (isQteActive || isQteProcessed) return;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player != null)
        {
            activePlayer = player;
            StartQTE();
        }
    }

    private void StartQTE()
    {
        isQteActive = true;
        isQteProcessed = false;
        qteTimer = 0f;

        // 1화면 중앙에 예고 텍스트 띄우기
        if (qteText != null)
        {
            qteText.gameObject.SetActive(true);
            qteText.text = "CARD TAG!!\n[ Press SPACE ]";
            qteText.color = Color.yellow;
        }

        // 게임 속도를 느리게 만들어 플레이어에게 반응할 시간 주기
        Time.timeScale = slowMotionScale;
        // 슬로우 모션 상태에서도 일정한 고정 프레임 연산 속도를 유지하기 위해 타임스텝을 보정.
        Time.fixedDeltaTime = 0.02f * Time.timeScale; 

        Debug.Log("개찰구 진입! QTE 시작!");
    }

    private void Update()
    {
        if (!isQteActive) return;

        // 슬로우 모션 중에도 타이머는 실제 흐르는 현실 시간(UnscaledDeltaTime) 기준으로 똑바로 가야 합니다.
        qteTimer += Time.unscaledDeltaTime;

        // 제한 시간 동안 플레이어가 스페이스바를 눌렀는지 실시간 체크
        if (Input.GetKeyDown(KeyCode.Space))
        {
            SuccessQTE();
            return;
        }

        // 제한 시간을 넘겼다면 실패 처리
        if (qteTimer >= qteTimeLimit)
        {
            FailQTE();
        }
    }

    // 타이밍 맞춰 스페이스바를 누른 경우
    private void SuccessQTE()
    {
        EndQTE();
        Debug.Log("삑! 개찰구 통과");

        if (qteText != null)
        {
            qteText.text = "SUCCESS!";
            qteText.color = Color.green;
            // 0.5초 뒤 텍스트를 완전히 끄는 예약 함수 호출
            Invoke("HideText", 0.5f);
        }

        // 통과했으므로 개찰구 벽의 Collider를 비활성화하여 통과시켜 줍니다.
        Collider gateCollider = GetComponent<Collider>();
        if (gateCollider != null) gateCollider.isTrigger = true; // 통과 가능 상태 유지
    }

    // 제한 시간 내에 누르지 못한 경우
    private void FailQTE()
    {
        EndQTE();
        Debug.Log("카드가 찍히지 않았습니다");

        if (qteText != null)
        {
            qteText.text = "FAIL... [BLOCKED]";
            qteText.color = Color.red;
        }

        // [프로토타입 정지 연출] 플레이어의 속도를 강제로 0으로 만들고 뒤로 안 가도록 제어
        if (activePlayer != null)
        {
            activePlayer.moveSpeed = 0f; // 속도를 0으로 만들어 쾅 부딪혀 멈춘 연출
        }

        // 개찰구를 물리적인 벽으로 바꾸어 더 이상 못 지나가게 막아버립니다.
        Collider gateCollider = GetComponent<Collider>();
        if (gateCollider != null) gateCollider.isTrigger = false; 
    }

    // QTE가 종료될 때 공통으로 시간에 관련된 시스템을 정상 복구하는 함수
    private void EndQTE()
    {
        isQteActive = false;
        isQteProcessed = true;

        // 원래 정상 속도로 게임을 복구시킵니다.
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
    }

    private void HideText()
    {
        if (qteText != null) qteText.gameObject.SetActive(false);
    }
}