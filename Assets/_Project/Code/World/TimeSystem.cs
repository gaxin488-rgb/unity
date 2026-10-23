using UnityEngine;
using System.Collections.Generic;
using System.Collections;

// ============================================================
//  TimeSystem — In-game day/night cycle
//  Attach to: World_Persistent scene
// ============================================================
namespace EchoesOfTheLost.World
{
    public class TimeSystem : Utils.Singleton<TimeSystem>
    {
        [Header("Time Settings")]
        [Tooltip("How many real seconds = 1 in-game hour")]
        [SerializeField] private float _realSecondsPerGameHour = 75f; // 30min real = 24h game

        [Header("Starting Time")]
        [SerializeField] [Range(0f, 24f)] private float _startingHour = 8f;

        [Header("Sun & Moon")]
        [SerializeField] private Light _sunLight;
        [SerializeField] private Light _moonLight;
        [SerializeField] private Gradient _sunColor;
        [SerializeField] private AnimationCurve _sunIntensityCurve;
        [SerializeField] private AnimationCurve _moonIntensityCurve;

        [Header("Sky")]
        [SerializeField] private Material _skyboxMaterial;
        [SerializeField] private Gradient _skyColor;
        [SerializeField] private Gradient _horizonColor;
        [SerializeField] private Gradient _fogColor;

        // ── State ─────────────────────────────────────────────
        private float _currentHour;
        private bool _isPaused;

        public float CurrentHour => _currentHour;
        public bool IsDaytime => _currentHour >= 6f && _currentHour < 20f;
        public bool IsNighttime => !IsDaytime;

        // Time-of-day thresholds
        public bool IsMorning => _currentHour >= 6f && _currentHour < 10f;
        public bool IsAfternoon => _currentHour >= 10f && _currentHour < 17f;
        public bool IsEvening => _currentHour >= 17f && _currentHour < 20f;
        public bool IsNight => _currentHour >= 20f || _currentHour < 6f;

        public string FormattedTime
        {
            get
            {
                int h = (int)_currentHour;
                int m = (int)((_currentHour - h) * 60f);
                string ampm = h < 12 ? "AM" : "PM";
                int h12 = h % 12;
                if (h12 == 0) h12 = 12;
                return $"{h12:00}:{m:00} {ampm}";
            }
        }

        private float _lastHourBroadcast = -1f;

        protected override void Awake()
        {
            base.Awake();
            _currentHour = _startingHour;
            EnsureGradientsAndCurves();
            Core.ServiceLocator.Register<TimeSystem>(this);
        }

        public void ConfigureLightingDefaults(Light sun, Light moon)
        {
            _sunLight = sun;
            _moonLight = moon;
            EnsureGradientsAndCurves();
            UpdateSkyAndLighting();
        }

