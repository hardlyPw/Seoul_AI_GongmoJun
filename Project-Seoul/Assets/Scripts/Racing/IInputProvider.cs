public interface IInputProvider
{
    float GetLaneChange();   // W/S  - 라인 변경 (위/아래)
    bool  GetJumpDown();     // K    - 점프
    bool  GetSprint();       // J    - 빠르게 달리기 (홀드, 대시)
    bool  GetItemUse();      // L    - 아이템 사용
    bool  GetInteractDown(); // Q    - QTE / 기믹 상호작용
}
