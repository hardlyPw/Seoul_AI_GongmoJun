using UnityEngine;
using TMPro;

namespace Seoul.Network.Game
{
    public class CafeGimmickUI : MonoBehaviour
    {
        public static CafeGimmickUI Instance { get; private set; }

        [Header("Interaction Prompt (E Key)")]
        [SerializeField] private GameObject promptPanel;
        [SerializeField] private TMP_Text promptText;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            if (promptPanel != null) promptPanel.SetActive(false);
        }

        // 카페 구역 진입 시 E 키 가이드 표시
        public void ShowPrompt(string itemName)
        {
            if (promptPanel == null) return;
            
            if (promptText != null)
            {
                promptText.text = $"[E]를 눌러 {itemName} 받기";
            }
            promptPanel.SetActive(true);
        }

        // 카페 구역 퇴장 혹은 아이템 획득 시 가이드 숨김
        public void HidePrompt()
        {
            if (promptPanel != null) promptPanel.SetActive(false);
        }
    }
}