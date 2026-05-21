using UnityEngine.InputSystem;

public class PlayerInputProvider : IInputProvider
{
    public float GetHorizontal()
    {
        var kb = Keyboard.current;
        if (kb == null) return 0f;
        float h = 0f;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  h -= 1f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) h += 1f;
        return h;
    }

    public float GetLaneChange()
    {
        var kb = Keyboard.current;
        if (kb == null) return 0f;
        float v = 0f;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed)   v += 1f;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v -= 1f;
        return v;
    }

    public bool GetJumpDown()     => Keyboard.current?.kKey.wasPressedThisFrame ?? false;
    public bool GetSprint()       => Keyboard.current?.jKey.isPressed           ?? false;
    public bool GetItemUse()      => Keyboard.current?.lKey.wasPressedThisFrame ?? false;
    public bool GetInteractDown() => Keyboard.current?.qKey.wasPressedThisFrame ?? false;
}
