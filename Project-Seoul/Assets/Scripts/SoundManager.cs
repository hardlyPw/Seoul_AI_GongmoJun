using System.Collections.Generic;
using UnityEngine;

namespace Seoul
{
    public class SoundManager : MonoBehaviour
    {
        private static SoundManager _instance;
        public static SoundManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("SoundManager");
                    _instance = go.AddComponent<SoundManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        private Dictionary<string, AudioClip> _clipCache = new Dictionary<string, AudioClip>();
        private List<AudioSource> _sfxSources = new List<AudioSource>();
        private AudioSource _bgmSource;
        private int _initialPoolSize = 5;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            // Ensure SoundManager is created at startup
            var instance = Instance;
        }

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            // Initialize BGM Source
            var bgmGo = new GameObject("BGMSource");
            bgmGo.transform.SetParent(transform);
            _bgmSource = bgmGo.AddComponent<AudioSource>();
            _bgmSource.playOnAwake = false;
            _bgmSource.loop = true;
            _bgmSource.spatialBlend = 0f; // 2D 사운드
            _bgmSource.volume = 0.5f;     // BGM 볼륨 절반으로 조정

            // Initialize SFX Source Pool
            for (int i = 0; i < _initialPoolSize; i++)
            {
                CreateNewAudioSource();
            }

            // Play Main BGM
            PlayBGM("Melon-Beat-End-Roll");
        }

        private AudioSource CreateNewAudioSource()
        {
            var go = new GameObject("SFXSource");
            go.transform.SetParent(transform);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f; // 2D 사운드 (로컬 사용자 전용)
            _sfxSources.Add(source);
            return source;
        }

        public void PlayBGM(string clipName)
        {
            if (string.IsNullOrEmpty(clipName)) return;

            if (!_clipCache.TryGetValue(clipName, out var clip))
            {
                clip = Resources.Load<AudioClip>($"sounds/{clipName}");
                if (clip != null)
                {
                    _clipCache[clipName] = clip;
                }
                else
                {
                    Debug.LogWarning($"[SoundManager] BGM AudioClip not found in Resources: sounds/{clipName}");
                    return;
                }
            }

            if (_bgmSource.clip == clip && _bgmSource.isPlaying) return;

            _bgmSource.clip = clip;
            _bgmSource.Play();
        }

        public void PlaySFX(string clipName)
        {
            if (string.IsNullOrEmpty(clipName)) return;

            if (!_clipCache.TryGetValue(clipName, out var clip))
            {
                clip = Resources.Load<AudioClip>($"sounds/{clipName}");
                if (clip != null)
                {
                    _clipCache[clipName] = clip;
                }
                else
                {
                    Debug.LogWarning($"[SoundManager] AudioClip not found in Resources: sounds/{clipName}");
                    return;
                }
            }

            AudioSource availableSource = null;
            for (int i = 0; i < _sfxSources.Count; i++)
            {
                if (!_sfxSources[i].isPlaying)
                {
                    availableSource = _sfxSources[i];
                    break;
                }
            }

            if (availableSource == null)
            {
                availableSource = CreateNewAudioSource();
            }

            availableSource.clip = clip;
            availableSource.Play();
        }
    }
}
