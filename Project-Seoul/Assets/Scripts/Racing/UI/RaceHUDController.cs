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

        [Header("QTE UI (Optional)")]
        [SerializeField] private TMP_Text qteStateText;

        private float _refreshTimer;
        private readonly List<NetworkPlayer> _sorted = new();

        private void Awake()
        {
            EnsureItemText();
        }

        private void Update()
        {
            UpdateQTEUI(); // QTE는 즉각적인 피드백이 중요하므로 타이머와 무관하게 매 프레임 업데이트

            _refreshTimer += Time.deltaTime;
            if (_refreshTimer < refreshInterval) return;
            _refreshTimer = 0f;

            UpdateMyScore();
            UpdateMyItem();
            UpdateScoreboard();
            UpdateWeather();
        }

        private void UpdateQTEUI()
        {
            NetworkPlayer me = null;
            foreach (var p in NetworkPlayer.All)
            {
                if (p == null) continue;
                if (p.IsOwner) { me = p; break; }
            }
            if (me == null) return;

            if (!me.TryGetComponent<PlayerController>(out var player)) return;

            // qteStateText가 없으면 런타임에 Canvas를 찾아 동적으로 생성하여 화면 중앙 상단에 배치
            if (qteStateText == null)
            {
                Canvas canvas = GetComponentInParent<Canvas>();
                if (canvas == null && myScoreText != null) canvas = myScoreText.GetComponentInParent<Canvas>();
                if (canvas != null)
                {
                    GameObject qteObj = new GameObject("QTE_StateText_Dynamic");
                    qteObj.transform.SetParent(canvas.transform, false);
                    var rt = qteObj.AddComponent<RectTransform>();
                    rt.anchorMin = new Vector2(0.5f, 0.8f);
                    rt.anchorMax = new Vector2(0.5f, 0.8f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = new Vector2(0f, 0f);
                    rt.sizeDelta = new Vector2(500f, 300f);

                    var tmp = qteObj.AddComponent<TextMeshProUGUI>();
                    tmp.fontSize = 36f;
                    tmp.alignment = TextAlignmentOptions.Center;
                    tmp.overflowMode = TextOverflowModes.Overflow;
                    tmp.text = "";
                    qteStateText = tmp;
                    Debug.Log("[RaceHUDController] QTE 전용 UI 텍스트 캔버스 중앙 상단에 동적 생성 완료!");
                }
            }

            if (qteStateText != null)
            {
                if (player.IsFallen) qteStateText.text = "<color=#FF0000>넘어짐!</color>";
                else if (player.IsAirborne)
                {
                    if (player.AirborneState.IsQTESuccess)
                        qteStateText.text = "<color=#00FF00>QTE 성공!</color>\n<size=28>(+30pt)</size>";
                    else
                        qteStateText.text = $"[QTE 묘기]\n입력 키: <color=#FFFF00>{player.AirborneState.CurrentRequiredKey}</color>\n<size=28>성공: {player.AirborneState.SuccessCount}/5</size>";
                }
                else
                {
                    qteStateText.text = "";
                }
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
