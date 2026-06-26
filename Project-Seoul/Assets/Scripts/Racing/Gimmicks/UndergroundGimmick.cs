using UnityEngine;

namespace Seoul.Network.Game
{
    // 지하차도 기믹 (정적 배치, 멀티 동기화 없음).
    // - 씬에 prefab을 끌어다 놓기만 하면 시각/충돌이 자동 생성된다.
    // - 인스펙터에서 holeLength/holeMinLane/holeMaxLane/undergroundDepth 등을 바꾸면 에디터에서 즉시 갱신.
    // - 동작 원리:
    //   * 도로 위에 검은 quad("구멍" 그림) + UndergroundHole trigger 얹음.
    //   * 캐릭터가 trigger에 진입하면 PlayerController가 ground 판정을 무시 → 추락.
    //   * 점프해서 trigger를 지나가면 _velocity.y가 살아있어 도로 반대편에 그대로 착지.
    //   * 떨어진 캐릭터는 지하 floor(별도 Ground collider)에 착지 후 출구 ramp로 다시 지상.
    // - lane change: 통로/계단/exit ramp 전체에 LaneRangeZone → [holeMinLane, holeMaxLane]만 허용 (옆 lane으로 빠져 추락 방지).
    // - 자동 생성된 자식들은 HideFlags.DontSave → prefab/scene에 저장되지 않고 OnEnable 때마다 재생성.
    [ExecuteAlways]
    public class UndergroundGimmick : MonoBehaviour
    {
        [Header("Hole (도로 위 구멍)")]
        [Tooltip("구멍의 진행 방향 길이(x). 점프로 건너야 하는 거리.")]
        [SerializeField] private float holeLength = 4f;
        [Tooltip("구멍이 시작되는 lane (포함). 예: lane 2~3을 막으려면 2.")]
        [SerializeField] private int holeMinLane = 2;
        [Tooltip("구멍이 끝나는 lane (포함). 예: lane 2~3을 막으려면 3.")]
        [SerializeField] private int holeMaxLane = 3;
        [Tooltip("LaneManager가 없을 때(에디터 단독 편집 등)만 쓰는 폴백 폭(z). 런타임/씬에 LaneManager가 있으면 무시되고 lane 범위로 계산됨.")]
        [SerializeField] private float fallbackHoleWidth = 2f;
        [Tooltip("Rebuild 시 transform.position.z를 LaneManager의 lane 중심으로 자동 정렬할지. 기존 좌표를 유지하고 폭만 lane 기반으로 받고 싶으면 끄세요.")]
        [SerializeField] private bool snapTransformToLaneCenter = true;

        [Header("Lane Zone Behavior")]
        [Tooltip("HardBlock = 범위 밖 lane change 차단. Penalty = lane change는 허용, 닿으면 감속+무적+깜빡임.")]
        [SerializeField] private LaneRangeZone.BlockMode laneZoneMode = LaneRangeZone.BlockMode.HardBlock;
        [Tooltip("도로 표면보다 이 값만큼 아래로 내려갔을 때부터 지하 lane 제한을 적용합니다.")]
        [SerializeField] private float laneLimitActiveBelowSurface = 0.25f;
        [Tooltip("ground 무시 trigger의 높이(y). 점프 정점보다 커야 통과 중 ground=false 유지.")]
        [SerializeField] private float holeTriggerHeight = 3f;
        [Tooltip("trigger의 z 폭을 holeWidth보다 양쪽에서 이만큼 줄여, 옆 lane 캐릭터 capsule이 trigger에 침범해 잘못 추락하는 걸 방지. 캐릭터 radius보다 약간 크게.")]
        [SerializeField] private float holeTriggerZClearance = 0.4f;

