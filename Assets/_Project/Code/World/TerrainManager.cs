using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

// ============================================================
//  TerrainManager — Multi-terrain biome system with smooth blending
//  Attach to: World_Persistent scene
// ============================================================
namespace EchoesOfTheLost.World
{
    [System.Serializable]
    public class TerrainRegion
    {
        public string regionId;
        public Terrain terrain;
        public BiomeData biome;
        public Bounds bounds;
        public bool isLoaded;
    }

    public class TerrainManager : Utils.Singleton<TerrainManager>
    {
        [Header("Terrain Grid")]
        [SerializeField] private TerrainRegion[] _regions;
        [SerializeField] private int _terrainGridWidth = 4;
        [SerializeField] private int _terrainGridHeight = 4;
        [SerializeField] private float _terrainSize = 500f;

        [Header("LOD Settings")]
        [SerializeField] private float _pixelError_Near = 1f;
        [SerializeField] private float _pixelError_Far = 20f;
        [SerializeField] private float _nearDistance = 300f;

        [Header("Detail Settings")]
        [SerializeField] private float _detailViewDistance = 100f;
        [SerializeField] private float _treeBillboardStart = 200f;
        [SerializeField] private float _treeMaxDistance = 500f;

        [Header("Player Reference")]
        [SerializeField] private Transform _player;

        // ── State ─────────────────────────────────────────────
        private TerrainRegion _currentRegion;
        private Dictionary<string, TerrainRegion> _regionMap = new Dictionary<string, TerrainRegion>();

        public BiomeData CurrentBiome => _currentRegion?.biome;
        public event System.Action<BiomeData> OnBiomeChanged;

        protected override void Awake()
        {
            base.Awake();
            Core.ServiceLocator.Register<TerrainManager>(this);
            BuildRegionMap();
        }

        private void Start()
        {
            if (_player == null)
            {
                var go = GameObject.FindGameObjectWithTag("Player");
                if (go != null) _player = go.transform;
            }
            ApplyTerrainSettings();
        }

        private void Update()
        {
            if (_player == null) return;
            UpdateLOD();
            CheckCurrentRegion();
        }

        // ── Region Detection ─────────────────────────────────

        private void CheckCurrentRegion()
        {
            foreach (var region in _regions)
            {
                if (region.terrain == null) continue;
                if (region.bounds.Contains(_player.position))
                {
                    if (region != _currentRegion)
                    {
                        _currentRegion = region;
                        OnBiomeChanged?.Invoke(region.biome);
                        ApplyBiomeTransition(region.biome);
                    }
                    return;
                }
            }
        }

        // ── Biome Transition ─────────────────────────────────

        private void ApplyBiomeTransition(BiomeData biome)
        {
            if (biome == null) return;

            // Notify other systems
            if (Core.ServiceLocator.TryGet<WeatherSystem>(out var weather))
            {
                // Weather system will handle biome-specific weather
            }

            if (Core.ServiceLocator.TryGet<EchoesOfTheLost.Systems.AudioManager>(out var audio))
            {
                if (biome.ambientSounds != null && biome.ambientSounds.Length > 0)
                    audio.PlayAmbience(biome.ambientSounds[Random.Range(0, biome.ambientSounds.Length)]);

                if (biome.musicVariants != null && biome.musicVariants.Length > 0)
                    audio.PlayMusic(biome.musicVariants[Random.Range(0, biome.musicVariants.Length)]);
            }
        }

        // ── LOD Management ───────────────────────────────────

        private void UpdateLOD()
        {
            foreach (var region in _regions)
            {
                if (region.terrain == null) continue;

                float dist = Vector3.Distance(_player.position, region.bounds.center);
                float t = Mathf.Clamp01((dist - _nearDistance) / (_terrainSize * 2f));

                region.terrain.heightmapPixelError = Mathf.Lerp(_pixelError_Near, _pixelError_Far, t);
                region.terrain.detailObjectDistance = dist < _nearDistance ? _detailViewDistance : 0f;
            }
        }

        // ── Terrain Settings ─────────────────────────────────

        private void ApplyTerrainSettings()
        {
            foreach (var region in _regions)
            {
                if (region.terrain == null) continue;

                var t = region.terrain;
                t.treeBillboardDistance = _treeBillboardStart;
                t.treeMaximumFullLODCount = 50;
                t.treeCrossFadeLength = 5f;
                t.treeDistance = _treeMaxDistance;
                t.detailObjectDistance = _detailViewDistance;

                // Initialize bounds from terrain data
                region.bounds = new Bounds(
                    t.transform.position + new Vector3(_terrainSize * 0.5f, 0f, _terrainSize * 0.5f),
                    new Vector3(_terrainSize, 1000f, _terrainSize));
            }
        }

        private void BuildRegionMap()
        {
            foreach (var region in _regions)
                if (!string.IsNullOrEmpty(region.regionId))
                    _regionMap[region.regionId] = region;
        }

        // ── Public API ──────────────────────────────────────

        /// <summary>Get terrain height at a world XZ position.</summary>
        public float GetHeightAt(float worldX, float worldZ)
        {
            var terrain = GetTerrainAt(new Vector3(worldX, 0, worldZ));
            if (terrain == null) return 0f;
            return terrain.SampleHeight(new Vector3(worldX, 0, worldZ));
        }

        /// <summary>Get BiomeData at a world position.</summary>
        public BiomeData GetBiomeAt(Vector3 worldPos)
        {
            foreach (var region in _regions)
                if (region.bounds.Contains(worldPos)) return region.biome;
            return null;
        }

        private Terrain GetTerrainAt(Vector3 worldPos)
        {
            foreach (var region in _regions)
                if (region.bounds.Contains(worldPos)) return region.terrain;
            return Terrain.activeTerrain;
        }

        // ── Terrain Painting Helpers (Editor) ────────────────

        /// <summary>Apply biome terrain layers to a specific terrain.</summary>
        public void PaintBiomeLayers(Terrain terrain, BiomeData biome)
        {
            if (terrain == null || biome?.terrainLayers == null) return;
            terrain.terrainData.terrainLayers = biome.terrainLayers;
        }
    }
}
