using UnityEngine;

namespace Seoul.Network.Game
{
    // 출구 비탈 영역 마커 + 시작/끝 좌표 보관.
    // - 시각 ramp cube 옆에 평행한 BoxCollider trigger로 부착.
    // - 캐릭터가 trigger 안에 있는 동안 PlayerController가 캐릭터 y를 SurfaceYAt(world x)로 자동 보정.
    //   PlayerController의 -y SphereCast로는 비탈 표면 따라 올라갈 수 없어 별도 처리가 필요함.
    [RequireComponent(typeof(BoxCollider))]
    public class UndergroundExitRamp : MonoBehaviour
    {
        [Tooltip("비탈 낮은 쪽 (시작) world X / Y")]
        public Vector2 startWorld;
        [Tooltip("비탈 높은 쪽 (끝) world X / Y")]
        public Vector2 endWorld;
        public bool UseLaneRange;
        public int MinLane;
        public int MaxLane;

        private void Reset() => EnsureTrigger();
        private void Awake() => EnsureTrigger();

        private void EnsureTrigger()
        {
            if (TryGetComponent<BoxCollider>(out var box))
                box.isTrigger = true;
        }

        // 캐릭터 world X에 해당하는 비탈 표면 world Y. 비탈 구간 밖이면 가장 가까운 끝점 Y로 clamp.
        public float SurfaceYAt(float worldX)
        {
            float t = Mathf.InverseLerp(startWorld.x, endWorld.x, worldX);
            return Mathf.Lerp(startWorld.y, endWorld.y, t);
        }

        public bool IsActiveForLane(int lane)
        {
            if (!UseLaneRange) return true;
            return lane >= MinLane && lane <= MaxLane;
        }
    }
}