        private void EnsureGradientsAndCurves()
        {
            if (_sunColor == null || _sunColor.colorKeys.Length == 0)
            {
                _sunColor = new Gradient();
                _sunColor.SetKeys(
                    new[]
                    {
                        new GradientColorKey(new Color(0.12f, 0.16f, 0.32f), 0.0f),
                        new GradientColorKey(new Color(1.00f, 0.72f, 0.50f), 0.25f), // 6 AM Dawn
                        new GradientColorKey(new Color(1.00f, 0.98f, 0.90f), 0.50f), // 12 PM Noon
                        new GradientColorKey(new Color(1.00f, 0.55f, 0.25f), 0.75f), // 6 PM Sunset
                        new GradientColorKey(new Color(0.40f, 0.25f, 0.45f), 0.85f), // Dusk
                        new GradientColorKey(new Color(0.12f, 0.16f, 0.32f), 1.0f)
                    },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            }

            if (_sunIntensityCurve == null || _sunIntensityCurve.length == 0)
            {
                _sunIntensityCurve = new AnimationCurve(
                    new Keyframe(0.0f, 0f),
                    new Keyframe(0.20f, 0f),
                    new Keyframe(0.25f, 0.35f),
                    new Keyframe(0.50f, 1.35f),
                    new Keyframe(0.75f, 0.45f),
                    new Keyframe(0.82f, 0f),
                    new Keyframe(1.0f, 0f));
            }

            if (_moonIntensityCurve == null || _moonIntensityCurve.length == 0)
            {
                _moonIntensityCurve = new AnimationCurve(
                    new Keyframe(0.0f, 0.45f),
                    new Keyframe(0.22f, 0f),
                    new Keyframe(0.78f, 0f),
                    new Keyframe(0.85f, 0.35f),
                    new Keyframe(1.0f, 0.45f));
            }

            if (_fogColor == null || _fogColor.colorKeys.Length == 0)
            {
                _fogColor = new Gradient();
                _fogColor.SetKeys(
                    new[]
                    {
                        new GradientColorKey(new Color(0.08f, 0.10f, 0.18f), 0.0f),
                        new GradientColorKey(new Color(0.85f, 0.72f, 0.68f), 0.25f),
                        new GradientColorKey(new Color(0.68f, 0.82f, 0.92f), 0.50f),
                        new GradientColorKey(new Color(0.92f, 0.58f, 0.42f), 0.75f),
                        new GradientColorKey(new Color(0.08f, 0.10f, 0.18f), 1.0f)
                    },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            }

            if (_skyColor == null || _skyColor.colorKeys.Length == 0)
            {
                _skyColor = new Gradient();
                _skyColor.SetKeys(
                    new[]
                    {
                        new GradientColorKey(new Color(0.08f, 0.09f, 0.15f), 0.0f),
                        new GradientColorKey(new Color(0.55f, 0.45f, 0.45f), 0.25f),
                        new GradientColorKey(new Color(0.45f, 0.50f, 0.60f), 0.50f),
                        new GradientColorKey(new Color(0.50f, 0.35f, 0.35f), 0.75f),
                        new GradientColorKey(new Color(0.08f, 0.09f, 0.15f), 1.0f)
                    },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            }
        }

        private void Update()
        {
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb.f1Key.wasPressedThisFrame) SetTime(6.5f);  // Dawn
                if (kb.f2Key.wasPressedThisFrame) SetTime(12.0f); // Noon
                if (kb.f3Key.wasPressedThisFrame) SetTime(18.5f); // Sunset
                if (kb.f4Key.wasPressedThisFrame) SetTime(0.0f);  // Midnight
            }

            if (_isPaused) return;

            _currentHour += (Time.deltaTime / _realSecondsPerGameHour);
            if (_currentHour >= 24f) _currentHour -= 24f;

            UpdateSkyAndLighting();

            // Broadcast whole-hour events
            int hour = (int)_currentHour;
            if (hour != (int)_lastHourBroadcast)
            {
                _lastHourBroadcast = hour;
                Core.EventBus.Publish(new Core.DaytimeChangedEvent { Hour = _currentHour });
            }
        }

        public void UpdateSkyAndLighting()
        {
            EnsureGradientsAndCurves();
            float t = _currentHour / 24f;

            // Sun rotation (rises in east, sets in west)
            float sunAngle = (t - 0.25f) * 360f;
            if (_sunLight != null)
            {
                _sunLight.transform.rotation = Quaternion.Euler(sunAngle, -30f, 0f);
                if (_sunColor != null) _sunLight.color = _sunColor.Evaluate(t);
                if (_sunIntensityCurve != null) _sunLight.intensity = _sunIntensityCurve.Evaluate(t);
            }

            // Moon opposite to sun
            if (_moonLight != null)
            {
                _moonLight.transform.rotation = Quaternion.Euler(sunAngle + 180f, -30f, 0f);
                if (_moonIntensityCurve != null) _moonLight.intensity = _moonIntensityCurve.Evaluate(t);
            }

            // Fog & ambient
            if (_fogColor != null) RenderSettings.fogColor = _fogColor.Evaluate(t);
            if (_skyColor != null) RenderSettings.ambientLight = _skyColor.Evaluate(t);
        }

        // ── Public API ──────────────────────────────────────

        public void SetTime(float hour) => _currentHour = Mathf.Clamp(hour, 0f, 24f);
        public void SkipToMorning() => SetTime(8f);
        public void PauseTime() => _isPaused = true;
        public void ResumeTime() => _isPaused = false;
    }
}
