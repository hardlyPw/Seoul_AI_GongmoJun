public class AIInputProvider : IInputProvider
{
    private float _h      = 1f;
    private float _lane   = 0f;
    private bool  _jump;
    private bool  _sprint;
    private bool  _item;
    private bool  _interact;

    public void SetInput(float h, float lane, bool jump, bool sprint, bool item, bool interact)
    {
        _h       = h;
        _lane    = lane;
        _jump    = jump;
        _sprint  = sprint;
        _item    = item;
        _interact = interact;
    }

    public float GetHorizontal()   => _h;
    public float GetLaneChange()   => _lane;
    public bool  GetJumpDown()     => _jump;
    public bool  GetSprint()       => _sprint;
    public bool  GetItemUse()      => _item;
    public bool  GetInteractDown() => _interact;
}
