using UnityEngine;

public class CoffeeItemEffect : ItemEffectBase
{
    [SerializeField] private float staminaRecover = 50f;

    public override string ItemName => "커피";

    public override void Apply(PlayerController player)
    {
        player.RecoverStamina(staminaRecover);
        Debug.Log($"[아이템] 커피 사용: 스태미나 +{staminaRecover}");
    }
}
