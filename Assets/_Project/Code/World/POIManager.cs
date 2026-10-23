using UnityEngine;
using System.Collections.Generic;

// ============================================================
//  POIManager — Points of Interest: discovery, fog of war, map
//  Attach to: World_Persistent scene
// ============================================================
namespace EchoesOfTheLost.World
{
    public enum POIType
    {
        Town, Village, Ruin, Dungeon, Cave, Tower,
        Camp_Enemy, Camp_Friendly, Viewpoint, Hidden, Boss
    }

    [System.Serializable]
    public class PointOfInterest
    {
        public string poiId;
        public string displayName;
        [TextArea(1, 2)] public string description;
        public POIType type;
        public Vector3 worldPosition;
        public Sprite mapIcon;
        public int recommendedLevel = 1;
        public string[] rewardItemIds;
        public bool isDiscovered;
        public bool isCleared;
        public bool showOnMapBeforeDiscover;  // some POIs visible from start
    }

    public class POIManager : Utils.Singleton<POIManager>
    {
        [Header("POIs")]
        [SerializeField] private PointOfInterest[] _allPOIs;

        [Header("Discovery")]
        [SerializeField] private float _autoDiscoverRadius = 30f;  // auto-discover when this close
        [SerializeField] private float _mapRevealRadius = 80f;     // reveal map fog this far

        [Header("Player")]
        [SerializeField] private Transform _player;

        // ── State ─────────────────────────────────────────────
        private Dictionary<string, PointOfInterest> _poiMap = new Dictionary<string, PointOfInterest>();
        private float _checkTimer;
        private const float CHECK_INTERVAL = 0.5f;

        public event System.Action<PointOfInterest> OnPOIDiscovered;
        public event System.Action<PointOfInterest> OnPOICleared;

        public IReadOnlyList<PointOfInterest> AllPOIs => _allPOIs;

        protected override void Awake()
        {
            base.Awake();
            Core.ServiceLocator.Register<POIManager>(this);

            foreach (var poi in _allPOIs)
                if (!string.IsNullOrEmpty(poi.poiId))
                    _poiMap[poi.poiId] = poi;
        }

        private void Start()
        {
            if (_player == null)
            {
                var go = GameObject.FindGameObjectWithTag("Player");
                if (go != null) _player = go.transform;
            }
        }

        private void Update()
        {
            _checkTimer += Time.deltaTime;
            if (_checkTimer >= CHECK_INTERVAL)
            {
                _checkTimer = 0f;
                CheckNearbyPOIs();
            }
        }

        // ── Discovery ────────────────────────────────────────

        private void CheckNearbyPOIs()
        {
            if (_player == null) return;

            foreach (var poi in _allPOIs)
            {
                if (poi.isDiscovered) continue;

                float dist = Vector3.Distance(_player.position, poi.worldPosition);
                if (dist <= _autoDiscoverRadius)
                    DiscoverPOI(poi.poiId);
            }
        }

        public void DiscoverPOI(string poiId)
        {
            if (!_poiMap.TryGetValue(poiId, out var poi)) return;
            if (poi.isDiscovered) return;

            poi.isDiscovered = true;
            OnPOIDiscovered?.Invoke(poi);

            // XP reward for first discovery
            var playerStats = _player?.GetComponent<Characters.Player.PlayerStats>();
            playerStats?.AddXP(50);

            Debug.Log($"[POIManager] Discovered: {poi.displayName}");
        }

        public void ClearPOI(string poiId)
        {
            if (!_poiMap.TryGetValue(poiId, out var poi)) return;
            if (poi.isCleared) return;

            poi.isCleared = true;
            OnPOICleared?.Invoke(poi);

            // Grant cleared rewards
            var inv = Core.ServiceLocator.TryGet<Systems.Inventory.InventoryManager>(out var i) ? i : null;
            if (inv != null && poi.rewardItemIds != null)
            {
                // Would need ItemDatabase to resolve IDs — simplified
            }

            Debug.Log($"[POIManager] Cleared: {poi.displayName}");
        }

        // ── Queries ───────────────────────────────────────────

        public PointOfInterest GetPOI(string id) =>
            _poiMap.TryGetValue(id, out var poi) ? poi : null;

        public PointOfInterest[] GetDiscoveredPOIs()
        {
            var result = new System.Collections.Generic.List<PointOfInterest>();
            foreach (var poi in _allPOIs)
                if (poi.isDiscovered) result.Add(poi);
            return result.ToArray();
        }

        public PointOfInterest[] GetPOIsByType(POIType type)
        {
            var result = new System.Collections.Generic.List<PointOfInterest>();
            foreach (var poi in _allPOIs)
                if (poi.type == type) result.Add(poi);
            return result.ToArray();
        }

        public PointOfInterest GetNearestPOI(Vector3 worldPos, POIType? filterType = null)
        {
            PointOfInterest nearest = null;
            float nearestDist = float.MaxValue;

            foreach (var poi in _allPOIs)
            {
                if (filterType.HasValue && poi.type != filterType.Value) continue;
                float dist = Vector3.Distance(worldPos, poi.worldPosition);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = poi;
                }
            }
            return nearest;
        }

        // ── Save/Load ────────────────────────────────────────

        public List<string> GetDiscoveredIds()
        {
            var list = new List<string>();
            foreach (var poi in _allPOIs)
                if (poi.isDiscovered) list.Add(poi.poiId);
            return list;
        }

        public void RestoreFromSave(List<string> discoveredIds, List<string> clearedIds)
        {
            if (discoveredIds != null)
                foreach (var id in discoveredIds)
                    if (_poiMap.TryGetValue(id, out var poi))
                        poi.isDiscovered = true;

            if (clearedIds != null)
                foreach (var id in clearedIds)
                    if (_poiMap.TryGetValue(id, out var poi))
                        poi.isCleared = true;
        }

        // ── Gizmos ────────────────────────────────────────────
        private void OnDrawGizmos()
        {
            if (_allPOIs == null) return;
            foreach (var poi in _allPOIs)
            {
                Gizmos.color = poi.isDiscovered ? Color.green : Color.yellow;
                Gizmos.DrawWireSphere(poi.worldPosition, 5f);
#if UNITY_EDITOR
                UnityEditor.Handles.Label(poi.worldPosition + Vector3.up * 6f, poi.displayName);
#endif
            }
        }
    }
}
