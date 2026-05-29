public class AIInputProvider : IInputProvider
{
    private float _lane;
    private bool  _jump;
    private bool  _sprint;
    private bool  _item;
    private bool  _interact;

    public void SetInput(float lane, bool jump, bool sprint, bool item, bool interact)
    {
        _lane     = lane;
        _jump     = jump;
        _sprint   = sprint;
        _item     = item;
        _interact = interact;
    }

    public float GetLaneChange()   => _lane;
    public bool  GetJumpDown()     => _jump;
    public bool  GetSprint()       => _sprint;
    public bool  GetItemUse()      => _item;
    public bool  GetInteractDown() => _interact;
}
