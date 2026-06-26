using UnityEngine;

namespace Seoul.Network.Game
{
    // Subscribes to WeatherGimmick and toggles local visual/audio weather effects.
    public class WeatherVFXController : MonoBehaviour
    {
        [Header("Rain")]
        [Tooltip("Optional. If empty, a default rain particle system is generated at runtime.")]
        [SerializeField] private ParticleSystem rainParticlePrefab;
        [Tooltip("Optional. Used by the generated rain particle system.")]
        [SerializeField] private Material rainMaterial;
        [SerializeField] private AudioClip rainSoundClip;
        [Range(0f, 1f)]
        [SerializeField] private float rainVolume = 0.4f;
        [Tooltip("Width/depth of the rain emission area around the camera.")]
        [SerializeField] private float rainAreaSize = 30f;
        [Tooltip("Distance in front of the camera where screen rain is rendered.")]
        [SerializeField] private float rainDistance = 3f;

        [Header("Dust")]
        [Tooltip("Optional. If empty, a default dust particle system is generated at runtime.")]
        [SerializeField] private ParticleSystem dustParticlePrefab;
        [Tooltip("Optional. Used by the generated dust particle system.")]
        [SerializeField] private Material dustMaterial;
        [SerializeField] private AudioClip dustSoundClip;
        [Range(0f, 1f)]
        [SerializeField] private float dustVolume = 0.35f;
        [Tooltip("Width/depth of the dust emission area in front of the camera.")]
        [SerializeField] private float dustAreaSize = 25f;
        [Tooltip("Distance in front of the camera where dust haze is rendered.")]
        [SerializeField] private float dustDistance = 4f;
        [Tooltip("Yellow-brown fog color applied while dust is active.")]
        [SerializeField] private Color dustFogColor = new Color(0.78f, 0.65f, 0.38f);
        [Range(0f, 0.1f)]
        [Tooltip("Higher = thicker yellow haze.")]
        [SerializeField] private float dustFogDensity = 0.025f;

        [Header("Typhoon")]
        [Tooltip("Optional. If empty, a default wind streak particle system is generated at runtime.")]
        [SerializeField] private ParticleSystem typhoonParticlePrefab;
        [Tooltip("Optional. Used by the generated typhoon particle system.")]
        [SerializeField] private Material typhoonMaterial;
        [SerializeField] private AudioClip typhoonSoundClip;
        [Range(0f, 1f)]
        [SerializeField] private float typhoonVolume = 0.5f;
        [Tooltip("Width/depth of the wind emission area in front of the camera.")]
        [SerializeField] private float typhoonAreaSize = 32f;
        [Tooltip("Distance in front of the camera where wind streaks are rendered.")]
        [SerializeField] private float typhoonDistance = 3.5f;
        [Tooltip("Horizontal wind speed for typhoon streaks (m/s).")]
        [SerializeField] private Vector2 typhoonWindSpeed = new Vector2(22f, 34f);
        [Tooltip("Grey storm fog color applied while typhoon is active.")]
        [SerializeField] private Color typhoonFogColor = new Color(0.55f, 0.55f, 0.6f);
        [Range(0f, 0.1f)]
        [Tooltip("Higher = thicker grey haze.")]
        [SerializeField] private float typhoonFogDensity = 0.018f;

        private ParticleSystem _rainInstance;
        private AudioSource    _rainAudio;
        private ParticleSystem _dustInstance;
        private AudioSource    _dustAudio;
        private ParticleSystem _typhoonInstance;
        private AudioSource    _typhoonAudio;
        private bool _subscribed;
        private bool _warnedMissingCamera;

        // Stash original RenderSettings.fog state so toggling dust off restores the scene defaults.
        private bool    _fogStashed;
        private bool    _fogWasEnabled;
        private Color   _fogColorOriginal;
        private float   _fogDensityOriginal;
        private FogMode _fogModeOriginal;

        private void OnDestroy()
        {
            RestoreFog();
            if (_subscribed && WeatherGimmick.Instance != null)
            {
                WeatherGimmick.Instance.Current.OnValueChanged -= OnWeatherChanged;
            }
        }

        private void Update()
        {
            if (!_subscribed) TrySubscribe();

            if (_subscribed && WeatherGimmick.Instance != null)
            {
                var current = WeatherGimmick.Instance.Current.Value;
                if (_rainInstance == null && current == WeatherType.Rain) ApplyRain(true);
                if (_dustInstance == null && current == WeatherType.Dust) ApplyDust(true);
                if (_typhoonInstance == null && current == WeatherType.Typhoon) ApplyTyphoon(true);
            }
        }

