using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using Seoul.Network.Game;

public class MashQTE : BaseQTE
{
    [Header("Mash Settings")]
    [SerializeField] private int requiredMashCount = 10;
    [SerializeField] private BaseQTE.QteActionType actionType = BaseQTE.QteActionType.SubwayGetOn;
    [SerializeField] private GameObject targetVisualObject;

    [Header("QTE UI Elements")]
    [SerializeField] private GameObject qteUiCanvas;       
    [SerializeField] private Slider mashProgressSlider;     // 연타 게이지 바
    [SerializeField] private Slider timerSlider;            // 🌟 [추가] 남은 시간 게이지 바 (선택 사항)
    [SerializeField] private TextMeshProUGUI timerText;     // 🌟 남은 시간 텍스트
    [SerializeField] private TextMeshProUGUI guideText;     

    private int _currentMashCount = 0;

    protected override void OnQteStart()
    {
        _currentMashCount = 0;
        Debug.Log($"[{actionType}] J/K 연타 QTE 시작! 제한시간: {timeLimit}초, 목표 연타수: {requiredMashCount}");

        if (qteUiCanvas != null) qteUiCanvas.SetActive(true);
        
        // 연타 게이지 설정
        if (mashProgressSlider != null)
        {
            mashProgressSlider.minValue = 0;
            mashProgressSlider.maxValue = requiredMashCount;
            mashProgressSlider.value = 0;
        }

        // 🌟 타이머 게이지 초기 설정 (최대치를 부모의 timeLimit으로 설정)
        if (timerSlider != null)
        {
            timerSlider.minValue = 0;
            timerSlider.maxValue = timeLimit;
            timerSlider.value = timeLimit;
        }

        if (guideText != null)
        {
            guideText.text = "J / K 연타하세요!!!";
        }
    }

    protected override void OnQteUpdate()
    {
        // 🌟 부모 클래스(BaseQTE)의 _timer 변수를 실시간 매칭
        if (timerText != null)
        {
            // 소수점 첫째 자리까지 표시 (예: 2.5s)
            // 만약 시간이 마이너스로 내려가는 걸 방지하려면 Mathf.Max(0, _timer) 사용
            timerText.text = $"{Mathf.Max(0f, _timer):F1}s"; 
        }

        // 🌟 타이머 게이지 바 실시간 반영
        if (timerSlider != null)
        {
            timerSlider.value = _timer;
        }

        var kb = Keyboard.current;
        if (kb == null) return;

        // J 키 또는 K 키 입력 확인
        if (kb[Key.J].wasPressedThisFrame || kb[Key.K].wasPressedThisFrame)
        {
            Seoul.SoundManager.Instance.PlaySFX("Ui_button_click1");
            _currentMashCount++;
            Debug.Log($"[{actionType}] J/K 입력 감지! 현재 연타 횟수: {_currentMashCount} / {requiredMashCount}");

            if (mashProgressSlider != null)
            {
                mashProgressSlider.value = _currentMashCount;
            }

            if (_currentMashCount >= requiredMashCount)
            {
                HandleSuccess();
            }
        }
    }

    protected override BaseQTE.QteActionType GetActionType() => actionType;

    protected override void OnLocalSuccessVisual()
    {
        if (targetVisualObject != null) targetVisualObject.SetActive(false);
        HideQteUi();
    }

    // 🌟 부모의 ResetQteSession이 호출될 때(성공/실패/트리거 이탈 등 모든 종료 시점) UI를 끕니다.
    protected override void ResetQteSession()
    {
        base.ResetQteSession(); // 부모의 원본 초기화 로직 실행 필수!
        HideQteUi();
    }

    private void HideQteUi()
    {
        if (qteUiCanvas != null) qteUiCanvas.SetActive(false);
    }
}