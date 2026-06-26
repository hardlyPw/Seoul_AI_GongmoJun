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
        [SerializeField] private TMP_FontAsset koreanFont; // 한글 폰트 에셋 직접 할당 슬롯

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
                    
                    // [한글 폰트 스마트 바인딩 로직]
                    if (koreanFont == null)
                    {
#if UNITY_EDITOR
                        // 에디터 환경일 경우, 인스펙터 할당이 누락되었어도 프로젝트 내 GmarketSans 한글 폰트를 다이렉트 로드하여 자동 해결
                        koreanFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/fonts/GmarketSansTTFBold SDF.asset");
                        if (koreanFont != null) Debug.Log("[RaceHUDController] AssetDatabase를 통해 GmarketSansTTFBold SDF 한글 폰트 자동 로드 완료!");
#endif
                    }

                    if (koreanFont != null)
                    {
                        tmp.font = koreanFont;
                        Debug.Log("[RaceHUDController] Korean Font (GmarketSans) 에셋 최종 적용 완료!");
                    }
                    else
                    {
                        // 1. 씬 내의 모든 텍스트 중 기본 폰트(Liberation)가 아닌 한글 지원 폰트(malgun, Gmarket 등)를 자동 탐색
                        foreach (var anyText in FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
                        {
                            if (anyText.font != null && !anyText.font.name.Contains("Liberation"))
                            {
                                tmp.font = anyText.font;
                                Debug.Log($"[RaceHUDController] 씬 내에서 한글 지원 추정 폰트 자동 발견 및 적용: {anyText.font.name}");
                                break;
                            }
                        }
                        
                        // 2. 그래도 못 찾았다면 myScoreText.font 적용 후 인스펙터 할당 안내 로그 출력
                        if (tmp.font == null || tmp.font.name.Contains("Liberation"))
                        {
                            if (myScoreText != null) tmp.font = myScoreText.font;
                            Debug.LogWarning("[RaceHUDController] 현재 씬의 모든 UI가 영문 기본 폰트(LiberationSans)를 사용 중이어서 한글이 깨질 수 있습니다. RaceHUDController 인스펙터의 'Korean Font' 슬롯에 한글 폰트(GmarketSans SDF 등)를 넣어주세요!");
                        }
                    }

                    tmp.fontSize = 36f;
                    tmp.alignment = TextAlignmentOptions.Center;
                    tmp.overflowMode = TextOverflowModes.Overflow;
                    tmp.text = "";
                    qteStateText = tmp;
                    Debug.Log("[RaceHUDController] QTE 전용 UI 텍스트 캔버스 중앙 상단에 동적 생성 완료 (한글 폰트 적용)!");
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