        private void TrySubscribe()
        {
            if (WeatherGimmick.Instance == null) return;

            WeatherGimmick.Instance.Current.OnValueChanged += OnWeatherChanged;
            Apply(WeatherGimmick.Instance.Current.Value);
            _subscribed = true;
            Debug.Log($"[WeatherVFXController] Subscribed. Current weather: {WeatherGimmick.Instance.Current.Value}");
        }

        private void OnWeatherChanged(WeatherType prev, WeatherType next)
        {
            Debug.Log($"[WeatherVFXController] Weather changed: {prev} -> {next}");
            Apply(next);
        }

        private void Apply(WeatherType weather)
        {
            ApplyRain(weather == WeatherType.Rain);
            ApplyDust(weather == WeatherType.Dust);
            ApplyTyphoon(weather == WeatherType.Typhoon);

            if (weather != WeatherType.Dust && weather != WeatherType.Typhoon)
            {
                RestoreFog();
            }
        }

        // ---------- Rain ----------

        private void ApplyRain(bool on)
        {
            if (on)
            {
                EnsureRainInstance();
                if (_rainInstance != null && !_rainInstance.isPlaying) _rainInstance.Play(true);
                if (_rainAudio != null && !_rainAudio.isPlaying) _rainAudio.Play();
            }
            else
            {
                if (_rainInstance != null && _rainInstance.isPlaying)
                    _rainInstance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                if (_rainAudio != null && _rainAudio.isPlaying)
                    _rainAudio.Stop();
            }
        }

        private void EnsureRainInstance()
        {
            if (_rainInstance != null) return;

            var cam = Camera.main;
            if (cam == null)
            {
                if (!_warnedMissingCamera)
                {
                    Debug.LogWarning("[WeatherVFXController] Main camera not found. Rain VFX will retry when a camera tagged MainCamera exists.");
                    _warnedMissingCamera = true;
                }
                return;
            }

            _warnedMissingCamera = false;

            if (rainParticlePrefab != null)
            {
                _rainInstance = Instantiate(rainParticlePrefab, cam.transform);
                _rainInstance.transform.localPosition = new Vector3(0f, 0f, rainDistance);
                _rainInstance.transform.localRotation = Quaternion.identity;
            }
            else
            {
                _rainInstance = BuildDefaultRainParticle(cam.transform);
            }

            if (rainSoundClip != null && _rainAudio == null)
            {
                _rainAudio = _rainInstance.gameObject.AddComponent<AudioSource>();
                _rainAudio.clip = rainSoundClip;
                _rainAudio.loop = true;
                _rainAudio.volume = rainVolume;
                _rainAudio.spatialBlend = 0f;
                _rainAudio.playOnAwake = false;
            }
        }

        private ParticleSystem BuildDefaultRainParticle(Transform parent)
        {
            var go = new GameObject("Rain_Particle_Auto");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0f, rainDistance);
            go.transform.localRotation = Quaternion.identity;

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 0.35f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.028f);
            main.startColor = new Color(0.48f, 0.78f, 1f, 0.42f);
            main.maxParticles = 3500;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.gravityModifier = 0f;
            main.playOnAwake = false;

            var emission = ps.emission;
            emission.rateOverTime = 950f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(rainAreaSize, rainAreaSize, 0.1f);

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(-22f, -16f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.lengthScale = 0.65f;
            renderer.velocityScale = 0.025f;
            renderer.material = rainMaterial != null ? rainMaterial : BuildFallbackRainMaterial();
            ConfigureOverlayMaterial(renderer.material, new Color(0.48f, 0.78f, 1f, 0.42f));

            return ps;
        }

        private static Material BuildFallbackRainMaterial()
        {
            Shader shader =
                Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                Shader.Find("Particles/Standard Unlit") ??
                Shader.Find("Legacy Shaders/Particles/Alpha Blended") ??
                Shader.Find("Sprites/Default");

            if (shader == null)
            {
                Debug.LogWarning("[WeatherVFXController] Could not find a usable particle shader. Assign rainMaterial in the inspector.");
                return null;
            }

            var mat = new Material(shader);
            ConfigureOverlayMaterial(mat, new Color(0.48f, 0.78f, 1f, 0.42f));
            return mat;
        }

        // ---------- Dust ----------

