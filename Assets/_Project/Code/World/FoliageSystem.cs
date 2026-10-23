using UnityEngine;
using System.Collections.Generic;

// ============================================================
//  FoliageSystem — GPU-Instanced vegetation placement
//  Attach to: World_Persistent scene
// ============================================================
namespace EchoesOfTheLost.World
{
    [System.Serializable]
    public class FoliageLayer
    {
        public string layerId;
        public Mesh mesh;
        public Material material;
        public BiomeData[] allowedBiomes;
        [Range(0f, 1f)] public float density = 0.5f;
        public float minScale = 0.8f;
        public float maxScale = 1.3f;
        public float maxSlopeAngle = 35f;
        public float maxAltitude = 1000f;
        public float minAltitude = 0f;
        public bool castShadows = true;
        public bool receiveShadows = true;
        public float viewDistance = 100f;
    }

    [System.Serializable]
    public class FoliageChunk
    {
        public Vector2Int coord;
        public List<Matrix4x4>[] matricesPerLayer;
        public bool isDirty;
    }

    public class FoliageSystem : Utils.Singleton<FoliageSystem>
    {
        [Header("Foliage Layers")]
        [SerializeField] private FoliageLayer[] _layers;

        [Header("Generation Settings")]
        [SerializeField] private float _chunkSize = 50f;
        [SerializeField] private int _seed = 12345;
        [SerializeField] private float _noiseFrequency = 0.05f;
        [SerializeField] private int _maxInstancesPerChunk = 512;

        [Header("Rendering")]
        [SerializeField] private int _drawChunkRadius = 4;
        [SerializeField] private bool _frustumCull = true;

        [Header("Wind")]
        [SerializeField] private Vector4 _windParams = new Vector4(0.5f, 0.5f, 1f, 0.1f);
        // x=strength, y=frequency, z=turbulence, w=leaf flutter

        [Header("References")]
        [SerializeField] private Transform _player;

        // ── State ─────────────────────────────────────────────
        private Dictionary<Vector2Int, FoliageChunk> _chunks = new Dictionary<Vector2Int, FoliageChunk>();
        private Camera _mainCamera;
        private Plane[] _frustumPlanes = new Plane[6];
        private Vector2Int _lastPlayerChunk = new Vector2Int(int.MinValue, 0);

        protected override void Awake()
        {
            base.Awake();
            _mainCamera = Camera.main;
        }

        private void Start()
        {
            if (_player == null)
            {
                var go = GameObject.FindGameObjectWithTag("Player");
                if (go != null) _player = go.transform;
            }

            // Set wind shader globals
            Shader.SetGlobalVector("_WindParams", _windParams);
        }

        private void Update()
        {
            if (_player == null) return;

            Vector2Int playerChunk = WorldToChunk(_player.position);
            if (playerChunk != _lastPlayerChunk)
            {
                _lastPlayerChunk = playerChunk;
                UpdateChunks(playerChunk);
            }

            RenderFoliage();
        }

        // ── Chunk Management ─────────────────────────────────

        private void UpdateChunks(Vector2Int center)
        {
            // Unload far chunks
            var toRemove = new List<Vector2Int>();
            foreach (var kvp in _chunks)
            {
                int dx = Mathf.Abs(kvp.Key.x - center.x);
                int dz = Mathf.Abs(kvp.Key.y - center.y);
                if (dx > _drawChunkRadius + 1 || dz > _drawChunkRadius + 1)
                    toRemove.Add(kvp.Key);
            }
            foreach (var key in toRemove) _chunks.Remove(key);

            // Load/generate near chunks
            for (int x = center.x - _drawChunkRadius; x <= center.x + _drawChunkRadius; x++)
            {
                for (int z = center.y - _drawChunkRadius; z <= center.y + _drawChunkRadius; z++)
                {
                    var coord = new Vector2Int(x, z);
                    if (!_chunks.ContainsKey(coord))
                        _chunks[coord] = GenerateChunk(coord);
                }
            }
        }

        // ── Generation ───────────────────────────────────────

