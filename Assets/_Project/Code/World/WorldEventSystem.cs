using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// ============================================================
//  WorldEventSystem — Dynamic world events (raids, festivals...)
//  Attach to: World_Persistent scene
// ============================================================
namespace EchoesOfTheLost.World
{
    public enum WorldEventType
    {
        BanditRaid,
        MonsterMigration,
        MeteorShower,
        Festival,
        Eclipse,
        ResonanceStorm,
        ArchaeologicalFind
    }

    [System.Serializable]
    public class WorldEvent
    {
        public string eventId;
        public string displayName;
        public WorldEventType type;
        [TextArea(2, 3)] public string description;

        [Header("Timing")]
        public float duration = 300f;           // seconds
        public float minTimeBetween = 1800f;    // 30 minutes min between same event
        public float triggerChance = 0.1f;      // per check cycle

        [Header("Conditions")]
        public float minHour = 0f;
        public float maxHour = 24f;
        public WeatherType[] requiredWeather;
        public string[] requiredQuestIds;   // quests that must be active/complete

        [Header("Gameplay Effect")]
        public float enemySpawnMultiplier = 1f;
        public float lootMultiplier = 1f;
        public float economyModifier = 0f;   // ± to shop prices globally

        [Header("Notification")]
        public Sprite icon;
        [TextArea(1, 2)] public string notificationMessage;

        [System.NonSerialized] public float lastTriggeredTime = -9999f;
        [System.NonSerialized] public bool isActive;
    }

    public class WorldEventSystem : Utils.Singleton<WorldEventSystem>
    {
        [Header("Events")]
        [SerializeField] private WorldEvent[] _worldEvents;

        [Header("Check Settings")]
        [SerializeField] private float _checkInterval = 60f;  // check every 1 minute

        private float _checkTimer;
        private List<WorldEvent> _activeEvents = new List<WorldEvent>();

        public event System.Action<WorldEvent> OnEventStarted;
        public event System.Action<WorldEvent> OnEventEnded;

        public IReadOnlyList<WorldEvent> ActiveEvents => _activeEvents;

        protected override void Awake()
        {
            base.Awake();
            Core.ServiceLocator.Register<WorldEventSystem>(this);
        }

        private void Update()
        {
            _checkTimer += Time.deltaTime;
            if (_checkTimer >= _checkInterval)
            {
                _checkTimer = 0f;
                CheckForNewEvents();
            }

            // Update active event timers
            for (int i = _activeEvents.Count - 1; i >= 0; i--)
            {
                var evt = _activeEvents[i];
                evt.duration -= Time.deltaTime;
                if (evt.duration <= 0f) EndEvent(evt);
            }
        }

        // ── Event Triggering ──────────────────────────────────

        private void CheckForNewEvents()
        {
            if (_worldEvents == null) return;

            var timeSystem = Core.ServiceLocator.TryGet<TimeSystem>(out var ts) ? ts : null;
            var weatherSystem = Core.ServiceLocator.TryGet<WeatherSystem>(out var ws) ? ws : null;

            foreach (var evt in _worldEvents)
            {
                if (evt.isActive) continue;
                if (Time.time - evt.lastTriggeredTime < evt.minTimeBetween) continue;
                if (!IsTimeValid(evt, timeSystem?.CurrentHour ?? 12f)) continue;
                if (!IsWeatherValid(evt, weatherSystem?.CurrentWeather ?? WeatherType.Clear)) continue;
                if (Random.value > evt.triggerChance) continue;

                TriggerEvent(evt);
            }
        }

        private void TriggerEvent(WorldEvent evt)
        {
            evt.isActive = true;
            evt.lastTriggeredTime = Time.time;
            _activeEvents.Add(evt);

            // Apply economy modifier
            // EconomySystem.Instance?.ApplyModifier(evt.economyModifier);

            OnEventStarted?.Invoke(evt);
            Debug.Log($"[WorldEventSystem] Event started: {evt.displayName}");

            // Schedule end
            StartCoroutine(AutoEndEvent(evt, evt.duration));
        }

        private void EndEvent(WorldEvent evt)
        {
            evt.isActive = false;
            _activeEvents.Remove(evt);
            OnEventEnded?.Invoke(evt);
            Debug.Log($"[WorldEventSystem] Event ended: {evt.displayName}");
        }

        private IEnumerator AutoEndEvent(WorldEvent evt, float duration)
        {
            yield return new WaitForSeconds(duration);
            if (evt.isActive) EndEvent(evt);
        }

        // ── Validity Checks ───────────────────────────────────

        private bool IsTimeValid(WorldEvent evt, float currentHour)
        {
            if (evt.minHour <= evt.maxHour)
                return currentHour >= evt.minHour && currentHour <= evt.maxHour;
            else  // wraps midnight: e.g. 22:00 to 04:00
                return currentHour >= evt.minHour || currentHour <= evt.maxHour;
        }

        private bool IsWeatherValid(WorldEvent evt, WeatherType current)
        {
            if (evt.requiredWeather == null || evt.requiredWeather.Length == 0) return true;
            foreach (var w in evt.requiredWeather)
                if (w == current) return true;
            return false;
        }

        // ── Manual Trigger (for testing/quests) ──────────────
        public void ForceEvent(string eventId)
        {
            if (_worldEvents == null) return;
            foreach (var evt in _worldEvents)
                if (evt.eventId == eventId) { TriggerEvent(evt); return; }
        }
    }
}
