using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ResultScreen : MonoBehaviour
{
    [SerializeField] private GameObject       panel;
    [SerializeField] private TextMeshProUGUI  resultText;
    [SerializeField] private Button           restartButton;

    private void Start()
    {
        panel.SetActive(false);
        StageManager.Instance.OnAllPlayersFinished.AddListener(ShowResult);
        restartButton.onClick.AddListener(Restart);
    }

    private void OnDestroy()
    {
        if (StageManager.Instance != null)
            StageManager.Instance.OnAllPlayersFinished.RemoveListener(ShowResult);
    }

    private void ShowResult()
    {
        panel.SetActive(true);
        Time.timeScale = 0f;

        var finished = StageManager.Instance.FinishedPlayers;
        var sb       = new StringBuilder();

        string[] medals = { "1위", "2위", "3위" };

        for (int i = 0; i < finished.Count; i++)
        {
            var    player = finished[i];
            int    score  = ScoreManager.Instance?.GetScore(player) ?? 0;
            string rank   = i < medals.Length ? medals[i] : $"{i + 1}위";
            sb.AppendLine($"{rank}  {player.name}  {score}점");
        }

        resultText.text = sb.ToString();
    }

    private void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
