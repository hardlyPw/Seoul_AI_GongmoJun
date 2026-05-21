using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Seoul.Network.Game
{
    public class RaceHUDController : MonoBehaviour
    {
        [Header("My Score")]
        [SerializeField] private TMP_Text myScoreText;

        [Header("Scoreboard (size 4)")]
        [SerializeField] private TMP_Text[] scoreboardEntries = new TMP_Text[4];

        [Header("Settings")]
        [SerializeField] private float refreshInterval = 0.2f;

        private float _refreshTimer;
        private readonly List<NetworkPlayer> _sorted = new();

        private void Update()
        {
            _refreshTimer += Time.deltaTime;
            if (_refreshTimer < refreshInterval) return;
            _refreshTimer = 0f;

            UpdateMyScore();
            UpdateScoreboard();
        }

        private void UpdateMyScore()
        {
            if (myScoreText == null) return;

            NetworkPlayer me = null;
            foreach (var p in NetworkPlayer.All)
            {
                if (p == null) continue;
                if (p.IsOwner) { me = p; break; }
            }

            myScoreText.text = me != null ? $"Score: {me.Score.Value}" : "Score: 0";
        }

        private void UpdateScoreboard()
        {
            _sorted.Clear();
            foreach (var p in NetworkPlayer.All)
            {
                if (p != null) _sorted.Add(p);
            }
            _sorted.Sort((a, b) => b.Score.Value.CompareTo(a.Score.Value));

            for (int i = 0; i < scoreboardEntries.Length; i++)
            {
                var entry = scoreboardEntries[i];
                if (entry == null) continue;

                if (i < _sorted.Count)
                {
                    var p    = _sorted[i];
                    string label = p.IsOwner ? $"P{p.OwnerClientId} (You)" : $"P{p.OwnerClientId}";
                    entry.text = $"{i + 1}. {label}  -  {p.Score.Value}";
                }
                else
                {
                    entry.text = $"{i + 1}. -";
                }
            }
        }
    }
}