        [Header("Underground (지하 도로)")]
        [Tooltip("지상 도로(y=0)에서 지하 floor까지의 깊이(양수).")]
        [SerializeField] private float undergroundDepth = 3f;
        [Tooltip("지하 도로 전체 길이(x). 구멍 + 앞쪽 leadIn + 뒤쪽 ramp 포함.")]
        [SerializeField] private float undergroundLength = 24f;
        [Tooltip("구멍 시작점보다 앞으로 지하가 더 이어지는 여유(분위기/계단 공간).")]
        [SerializeField] private float undergroundLeadIn = 2f;

        [Header("Exit Ramp (지하 → 지상 비탈)")]
        [Tooltip("출구 비탈의 진행 방향 길이.")]
        [SerializeField] private float exitRampLength = 6f;

        [Header("Wall (지하 좌우 벽 — 지하 안쪽만)")]
        [Tooltip("지하 벽 높이(y). 지하 floor 위부터 위로 세움. 보통 undergroundDepth와 같거나 살짝 작게.")]
        [SerializeField] private float undergroundWallHeight = 3f;

        [Header("Entrance Guard (입구 위쪽 가드 — hole 영역에만)")]
        [Tooltip("hole 영역에만 지상 위로 세우는 가드 벽 높이(y). 옆에서 hole로 들어오려는 걸 시각적으로 막음. " +
                 "0 이하로 두면 가드 생성 안 함.")]
        [SerializeField] private float entranceGuardHeight = 1.5f;

        [Header("Exit Guard (출구 ㄷ자 안전 벽)")]
        [Tooltip("출구 ramp 끝에서 앞으로 이어지는 ㄷ자 안전 벽 길이(x).")]
        [SerializeField] private float exitGuardLength = 4.5f;
        [Tooltip("출구 ㄷ자 안전 벽 높이(y).")]
        [SerializeField] private float exitGuardHeight = 1.5f;
        [Tooltip("출구 ㄷ자 안전 벽 두께.")]
        [SerializeField] private float exitGuardThickness = 0.2f;
        [Tooltip("출구 ㄷ자 벽과 접근불가 trigger를 x축 기준으로 이동합니다. +면 진행 방향, -면 ramp 쪽입니다.")]
        [SerializeField] private float exitGuardForwardOffset = -1.6f;

        [Header("Entrance Stairs (입구 계단 — 시각용)")]
        [Tooltip("입구 계단 단 개수. 0 이면 계단 안 만듦. 시각용이라 collider 없음 — 캐릭터는 그대로 추락.")]
        [SerializeField] private int entranceStairCount = 5;

        [Header("Layers")]
        [Tooltip("지하 floor/ramp가 사용할 layer 이름. PlayerController.groundLayer mask에 포함된 layer여야 착지 가능.")]
        [SerializeField] private string groundLayerName = "Ground";

        [Header("Materials (선택 — 비워두면 Unity 기본 회색)")]
        [SerializeField] private Material holeMaterial;
        [SerializeField] private Material undergroundMaterial;
        [SerializeField] private Material rampMaterial;
        [SerializeField] private Material stairsMaterial;
        [Tooltip("지하 천장 전용 머티리얼 (형광등 등). 비워두면 undergroundMaterial로 폴백.")]
        [SerializeField] private Material ceilingMaterial;

        [Header("Visibility - 추가로 숨길 대상")]
        [Tooltip("지하차도 진입 시 자기 카메라 시야에서 처리할 MeshRenderer. 도로 GameObject 등을 할당.")]
        [SerializeField] private MeshRenderer[] extraHideRenderers;
        [Tooltip("위 Renderer들에 swap할 반투명 material. 비워두면 단순 hide. " +
                 "Transparent shader + alpha < 1 material을 만들어 할당하면 도로가 반투명으로 표시됨.")]
        [SerializeField] private Material extraFadeMaterial;

        private const string ContainerName = "_GeneratedVisuals";
        private static Material s_RuntimeFallbackMaterial;

