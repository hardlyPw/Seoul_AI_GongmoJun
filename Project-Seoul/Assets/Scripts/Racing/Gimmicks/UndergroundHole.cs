using UnityEngine;

namespace Seoul.Network.Game
{
    // 지하차도 입구 "구멍" 마커.
    // - 도로 위에 얹은 trigger BoxCollider에 붙는다.
    // - PlayerController가 OnTriggerEnter/Exit에서 이 컴포넌트를 감지해
    //   trigger 안에 있는 동안 ground 판정을 강제로 false로 만든다.
    // - 단, 캐릭터의 현재 lane이 [MinLane, MaxLane] 범위 안일 때만 추락 처리 → 옆 lane에서 trigger 살짝 침범해도 안 빠짐.
    // - 결과: 캐릭터가 hole lane 위를 그대로 달리면 도로 collider가 있어도 _isGrounded=false → 추락(지하).
    //         점프해서 진입하면 _velocity.y가 살아있으니 곡선을 그리며 도로 반대편에 착지.
    [RequireComponent(typeof(BoxCollider))]
    public class UndergroundHole : MonoBehaviour
    {
        // 추락이 발생할 lane 범위 (포함, 포함). UndergroundGimmick.Rebuild에서 hole 차선과 동일하게 설정.
        public int MinLane;
        public int MaxLane;

        private void Reset()  => EnsureTrigger();
        private void Awake()  => EnsureTrigger();

        private void EnsureTrigger()
        {
            if (TryGetComponent<BoxCollider>(out var box))
                box.isTrigger = true;
        }
    }
}
