using System.Collections.Generic;
using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    private readonly Dictionary<PlayerController, int> _scores = new();

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void AddScore(PlayerController player, int amount)
    {
        if (!_scores.ContainsKey(player)) _scores[player] = 0;
        _scores[player] += amount;
        Debug.Log($"{player.name} 점수: {_scores[player]} (+{amount})");
    }

    public int GetScore(PlayerController player) =>
        _scores.TryGetValue(player, out int score) ? score : 0;
}
