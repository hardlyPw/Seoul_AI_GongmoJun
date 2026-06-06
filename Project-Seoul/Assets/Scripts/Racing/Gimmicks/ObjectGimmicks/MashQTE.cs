using UnityEngine;
using UnityEngine.InputSystem;
using Seoul.Network.Game;

public class MashQTE : BaseQTE
{
    [Header("Mash Settings")]
    [SerializeField] private Key mashKey = Key.Space;
    [SerializeField] private int requiredMashCount = 10;
    [SerializeField] private BaseQTE.QteActionType actionType;
    [SerializeField] private GameObject targetVisualObject;

    private int _currentMashCount = 0;

    protected override void OnQteStart()
    {
        _currentMashCount = 0;
        Debug.Log($"[{actionType}] 연타 QTE 시작! 제한시간: {timeLimit}초, 목표 연타수: {requiredMashCount}");
    }

    protected override void OnQteUpdate()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb[mashKey].wasPressedThisFrame)
        {
            _currentMashCount++;
            Debug.Log($"[{actionType}] 키 입력 감지! 현재 연타 횟수: {_currentMashCount} / {requiredMashCount}");

            if (_currentMashCount >= requiredMashCount)
            {
                HandleSuccess(); // 부모 클래스의 성공 처리 호출
            }
        }
    }

    protected override BaseQTE.QteActionType GetActionType() => actionType;

    protected override void OnLocalSuccessVisual()
    {
        if (targetVisualObject != null) targetVisualObject.SetActive(false);
    }
}