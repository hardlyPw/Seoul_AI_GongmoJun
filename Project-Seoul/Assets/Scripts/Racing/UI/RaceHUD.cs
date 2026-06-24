using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RaceHUD : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerController player;
    [SerializeField] private ItemInventory    inventory;

    [Header("UI Elements")]
    [SerializeField] private Slider          staminaBar;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI itemText;
    [SerializeField] private TextMeshProUGUI stateText;

    private void Start()
    {
        AutoPositionUI();
    }

    private void AutoPositionUI()
    {
        // 스태미나 바 - 하단 중앙
        if (staminaBar != null)
        {
            var rt = staminaBar.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.25f, 0f);
            rt.anchorMax = new Vector2(0.75f, 0f);
            rt.anchoredPosition = new Vector2(0f, 60f);
            rt.sizeDelta        = new Vector2(0f, 30f);
        }

        // 점수 - 우측 상단
        if (scoreText != null)
        {
            var rt = scoreText.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-20f, -20f);
            rt.sizeDelta        = new Vector2(200f, 50f);
            scoreText.fontSize  = 24f;
            scoreText.alignment = TextAlignmentOptions.Right;
        }

        // 아이템 - 하단 좌측
        if (itemText != null)
        {
            var rt = itemText.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot     = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(20f, 100f);
            rt.sizeDelta        = new Vector2(200f, 50f);
            itemText.fontSize   = 20f;
        }

        // 상태 텍스트 - 중앙 상단
        if (stateText != null)
        {
            var rt = stateText.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -20f);
            rt.sizeDelta        = new Vector2(400f, 200f); // 멀티라인(3줄) 텍스트가 잘리지 않도록 높이/너비 대폭 확장
            stateText.fontSize  = 28f;
            stateText.alignment = TextAlignmentOptions.Center;
            stateText.overflowMode = TextOverflowModes.Overflow; // 텍스트 영역을 벗어나도 강제로 출력되도록 설정
        }
    }

    private void Update()
    {
        if (player == null)
        {
            // 로컬 플레이어 자동 연결 (멀티플레이 동적 생성 환경 대응)
            foreach (var p in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            {
                if (p.TryGetComponent<Unity.Netcode.NetworkObject>(out var netObj))
                {
                    if (netObj.IsOwner) { player = p; break; }
                }
                else { player = p; break; }
            }
            if (player == null) return;
            Debug.Log($"[RaceHUD] 로컬 플레이어 자동 바인딩 완료: {player.name}");
        }

        if (staminaBar != null)
            staminaBar.value = player.Stamina / player.MaxStamina;

        if (scoreText != null && ScoreManager.Instance != null)
            scoreText.text = $"SCORE\n{ScoreManager.Instance.GetScore(player)}";

        if (itemText != null)
            itemText.text = (inventory != null && inventory.HasItem)
                ? $"ITEM: {inventory.HeldItem.ItemName}"
                : "ITEM: 없음";

        if (stateText == null)
        {
            // 인스펙터에서 stateText가 누락되었을 경우 자식 오브젝트에서 자동 탐색 시도
            foreach (var tmp in GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (tmp.name.Contains("State") || tmp.name.Contains("state"))
                {
                    stateText = tmp;
                    Debug.Log($"[RaceHUD] stateText 자동 탐색 및 바인딩 완료: {tmp.name}");
                    break;
                }
            }
        }

        if (stateText != null)
        {
            if (!stateText.gameObject.activeSelf) stateText.gameObject.SetActive(true);

            if (player.IsFallen)         stateText.text = "넘어짐!";
            else if (player.IsAirborne)
            {
                if (player.AirborneState.IsQTESuccess)
                    stateText.text = "QTE 성공!\n(+30pt)";
                else
                    stateText.text = $"[QTE 묘기]\n입력 키: {player.AirborneState.CurrentRequiredKey}\n성공: {player.AirborneState.SuccessCount}/5";
                
                Debug.Log($"[RaceHUD] 체공 중 UI 표시 업데이트 성공: {stateText.text}");
            }
            else if (player.IsSprinting) stateText.text = "SPRINT";
            else                          stateText.text = "";
        }
        else
        {
            if (player.IsAirborne)
            {
                Debug.LogWarning("[RaceHUD] 플레이어가 체공 중이나 stateText가 null이어서 UI에 표시하지 못했습니다. RaceHUD 프리팹 인스펙터에서 StateText를 연결해주세요.");
            }
        }
    }
}
