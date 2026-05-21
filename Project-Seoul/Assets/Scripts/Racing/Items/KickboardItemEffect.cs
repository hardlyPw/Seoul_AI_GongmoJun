using System.Collections;
using UnityEngine;

public class KickboardItemEffect : ItemEffectBase
{
    private readonly float _speedBoost = 1.5f;
    private readonly float _duration   = 5f;

    public override string ItemName => "전동 킥보드";

    public override void Apply(PlayerController player)
    {
        player.StartCoroutine(SpeedBoostRoutine(player));
        Debug.Log($"[아이템] 전동 킥보드 사용: 속도 x{_speedBoost} ({_duration}초)");
    }

    private IEnumerator SpeedBoostRoutine(PlayerController player)
    {
        player.SetSpeedMultiplier(_speedBoost);
        yield return new WaitForSeconds(_duration);
        player.SetSpeedMultiplier(1f);
    }
}
