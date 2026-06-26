using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

// 에디터 배치 도구 — 같은 prefab 을 일정 간격으로 일렬 배치.
// 사용법:
//  1. 빈 GameObject 만들고 이 컴포넌트 붙임
//  2. 인스펙터에서 Prefab, Count, Spacing, Random 옵션 설정
//  3. 컴포넌트 헤더 우클릭 → "Spawn" 클릭
//  4. 결과 마음에 안 들면 "Clear" → 파라미터 조정 → 다시 Spawn
// 자식으로 생성되므로 Spawner GameObject 통째로 옮기면 전체 일괄 이동됨.
public class PrefabLineSpawner : MonoBehaviour
{
    [Header("필수")]
    [Tooltip("배치할 prefab")]
    public GameObject prefab;
    [Tooltip("총 몇 개 배치할지")]
    public int count = 10;
    [Tooltip("인접 인스턴스 사이의 local 위치 차이")]
    public Vector3 spacing = new Vector3(5f, 0f, 0f);

    [Header("선택 — 시작 위치 오프셋")]
    [Tooltip("첫 번째 인스턴스의 local 시작 위치")]
    public Vector3 startOffset = Vector3.zero;

    [Header("Base Transform")]
    [Tooltip("모든 인스턴스의 기본 스케일. prefab 이 너무 작으면 큰 값 (예: 300) 입력.")]
    public Vector3 baseScale = Vector3.one;
    [Tooltip("모든 인스턴스의 기본 회전 (Euler 각). 가드레일처럼 진행방향에 맞춰야 할 때 사용.")]
    public Vector3 baseRotation = Vector3.zero;

    [Header("선택 — 자연스러운 변화")]
    [Tooltip("매 인스턴스마다 z 축으로 ±이 값 만큼 랜덤 오프셋 (보도 폭 내 자연 배치)")]
    public float zJitter = 0f;
    [Tooltip("매 인스턴스마다 ±이 값 만큼 y축 랜덤 회전 (도/°)")]
    public float randomYRotation = 0f;
    [Tooltip("매 인스턴스 스케일을 이 범위 안에서 랜덤. (1, 1) 이면 변화 없음")]
    public Vector2 randomScaleRange = new Vector2(1f, 1f);

    [ContextMenu("Spawn")]
    public void Spawn()
    {
#if UNITY_EDITOR
        if (prefab == null)
        {
            Debug.LogWarning("[PrefabLineSpawner] prefab 비어있음.", this);
            return;
        }

        ClearChildren();

        for (int i = 0; i < count; i++)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, transform);
            if (go == null) continue;

            Vector3 pos = startOffset + spacing * i;
            if (zJitter > 0f) pos.z += Random.Range(-zJitter, zJitter);
            go.transform.localPosition = pos;

            float yawOffset = randomYRotation > 0f ? Random.Range(-randomYRotation, randomYRotation) : 0f;
            go.transform.localRotation = Quaternion.Euler(
                baseRotation.x,
                baseRotation.y + yawOffset,
                baseRotation.z);

            float scaleFactor = 1f;
            if (randomScaleRange.x > 0f && (randomScaleRange.x != 1f || randomScaleRange.y != 1f))
                scaleFactor = Random.Range(randomScaleRange.x, randomScaleRange.y);
            go.transform.localScale = baseScale * scaleFactor;

            Undo.RegisterCreatedObjectUndo(go, "Spawn Prefab Line");
        }

        EditorUtility.SetDirty(this);
        Debug.Log($"[PrefabLineSpawner] {count}개 배치 완료.", this);
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