        private void ApplyDust(bool on)
        {
            if (on)
            {
                EnsureDustInstance();
                if (_dustInstance != null && !_dustInstance.isPlaying) _dustInstance.Play(true);
                if (_dustAudio != null && !_dustAudio.isPlaying) _dustAudio.Play();
                EnableDustFog();
            }
            else
            {
                if (_dustInstance != null && _dustInstance.isPlaying)
                    _dustInstance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                if (_dustAudio != null && _dustAudio.isPlaying)
                    _dustAudio.Stop();
            }
        }

        private void EnsureDustInstance()
        {
            if (_dustInstance != null) return;

            var cam = Camera.main;
            if (cam == null)
            {
                if (!_warnedMissingCamera)
                {
                    Debug.LogWarning("[WeatherVFXController] Main camera not found. Dust VFX will retry when a camera tagged MainCamera exists.");
                    _warnedMissingCamera = true;
                }
                return;
            }

            _warnedMissingCamera = false;

            if (dustParticlePrefab != null)
            {
                _dustInstance = Instantiate(dustParticlePrefab, cam.transform);
                _dustInstance.transform.localPosition = new Vector3(0f, 0f, dustDistance);
                _dustInstance.transform.localRotation = Quaternion.identity;
            }
            else
            {
                _dustInstance = BuildDefaultDustParticle(cam.transform);
            }

            if (dustSoundClip != null && _dustAudio == null)
            {
                _dustAudio = _dustInstance.gameObject.AddComponent<AudioSource>();
                _dustAudio.clip = dustSoundClip;
                _dustAudio.loop = true;
                _dustAudio.volume = dustVolume;
                _dustAudio.spatialBlend = 0f;
                _dustAudio.playOnAwake = false;
            }
        }

        // Soft yellow-brown motes drifting across the screen with a gentle sideways wind.
        private ParticleSystem BuildDefaultDustParticle(Transform parent)
        {
            var go = new GameObject("Dust_Particle_Auto");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0f, dustDistance);
            go.transform.localRotation = Quaternion.identity;

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime   = new ParticleSystem.MinMaxCurve(2f, 4f);
            main.startSpeed      = 0f;
            main.startSize       = new ParticleSystem.MinMaxCurve(0.025f, 0.07f);
            main.startColor      = new Color(0.85f, 0.7f, 0.4f, 0.5f);
            main.maxParticles    = 1500;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.gravityModifier = 0f;
            main.playOnAwake     = false;

            var emission = ps.emission;
            emission.rateOverTime = 280f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale     = new Vector3(dustAreaSize, dustAreaSize * 0.7f, 0.1f);

            // Sideways wind + slight vertical jitter for a "blowing sand" feel.
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space   = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
            velocity.y = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment  = ParticleSystemRenderSpace.View;
            renderer.material   = dustMaterial != null ? dustMaterial : BuildFallbackDustMaterial();
            ConfigureOverlayMaterial(renderer.material, new Color(0.85f, 0.7f, 0.4f, 0.5f));

