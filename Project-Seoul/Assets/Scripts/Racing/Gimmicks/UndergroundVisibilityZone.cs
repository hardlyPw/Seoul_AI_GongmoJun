using System.Collections.Generic;
using UnityEngine;

namespace Seoul.Network.Game
{
    // 자기 캐릭터(NetworkObject.IsOwner)가 이 trigger 안에 있는 동안:
    //  1) 부모 컨테이너의 wall/ceiling 자식들 MeshRenderer를 끈다 (자기 화면만).
    //  2) 자기 카메라(Camera.main)의 cullingMask에서 hideLayerName layer를 빼서 도로가 안 보이게 한다 (자기 화면만).
    // 나가면 모두 복원. trigger 진입 카운트 기반이라 잠깐 겹쳤다 빠져도 안전.
    [RequireComponent(typeof(BoxCollider))]
    public class UndergroundVisibilityZone : MonoBehaviour
    {
        [Tooltip("자기 카메라 cullingMask에서 제거할 layer 이름. 비워두면 cullingMask 처리 skip. " +
                 "주의: Underground_Floor와 같은 layer면 floor도 함께 사라짐 — 분리 layer 필요.")]
        [SerializeField] private string hideLayerName = "";

        [Tooltip("Extra Renderers에 진입 시 swap할 반투명 material. 비워두면 단순 hide(enabled=false). " +
                 "Transparent shader + alpha < 1로 만들어 할당하면 도로가 반투명으로 보임.")]
        [SerializeField] private Material extraFadeMaterial;

        private MeshRenderer[] _toHide;
        private MeshRenderer[] _extras;
        private Material[]     _extraOriginalMats;
        private int _ownerOverlap;
        private Camera _hiddenCam;
        private int   _hiddenCamOriginalMask;

        public void SetExtraRenderers(MeshRenderer[] extras) => _extras = extras;
        public void SetExtraFadeMaterial(Material mat) => extraFadeMaterial = mat;

        private void Awake()
        {
            if (TryGetComponent<BoxCollider>(out var box))
                box.isTrigger = true;
        }

        private void OnTriggerEnter(Collider col)
        {
            if (!IsLocalOwner(col)) return;
            if (_toHide == null) CollectRenderers();
            _ownerOverlap++;
            if (_ownerOverlap == 1) SetHidden(true);
        }

        private void OnTriggerExit(Collider col)
        {
            if (!IsLocalOwner(col)) return;
            _ownerOverlap = Mathf.Max(0, _ownerOverlap - 1);
            if (_ownerOverlap == 0) SetHidden(false);
        }

        private void OnDisable()
        {
            // 영역 안에 있는 상태로 prefab disable/destroy되면 카메라 mask가 stale 상태로 남음 → 복원.
            if (_ownerOverlap > 0) SetHidden(false);
            _ownerOverlap = 0;
        }

        private static bool IsLocalOwner(Collider col)
        {
            var pc = col.GetComponent<PlayerController>() ?? col.GetComponentInParent<PlayerController>();
            if (pc == null) return false;
            if (pc.TryGetComponent<Unity.Netcode.NetworkObject>(out var no))
                return no.IsOwner;
            return true; // NetworkObject 없으면(싱글/디버그) 통과
        }

        private void CollectRenderers()
        {
            var list = new List<MeshRenderer>();
            var parent = transform.parent;
            if (parent != null)
            {
                foreach (Transform t in parent)
                {
                    var n = t.name;
                    if (n.StartsWith("Underground_Wall_") || n.StartsWith("Underground_Ceiling_") || n.StartsWith("Entrance_Guard_"))
                        if (t.TryGetComponent<MeshRenderer>(out var r))
                            list.Add(r);
                }
            }
            _toHide = list.ToArray();
            if (_toHide.Length == 0)
                Debug.LogWarning("[UndergroundVisibilityZone] 부모 컨테이너에서 Underground_Wall_*/Underground_Ceiling_* 자식을 못 찾음. 시야 처리 일부만 동작할 수 있음.", this);
        }

        private void SetHidden(bool hidden)
        {
            // 1) prefab 자체의 wall/ceiling MeshRenderer
            if (_toHide != null)
                foreach (var r in _toHide)
                    if (r != null) r.enabled = !hidden;

            // 2) Extra MeshRenderer (도로 등) — fadeMaterial 있으면 swap, 없으면 단순 hide
            if (_extras != null)
            {
                if (extraFadeMaterial != null)
                {
                    if (hidden)
                    {
                        _extraOriginalMats = new Material[_extras.Length];
                        for (int i = 0; i < _extras.Length; i++)
                        {
                            if (_extras[i] == null) continue;
                            _extraOriginalMats[i] = _extras[i].sharedMaterial;
                            _extras[i].sharedMaterial = extraFadeMaterial;
                        }
                    }
                    else if (_extraOriginalMats != null)
                    {
                        for (int i = 0; i < _extras.Length; i++)
                            if (_extras[i] != null) _extras[i].sharedMaterial = _extraOriginalMats[i];
                        _extraOriginalMats = null;
                    }
                }
                else
                {
                    foreach (var r in _extras)
                        if (r != null) r.enabled = !hidden;
                }
            }

            // 3) 자기 카메라 cullingMask에서 hideLayerName 제거/복원 (옵션 — 이름이 비어있으면 skip)
            if (string.IsNullOrEmpty(hideLayerName))
            {
                _hiddenCam = null;
                return;
            }

            if (hidden)
            {
                _hiddenCam = Camera.main;
                if (_hiddenCam == null) return;
                _hiddenCamOriginalMask = _hiddenCam.cullingMask;
                int layer = LayerMask.NameToLayer(hideLayerName);
                if (layer >= 0)
                    _hiddenCam.cullingMask &= ~(1 << layer);
                else
                    Debug.LogWarning($"[UndergroundVisibilityZone] Layer '{hideLayerName}' 못 찾음 — culling skip.", this);
            }
            else
            {
                if (_hiddenCam != null)
                {
                    _hiddenCam.cullingMask = _hiddenCamOriginalMask;
                    _hiddenCam = null;
                }
            }
        }
    }
}
