using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Seoul.Network.Game
{
    // 카페 기믹: startLane ~ startLane+2 총 3 lane을 점유.
    // - startLane (가장 번호 낮은 lane): BlockWall로 막힘 — 진입 불가
    // - startLane+1, startLane+2: InteriorTrigger 안에서 E(GetInteractDown) → itemToGrant 지급
    //
    // Prefab 구조 (스크립트가 자동 정렬/크기 조정):
    //   CafeGimmick
    //   ├ Visual           (3 lane 폭 floor 마커)
    //   ├ BlockWall        (1 lane, 비-트리거 collider — 플레이어 차단)
    //   └ InteriorTrigger  (2 lane, 트리거 collider + CafeInteriorTrigger 컴포넌트)
    public class CafeGimmick : MonoBehaviour
    {
        [Header("Lane")]
        [Tooltip("카페가 점유할 시작 lane. 이 lane은 막히고, +1/+2 lane은 진입 가능.")]
        [SerializeField] private int startLane = 0;

        [Header("Item")]
        [Tooltip("진입 lane 안에서 E 키를 눌렀을 때 지급할 아이템 타입.")]
        [SerializeField] private ItemType itemToGrant = ItemType.Coffee;
        [Tooltip("true면 한 플레이어가 카페 진입 동안 한 번만 받을 수 있음 (퇴장 후 재진입 시 가능).")]
        [SerializeField] private bool onePickupPerVisit = true;

        private readonly Dictionary<PlayerController, Action> _handlers = new Dictionary<PlayerController, Action>();
        private readonly HashSet<PlayerController> _pickedThisVisit = new HashSet<PlayerController>();

        private void Reset() => ApplyLaneLayout();
        private void Awake() => ApplyLaneLayout();
        private void Start() => ApplyLaneLayout();

#if UNITY_EDITOR
        private void OnValidate()
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                ApplyLaneLayout();
            };
        }
#endif

        private void ApplyLaneLayout()
        {
            var lm = LaneManager.Instance;
            if (lm == null) return;

            int blockedLane = Mathf.Clamp(startLane, 0, lm.LaneCount - 1);
            int entryMin    = Mathf.Clamp(startLane + 1, 0, lm.LaneCount - 1);
            int entryMax    = Mathf.Clamp(startLane + 2, 0, lm.LaneCount - 1);
            int footprintMax = entryMax;

            // Root world Z = 전체 footprint 중심
            var pos = transform.position;
            pos.z = lm.GetLaneCenterZ(blockedLane, footprintMax);
            transform.position = pos;
            float rootZ = pos.z;

            float spacing = lm.LaneSpacing;

            // Visual (floor, 3 lane 전체)
            var visual = transform.Find("Visual");
            if (visual != null)
            {
                var s = visual.localScale;
                s.z = lm.GetLaneSpanZ(blockedLane, footprintMax);
                visual.localScale = s;
                var lp = visual.localPosition;
                lp.z = 0f;
                visual.localPosition = lp;
            }

            // BlockWall (가장 번호 낮은 lane, 차단)
            var wall = transform.Find("BlockWall");
            if (wall != null)
            {
                float wallZ = lm.GetLaneZ(blockedLane);
                var lp = wall.localPosition;
                lp.z = wallZ - rootZ;
                wall.localPosition = lp;

                var s = wall.localScale;
                s.z = spacing;
                wall.localScale = s;
            }

            // InteriorTrigger (entry lane 2칸, E 픽업 zone)
            var trigger = transform.Find("InteriorTrigger");
            if (trigger != null)
            {
                float trigZ = lm.GetLaneCenterZ(entryMin, entryMax);
                var lp = trigger.localPosition;
                lp.z = trigZ - rootZ;
                trigger.localPosition = lp;

                if (trigger.TryGetComponent<BoxCollider>(out var col))
                {
                    var s = col.size;
                    // localScale이 1이라고 가정 — 만약 scale을 쓴다면 나눠줘야 함.
                    s.z = lm.GetLaneSpanZ(entryMin, entryMax) / Mathf.Max(0.001f, trigger.localScale.z);
                    col.size = s;
                }
            }
        }

        // ── CafeInteriorTrigger가 호출하는 콜백 ───────────────────
        public void OnPlayerEnter(PlayerController player)
        {
            if (player == null) return;
            if (_handlers.ContainsKey(player)) return;

            // 본인 클라이언트만 입력을 받으므로 원격 플레이어는 무시.
            if (player.TryGetComponent<NetworkObject>(out var no) && !no.IsOwner) return;

            Action handler = () => TryGrantItem(player);
            _handlers[player] = handler;
            player.OnInteract += handler;
        }

        public void OnPlayerExit(PlayerController player)
        {
            if (player == null) return;
            if (_handlers.TryGetValue(player, out var h))
            {
                player.OnInteract -= h;
                _handlers.Remove(player);
            }
            _pickedThisVisit.Remove(player);
        }

        private void OnDisable()
        {
            foreach (var kv in _handlers)
            {
                if (kv.Key != null) kv.Key.OnInteract -= kv.Value;
            }
            _handlers.Clear();
            _pickedThisVisit.Clear();
        }

        private void TryGrantItem(PlayerController player)
        {
            if (player == null) return;
            if (onePickupPerVisit && _pickedThisVisit.Contains(player)) return;
            if (!player.TryGetComponent<NetworkItemInventory>(out var inv)) return;
            if (inv.currentItem.Value != ItemType.None) return;

            inv.TryPickupLocal(itemToGrant);
            _pickedThisVisit.Add(player);
        }
    }
}
