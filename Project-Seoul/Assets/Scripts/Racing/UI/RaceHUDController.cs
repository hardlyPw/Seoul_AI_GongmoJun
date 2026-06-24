using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Seoul.Network.Game
{
    public class RaceHUDController : MonoBehaviour
    {
        [Header("My Score")]
        [SerializeField] private TMP_Text myScoreText;

        [Header("My Item")]
        [SerializeField] private TMP_Text itemText;

        [Header("Scoreboard (size 4)")]
        [SerializeField] private TMP_Text[] scoreboardEntries = new TMP_Text[4];

        [Header("Weather")]
        [SerializeField] private TMP_Text weatherText;

        [Header("Settings")]
        [SerializeField] private float refreshInterval = 0.2f;

        private float _refreshTimer;
        private readonly List<NetworkPlayer> _sorted = new();

        private void Awake()
        {
            EnsureItemText();
        }

        private void Update()
        {
            _refreshTimer += Time.deltaTime;
            if (_refreshTimer < refreshInterval) return;
            _refreshTimer = 0f;

            UpdateMyScore();
            UpdateMyItem();
            UpdateScoreboard();
            UpdateWeather();
        }

        private void EnsureItemText()
        {
            if (itemText != null) return;

            var itemObject = new GameObject("ItemText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            itemObject.transform.SetParent(transform, false);

            var rect = itemObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(20f, -140f);
            rect.sizeDelta = new Vector2(500f, 60f);

            itemText = itemObject.GetComponent<TextMeshProUGUI>();
            itemText.fontSize = 36f;
            itemText.alignment = TextAlignmentOptions.Left;
            itemText.color = Color.white;
            itemText.raycastTarget = false;
            itemText.text = "Item: -";
        }

        private void UpdateWeather()
        {
            if (weatherText == null) return;

            weatherText.text = WeatherGimmick.Instance != null
                ? $"Weather: {WeatherGimmick.Instance.Current.Value}"
                : "";
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

            // [���� �Ϸ�] �� NetworkPlayer ������Ʈ���� PlayerScore ������Ʈ�� ã�� ���� ���
            if (me != null && me.TryGetComponent<PlayerScore>(out var playerScore))
            {
                myScoreText.text = $"Score: {playerScore.Score.Value}";
            }
            else
            {
                myScoreText.text = "Score: 0";
            }
        }

        private void UpdateMyItem()
        {
            if (itemText == null) return;

            var me = FindLocalPlayer();
            if (me != null && me.TryGetComponent<NetworkItemInventory>(out var inventory))
            {
                itemText.text = $"Item: {GetItemDisplayName(inventory.currentItem.Value)}";
            }
            else
            {
                itemText.text = "Item: -";
            }
        }

        private static NetworkPlayer FindLocalPlayer()
        {
            foreach (var p in NetworkPlayer.All)
            {
                if (p == null) continue;
                if (p.IsOwner) return p;
            }

            return null;
        }

        private static string GetItemDisplayName(ItemType item)
        {
            return item switch
            {
                ItemType.None => "-",
                ItemType.Coffee => "Coffee",
                ItemType.AlarmClock => "Alarm Clock",
                ItemType.Coin => "Coin",
                ItemType.Kickboard => "Kickboard",
                ItemType.Taxi => "Taxi",
                _ => item.ToString()
            };
        }

        private void UpdateScoreboard()
        {
            _sorted.Clear();
            foreach (var p in NetworkPlayer.All)
            {
                if (p != null) _sorted.Add(p);
            }

            // [���� �Ϸ�] �� �÷��̾ ���� PlayerScore�� Score.Value ���� ���Ͽ� ����
            _sorted.Sort((a, b) =>
            {
                int scoreA = a.TryGetComponent<PlayerScore>(out var sA) ? sA.Score.Value : 0;
                int scoreB = b.TryGetComponent<PlayerScore>(out var sB) ? sB.Score.Value : 0;
                return scoreB.CompareTo(scoreA); // �������� ����
            });

            for (int i = 0; i < scoreboardEntries.Length; i++)
            {
                var entry = scoreboardEntries[i];
                if (entry == null) continue;

                if (i < _sorted.Count)
                {
                    var p = _sorted[i];
                    
                    // [���� �Ϸ�] ȭ�鿡 ǥ���� ���� �÷��̾��� ���� ���� �Ľ�
                    int finalScore = p.TryGetComponent<PlayerScore>(out var s) ? s.Score.Value : 0;
                    
                    string label = p.IsOwner ? $"P{p.OwnerClientId} (You)" : $"P{p.OwnerClientId}";
                    entry.text = $"{i + 1}. {label}  -  {finalScore}";
                }
                else
                {
                    entry.text = $"{i + 1}. -";
                }
            }
        }
    }
}
