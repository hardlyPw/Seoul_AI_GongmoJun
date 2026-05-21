// 아이템 효과 기반 클래스 (MonoBehaviour 아님 - new로 생성)
public abstract class ItemEffectBase
{
    public abstract string ItemName { get; }
    public abstract void Apply(PlayerController player);
}