            return ps;
        }

        private static Material BuildFallbackDustMaterial()
        {
            Shader shader =
                Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                Shader.Find("Particles/Standard Unlit") ??
                Shader.Find("Legacy Shaders/Particles/Alpha Blended") ??
                Shader.Find("Sprites/Default");

            if (shader == null)
            {
                Debug.LogWarning("[WeatherVFXController] Could not find a usable particle shader. Assign dustMaterial in the inspector.");
                return null;
            }

            var mat = new Material(shader);
            ConfigureOverlayMaterial(mat, new Color(0.85f, 0.7f, 0.4f, 0.5f));
            return mat;
        }

        private static void ConfigureOverlayMaterial(Material mat, Color color)
        {
            if (mat == null) return;

            if (mat.HasProperty("_Color")) mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            if (mat.HasProperty("_ZTest")) mat.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        // ---------- Typhoon ----------

        private void ApplyTyphoon(bool on)
        {
            if (on)
            {
                EnsureTyphoonInstance();
                if (_typhoonInstance != null && !_typhoonInstance.isPlaying) _typhoonInstance.Play(true);
                if (_typhoonAudio != null && !_typhoonAudio.isPlaying) _typhoonAudio.Play();
                EnableTyphoonFog();
            }
            else
            {
                if (_typhoonInstance != null && _typhoonInstance.isPlaying)
                    _typhoonInstance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                if (_typhoonAudio != null && _typhoonAudio.isPlaying)
                    _typhoonAudio.Stop();
            }
        }

        private void EnsureTyphoonInstance()
        {
            if (_typhoonInstance != null) return;

            var cam = Camera.main;
            if (cam == null)
            {
                if (!_warnedMissingCamera)
                {
                    Debug.LogWarning("[WeatherVFXController] Main camera not found. Typhoon VFX will retry when a camera tagged MainCamera exists.");
                    _warnedMissingCamera = true;
                }
                return;
            }

            _warnedMissingCamera = false;

            if (typhoonParticlePrefab != null)
            {
                _typhoonInstance = Instantiate(typhoonParticlePrefab, cam.transform);
                _typhoonInstance.transform.localPosition = new Vector3(0f, 0f, typhoonDistance);
                _typhoonInstance.transform.localRotation = Quaternion.identity;
            }
            else
            {
                _typhoonInstance = BuildDefaultTyphoonParticle(cam.transform);
            }

            if (typhoonSoundClip != null && _typhoonAudio == null)
            {
                _typhoonAudio = _typhoonInstance.gameObject.AddComponent<AudioSource>();
                _typhoonAudio.clip = typhoonSoundClip;
                _typhoonAudio.loop = true;
                _typhoonAudio.volume = typhoonVolume;
                _typhoonAudio.spatialBlend = 0f;
                _typhoonAudio.playOnAwake = false;
            }
        }

        // Long, fast horizontal streaks sweeping across the screen for a strong gust feel.
        private ParticleSystem BuildDefaultTyphoonParticle(Transform parent)
        {
            var go = new GameObject("Typhoon_Particle_Auto");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0f, typhoonDistance);
            go.transform.localRotation = Quaternion.identity;

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime   = 0.42f;
            main.startSpeed      = 0f;
            main.startSize       = new ParticleSystem.MinMaxCurve(0.006f, 0.018f);
            main.startColor      = new Color(0.82f, 0.88f, 0.95f, 0.24f);
            main.maxParticles    = 1800;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.gravityModifier = 0f;
            main.playOnAwake     = false;

            var emission = ps.emission;
            emission.rateOverTime = 360f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale     = new Vector3(typhoonAreaSize, typhoonAreaSize * 0.55f, 0.1f);

            // Strong horizontal wind + small vertical jitter for chaotic gust.
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space   = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(typhoonWindSpeed.x, typhoonWindSpeed.y);
            velocity.y = new ParticleSystem.MinMaxCurve(-1.2f, 1.2f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.alignment  = ParticleSystemRenderSpace.View;
            renderer.lengthScale = 0.85f;
            renderer.velocityScale = 0.035f;
            renderer.material = typhoonMaterial != null ? typhoonMaterial : BuildFallbackTyphoonMaterial();
            ConfigureOverlayMaterial(renderer.material, new Color(0.82f, 0.88f, 0.95f, 0.24f));

            return ps;
        }

        private static Material BuildFallbackTyphoonMaterial()
        {
            Shader shader =
                Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                Shader.Find("Particles/Standard Unlit") ??
                Shader.Find("Legacy Shaders/Particles/Alpha Blended") ??
                Shader.Find("Sprites/Default");

            if (shader == null)
            {
                Debug.LogWarning("[WeatherVFXController] Could not find a usable particle shader. Assign typhoonMaterial in the inspector.");
                return null;
            }

            var mat = new Material(shader);
            ConfigureOverlayMaterial(mat, new Color(0.82f, 0.88f, 0.95f, 0.24f));
            return mat;
        }

        // ---------- Fog ----------

        private void EnableDustFog()    => EnableFog(dustFogColor, dustFogDensity);
        private void EnableTyphoonFog() => EnableFog(typhoonFogColor, typhoonFogDensity);

        private void EnableFog(Color color, float density)
        {
            if (!_fogStashed)
            {
                _fogWasEnabled      = RenderSettings.fog;
                _fogColorOriginal   = RenderSettings.fogColor;
                _fogDensityOriginal = RenderSettings.fogDensity;
                _fogModeOriginal    = RenderSettings.fogMode;
                _fogStashed         = true;
            }
            RenderSettings.fog        = true;
            RenderSettings.fogMode    = FogMode.ExponentialSquared;
            RenderSettings.fogColor   = color;
            RenderSettings.fogDensity = density;
        }

        private void RestoreFog()
        {
            if (!_fogStashed) return;
            RenderSettings.fog        = _fogWasEnabled;
            RenderSettings.fogColor   = _fogColorOriginal;
            RenderSettings.fogDensity = _fogDensityOriginal;
            RenderSettings.fogMode    = _fogModeOriginal;
            _fogStashed = false;
        }
    }
}
