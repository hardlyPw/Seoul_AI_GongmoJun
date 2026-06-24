using UnityEngine;

namespace Seoul.Network.Game
{
    // 육교 기믹 (정적 배치, 멀티 동기화 없음).
    // - 구성: 입구 계단 → 다리 위 평평한 길 → 출구 계단.
    // - 계단은 시각용 cube. 캐릭터 y는 UndergroundExitRamp(재사용) snap으로 보정.
    // - lane change: 다리 평평한 구간 위에선 LaneRangeZone으로 [bridgeMinLane, bridgeMaxLane]만 허용. 계단/도로선 자유.
    // - 진행 흐름 (캐릭터 +x):
    //     도로 → 입구 ramp snap trigger → 자동 y 올라감 → 다리 위 (Bridge_Floor, ground layer) →
    //     출구 ramp snap trigger → 자동 y 내려감 → 도로
    [ExecuteAlways]
    public class OverpassGimmick : MonoBehaviour
    {
        [Header("Bridge (다리 위 평평한 길)")]
        [Tooltip("다리 위 평평한 구간 길이(x).")]
        [SerializeField] private float bridgeLength = 12f;
        [Tooltip("도로 표면에서 다리 위까지 높이(y).")]
        [SerializeField] private float bridgeHeight = 3f;
        [Tooltip("다리가 시작되는 lane (포함). 예: lane 2~3을 덮으려면 2.")]
        [SerializeField] private int bridgeMinLane = 2;
        [Tooltip("다리가 끝나는 lane (포함). 예: lane 2~3을 덮으려면 3.")]
        [SerializeField] private int bridgeMaxLane = 3;
        [Tooltip("LaneManager가 없을 때(에디터 단독 편집 등)만 쓰는 폴백 폭(z). 런타임/씬에 LaneManager가 있으면 무시되고 lane 범위로 계산됨.")]
        [SerializeField] private float fallbackBridgeWidth = 2f;
        [Tooltip("Rebuild 시 transform.position.z를 LaneManager의 lane 중심으로 자동 정렬할지. 기존 좌표 유지하고 폭만 lane 기반으로 받고 싶으면 끄세요.")]
        [SerializeField] private bool snapTransformToLaneCenter = true;

        [Header("Lane Zone Behavior")]
        [Tooltip("HardBlock = 범위 밖 lane change 차단. Penalty = lane change는 허용, 닿으면 감속+무적+깜빡임.")]
        [SerializeField] private LaneRangeZone.BlockMode laneZoneMode = LaneRangeZone.BlockMode.HardBlock;

        [Header("Stairs (입구 + 출구 계단)")]
        [Tooltip("각 계단의 x 길이.")]
        [SerializeField] private float stairLength = 4f;
        [Tooltip("계단 단 개수.")]
        [SerializeField] private int   stairCount  = 5;

        [Header("Wall (다리 위 좌우 벽)")]
        [Tooltip("다리 위 좌우 벽 높이(y). 옆으로 떨어지지 못하게 시각적으로 표시.")]
        [SerializeField] private float bridgeWallHeight = 1.5f;

        [Header("Layers")]
        [Tooltip("Bridge_Floor가 사용할 layer. PlayerController.groundLayer mask에 포함된 layer여야 착지 가능.")]
        [SerializeField] private string groundLayerName = "Ground";

        [Header("Materials (선택)")]
        [SerializeField] private Material bridgeMaterial;
        [SerializeField] private Material stairsMaterial;
        [SerializeField] private Material wallMaterial;

        private const string ContainerName = "_GeneratedVisuals";
        private static Material s_RuntimeFallbackMaterial;

        private void OnEnable() => Rebuild();

#if UNITY_EDITOR
        private void OnValidate()
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                Rebuild();
            };
        }
#endif

        [ContextMenu("Rebuild Visuals")]
        public void Rebuild()
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorUtility.IsPersistent(this)) return;
#endif
            var existing = transform.Find(ContainerName);
            if (existing != null) DestroySafe(existing.gameObject);

            var container = new GameObject(ContainerName);
            container.transform.SetParent(transform, false);
            container.hideFlags = HideFlags.DontSave;

            int groundLayer = LayerMask.NameToLayer(groundLayerName);
            if (groundLayer < 0)
            {
                Debug.LogWarning($"[OverpassGimmick] Layer '{groundLayerName}' not found. Default(0) fallback.", this);
                groundLayer = 0;
            }

            // Lane → z(중심/폭) 변환. 런타임이면 Instance, 에디터 편집 중이면 FindFirstObjectByType.
            // LaneManager가 전혀 없으면 fallback 폭 사용 (transform.position.z는 건드리지 않음).
            var lm = LaneManager.Instance;
