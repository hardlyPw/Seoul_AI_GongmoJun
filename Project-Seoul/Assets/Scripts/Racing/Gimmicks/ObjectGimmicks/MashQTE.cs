using UnityEngine;
using UnityEngine.InputSystem;
using Seoul.Network.Game;

public class MashQTE : BaseQTE
{
    [Header("Mash Settings")]
    [SerializeField] private int requiredMashCount = 10;
    [SerializeField] private BaseQTE.QteActionType actionType = BaseQTE.QteActionType.SubwayGetOn;
    [SerializeField] private GameObject targetVisualObject;

    private int _currentMashCount = 0;

    protected override void OnQteStart()
    {
        _currentMashCount = 0;
        Debug.Log($"[{actionType}] J/K 연타 QTE 시작! 제한시간: {timeLimit}초, 목표 연타수: {requiredMashCount}");
    }

    protected override void OnQteUpdate()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        // 🌟 [수정] J 키 또는 K 키가 이번 프레임에 눌렸는지 직접 확인
        // QTE 중에는 QTEInputProvider가 활성이므로 J/K만 응답하고 다른 키는 자동 차단됨
        if (kb[Key.J].wasPressedThisFrame || kb[Key.K].wasPressedThisFrame)
        {
            _currentMashCount++;
            Debug.Log($"[{actionType}] J/K 입력 감지! 현재 연타 횟수: {_currentMashCount} / {requiredMashCount}");

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
    }
}
