using UnityEngine;

namespace Seoul.Network.Game
{
    // CafeGimmick의 InteriorTrigger 자식에 붙어 trigger 이벤트를 부모 CafeGimmick으로 위임.
    // (Unity OnTrigger 이벤트는 collider가 붙은 GameObject로만 전달되므로 자식→부모 위임이 필요.)
    [RequireComponent(typeof(BoxCollider))]
    public class CafeInteriorTrigger : MonoBehaviour
    {
        [SerializeField] private CafeGimmick cafe;

        private void Reset()
        {
            cafe = GetComponentInParent<CafeGimmick>();
            if (TryGetComponent<BoxCollider>(out var col)) col.isTrigger = true;
        }

        private void Awake()
        {
            if (cafe == null) cafe = GetComponentInParent<CafeGimmick>();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (cafe == null) return;
            if (other.TryGetComponent<PlayerController>(out var p)) cafe.OnPlayerEnter(p);
        }

        private void OnTriggerExit(Collider other)
        {
            if (cafe == null) return;
            if (other.TryGetComponent<PlayerController>(out var p)) cafe.OnPlayerExit(p);
        }
    }
}
