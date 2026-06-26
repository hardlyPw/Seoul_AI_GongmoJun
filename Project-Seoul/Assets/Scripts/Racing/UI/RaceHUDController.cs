using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Seoul.Network.Game
{
    public class RaceHUDController : MonoBehaviour
    {
#pragma warning disable 0649
        [System.Serializable]
        private struct ItemSpriteEntry
        {
            public ItemType type;
            public Sprite   sprite;
        }
#pragma warning restore 0649

        [Header("My Score")]
        [SerializeField] private TMP_Text myScoreText;

        [Header("My Item (Icon)")]
        [SerializeField] private Image             itemIcon;
        [SerializeField] private ItemSpriteEntry[] itemSprites;
        [SerializeField] private Vector2           itemSlotAnchoredPosition = new Vector2(20f, -140f);
        [SerializeField] private Vector2           itemSlotSize = new Vector2(110f, 110f);
        [SerializeField] private float             itemIconPadding = 12f;

        [Header("Scoreboard (size 4)")]
        [SerializeField] private TMP_Text[] scoreboardEntries = new TMP_Text[4];

        [Header("Weather")]
        [SerializeField] private TMP_Text weatherText;

        [Header("Stamina Bar")]
        [SerializeField] private Vector2 staminaBarAnchoredPosition = new Vector2(0f, 46f);
        [SerializeField] private Vector2 staminaBarSize = new Vector2(380f, 34f);

        [Header("Settings")]
        [SerializeField] private float refreshInterval = 0.2f;

        [Header("QTE UI (Optional)")]
        [SerializeField] private TMP_Text qteStateText;
        [SerializeField] private TMP_FontAsset koreanFont; // 한글 폰트 에셋 직접 할당 슬롯

        private float _refreshTimer;
        private readonly List<NetworkPlayer> _sorted = new();
        private Dictionary<ItemType, Sprite> _spriteMap;
        private Image _itemSlotBackground;
        private Image _staminaBarBackground;
        private Image _staminaBarFill;
        private static Sprite _roundedSlotSprite;
        private static Sprite _staminaBackgroundSprite;
        private static Sprite _staminaFillSprite;

        private void Awake()
        {
            EnsureItemSlot();
            EnsureStaminaBar();
            BuildSpriteMap();
            if (itemIcon != null) itemIcon.enabled = false;
        }

        private void EnsureItemSlot()
        {
            if (_itemSlotBackground == null)
            {
                var slotObject = new GameObject("ItemSlot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                slotObject.transform.SetParent(transform, false);

                var slotRect = slotObject.GetComponent<RectTransform>();
                slotRect.anchorMin = new Vector2(0f, 1f);
                slotRect.anchorMax = new Vector2(0f, 1f);
                slotRect.pivot = new Vector2(0f, 1f);
                slotRect.anchoredPosition = itemSlotAnchoredPosition;
                slotRect.sizeDelta = itemSlotSize;

                _itemSlotBackground = slotObject.GetComponent<Image>();
                _itemSlotBackground.sprite = GetRoundedSlotSprite();
                _itemSlotBackground.type = Image.Type.Sliced;
                _itemSlotBackground.color = Color.white;
                _itemSlotBackground.raycastTarget = false;
            }

            if (itemIcon == null)
            {
                var iconObject = new GameObject("ItemIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconObject.transform.SetParent(_itemSlotBackground.transform, false);

                itemIcon = iconObject.GetComponent<Image>();
                itemIcon.raycastTarget = false;
            }

            var iconRect = itemIcon.GetComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = new Vector2(-itemIconPadding * 2f, -itemIconPadding * 2f);
            itemIcon.preserveAspect = true;
        }

        private void EnsureStaminaBar()
        {
            if (_staminaBarBackground == null)
            {
                var barObject = new GameObject("StaminaBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                barObject.transform.SetParent(transform, false);

                var barRect = barObject.GetComponent<RectTransform>();
                barRect.anchorMin = new Vector2(0.5f, 0f);
                barRect.anchorMax = new Vector2(0.5f, 0f);
                barRect.pivot = new Vector2(0.5f, 0.5f);
                barRect.anchoredPosition = staminaBarAnchoredPosition;
                barRect.sizeDelta = staminaBarSize;

                _staminaBarBackground = barObject.GetComponent<Image>();
                _staminaBarBackground.sprite = GetStaminaBackgroundSprite();
                _staminaBarBackground.type = Image.Type.Sliced;
                _staminaBarBackground.raycastTarget = false;
            }

            if (_staminaBarFill == null)
            {
                var fillObject = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                fillObject.transform.SetParent(_staminaBarBackground.transform, false);

                var fillRect = fillObject.GetComponent<RectTransform>();
                fillRect.anchorMin = Vector2.zero;
                fillRect.anchorMax = Vector2.one;
                fillRect.pivot = new Vector2(0.5f, 0.5f);
                fillRect.anchoredPosition = Vector2.zero;
                fillRect.sizeDelta = new Vector2(-8f, -8f);

                _staminaBarFill = fillObject.GetComponent<Image>();
                _staminaBarFill.sprite = GetStaminaFillSprite();
                _staminaBarFill.type = Image.Type.Filled;
                _staminaBarFill.fillMethod = Image.FillMethod.Horizontal;
                _staminaBarFill.fillOrigin = (int)Image.OriginHorizontal.Left;
                _staminaBarFill.raycastTarget = false;
            }

            _staminaBarBackground.color = Color.white;
            _staminaBarFill.color = Color.white;
        }

        private static Sprite GetRoundedSlotSprite()
        {
            if (_roundedSlotSprite != null) return _roundedSlotSprite;

            const int size = 64;
            const int radius = 10;
            const int border = 4;
            Color32 clear = new Color32(0, 0, 0, 0);
            Color32 borderColor = new Color32(150, 150, 150, 255);
            Color32 fillColor = new Color32(8, 8, 8, 230);

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Runtime_ItemSlotRounded",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool inOuter = IsInsideRoundedRect(x, y, size, radius);
                    bool inInner = IsInsideRoundedRect(x, y, size, radius - border, border);
                    texture.SetPixel(x, y, !inOuter ? clear : inInner ? fillColor : borderColor);
                }
            }

            texture.Apply(false, true);

            _roundedSlotSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
            _roundedSlotSprite.name = "Runtime_ItemSlotRounded";
            _roundedSlotSprite.hideFlags = HideFlags.HideAndDontSave;
            return _roundedSlotSprite;
        }

        private static Sprite GetStaminaBackgroundSprite()
        {
            if (_staminaBackgroundSprite != null) return _staminaBackgroundSprite;

            const int width = 256;
            const int height = 40;
            const int radius = 16;
            const int whiteBorder = 3;
            const int darkBorder = 6;
            Color32 clear = new Color32(0, 0, 0, 0);
            Color32 white = new Color32(245, 245, 242, 255);
            Color32 border = new Color32(25, 25, 25, 255);
            Color32 innerTop = new Color32(24, 24, 24, 245);
            Color32 innerBottom = new Color32(55, 55, 55, 245);

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Runtime_StaminaBackground",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!IsInsideRoundedRect(x, y, width, height, radius))
                    {
                        texture.SetPixel(x, y, clear);
                        continue;
                    }

                    if (!IsInsideRoundedRect(x, y, width, height, radius - whiteBorder, whiteBorder))
                    {
                        texture.SetPixel(x, y, white);
                        continue;
                    }

                    if (!IsInsideRoundedRect(x, y, width, height, radius - darkBorder, darkBorder))
                    {
                        texture.SetPixel(x, y, border);
                        continue;
                    }

                    float t = Mathf.InverseLerp(darkBorder, height - darkBorder, y);
                    texture.SetPixel(x, y, Color32.Lerp(innerBottom, innerTop, t));
                }
            }

            texture.Apply(false, true);
            _staminaBackgroundSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
            _staminaBackgroundSprite.name = "Runtime_StaminaBackground";
            _staminaBackgroundSprite.hideFlags = HideFlags.HideAndDontSave;
            return _staminaBackgroundSprite;
        }

        private static Sprite GetStaminaFillSprite()
        {
            if (_staminaFillSprite != null) return _staminaFillSprite;

            const int width = 256;
            const int height = 32;
            const int radius = 14;
            const int border = 3;
            Color32 clear = new Color32(0, 0, 0, 0);
            Color32 darkBorder = new Color32(40, 40, 34, 255);
            Color32 yellow = new Color32(255, 234, 24, 255);
            Color32 orange = new Color32(255, 93, 45, 255);
            Color32 shine = new Color32(255, 255, 210, 120);

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Runtime_StaminaFill",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!IsInsideRoundedRect(x, y, width, height, radius))
                    {
                        texture.SetPixel(x, y, clear);
                        continue;
                    }

                    if (!IsInsideRoundedRect(x, y, width, height, radius - border, border))
                    {
                        texture.SetPixel(x, y, darkBorder);
                        continue;
                    }

                    float xT = Mathf.InverseLerp(border, width - border, x);
                    Color baseColor = Color32.Lerp(yellow, orange, xT);
                    if (y > height - 8 && y < height - 3)
                    {
                        baseColor = Color.Lerp(baseColor, shine, 0.45f);
                    }
                    texture.SetPixel(x, y, baseColor);
                }
            }

            texture.Apply(false, true);
            _staminaFillSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
            _staminaFillSprite.name = "Runtime_StaminaFill";
            _staminaFillSprite.hideFlags = HideFlags.HideAndDontSave;
            return _staminaFillSprite;
        }

        private static bool IsInsideRoundedRect(int x, int y, int size, int radius, int inset = 0)
        {
            int min = inset;
            int max = size - 1 - inset;
            if (x < min || x > max || y < min || y > max) return false;

            int cornerRadius = Mathf.Max(0, radius);
            int left = min + cornerRadius;
            int right = max - cornerRadius;
            int bottom = min + cornerRadius;
            int top = max - cornerRadius;

            int nearestX = Mathf.Clamp(x, left, right);
            int nearestY = Mathf.Clamp(y, bottom, top);
            int dx = x - nearestX;
            int dy = y - nearestY;
            return dx * dx + dy * dy <= cornerRadius * cornerRadius;
        }

        private static bool IsInsideRoundedRect(int x, int y, int width, int height, int radius, int inset = 0)
        {
            int minX = inset;
            int maxX = width - 1 - inset;
            int minY = inset;
            int maxY = height - 1 - inset;
            if (x < minX || x > maxX || y < minY || y > maxY) return false;

            int cornerRadius = Mathf.Max(0, radius);
            int left = minX + cornerRadius;
            int right = maxX - cornerRadius;
            int bottom = minY + cornerRadius;
            int top = maxY - cornerRadius;

            int nearestX = Mathf.Clamp(x, left, right);
            int nearestY = Mathf.Clamp(y, bottom, top);
            int dx = x - nearestX;
            int dy = y - nearestY;
            return dx * dx + dy * dy <= cornerRadius * cornerRadius;
        }

        private void BuildSpriteMap()
        {
            _spriteMap = new Dictionary<ItemType, Sprite>();
            if (itemSprites == null) return;
            foreach (var e in itemSprites)
            {
                if (e.sprite != null) _spriteMap[e.type] = e.sprite;
            }
        }

        private void Update()
        {
            UpdateStaminaBar();
            UpdateQTEUI(); // QTE는 즉각적인 피드백이 중요하므로 타이머와 무관하게 매 프레임 업데이트

            _refreshTimer += Time.deltaTime;
            if (_refreshTimer < refreshInterval) return;
            _refreshTimer = 0f;

            UpdateMyScore();
            UpdateMyItem();
            UpdateScoreboard();
            UpdateWeather();
        }

        private void UpdateStaminaBar()
        {
            EnsureStaminaBar();
            if (_staminaBarBackground == null || _staminaBarFill == null) return;

            var me = FindLocalPlayer();
            if (me == null || !me.TryGetComponent<PlayerController>(out var player))
            {
                _staminaBarBackground.enabled = false;
                _staminaBarFill.enabled = false;
                return;
            }

            _staminaBarBackground.enabled = true;
            _staminaBarFill.enabled = true;

            float ratio = player.MaxStamina > 0f ? Mathf.Clamp01(player.Stamina / player.MaxStamina) : 0f;
            _staminaBarFill.fillAmount = ratio;
            _staminaBarFill.color = Color.white;
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
            EnsureItemSlot();
            if (itemIcon == null) return;

            var me = FindLocalPlayer();
            ItemType currentItem = ItemType.None;
            if (me != null && me.TryGetComponent<NetworkItemInventory>(out var inventory))
            {
                currentItem = inventory.currentItem.Value;
            }

            if (currentItem == ItemType.None || !_spriteMap.TryGetValue(currentItem, out var sprite))
            {
                itemIcon.enabled = false;
                return;
            }

            itemIcon.sprite  = sprite;
            itemIcon.enabled = true;
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

        private void UpdateScoreboard()
        {
            _sorted.Clear();
            foreach (var p in NetworkPlayer.All)
            {
                if (p != null) _sorted.Add(p);
            }

            _sorted.Sort((a, b) =>
            {
                int scoreA = a.TryGetComponent<PlayerScore>(out var sA) ? sA.Score.Value : 0;
                int scoreB = b.TryGetComponent<PlayerScore>(out var sB) ? sB.Score.Value : 0;
                return scoreB.CompareTo(scoreA);
            });

            for (int i = 0; i < scoreboardEntries.Length; i++)
            {
                var entry = scoreboardEntries[i];
                if (entry == null) continue;

                if (i < _sorted.Count)
                {
                    var p = _sorted[i];

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
