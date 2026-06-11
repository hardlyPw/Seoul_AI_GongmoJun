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
            rt.sizeDelta        = new Vector2(200f, 50f);
            stateText.fontSize  = 24f;
            stateText.alignment = TextAlignmentOptions.Center;
        }
    }

    private void Update()
    {
        if (player == null) return;

        if (staminaBar != null)
            staminaBar.value = player.Stamina / player.MaxStamina;

        if (scoreText != null && ScoreManager.Instance != null)
            scoreText.text = $"SCORE\n{ScoreManager.Instance.GetScore(player)}";

        if (itemText != null)
            itemText.text = (inventory != null && inventory.HasItem)
                ? $"ITEM: {inventory.HeldItem.ItemName}"
                : "ITEM: 없음";

        if (stateText != null)
        {
            if (player.IsFallen)         stateText.text = "넘어짐!";
            else if (player.IsSprinting) stateText.text = "SPRINT";
            else                          stateText.text = "";
        }
    }
}
