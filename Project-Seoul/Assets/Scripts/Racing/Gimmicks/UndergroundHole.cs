using UnityEngine;

namespace Seoul.Network.Game
{
    // 지하차도 입구 "구멍" 마커.
    // - 도로 위에 얹은 trigger BoxCollider에 붙는다.
    // - PlayerController가 OnTriggerEnter/Exit에서 이 컴포넌트를 감지해
    //   trigger 안에 있는 동안 ground 판정을 강제로 false로 만든다.
    // - 결과: 캐릭터가 lane 위를 그대로 달리면 도로 collider가 있어도 _isGrounded=false → 추락(지하).
    //         점프해서 진입하면 _velocity.y가 살아있으니 곡선을 그리며 도로 반대편에 착지.
    [RequireComponent(typeof(BoxCollider))]
    public class UndergroundHole : MonoBehaviour
    {
        private void Reset()  => EnsureTrigger();
        private void Awake()  => EnsureTrigger();

        private void EnsureTrigger()
        {
            if (TryGetComponent<BoxCollider>(out var box))
                box.isTrigger = true;
        }
    }
}
