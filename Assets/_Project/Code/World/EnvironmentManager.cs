using UnityEngine;
using System.Collections;

// ============================================================
//  EnvironmentManager — Per-biome post-processing & atmosphere
//  Attach to: World_Persistent scene
//  Requires: URP Volume system
// ============================================================
namespace EchoesOfTheLost.World
{
    [System.Serializable]
    public class AtmospherePreset
    {
        public string biomeId;
        public BiomeData biome;

        [Header("Post-Processing (URP Volume)")]
        public UnityEngine.Rendering.VolumeProfile volumeProfile;
        public float transitionDuration = 5f;

        [Header("Fog")]
        public bool fogEnabled = true;
        public Color fogColor = Color.white;
        public float fogDensity = 0.005f;
        public float fogStartDistance = 50f;
        public float fogEndDistance = 400f;

        [Header("Ambient Lighting")]
        public Color skyColor = Color.cyan;
        public Color equatorColor = Color.grey;
        public Color groundColor = Color.black;
        public float ambientIntensity = 1f;

        [Header("Sun Override")]
        public Color sunColor = Color.white;
        public float sunIntensity = 1f;

        [Header("Wind")]
        public float windSpeed = 0.5f;
        public Vector3 windDirection = Vector3.forward;
    }

    public class EnvironmentManager : Utils.Singleton<EnvironmentManager>
    {
        [Header("Atmosphere Presets")]
        [SerializeField] private AtmospherePreset[] _presets;

        [Header("Global Volume")]
        [SerializeField] private UnityEngine.Rendering.Volume _globalVolume;
        [SerializeField] private Light _sunLight;

        [Header("Default")]
        [SerializeField] private AtmospherePreset _defaultPreset;

        // ── State ─────────────────────────────────────────────
        private AtmospherePreset _currentPreset;
        private AtmospherePreset _targetPreset;
        private Coroutine _transitionCoroutine;

        protected override void Awake()
        {
            base.Awake();
            Core.ServiceLocator.Register<EnvironmentManager>(this);
        }

        private void Start()
        {
            // Subscribe to biome changes
            if (Core.ServiceLocator.TryGet<TerrainManager>(out var tm))
                tm.OnBiomeChanged += OnBiomeChanged;

            if (_defaultPreset != null)
                ApplyPresetInstant(_defaultPreset);
        }

        // ── Biome Response ────────────────────────────────────

        private void OnBiomeChanged(BiomeData newBiome)
        {
            if (newBiome == null) return;

            // Find matching atmosphere preset
            AtmospherePreset target = _defaultPreset;
            foreach (var preset in _presets)
            {
                if (preset.biomeId == newBiome.biomeId)
                {
                    target = preset;
                    break;
                }
            }

            TransitionToPreset(target);
        }

        // ── Transitions ───────────────────────────────────────

        public void TransitionToPreset(AtmospherePreset target)
        {
            if (target == null || target == _currentPreset) return;
            _targetPreset = target;

            if (_transitionCoroutine != null) StopCoroutine(_transitionCoroutine);
            _transitionCoroutine = StartCoroutine(TransitionCoroutine(target));
        }

        private IEnumerator TransitionCoroutine(AtmospherePreset target)
        {
            float elapsed = 0f;
            float duration = target.transitionDuration;

            if (_currentPreset == null)
            {
                ApplyPresetInstant(target);
                yield break;
            }

            Color fromFog = RenderSettings.fogColor;
            float fromFogDensity = RenderSettings.fogDensity;
            Color fromSky = RenderSettings.ambientSkyColor;
            Color fromEquator = RenderSettings.ambientEquatorColor;
            Color fromGround = RenderSettings.ambientGroundColor;

            while (elapsed < duration)
            {
                float t = elapsed / duration;
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                RenderSettings.fogColor = Color.Lerp(fromFog, target.fogColor, smoothT);
                RenderSettings.fogDensity = Mathf.Lerp(fromFogDensity, target.fogDensity, smoothT);
                RenderSettings.fogStartDistance = Mathf.Lerp(RenderSettings.fogStartDistance, target.fogStartDistance, smoothT);
                RenderSettings.ambientSkyColor = Color.Lerp(fromSky, target.skyColor, smoothT);
                RenderSettings.ambientEquatorColor = Color.Lerp(fromEquator, target.equatorColor, smoothT);
                RenderSettings.ambientGroundColor = Color.Lerp(fromGround, target.groundColor, smoothT);
                RenderSettings.ambientIntensity = Mathf.Lerp(RenderSettings.ambientIntensity, target.ambientIntensity, smoothT);

                if (_sunLight != null)
                {
                    _sunLight.color = Color.Lerp(_sunLight.color, target.sunColor, smoothT);
                    _sunLight.intensity = Mathf.Lerp(_sunLight.intensity, target.sunIntensity, smoothT);
                }

                // Blend URP volumes
                if (_globalVolume != null && target.volumeProfile != null)
                {
                    _globalVolume.weight = Mathf.Lerp(_globalVolume.weight, 1f, smoothT);
                    _globalVolume.profile = target.volumeProfile;
                }

                // Wind shader globals
                Shader.SetGlobalVector("_WindDirection", target.windDirection.normalized);
                Shader.SetGlobalFloat("_WindSpeed", Mathf.Lerp(
                    _currentPreset.windSpeed, target.windSpeed, smoothT));

                elapsed += Time.deltaTime;
                yield return null;
            }

            ApplyPresetInstant(target);
            _currentPreset = target;
        }

        private void ApplyPresetInstant(AtmospherePreset preset)
        {
            if (preset == null) return;

            RenderSettings.fog = preset.fogEnabled;
            RenderSettings.fogColor = preset.fogColor;
            RenderSettings.fogDensity = preset.fogDensity;
            RenderSettings.fogStartDistance = preset.fogStartDistance;
            RenderSettings.fogEndDistance = preset.fogEndDistance;
            RenderSettings.ambientSkyColor = preset.skyColor;
            RenderSettings.ambientEquatorColor = preset.equatorColor;
            RenderSettings.ambientGroundColor = preset.groundColor;
            RenderSettings.ambientIntensity = preset.ambientIntensity;

            if (_sunLight != null)
            {
                _sunLight.color = preset.sunColor;
                _sunLight.intensity = preset.sunIntensity;
            }

            if (_globalVolume != null && preset.volumeProfile != null)
                _globalVolume.profile = preset.volumeProfile;

            Shader.SetGlobalVector("_WindDirection", preset.windDirection.normalized);
            Shader.SetGlobalFloat("_WindSpeed", preset.windSpeed);

            _currentPreset = preset;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (Core.ServiceLocator.TryGet<TerrainManager>(out var tm))
                tm.OnBiomeChanged -= OnBiomeChanged;
        }
    }
}
