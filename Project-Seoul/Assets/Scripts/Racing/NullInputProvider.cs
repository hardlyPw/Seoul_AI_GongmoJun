public class NullInputProvider : IInputProvider
{
    public float GetLaneChange()   => 0f;
    public bool  GetJumpDown()     => false;
    public bool  GetSprint()       => false;
    public bool  GetItemUse()      => false;
    public bool  GetInteractDown() => false;
}
