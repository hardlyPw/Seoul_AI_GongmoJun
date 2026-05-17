using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("UI Elements")]
    public Text scoreText;
    public Text staminaText;
    public Text itemText;

    public void UpdateUI(int score, float stamina, ItemType item)
    {
        if (scoreText != null)
            scoreText.text = "Score: " + score;

        if (staminaText != null)
            staminaText.text = "Stamina: " + Mathf.RoundToInt(stamina);

        if (itemText != null)
            itemText.text = "Item: " + item.ToString();
    }
}