        private void OnEnable() => Rebuild();

#if UNITY_EDITOR
        private void OnValidate()
        {
            // OnValidate 안에서 Destroy/Instantiate를 직접 호출하면 Unity가 경고 → delayCall로 다음 프레임에 처리.
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
            // Prefab Asset(에셋 파일 자체)은 hierarchy 변경이 금지됨. import 시 ExecuteAlways가 호출돼도 skip.
            // Prefab Stage(편집 중)나 씬 인스턴스는 IsPersistent=false라 정상 동작.
            if (UnityEditor.EditorUtility.IsPersistent(this)) return;
#endif
            var existing = transform.Find(ContainerName);
            if (existing != null) DestroySafe(existing.gameObject);

            var container = new GameObject(ContainerName);
            container.transform.SetParent(transform, false);
            container.hideFlags = HideFlags.DontSave;

            // 좌표 기준: local origin = 구멍 시작 지점, lane 폭 중심(z=0).
            float holeStartX = 0f;
            float holeEndX   = holeLength;
            float holeMidX   = holeLength * 0.5f;

            float undergroundStartX = holeStartX - undergroundLeadIn;
            float undergroundEndX   = undergroundStartX + undergroundLength;
            float undergroundMidX   = (undergroundStartX + undergroundEndX) * 0.5f;
            float undergroundY      = -undergroundDepth;

            int groundLayer = LayerMask.NameToLayer(groundLayerName);
            if (groundLayer < 0)
            {
                Debug.LogWarning($"[UndergroundGimmick] Layer '{groundLayerName}' not found in Tags & Layers. Falling back to Default(0). 지하 floor/ramp에 캐릭터가 착지 못할 수 있음 — PlayerController.groundLayer mask와 일치하는 이름으로 바꿔주세요.", this);
                groundLayer = 0;
            }

            // Lane → z(중심/폭) 변환. 런타임이면 Instance, 에디터 편집 중이면 FindFirstObjectByType.
            // LaneManager가 전혀 없으면 fallback 폭 사용 (transform.position.z는 건드리지 않음).
            var lm = LaneManager.Instance;
#if UNITY_EDITOR
            if (lm == null) lm = FindFirstObjectByType<LaneManager>();
#endif
            float holeWidth;
            // Wall/Visibility trigger가 hole 양옆에 추가로 덮을 폭(편측). LaneSpacing × 1 = 옆 lane 1칸.
            float sideLaneMargin;
            if (lm != null)
            {
                int minLane = Mathf.Clamp(Mathf.Min(holeMinLane, holeMaxLane), 0, lm.LaneCount - 1);
                int maxLane = Mathf.Clamp(Mathf.Max(holeMinLane, holeMaxLane), 0, lm.LaneCount - 1);
                holeWidth = lm.GetLaneSpanZ(minLane, maxLane);
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
                holeWidth = fallbackHoleWidth;
                sideLaneMargin = 1f;
            }
            // 양옆 합치면 lane 2칸 폭.
            float sideMarginBoth = sideLaneMargin * 2f;

            // (Hole_Visual 제거 — 도로가 반투명화되면 hole 영역이 자연스럽게 보임)

            // 2) Hole Trigger (PlayerController가 감지)
            var triggerGO = new GameObject("Hole_Trigger");
            triggerGO.transform.SetParent(container.transform, false);
            triggerGO.transform.localPosition = new Vector3(holeMidX, holeTriggerHeight * 0.5f, 0f);
            triggerGO.hideFlags = HideFlags.DontSave;
            var triggerCol = triggerGO.AddComponent<BoxCollider>();
            triggerCol.isTrigger = true;
            // trigger z 폭만 양쪽에서 clearance씩 줄여 옆 lane capsule이 침범하지 않게.
            // 시각용 Hole_Visual quad는 holeWidth 그대로 → lane 영역 전체 덮음.
            float triggerZ = Mathf.Max(0.1f, holeWidth - holeTriggerZClearance * 2f);
            triggerCol.size = new Vector3(holeLength, holeTriggerHeight, triggerZ);

            // 2-2) Lane Range Zone — 지하 통로 + 계단 + exit ramp 전체 덮음.
            //      안에 있는 동안 lane change를 [holeMinLane, holeMaxLane]로 제한 (PlayerController.HandleLaneChange).
            //      이미 범위 밖 lane에서 진입해도 안쪽으로 좁히는 변경은 허용 → 입구 정렬 보조.
            //      계단 위/아래/지하 floor에서 옆 lane으로 빠져 추락하는 사고 방지.
            if (lm != null)
            {
                var zoneGO = new GameObject("LaneRange_Trigger");
                zoneGO.transform.SetParent(container.transform, false);
                float zoneYCenter = (holeTriggerHeight - undergroundDepth - 0.5f) * 0.5f;
                float zoneYSize   = holeTriggerHeight + undergroundDepth + 0.5f;
                zoneGO.transform.localPosition = new Vector3(undergroundMidX, zoneYCenter, 0f);
                zoneGO.hideFlags = HideFlags.DontSave;
                var zoneCol = zoneGO.AddComponent<BoxCollider>();
                zoneCol.isTrigger = true;
                zoneCol.size = new Vector3(undergroundLength, zoneYSize, holeWidth + sideMarginBoth);
                var zone = zoneGO.AddComponent<LaneRangeZone>();
                int minLaneZ = Mathf.Clamp(Mathf.Min(holeMinLane, holeMaxLane), 0, lm.LaneCount - 1);
                int maxLaneZ = Mathf.Clamp(Mathf.Max(holeMinLane, holeMaxLane), 0, lm.LaneCount - 1);
                zone.MinLane = minLaneZ;
                zone.MaxLane = maxLaneZ;
                zone.Mode = laneZoneMode;
                zone.UseMaxActiveWorldY = true;
                zone.MaxActiveWorldY = transform.position.y - laneLimitActiveBelowSurface;
            }

            // 2-3) (UndergroundVisibilityZone 제거됨)
            //      투명화는 이제 PlayerController.UpdateCameraOcclusion이 카메라-플레이어 raycast로 처리.
            //      지하 안 쓰는 플레이어 카메라에는 적용 안 됨 (도로가 카메라-플레이어 사이에 안 끼니까).
            var holeMarker = triggerGO.AddComponent<UndergroundHole>();
            if (lm != null)
            {
                holeMarker.MinLane = Mathf.Clamp(Mathf.Min(holeMinLane, holeMaxLane), 0, lm.LaneCount - 1);
                holeMarker.MaxLane = Mathf.Clamp(Mathf.Max(holeMinLane, holeMaxLane), 0, lm.LaneCount - 1);
            }
            else
            {
                holeMarker.MinLane = holeMinLane;
                holeMarker.MaxLane = holeMaxLane;
            }

            // 3) 지하 floor (Ground 레이어, 추락한 캐릭터 착지)
            //    두께 1m — 입구 계단 마지막 단이 floor 안으로 살짝 들어가도 안 보이게.
            CreateCube(container.transform, "Underground_Floor",
                center: new Vector3(undergroundMidX, undergroundY - 0.5f, 0f),
                size:   new Vector3(undergroundLength, 1f, holeWidth),
                mat:    undergroundMaterial,
                groundLayer: groundLayer);

            // 4) 지하 좌/우 벽 — 지하 안쪽만 (y=undergroundY ~ 0). 지하 통로 전체 길이.
            if (undergroundWallHeight > 0.01f)
            {
                float undWallY = undergroundY + undergroundWallHeight * 0.5f;
                CreateCube(container.transform, "Underground_Wall_NegZ",
                    center: new Vector3(undergroundMidX, undWallY, -holeWidth * 0.5f - 0.1f),
                    size:   new Vector3(undergroundLength, undergroundWallHeight, 0.2f),
                    mat:    undergroundMaterial,
                    groundLayer: -1);
                CreateCube(container.transform, "Underground_Wall_PosZ",
                    center: new Vector3(undergroundMidX, undWallY, holeWidth * 0.5f + 0.1f),
                    size:   new Vector3(undergroundLength, undergroundWallHeight, 0.2f),
                    mat:    undergroundMaterial,
                    groundLayer: -1);
            }

            // 4-2) 입구 가드 — hole 영역에만 지상 위로 (y=0 ~ entranceGuardHeight).
            //      옆 lane에서 hole 쪽으로 진입하는 걸 시각적으로 막는 짧은 벽.
            if (entranceGuardHeight > 0.01f)
            {
                float guardY = entranceGuardHeight * 0.5f;
                CreateCube(container.transform, "Entrance_Guard_NegZ",
                    center: new Vector3(holeMidX, guardY, -holeWidth * 0.5f - 0.1f),
                    size:   new Vector3(holeLength, entranceGuardHeight, 0.2f),
                    mat:    undergroundMaterial,
                    groundLayer: -1);
                CreateCube(container.transform, "Entrance_Guard_PosZ",
                    center: new Vector3(holeMidX, guardY, holeWidth * 0.5f + 0.1f),
                    size:   new Vector3(holeLength, entranceGuardHeight, 0.2f),
                    mat:    undergroundMaterial,
                    groundLayer: -1);
            }

            if (lm != null)
            {
                var entranceLockGO = new GameObject("Entrance_BoundaryLockZone");
                entranceLockGO.transform.SetParent(container.transform, false);
                entranceLockGO.transform.localPosition = new Vector3(holeMidX, holeTriggerHeight * 0.5f, 0f);
                entranceLockGO.hideFlags = HideFlags.DontSave;
                var entranceLockCol = entranceLockGO.AddComponent<BoxCollider>();
                entranceLockCol.isTrigger = true;
                entranceLockCol.size = new Vector3(holeLength, holeTriggerHeight, holeWidth + sideMarginBoth);
                var entranceLock = entranceLockGO.AddComponent<LaneRangeZone>();
                entranceLock.Mode = LaneRangeZone.BlockMode.BoundaryLock;
                entranceLock.MinLane = Mathf.Clamp(Mathf.Min(holeMinLane, holeMaxLane), 0, lm.LaneCount - 1);
                entranceLock.MaxLane = Mathf.Clamp(Mathf.Max(holeMinLane, holeMaxLane), 0, lm.LaneCount - 1);
                entranceLock.UseMaxActiveWorldY = false;
            }

            // 5) 지하 천장 (도로 밑면 분위기 — 구멍 영역은 비움)
            float ceilingY = -0.3f;
            float frontLen = Mathf.Max(0f, holeStartX - undergroundStartX);
            float backLen  = Mathf.Max(0f, undergroundEndX - holeEndX);
            Material effectiveCeilingMat = ceilingMaterial != null ? ceilingMaterial : undergroundMaterial;
            if (frontLen > 0.01f)
                CreateCube(container.transform, "Underground_Ceiling_Front",
                    center: new Vector3(undergroundStartX + frontLen * 0.5f, ceilingY, 0f),
                    size:   new Vector3(frontLen, 0.2f, holeWidth),
                    mat:    effectiveCeilingMat,
                    groundLayer: -1);
            if (backLen > 0.01f)
                CreateCube(container.transform, "Underground_Ceiling_Back",
                    center: new Vector3(holeEndX + backLen * 0.5f, ceilingY, 0f),
                    size:   new Vector3(backLen, 0.2f, holeWidth),
                    mat:    effectiveCeilingMat,
                    groundLayer: -1);

            // 6) 출구 Ramp (지하 끝 → 지상) + 캐릭터 y 자동 보정용 trigger
            if (exitRampLength > 0.01f)
            {
                float rampStartX = undergroundEndX - exitRampLength;
                CreateRamp(container.transform, "Exit_Ramp",
                    startCenter: new Vector3(rampStartX, undergroundY, 0f),
                    length: exitRampLength,
                    rise:   undergroundDepth,
                    width:  holeWidth,
                    mat:    rampMaterial,
                    groundLayer: groundLayer);

                // ramp 위 trigger — PlayerController가 안에 있을 때 캐릭터 y를 surface로 snap.
                var rampTriggerGO = new GameObject("ExitRamp_Trigger");
                rampTriggerGO.transform.SetParent(container.transform, false);
                rampTriggerGO.transform.localPosition = new Vector3(
                    rampStartX + exitRampLength * 0.5f,
                    undergroundY + undergroundDepth * 0.5f + 0.5f, // ramp 위 0.5m 여유
                    0f);
                rampTriggerGO.hideFlags = HideFlags.DontSave;
                var rampTriggerCol = rampTriggerGO.AddComponent<BoxCollider>();
                rampTriggerCol.isTrigger = true;
                rampTriggerCol.size = new Vector3(exitRampLength, undergroundDepth + 1.5f, triggerZ);
                var exitRamp = rampTriggerGO.AddComponent<UndergroundExitRamp>();
                // 시작/끝 world 좌표 = prefab world position + local offset.
                exitRamp.startWorld = new Vector2(
                    transform.position.x + rampStartX,
                    transform.position.y + undergroundY);
                exitRamp.endWorld = new Vector2(
                    transform.position.x + rampStartX + exitRampLength,
                    transform.position.y + 0f);
                if (lm != null)
                {
                    exitRamp.UseLaneRange = true;
                    exitRamp.MinLane = Mathf.Clamp(Mathf.Min(holeMinLane, holeMaxLane), 0, lm.LaneCount - 1);
                    exitRamp.MaxLane = Mathf.Clamp(Mathf.Max(holeMinLane, holeMaxLane), 0, lm.LaneCount - 1);
                }

                // 6-2) 출구 safe zone — 다른 플레이어 차단용.
                //      (a) BackWall: ramp top 직전(상류 -x)에 모든 lane을 가로지르는 얇은 trigger.
                //          PlayerController가 _velocity.x를 클램프 → 외부 surface 플레이어는 더 못 감.
                //      (b) NoEntry LaneRangeZone: ramp top 위 safe zone 영역. 밖→안 lane change 차단,
                //          안에서의 이동은 자유 → 출구 플레이어가 안에서 좌우 정렬 가능.
                float safeZoneLength = Mathf.Max(0.1f, exitGuardLength);
                float safeZoneStartX = undergroundEndX + exitGuardForwardOffset;
                float safeZoneMidX   = safeZoneStartX + safeZoneLength * 0.5f;
                float safeWallThickness = Mathf.Max(0.05f, exitGuardThickness);
                float safeWallWidthZ = holeWidth + safeWallThickness * 2f;
                float backWallCenterX = safeZoneStartX - safeWallThickness * 0.5f;

                // Exit guard visuals: ㄷ shape, open toward +x.
                // The rear wall sits just behind the safe zone so the player can still run out forward.
                if (exitGuardHeight > 0.01f)
                {
                    float guardY = exitGuardHeight * 0.5f;
                    float sideZ = holeWidth * 0.5f + safeWallThickness * 0.5f;

                    CreateCube(container.transform, "Exit_Guard_NegZ",
                        center: new Vector3(safeZoneMidX, guardY, -sideZ),
                        size:   new Vector3(safeZoneLength, exitGuardHeight, safeWallThickness),
                        mat:    undergroundMaterial,
                        groundLayer: -1);
                    CreateCube(container.transform, "Exit_Guard_PosZ",
                        center: new Vector3(safeZoneMidX, guardY, sideZ),
                        size:   new Vector3(safeZoneLength, exitGuardHeight, safeWallThickness),
                        mat:    undergroundMaterial,
                        groundLayer: -1);
                    CreateCube(container.transform, "Exit_Guard_Back",
                        center: new Vector3(backWallCenterX, guardY, 0f),
                        size:   new Vector3(safeWallThickness, exitGuardHeight, safeWallWidthZ),
                        mat:    undergroundMaterial,
                        groundLayer: -1);
                }

                // BackWall — visible back wall과 같은 위치/폭의 접근 불가 trigger.
                // 출구 lane만 막으므로 지상 플레이어는 W/S로 옆 lane에 빠져 앞으로 계속 갈 수 있다.
                var backWallGO = new GameObject("Exit_BackWall");
                backWallGO.transform.SetParent(container.transform, false);
                backWallGO.transform.localPosition = new Vector3(backWallCenterX, holeTriggerHeight * 0.5f, 0f);
                backWallGO.hideFlags = HideFlags.DontSave;
                var bwCol = backWallGO.AddComponent<BoxCollider>();
                bwCol.isTrigger = true;
                bwCol.size = new Vector3(safeWallThickness, holeTriggerHeight, safeWallWidthZ);
                var backWall = backWallGO.AddComponent<BackWall>();
                backWall.UseMaxActiveWorldY = false;
                if (lm != null)
                {
                    backWall.UseLaneRange = true;
                    backWall.MinLane = Mathf.Clamp(Mathf.Min(holeMinLane, holeMaxLane), 0, lm.LaneCount - 1);
                    backWall.MaxLane = Mathf.Clamp(Mathf.Max(holeMinLane, holeMaxLane), 0, lm.LaneCount - 1);
                }

                // NoEntry side zone — safe zone 영역에서 밖 lane → 출구 lane 진입 차단.
                if (lm != null)
                {
                    var noEntryGO = new GameObject("Exit_NoEntryZone");
                    noEntryGO.transform.SetParent(container.transform, false);
                    noEntryGO.transform.localPosition = new Vector3(safeZoneMidX, holeTriggerHeight * 0.5f, 0f);
                    noEntryGO.hideFlags = HideFlags.DontSave;
                    var neCol = noEntryGO.AddComponent<BoxCollider>();
                    neCol.isTrigger = true;
                    neCol.size = new Vector3(safeZoneLength, holeTriggerHeight, holeWidth + sideMarginBoth);
                    var neZone = noEntryGO.AddComponent<LaneRangeZone>();
                    neZone.Mode = LaneRangeZone.BlockMode.NoEntry;
                    neZone.MinLane = Mathf.Clamp(Mathf.Min(holeMinLane, holeMaxLane), 0, lm.LaneCount - 1);
                    neZone.MaxLane = Mathf.Clamp(Mathf.Max(holeMinLane, holeMaxLane), 0, lm.LaneCount - 1);
                    neZone.UseMaxActiveWorldY = false;
                }
            }

            // 7-pre) 입구 ramp snap trigger — 계단 표면 따라 캐릭터가 부드럽게 내려가게.
            //         hole trigger와 같은 영역. PlayerController가 둘 다 처리:
            //         hole_trigger → ground 무시 / exit_ramp → y를 surface(0 → -depth)로 snap.
            //         점프해서 hole 위로 통과하는 캐릭터는 vy>0 또는 갭>1m로 snap skip → 곡선 자유.
            {
                var entRampGO = new GameObject("Entrance_RampTrigger");
                entRampGO.transform.SetParent(container.transform, false);
                entRampGO.transform.localPosition = new Vector3(holeMidX, -undergroundDepth * 0.5f + 0.5f, 0f);
                entRampGO.hideFlags = HideFlags.DontSave;
                var entRampCol = entRampGO.AddComponent<BoxCollider>();
                entRampCol.isTrigger = true;
                entRampCol.size = new Vector3(holeLength, undergroundDepth + 1.5f, triggerZ);
                var entRamp = entRampGO.AddComponent<UndergroundExitRamp>();
                entRamp.startWorld = new Vector2(transform.position.x + holeStartX, transform.position.y + 0f);
                entRamp.endWorld   = new Vector2(transform.position.x + holeEndX,   transform.position.y - undergroundDepth);
                if (lm != null)
                {
                    entRamp.UseLaneRange = true;
                    entRamp.MinLane = Mathf.Clamp(Mathf.Min(holeMinLane, holeMaxLane), 0, lm.LaneCount - 1);
                    entRamp.MaxLane = Mathf.Clamp(Mathf.Max(holeMinLane, holeMaxLane), 0, lm.LaneCount - 1);
                }
            }

            // 7) 입구 계단 (블록 여러 개) — 시각용, collider 없음.
            //    각 단 cube 두께 = stepY → 옆면이 다음 단과 연속되어 계단 모양 형성.
            //    마지막 단은 floor 안쪽으로 살짝 들어가지만 floor 두께(1m)가 가림.
            //    UndergroundVisibilityZone의 hide 패턴(Underground_Wall_/Ceiling_/Entrance_Guard_)에
            //    매칭 안 되도록 "Entrance_Stair_" prefix 사용 → 진입 시에도 계단 그대로 보임.
            if (entranceStairCount > 0)
            {
                float stepX = holeLength / entranceStairCount;
                float stepY = undergroundDepth / entranceStairCount;
                for (int i = 0; i < entranceStairCount; i++)
                {
                    float centerX = (i + 0.5f) * stepX;
                    float topY    = -(i + 1f) * stepY;
                    float centerY = topY - stepY * 0.5f;
                    CreateCube(container.transform, $"Entrance_Stair_{i}",
                        center: new Vector3(centerX, centerY, 0f),
                        size:   new Vector3(stepX, stepY, holeWidth),
                        mat:    stairsMaterial,
                        groundLayer: -1);
                }
            }
        }

