using TMPro;
using UnityEngine;

// 화면 밖 플레이어 위치를 말풍선 UI로 표시.
// Canvas (Screen Space - Overlay) 하위에 배치.
public class PlayerOffScreenIndicator : MonoBehaviour
{
    [SerializeField] private PlayerController  trackedPlayer;
    [SerializeField] private TextMeshProUGUI   distanceText;
    [SerializeField] private RectTransform     bubble;
    [SerializeField] private float             edgePadding = 60f;

    private Camera       _cam;
    private RectTransform _canvasRect;

    private void Start()
    {
        _cam        = Camera.main;
        _canvasRect = GetComponentInParent<Canvas>().GetComponent<RectTransform>();
    }

    private void Update()
    {
        if (trackedPlayer == null || _cam == null) return;

        Vector3 viewPos    = _cam.WorldToViewportPoint(trackedPlayer.transform.position);
        bool    isOffScreen = viewPos.z < 0 || viewPos.x < 0 || viewPos.x > 1 || viewPos.y < 0 || viewPos.y > 1;

        bubble.gameObject.SetActive(isOffScreen);
        if (!isOffScreen) return;

        // 화면 뒤에 있을 경우 반전
        if (viewPos.z < 0) viewPos.x = 1f - viewPos.x;

        // 스크린 좌표로 변환 후 가장자리에 클램프
        Vector2 screenPos = new Vector2(
            viewPos.x * Screen.width,
            viewPos.y * Screen.height);

        screenPos.x = Mathf.Clamp(screenPos.x, edgePadding, Screen.width  - edgePadding);
        screenPos.y = Mathf.Clamp(screenPos.y, edgePadding, Screen.height - edgePadding);

        // Canvas 로컬 좌표로 변환
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvasRect, screenPos, null, out Vector2 localPos);
        bubble.anchoredPosition = localPos;

        // 거리 표시
        if (distanceText != null)
        {
            float dist = Vector3.Distance(
                trackedPlayer.transform.position, _cam.transform.position);
            distanceText.text = $"{dist:F0}m";
        }
    }
}
