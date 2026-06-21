using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Seoul.Network.Game
{
    // 공원 정문 양옆 차단벽.
    // - 닿으면 Penalty: 감속 + 무적 + 깜빡임. lane change는 막지 않음.
    //   (LaneRangeZone(Penalty)을 같은 GameObject에 부착 → PlayerController.OnTriggerEnter에서 무적 타이머가
    //    먼저 세팅돼 ObstacleBase KnockDown도 같은 호출에서 자연스럽게 면역됨.)
    // - lane 범위는 서버가 Spawn 직후 NetworkVariable로 전파 → 각 클라가 자기 측 LaneManager로 정렬.
    //   (BoxCollider.size는 NGO 자동 동기화 대상이 아니라 클라별 적용 필요.)
    // - BoxCollider + 시각용 Cube는 코드가 만든다. prefab은 빈 GameObject + NetworkObject + 이 컴포넌트만 있으면 됨.
    public class ParkWall : ObstacleBase
    {
        [Header("Wall Dimensions")]
        [Tooltip("벽 두께 (x축). 캐릭터 진행 방향이라 trigger에 닿을 만큼만.")]
        [SerializeField] private float thickness = 1f;
        [Tooltip("벽 높이 (y축).")]
        [SerializeField] private float height = 3f;
        [Tooltip("벽 시각용 머티리얼. 비워두면 Unity 기본 회색.")]
        [SerializeField] private Material wallMaterial;

        [Header("Penalty 효과 파라미터")]
        [Tooltip("감속 비율 (0.5 = 절반 속도).")]
        [SerializeField] private float penaltySpeedRatio = 0.5f;
        [Tooltip("감속 지속 시간 (초).")]
        [SerializeField] private float penaltySlowDuration = 1.5f;
        [Tooltip("무적/깜빡임 지속 시간 (초). 이 동안 같은 zone 재진입 + KnockDown 장애물 면역.")]
        [SerializeField] private float invincibilityDuration = 1.5f;
        [Tooltip("깜빡임 1회 on/off 주기 (초).")]
        [SerializeField] private float blinkInterval = 0.1f;

        [Header("Gate Clearance")]
        [Tooltip("정문 통과 보장용 collider 여유 (m). 캐릭터 capsule radius보다 약간 크게. " +
                 "BoxCollider는 양쪽 끝에서 이만큼 줄여 정문 lane을 침범하지 않게 함. " +
                 "Visual cube는 lane 영역 전체 그대로 표시.")]
        [SerializeField] private float gateClearance = 0.6f;

        public NetworkVariable<int> MinLane = new(
            value: -1,
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);

        public NetworkVariable<int> MaxLane = new(
            value: -1,
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);

        private BoxCollider _box;
        private GameObject _visual;
        private Coroutine _fitRoutine;

        protected override void Awake()
        {
            // BoxCollider 먼저 확보. ObstacleBase.Awake가 발견하면 isTrigger=true 처리.
            if (!TryGetComponent<BoxCollider>(out _box))
                _box = gameObject.AddComponent<BoxCollider>();
            _box.size   = new Vector3(thickness, height, 1f); // z는 LaneManager가 lane 폭으로 덮어씀
            _box.center = new Vector3(0f, height * 0.5f, 0f); // 지면(y=0) 위로 올림

            // Penalty 모드 zone 부착 — 닿으면 감속+무적+깜빡임. lane change는 자유.
            // ObstacleBase.KnockDown은 무적 타이머가 같은 OnTriggerEnter에서 면역시키므로 별도 disable 불필요.
            if (!TryGetComponent<LaneRangeZone>(out var zone))
                zone = gameObject.AddComponent<LaneRangeZone>();
            zone.Mode = LaneRangeZone.BlockMode.Penalty;
            zone.PenaltySpeedRatio = penaltySpeedRatio;
            zone.PenaltySlowDuration = penaltySlowDuration;
            zone.InvincibilityDuration = invincibilityDuration;
            zone.BlinkInterval = blinkInterval;

            base.Awake();
        }

        public override void OnNetworkSpawn()
        {
            EnsureVisual();
            MinLane.OnValueChanged += OnLaneRangeChanged;
            MaxLane.OnValueChanged += OnLaneRangeChanged;
            RequestApplyLaneFit();
        }

        public override void OnNetworkDespawn()
        {
            MinLane.OnValueChanged -= OnLaneRangeChanged;
            MaxLane.OnValueChanged -= OnLaneRangeChanged;
            if (_fitRoutine != null)
            {
                StopCoroutine(_fitRoutine);
                _fitRoutine = null;
            }
        }

        private void OnLaneRangeChanged(int _, int __) => RequestApplyLaneFit();

        private void RequestApplyLaneFit()
        {
            if (_fitRoutine != null) StopCoroutine(_fitRoutine);
            _fitRoutine = StartCoroutine(FitWhenReady());
        }

        // 클라 측에서 LaneManager.Awake가 ParkWall spawn보다 늦을 수 있어 사용 가능해질 때까지 대기.
        private IEnumerator FitWhenReady()
        {
            while (LaneManager.Instance == null) yield return null;
            ApplyLaneFit();
            _fitRoutine = null;
        }

        private void ApplyLaneFit()
        {
            if (MinLane.Value < 0 || MaxLane.Value < 0) return;
            var lm = LaneManager.Instance;
            if (lm == null) return;

            // 위치 + BoxCollider.size.z를 lane span으로 정렬 (기존 FitToLaneRange).
            lm.FitToLaneRange(transform, MinLane.Value, MaxLane.Value);

            // Visual은 lane 영역 전체(=BoxCollider 적용 직후 size.z)를 시각적으로 덮음.
            float visualSpanZ = _box.size.z;

            // BoxCollider만 양 끝에서 clearance씩 줄여 정문 lane 통과 보장.
            // (캐릭터 capsule이 정문 lane 중심에 있어도 radius만큼 lane 경계 침범하기 때문)
            var size = _box.size;
            size.z = Mathf.Max(0.1f, visualSpanZ - gateClearance * 2f);
            _box.size = size;

            SyncVisual(visualSpanZ);
        }

        private void EnsureVisual()
        {
            if (_visual != null) return;
            _visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _visual.name = "Visual";
            _visual.transform.SetParent(transform, false);
            // CreatePrimitive가 붙이는 BoxCollider는 trigger collider와 겹쳐서 불필요 → 제거
            if (_visual.TryGetComponent<Collider>(out var c)) Destroy(c);
            if (wallMaterial != null && _visual.TryGetComponent<MeshRenderer>(out var mr))
                mr.sharedMaterial = wallMaterial;
        }

        // Visual cube는 lane 영역 전체(visualSpanZ)를 덮고, x/y는 BoxCollider와 동일.
        private void SyncVisual(float visualSpanZ)
        {
            if (_visual == null || _box == null) return;
            _visual.transform.localPosition = _box.center;
            _visual.transform.localScale    = new Vector3(_box.size.x, _box.size.y, visualSpanZ);
        }
    }
}
