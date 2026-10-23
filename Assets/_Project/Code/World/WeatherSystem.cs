using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// ============================================================
//  WeatherSystem — Dynamic weather transitions
//  Attach to: World_Persistent scene
// ============================================================
namespace EchoesOfTheLost.World
{
    [System.Serializable]
    public class WeatherPreset
    {
        public WeatherType type;
        [Header("Visual")]
        public float fogDensity = 0.005f;
        public float cloudCoverage = 0f;     // 0 = clear, 1 = overcast
        public Color sunLightColor = Color.white;
        public float sunIntensity = 1f;
        public Color ambientColor = Color.white;

        [Header("Particles")]
        public GameObject rainParticlePrefab;
        public GameObject snowParticlePrefab;
        public GameObject dustParticlePrefab;

        [Header("Audio")]
        public AudioClip weatherAmbientClip;
        [Range(0f, 1f)] public float windStrength = 0f;

        [Header("Gameplay")]
        public float playerSpeedModifier = 1f;   // 0.8 = 20% slower in storm
        public float visibilityModifier = 1f;     // affects enemy detection range
        public bool enableThunder = false;
        public float thunderInterval = 15f;
    }

    public class WeatherSystem : Utils.Singleton<WeatherSystem>
    {
        [Header("Weather Presets")]
        [SerializeField] private WeatherPreset[] _presets;

        [Header("References")]
        [SerializeField] private Light _sunLight;
        [SerializeField] private Transform _particleParent;

        [Header("Transition")]
        [SerializeField] private float _transitionDuration = 30f;
        [SerializeField] private float _minWeatherDuration = 120f;
        [SerializeField] private float _maxWeatherDuration = 600f;

        [Header("Debug")]
        [SerializeField] private WeatherType _forceWeather = WeatherType.Clear;
        [SerializeField] private bool _useForceWeather;

        // ── State ─────────────────────────────────────────────
        private WeatherPreset _currentPreset;
        private WeatherPreset _targetPreset;
        private GameObject _activeParticles;
        private float _weatherTimer;
        private Coroutine _transitionCoroutine;

        public WeatherType CurrentWeather { get; private set; } = WeatherType.Clear;
        public float WindStrength => _currentPreset?.windStrength ?? 0f;
        public float PlayerSpeedModifier => _currentPreset?.playerSpeedModifier ?? 1f;
        public float VisibilityModifier => _currentPreset?.visibilityModifier ?? 1f;

        public event System.Action<WeatherType> OnWeatherChanged;

        protected override void Awake()
        {
            base.Awake();
            _currentPreset = GetPreset(WeatherType.Clear);
            _targetPreset = _currentPreset;
            Core.ServiceLocator.Register<WeatherSystem>(this);
        }

        private void Start()
        {
            ApplyWeatherInstant(_currentPreset);
            _weatherTimer = Random.Range(_minWeatherDuration, _maxWeatherDuration);
        }

        private void Update()
        {
            if (_useForceWeather)
            {
                SetWeather(_forceWeather);
                _useForceWeather = false;
                return;
            }

            _weatherTimer -= Time.deltaTime;
            if (_weatherTimer <= 0f) PickRandomWeather();
        }

        // ── Public API ──────────────────────────────────────

        /// <summary>Set weather by type, optionally instantly.</summary>
        public void SetWeather(WeatherType type, bool instant = false)
        {
            if (CurrentWeather == type && !instant) return;

            var preset = GetPreset(type);
            if (preset == null) return;

            CurrentWeather = type;
            _targetPreset = preset;

            if (_transitionCoroutine != null) StopCoroutine(_transitionCoroutine);
            _transitionCoroutine = instant
                ? null
                : StartCoroutine(TransitionWeather(preset));

            if (instant) ApplyWeatherInstant(preset);

            _weatherTimer = Random.Range(_minWeatherDuration, _maxWeatherDuration);
            OnWeatherChanged?.Invoke(type);
        }

        /// <summary>Force clear weather (e.g., entering a cave).</summary>
        public void OverrideWeather(WeatherType type) => SetWeather(type, instant: true);

        // ── Private ─────────────────────────────────────────

        private IEnumerator TransitionWeather(WeatherPreset target)
        {
            float elapsed = 0f;
            WeatherPreset from = _currentPreset;

            // Spawn new particles
            SpawnParticles(target);

            while (elapsed < _transitionDuration)
            {
                float t = elapsed / _transitionDuration;

                // Lerp visual values
                RenderSettings.fogDensity = Mathf.Lerp(from.fogDensity, target.fogDensity, t);
                RenderSettings.ambientLight = Color.Lerp(from.ambientColor, target.ambientColor, t);

                if (_sunLight != null)
                {
                    _sunLight.color = Color.Lerp(from.sunLightColor, target.sunLightColor, t);
                    _sunLight.intensity = Mathf.Lerp(from.sunIntensity, target.sunIntensity, t);
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            ApplyWeatherInstant(target);
            _currentPreset = target;
        }

        private void ApplyWeatherInstant(WeatherPreset preset)
        {
            if (preset == null) return;

            RenderSettings.fogDensity = preset.fogDensity;
            RenderSettings.ambientLight = preset.ambientColor;

            if (_sunLight != null)
            {
                _sunLight.color = preset.sunLightColor;
                _sunLight.intensity = preset.sunIntensity;
            }

            _currentPreset = preset;
            SpawnParticles(preset);

            if (preset.enableThunder)
                StartCoroutine(ThunderRoutine(preset.thunderInterval));
        }

        private void SpawnParticles(WeatherPreset preset)
        {
            if (_activeParticles != null)
                Destroy(_activeParticles);

            GameObject particlePrefab = null;
            switch (preset.type)
            {
                case WeatherType.Raining:
                case WeatherType.Thunderstorm: particlePrefab = preset.rainParticlePrefab; break;
                case WeatherType.Snowing: particlePrefab = preset.snowParticlePrefab; break;
                case WeatherType.Dusty: particlePrefab = preset.dustParticlePrefab; break;
            }

            if (particlePrefab != null)
                _activeParticles = Instantiate(particlePrefab, _particleParent ?? transform);
        }

        private IEnumerator ThunderRoutine(float interval)
        {
            while (CurrentWeather == WeatherType.Thunderstorm)
            {
                yield return new WaitForSeconds(Random.Range(interval * 0.5f, interval * 1.5f));
                // Flash + thunder SFX — connect to AudioManager
                var audio = Core.ServiceLocator.TryGet<Systems.AudioManager>(out var am) ? am : null;
                // audio?.PlaySFX(thunderClip, Camera.main.transform.position);
            }
        }

        private void PickRandomWeather()
        {
            // Default: pick a random preset weighted by index (customize per biome)
            var weather = (WeatherType)Random.Range(0, 4);
            SetWeather(weather);
        }

        private WeatherPreset GetPreset(WeatherType type)
        {
            if (_presets == null) return null;
            foreach (var p in _presets)
                if (p.type == type) return p;
            return _presets.Length > 0 ? _presets[0] : null;
        }
    }
}
