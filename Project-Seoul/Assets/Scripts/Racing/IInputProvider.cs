public interface IInputProvider
{
    float GetLaneChange();   // W/S  - 라인 변경 (위/아래)
    bool  GetJumpDown();     // K    - 점프
    bool  GetSprint();       // J    - sprint (홀드)
    bool  GetDashDown();     // JJ   - dash / 추월 (J 더블탭, 1프레임만 true)
    bool  GetItemUse();      // L    - 아이템 사용
    bool  GetInteractDown(); // Q    - QTE / 기믹 상호작용
    bool GetQTEKeyDown(UnityEngine.InputSystem.Key key);
}
