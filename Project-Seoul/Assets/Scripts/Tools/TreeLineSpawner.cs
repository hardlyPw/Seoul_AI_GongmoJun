using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

// 트리 전용 일렬 스포너 — 여러 트리 prefab 을 랜덤하게 골라 일정 간격으로 배치.
// 가로수 효과용. PrefabLineSpawner 와 동일한 흐름이지만 prefab 배열 중 랜덤 선택이 추가됨.
// 사용법:
//  1. 빈 GameObject 만들고 이 컴포넌트 붙임 (보도 시작 지점에 위치)
//  2. Tree Prefabs 배열에 트리 3종 드래그
//  3. Count / Spacing / 랜덤 옵션 설정
//  4. 컴포넌트 헤더 우클릭 → "Spawn"
public class TreeLineSpawner : MonoBehaviour
{
    [Header("Tree Prefabs — 이 중 하나를 랜덤 선택")]
    public GameObject[] treePrefabs;

    [Header("Layout")]
    [Tooltip("총 트리 개수")]
    public int count = 20;
    [Tooltip("인접 트리 사이의 local 위치 차이 (보통 X 방향)")]
    public Vector3 spacing = new Vector3(8f, 0f, 0f);
    [Tooltip("첫 번째 트리의 local 시작 위치")]
    public Vector3 startOffset = Vector3.zero;

    [Header("Base Scale")]
    [Tooltip("모든 인스턴스의 기본 스케일. prefab 크기가 너무 작으면 여기 큰 값 (예: 900) 입력.")]
    public Vector3 baseScale = Vector3.one;

    [Header("자연스러운 변화 (0 이면 효과 없음)")]
    [Tooltip("z 축으로 ±값 만큼 랜덤 흔들기 (보도 폭 내 자연 배치)")]
    public float zJitter = 0.3f;
    [Tooltip("y 축 랜덤 회전 폭 (도). 180 이면 0~360 어느 방향이든 향함")]
    public float randomYRotation = 180f;
    [Tooltip("baseScale 에 곱해질 랜덤 배율. (1, 1) 이면 변화 없음. (0.85, 1.15) 면 ±15%.")]
    public Vector2 randomScaleRange = new Vector2(0.85f, 1.15f);

    [Header("Reproducibility")]
    [Tooltip("같은 seed = 매번 같은 배치 결과. 0 이면 매번 새 random.")]
    public int randomSeed = 0;

    [ContextMenu("Spawn")]
    public void Spawn()
    {
#if UNITY_EDITOR
        // null 항목 제외
        var valid = new System.Collections.Generic.List<GameObject>();
        if (treePrefabs != null)
            foreach (var p in treePrefabs) if (p != null) valid.Add(p);

        if (valid.Count == 0)
        {
            Debug.LogWarning("[TreeLineSpawner] treePrefabs 비어있거나 모두 null.", this);
            return;
        }

        ClearChildren();

        var rng = randomSeed != 0 ? new System.Random(randomSeed) : new System.Random();

        for (int i = 0; i < count; i++)
        {
            var prefab = valid[rng.Next(valid.Count)];
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, transform);
            if (go == null) continue;

            Vector3 pos = startOffset + spacing * i;
            if (zJitter > 0f) pos.z += (float)(rng.NextDouble() * 2 - 1) * zJitter;
            go.transform.localPosition = pos;

            if (randomYRotation > 0f)
            {
                float yaw = (float)(rng.NextDouble() * 2 - 1) * randomYRotation;
                go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            }

            float scaleFactor = 1f;
            if (randomScaleRange.x > 0f
                && (randomScaleRange.x != 1f || randomScaleRange.y != 1f))
            {
                float t = (float)rng.NextDouble();
                scaleFactor = Mathf.Lerp(randomScaleRange.x, randomScaleRange.y, t);
            }
            go.transform.localScale = baseScale * scaleFactor;

            Undo.RegisterCreatedObjectUndo(go, "Spawn Tree Line");
        }

        EditorUtility.SetDirty(this);
        Debug.Log($"[TreeLineSpawner] {count}개 트리 배치 완료 (prefab {valid.Count}종).", this);
#endif
    }

    [ContextMenu("Clear")]
    public void ClearChildren()
    {
#if UNITY_EDITOR
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Undo.DestroyObjectImmediate(transform.GetChild(i).gameObject);
        }
#endif
    }
}
