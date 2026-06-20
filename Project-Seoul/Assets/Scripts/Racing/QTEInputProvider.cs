using UnityEngine.InputSystem;

/// <summary>
/// QTE 진행 중에만 사용되는 입력 프로바이더.
/// J/K 키만 허용하고 다른 모든 입력(점프, 대시, 아이템 등)을 차단합니다.
/// </summary>
public class QTEInputProvider : IInputProvider
{
    public float GetLaneChange()   => 0f;                    // 레인 변경 차단
    public bool  GetJumpDown()     => false;                 // 점프(K) 차단
    public bool  GetSprint()       => false;                 // 스프린트(J 홀드) 차단
    public bool  GetDashDown()     => false;                 // 대시(JJ) 차단
    public bool  GetItemUse()      => false;                 // 아이템 사용(L) 차단
    public bool  GetInteractDown() => false;                 // 상호작용(Q) 차단
    
    /// <summary>
    /// J/K 키만 응답하도록 제한. 다른 키는 모두 false 반환.
    /// </summary>
    public bool GetQTEKeyDown(Key key)
    {
        if (Keyboard.current == null) return false;
        // J 또는 K 키만 허용
        if (key == Key.J || key == Key.K)
            return Keyboard.current[key].wasPressedThisFrame;
        return false;
    }
}
