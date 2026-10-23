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
            Core.ServiceLocator.Register<TimeSystem>(this);
        }

        private void Update()
        {
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

        private void UpdateSkyAndLighting()
        {
            // Normalize time: 0 = midnight, 0.25 = 6am, 0.5 = noon, 0.75 = 6pm
            float t = _currentHour / 24f;

            // Sun rotation (rises in east, sets in west)
            float sunAngle = (t - 0.25f) * 360f;
            if (_sunLight != null)
            {
                _sunLight.transform.rotation = Quaternion.Euler(sunAngle, -30f, 0f);
                _sunLight.color = _sunColor.Evaluate(t);
                _sunLight.intensity = _sunIntensityCurve.Evaluate(t);
            }

            // Moon opposite to sun
            if (_moonLight != null)
            {
                _moonLight.transform.rotation = Quaternion.Euler(sunAngle + 180f, -30f, 0f);
                _moonLight.intensity = _moonIntensityCurve.Evaluate(t);
            }

            // Fog
            RenderSettings.fogColor = _fogColor.Evaluate(t);
            RenderSettings.ambientLight = _skyColor.Evaluate(t);
        }

        // ── Public API ──────────────────────────────────────

        public void SetTime(float hour) => _currentHour = Mathf.Clamp(hour, 0f, 24f);
        public void SkipToMorning() => SetTime(8f);
        public void PauseTime() => _isPaused = true;
        public void ResumeTime() => _isPaused = false;
    }
}