        private FoliageChunk GenerateChunk(Vector2Int coord)
        {
            var chunk = new FoliageChunk
            {
                coord = coord,
                matricesPerLayer = new List<Matrix4x4>[_layers.Length],
                isDirty = false
            };

            for (int i = 0; i < _layers.Length; i++)
                chunk.matricesPerLayer[i] = new List<Matrix4x4>();

            Vector3 chunkOrigin = new Vector3(coord.x * _chunkSize, 0f, coord.y * _chunkSize);
            var terrain = Terrain.activeTerrain;

            for (int i = 0; i < _layers.Length; i++)
            {
                var layer = _layers[i];
                int instanceCount = Mathf.RoundToInt(_maxInstancesPerChunk * layer.density);

                for (int j = 0; j < instanceCount; j++)
                {
                    // Deterministic pseudo-random using seed + coord
                    float px = SeededRandom(coord.x * 1000 + i * 100 + j, _seed) * _chunkSize;
                    float pz = SeededRandom(coord.y * 1000 + i * 100 + j, _seed + 1) * _chunkSize;

                    Vector3 worldPos = chunkOrigin + new Vector3(px, 0f, pz);

                    // Sample terrain height
                    if (terrain != null)
                    {
                        worldPos.y = terrain.SampleHeight(worldPos);

                        // Slope check
                        Vector3 normal = terrain.terrainData.GetInterpolatedNormal(
                            (worldPos.x - terrain.transform.position.x) / terrain.terrainData.size.x,
                            (worldPos.z - terrain.transform.position.z) / terrain.terrainData.size.z);

                        float slopeAngle = Vector3.Angle(normal, Vector3.up);
                        if (slopeAngle > layer.maxSlopeAngle) continue;
                    }

                    // Altitude check
                    if (worldPos.y < layer.minAltitude || worldPos.y > layer.maxAltitude) continue;

                    // Density noise
                    float noise = Mathf.PerlinNoise(
                        worldPos.x * _noiseFrequency + _seed,
                        worldPos.z * _noiseFrequency + _seed);
                    if (noise < 1f - layer.density) continue;

                    // Create instance matrix
                    float scale = Mathf.Lerp(layer.minScale, layer.maxScale,
                        SeededRandom(j * 7 + i, _seed + 2));
                    float rotY = SeededRandom(j * 13 + i, _seed + 3) * 360f;

                    var matrix = Matrix4x4.TRS(
                        worldPos,
                        Quaternion.Euler(0f, rotY, 0f),
                        Vector3.one * scale);

                    chunk.matricesPerLayer[i].Add(matrix);
                }
            }

            return chunk;
        }

        // ── Rendering ────────────────────────────────────────

        private void RenderFoliage()
        {
            if (_frustumCull && _mainCamera != null)
                GeometryUtility.CalculateFrustumPlanes(_mainCamera, _frustumPlanes);

            foreach (var kvp in _chunks)
            {
                var chunk = kvp.Value;

                for (int i = 0; i < _layers.Length; i++)
                {
                    if (_layers[i].mesh == null || _layers[i].material == null) continue;
                    if (chunk.matricesPerLayer[i].Count == 0) continue;

                    // Distance cull
                    Vector3 chunkCenter = new Vector3(
                        kvp.Key.x * _chunkSize + _chunkSize * 0.5f,
                        0f,
                        kvp.Key.y * _chunkSize + _chunkSize * 0.5f);

                    if (Vector3.Distance(_player.position, chunkCenter) > _layers[i].viewDistance) continue;

                    // Frustum cull
                    if (_frustumCull)
                    {
                        var bounds = new Bounds(chunkCenter, Vector3.one * _chunkSize * 1.5f);
                        if (!GeometryUtility.TestPlanesAABB(_frustumPlanes, bounds)) continue;
                    }

                    // Batch render (max 1023 per DrawMeshInstanced call)
                    var matrices = chunk.matricesPerLayer[i];
                    for (int start = 0; start < matrices.Count; start += 1023)
                    {
                        int count = Mathf.Min(1023, matrices.Count - start);
                        var batch = matrices.GetRange(start, count).ToArray();

                        Graphics.DrawMeshInstanced(
                            _layers[i].mesh,
                            0,
                            _layers[i].material,
                            batch,
                            count,
                            null,
                            _layers[i].castShadows
                                ? UnityEngine.Rendering.ShadowCastingMode.On
                                : UnityEngine.Rendering.ShadowCastingMode.Off,
                            _layers[i].receiveShadows);
                    }
                }
            }
        }

        // ── Helpers ───────────────────────────────────────────

        private Vector2Int WorldToChunk(Vector3 worldPos)
            => new Vector2Int(
                Mathf.FloorToInt(worldPos.x / _chunkSize),
                Mathf.FloorToInt(worldPos.z / _chunkSize));

        private float SeededRandom(int index, int seed)
        {
            uint n = (uint)(index * 1664525 + seed * 1013904223);
            n = n ^ (n >> 16);
            n *= 0x45d9f3b;
            n = n ^ (n >> 16);
            return (n & 0xFFFF) / 65535f;
        }
    }
}