        // ── 헬퍼 ──────────────────────────────

        // groundLayer < 0 이면 시각 only (collider 제거). >= 0 이면 해당 layer로 두어 SphereCast가 잡게 함.
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
            else if (go.TryGetComponent<Collider>(out var col))
            {
                // 시각 only geometry도 카메라 occlusion raycast에는 잡혀야 한다.
                // trigger collider로 남겨 물리 이동은 막지 않되 투명화 대상 검출에만 사용한다.
                col.isTrigger = true;
            }

            ApplyMat(go, mat);
            go.hideFlags = HideFlags.DontSave;
        }

        private void CreateQuadOnFloor(Transform parent, string n, Vector3 center, float sizeX, float sizeZ, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = n;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // XY → XZ 평면
            go.transform.localScale    = new Vector3(sizeX, sizeZ, 1f);
            if (go.TryGetComponent<Collider>(out var col)) DestroySafe(col);
            ApplyMat(go, mat);
            go.hideFlags = HideFlags.DontSave;
        }

        // 비탈(ramp): cube를 z축 회전시켜 표현. startCenter는 ramp의 낮은 쪽 시작점 중심.
        // rise > 0 → +x로 가면서 +y로 올라가는 ramp (출구용).
        // rise < 0 → +x로 가면서 -y로 내려가는 ramp (입구용).
        // startCenter는 ramp의 시작 위치(+x 진입 지점)의 바닥 중심.
        private void CreateRamp(Transform parent, string n, Vector3 startCenter, float length, float rise, float width, Material mat, int groundLayer)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = n;
            go.transform.SetParent(parent, false);

            // atan2(rise, length): rise>0이면 양수(오르막), rise<0이면 음수(내리막).
            // Unity z축 +회전은 cube의 local +x를 world +y 쪽으로 보냄 → 부호 그대로 적용하면 cube가 (length, rise)와 같은 방향으로 누움.
            float rotZ  = Mathf.Atan2(rise, length) * Mathf.Rad2Deg;
            float hypot = Mathf.Sqrt(length * length + rise * rise);

            go.transform.localPosition = startCenter + new Vector3(length * 0.5f, rise * 0.5f, 0f);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, rotZ);
            go.transform.localScale    = new Vector3(hypot, 0.2f, width);

            if (groundLayer >= 0) go.layer = groundLayer;
            ApplyMat(go, mat);
            go.hideFlags = HideFlags.DontSave;
        }

        private static void ApplyMat(GameObject go, Material mat)
        {
            if (mat == null) mat = RuntimeFallbackMaterial;
            if (go.TryGetComponent<MeshRenderer>(out var mr)) mr.sharedMaterial = mat;
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