#if UNITY_EDITOR
            if (lm == null) lm = FindFirstObjectByType<LaneManager>();
#endif
            float bridgeWidth;
            // LaneLock_Trigger가 다리 양옆에 추가로 덮을 폭(편측). LaneSpacing × 1 = 옆 lane 1칸.
            float sideLaneMargin;
            if (lm != null)
            {
                int minLane = Mathf.Clamp(Mathf.Min(bridgeMinLane, bridgeMaxLane), 0, lm.LaneCount - 1);
                int maxLane = Mathf.Clamp(Mathf.Max(bridgeMinLane, bridgeMaxLane), 0, lm.LaneCount - 1);
                bridgeWidth = lm.GetLaneSpanZ(minLane, maxLane);
                sideLaneMargin = lm.LaneSpacing;

                if (snapTransformToLaneCenter)
                {
                    float centerZ = lm.GetLaneCenterZ(minLane, maxLane);
                    if (!Mathf.Approximately(transform.position.z, centerZ))
                    {
                        var p = transform.position;
                        p.z = centerZ;
                        transform.position = p;
                    }
                }
            }
            else
            {
                bridgeWidth = fallbackBridgeWidth;
                sideLaneMargin = 1f;
            }
            float sideMarginBoth = sideLaneMargin * 2f;
            int lockMinLane = lm != null ? Mathf.Clamp(Mathf.Min(bridgeMinLane, bridgeMaxLane), 0, lm.LaneCount - 1) : bridgeMinLane;
            int lockMaxLane = lm != null ? Mathf.Clamp(Mathf.Max(bridgeMinLane, bridgeMaxLane), 0, lm.LaneCount - 1) : bridgeMaxLane;

            // 좌표 기준: local origin = 입구 계단 시작점, 도로 표면(y=0).
            float entranceStartX = 0f;
            float entranceEndX   = stairLength;
            float bridgeStartX   = entranceEndX;
            float bridgeEndX     = bridgeStartX + bridgeLength;
            float exitStartX     = bridgeEndX;
            float exitEndX       = exitStartX + stairLength;
            float totalLength    = exitEndX - entranceStartX;
            float totalCenterX   = (entranceStartX + exitEndX) * 0.5f;

            // 1) 입구 계단 (시각용)
            BuildStairs(container.transform, "Entrance_Stair",
                xStart: entranceStartX,
                yStart: 0f, yEnd: bridgeHeight,
                widthZ: bridgeWidth);

            // 2) 다리 위 평평한 길 (ground layer)
            //    두께 0.2m — 1m면 캐릭터가 cube 안에 박힐 때 SphereCast 결과가 부정확해져 끼는 현상 발생.
            float bridgeCenterX = (bridgeStartX + bridgeEndX) * 0.5f;
            CreateCube(container.transform, "Bridge_Floor",
                center: new Vector3(bridgeCenterX, bridgeHeight - 0.1f, 0f), // top y = bridgeHeight
                size:   new Vector3(bridgeLength, 0.2f, bridgeWidth),
                mat:    bridgeMaterial,
                groundLayer: groundLayer);

            // 3) 출구 계단 (시각용, y 반전)
            BuildStairs(container.transform, "Exit_Stair",
                xStart: exitStartX,
                yStart: bridgeHeight, yEnd: 0f,
                widthZ: bridgeWidth);

            // 4) 다리 위 좌우 가드레일 — 다리 평평한 구간에만 (계단 영역엔 적용 안 함).
            //    Lane lock은 별도 trigger가 전체 영역 처리.
            if (bridgeWallHeight > 0.01f)
            {
                float wallY = bridgeHeight + bridgeWallHeight * 0.5f;
                CreateCube(container.transform, "Bridge_Wall_NegZ",
                    center: new Vector3(bridgeCenterX, wallY, -bridgeWidth * 0.5f - 0.1f),
                    size:   new Vector3(bridgeLength, bridgeWallHeight, 0.2f),
                    mat:    wallMaterial,
                    groundLayer: -1);
                CreateCube(container.transform, "Bridge_Wall_PosZ",
                    center: new Vector3(bridgeCenterX, wallY, bridgeWidth * 0.5f + 0.1f),
                    size:   new Vector3(bridgeLength, bridgeWallHeight, 0.2f),
                    mat:    wallMaterial,
                    groundLayer: -1);
            }

            // 5) Lane Range Zone — 다리 평평한 구간 위에서만 lane change를 다리 lane 범위로 제한.
            //    계단은 포함 안 함 → 계단 위/아래선 자유 (계단 오를 때 lane 정렬 가능).
            //    이미 범위 밖 lane에서 진입해도 안쪽으로 좁히는 변경은 허용 (PlayerController.HandleLaneChange 참조).
            if (lm != null)
            {
                var zoneGO = new GameObject("Bridge_LaneRangeZone");
                zoneGO.transform.SetParent(container.transform, false);
                zoneGO.transform.localPosition = new Vector3(bridgeCenterX, bridgeHeight + bridgeWallHeight * 0.5f, 0f);
                zoneGO.hideFlags = HideFlags.DontSave;
                var zoneCol = zoneGO.AddComponent<BoxCollider>();
                zoneCol.isTrigger = true;
                zoneCol.size = new Vector3(bridgeLength, bridgeWallHeight + 2f, bridgeWidth + sideMarginBoth);
                var zone = zoneGO.AddComponent<LaneRangeZone>();
                zone.MinLane = lockMinLane;
                zone.MaxLane = lockMaxLane;
                zone.Mode = laneZoneMode;
            }

            if (lm != null)
            {
                BuildLaneLockZone("Entrance_Stair_BoundaryLockZone",
                    container.transform,
                    xStart: entranceStartX,
                    xEnd: entranceEndX,
                    centerY: (bridgeHeight + bridgeWallHeight) * 0.5f,
                    sizeY: bridgeHeight + bridgeWallHeight + 1f,
                    widthZ: bridgeWidth + sideMarginBoth,
                    minLane: lockMinLane,
                    maxLane: lockMaxLane);

                BuildLaneLockZone("Exit_Stair_BoundaryLockZone",
                    container.transform,
                    xStart: exitStartX,
                    xEnd: exitEndX,
                    centerY: (bridgeHeight + bridgeWallHeight) * 0.5f,
                    sizeY: bridgeHeight + bridgeWallHeight + 1f,
                    widthZ: bridgeWidth + sideMarginBoth,
                    minLane: lockMinLane,
                    maxLane: lockMaxLane);
            }

            // 6) 입구 ramp snap trigger (UndergroundExitRamp 재사용, start=낮 end=높)
            BuildRampTrigger("Entrance_RampTrigger",
                container: container.transform,
                xStart: entranceStartX, xEnd: entranceEndX,
                startY: 0f, endY: bridgeHeight,
                widthZ: bridgeWidth,
                minLane: lockMinLane,
                maxLane: lockMaxLane,
                useLaneRange: lm != null);

            // 7) 출구 ramp snap trigger (start=높 end=낮)
            BuildRampTrigger("Exit_RampTrigger",
                container: container.transform,
                xStart: exitStartX, xEnd: exitEndX,
                startY: bridgeHeight, endY: 0f,
                widthZ: bridgeWidth,
                minLane: lockMinLane,
                maxLane: lockMaxLane,
                useLaneRange: lm != null);
        }

        // 계단 N개 cube 생성. 시각용(collider 없음).
        // 각 단의 bottom은 도로 표면(min(yStart, yEnd))까지 꽉 채움 → 옆에서 보면 진짜 계단 모양.
        // 두께 0인 단(예: 출구 마지막 단)은 건너뜀.
        private void BuildStairs(Transform parent, string namePrefix,
            float xStart, float yStart, float yEnd, float widthZ)
        {
            if (stairCount <= 0) return;
            float stepX = stairLength / stairCount;
            float stepY = (yEnd - yStart) / stairCount; // 양수 = 위로, 음수 = 아래로
            float baseY = Mathf.Min(yStart, yEnd);      // 도로 표면 (입구/출구 둘 다 0)
            for (int i = 0; i < stairCount; i++)
            {
                float centerX = xStart + (i + 0.5f) * stepX;
                float topY    = yStart + (i + 1f) * stepY;
                float thickness = Mathf.Abs(topY - baseY);
                if (thickness < 0.01f) continue; // 도로와 같은 높이 단은 안 만듦
                float centerY = (topY + baseY) * 0.5f;
                CreateCube(parent, $"{namePrefix}_{i}",
                    center: new Vector3(centerX, centerY, 0f),
                    size:   new Vector3(stepX, thickness, widthZ),
                    mat:    stairsMaterial,
                    groundLayer: -1);
            }
        }

        // Ramp snap trigger (UndergroundExitRamp) 생성.
        // PlayerController가 trigger 안일 때 캐릭터 y를 SurfaceYAt(x)로 자동 보정.
        private void BuildRampTrigger(string n, Transform container,
            float xStart, float xEnd, float startY, float endY, float widthZ,
            int minLane, int maxLane, bool useLaneRange)
        {
            var go = new GameObject(n);
            go.transform.SetParent(container, false);
            float lowY  = Mathf.Min(startY, endY);
            float highY = Mathf.Max(startY, endY);
            go.transform.localPosition = new Vector3((xStart + xEnd) * 0.5f, (lowY + highY) * 0.5f + 0.5f, 0f);
            go.hideFlags = HideFlags.DontSave;
            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(xEnd - xStart, (highY - lowY) + 2f, widthZ);
            var ramp = go.AddComponent<UndergroundExitRamp>();
            ramp.startWorld = new Vector2(transform.position.x + xStart, transform.position.y + startY);
            ramp.endWorld   = new Vector2(transform.position.x + xEnd,   transform.position.y + endY);
            ramp.UseLaneRange = useLaneRange;
            ramp.MinLane = minLane;
            ramp.MaxLane = maxLane;
        }

        private void BuildLaneLockZone(string n, Transform container,
            float xStart, float xEnd, float centerY, float sizeY, float widthZ,
            int minLane, int maxLane)
        {
            var go = new GameObject(n);
            go.transform.SetParent(container, false);
            go.transform.localPosition = new Vector3((xStart + xEnd) * 0.5f, centerY, 0f);
            go.hideFlags = HideFlags.DontSave;
            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(xEnd - xStart, sizeY, widthZ);
            var zone = go.AddComponent<LaneRangeZone>();
            zone.Mode = LaneRangeZone.BlockMode.BoundaryLock;
            zone.MinLane = minLane;
            zone.MaxLane = maxLane;
        }

        private void CreateCube(Transform parent, string n, Vector3 center, Vector3 size, Material mat, int groundLayer)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = n;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localScale    = size;
            if (groundLayer >= 0)
            {
                go.layer = groundLayer;
            }
            else if (go.TryGetComponent<Collider>(out var c))
            {
                // 시각 only geometry도 카메라 occlusion raycast에는 잡혀야 한다.
                // trigger collider로 남겨 물리 이동은 막지 않되 투명화 대상 검출에만 사용한다.
                c.isTrigger = true;
            }
            if (go.TryGetComponent<MeshRenderer>(out var mr)) mr.sharedMaterial = mat != null ? mat : RuntimeFallbackMaterial;
            go.hideFlags = HideFlags.DontSave;
        }

        private static Material RuntimeFallbackMaterial
        {
            get
            {
                if (s_RuntimeFallbackMaterial != null) return s_RuntimeFallbackMaterial;

                var shader = Shader.Find("Universal Render Pipeline/Lit")
                    ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                    ?? Shader.Find("Standard");
                if (shader == null) return null;

                s_RuntimeFallbackMaterial = new Material(shader)
                {
                    name = "GeneratedGimmickFallbackMaterial",
                    hideFlags = HideFlags.DontSave
                };
                if (s_RuntimeFallbackMaterial.HasProperty("_BaseColor"))
                    s_RuntimeFallbackMaterial.SetColor("_BaseColor", new Color(0.45f, 0.45f, 0.45f, 1f));
                else if (s_RuntimeFallbackMaterial.HasProperty("_Color"))
                    s_RuntimeFallbackMaterial.SetColor("_Color", new Color(0.45f, 0.45f, 0.45f, 1f));
                return s_RuntimeFallbackMaterial;
            }
        }

        private static void DestroySafe(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o);
            else                       DestroyImmediate(o);
        }
    }
}
