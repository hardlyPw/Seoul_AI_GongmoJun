using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Seoul.Network.Game
{
    // 검은 패널의 오→왼 슬라이드(어두워짐), 왼→오 슬라이드(밝아짐) 와이프 전환.
    // RuntimeInitializeOnLoadMethod로 앱 시작 시 자동 생성. 캔버스는 코드로 구성.
    // - 로컬 씬 로드: SceneTransition.Load(name) — 페이드아웃 후 로드, sceneLoaded 후 자동 페이드인
    // - NGO 씬 로드: NetworkManager.SceneManager.OnLoad 후킹해 페이드 끝날 때까지 씬 활성화 지연
    public class SceneTransition : MonoBehaviour
    {
        public static SceneTransition Instance { get; private set; }

        private const float Duration = 0.4f;

        private RectTransform _canvasRect;
        private RectTransform _panel;
        private bool _ngoSubscribed   = false;
        private bool _isTransitioning = false;

        public bool IsTransitioning => _isTransitioning;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoCreate()
        {
            if (Instance != null) return;
            var go = new GameObject("SceneTransition");
            go.AddComponent<SceneTransition>();
        }

        public static void Load(string sceneName)
        {
            if (Instance == null)
            {
                Debug.LogWarning("[SceneTransition] Instance null — falling back to direct LoadScene");
                SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
                return;
            }
            if (Instance._isTransitioning) return;
            Instance.LoadSceneLocal(sceneName);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            BuildCanvas();
            SetPanelOffsetRight();

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_ngoSubscribed
                && NetworkManager.Singleton != null
                && NetworkManager.Singleton.SceneManager != null)
            {
                NetworkManager.Singleton.SceneManager.OnLoad -= OnNGOLoad;
            }
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            bool ngoAvailable = NetworkManager.Singleton != null
                                && NetworkManager.Singleton.SceneManager != null;
            if (ngoAvailable && !_ngoSubscribed)
            {
                NetworkManager.Singleton.SceneManager.OnLoad += OnNGOLoad;
                _ngoSubscribed = true;
            }
            else if (!ngoAvailable && _ngoSubscribed)
            {
                _ngoSubscribed = false;
            }
        }

        private void BuildCanvas()
        {
            var canvasGO = new GameObject("FadeCanvas");
            canvasGO.transform.SetParent(transform, false);

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;

            _canvasRect = canvasGO.GetComponent<RectTransform>();

            var panelGO = new GameObject("Panel");
            panelGO.transform.SetParent(canvasGO.transform, false);

            var img = panelGO.AddComponent<Image>();
            img.color         = Color.black;
            img.raycastTarget = false;

            _panel = panelGO.GetComponent<RectTransform>();
            _panel.anchorMin = new Vector2(0f, 0f);
            _panel.anchorMax = new Vector2(1f, 1f);
            _panel.offsetMin = Vector2.zero;
            _panel.offsetMax = Vector2.zero;
        }

        private float ScreenW => Screen.width > 0 ? Screen.width : 1920f;

        private void SetPanelOffsetRight() => _panel.anchoredPosition = new Vector2(ScreenW, 0f);
        private void SetPanelCovered()     => _panel.anchoredPosition = Vector2.zero;

        private void LoadSceneLocal(string sceneName)
        {
            _isTransitioning = true;
            StartCoroutine(FadeOutThenLoadLocalCoroutine(sceneName));
        }

        private IEnumerator FadeOutThenLoadLocalCoroutine(string sceneName)
        {
            yield return FadeOutCoroutine();
            // 한 프레임 양보해서 NGO/물리/NetworkVariable 큐가 정리될 시간 줌
            yield return null;
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            // sceneLoaded 핸들러가 페이드인 시작
        }

        private void OnNGOLoad(ulong clientId, string sceneName, LoadSceneMode mode, AsyncOperation asyncOp)
        {
            if (NetworkManager.Singleton == null) return;
            if (clientId != NetworkManager.Singleton.LocalClientId) return;
            if (asyncOp == null) return;

            _isTransitioning = true;
            StartCoroutine(NGOLoadCoroutine(asyncOp));
        }

        private IEnumerator NGOLoadCoroutine(AsyncOperation asyncOp)
        {
            asyncOp.allowSceneActivation = false;
            yield return FadeOutCoroutine();
            asyncOp.allowSceneActivation = true;
            // 이후 sceneLoaded 핸들러가 페이드인 시작
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // 패널이 화면을 덮고 있는 상태(0)면 페이드인 진행.
            // 아니면 (초기 부팅 등) 위치만 우측 오프스크린으로 리셋.
            if (Mathf.Abs(_panel.anchoredPosition.x) < ScreenW * 0.5f)
            {
                StartCoroutine(FadeInCoroutine());
            }
            else
            {
                SetPanelOffsetRight();
                _isTransitioning = false;
            }
        }

        // 오 → 왼: 패널이 오른쪽 밖에서 들어와 화면을 덮음 → 화면이 오른쪽부터 어두워짐
        private IEnumerator FadeOutCoroutine()
        {
            float w = ScreenW;
            Vector2 startPos = new Vector2(w, 0f);
            Vector2 endPos   = Vector2.zero;
            float t = 0f;
            while (t < Duration)
            {
                t += Time.unscaledDeltaTime;
                float lerp = Mathf.Clamp01(t / Duration);
                _panel.anchoredPosition = Vector2.LerpUnclamped(startPos, endPos, EaseInOut(lerp));
                yield return null;
            }
            _panel.anchoredPosition = endPos;
        }

        // 왼 → 오: 패널이 화면 위에서 오른쪽으로 빠짐 → 화면이 왼쪽부터 밝아짐
        private IEnumerator FadeInCoroutine()
        {
            float w = ScreenW;
            Vector2 startPos = Vector2.zero;
            Vector2 endPos   = new Vector2(w, 0f);
            float t = 0f;
            while (t < Duration)
            {
                t += Time.unscaledDeltaTime;
                float lerp = Mathf.Clamp01(t / Duration);
                _panel.anchoredPosition = Vector2.LerpUnclamped(startPos, endPos, EaseInOut(lerp));
                yield return null;
            }
            _panel.anchoredPosition = endPos;
            _isTransitioning = false;
        }

        private static float EaseInOut(float t) => t * t * (3f - 2f * t);
    }
}
