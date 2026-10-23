using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Collections;
using System.Collections.Generic;

// ============================================================
//  ChunkSystem — World streaming via Addressables
//  Attach to: World_Persistent scene
// ============================================================
namespace EchoesOfTheLost.World
{
    [System.Serializable]
    public class WorldChunk
    {
        public int chunkX;
        public int chunkZ;
        public string addressableKey; // e.g. "Chunk_00_00"
        public BiomeData biome;
        public bool isLoaded;
        public GameObject loadedObject;
        [System.NonSerialized] public AsyncOperationHandle<GameObject> loadHandle;

        public Vector3 WorldCenter => new Vector3(
            chunkX * ChunkSystem.CHUNK_SIZE + ChunkSystem.CHUNK_SIZE * 0.5f,
            0f,
            chunkZ * ChunkSystem.CHUNK_SIZE + ChunkSystem.CHUNK_SIZE * 0.5f);

        public bool IsValid => !string.IsNullOrEmpty(addressableKey);
    }

    public class ChunkSystem : Utils.Singleton<ChunkSystem>
    {
        // ── Constants ─────────────────────────────────────────
        public const float CHUNK_SIZE = 500f;
        public const int LOAD_RADIUS = 3;
        public const int UNLOAD_RADIUS = 5;

        [Header("World Grid")]
        [SerializeField] private int _worldGridWidth = 10;   // chunks X
        [SerializeField] private int _worldGridHeight = 10;  // chunks Z

        [Header("References")]
        [SerializeField] private Transform _player;

        [Header("Debug")]
        [SerializeField] private bool _showGizmos = true;

        // ── State ─────────────────────────────────────────────
        private WorldChunk[,] _grid;
        private HashSet<WorldChunk> _loadedChunks = new HashSet<WorldChunk>();
        private Queue<WorldChunk> _loadQueue = new Queue<WorldChunk>();
        private Queue<WorldChunk> _unloadQueue = new Queue<WorldChunk>();
        private Vector2Int _lastPlayerChunk = new Vector2Int(-999, -999);
        private bool _isProcessingQueues;

        public event System.Action<WorldChunk> OnChunkLoaded;
        public event System.Action<WorldChunk> OnChunkUnloaded;

        protected override void Awake()
        {
            base.Awake();
            InitializeGrid();
            Core.ServiceLocator.Register<ChunkSystem>(this);
        }

        private void Start()
        {
            if (_player == null)
            {
                var playerGO = GameObject.FindGameObjectWithTag("Player");
                if (playerGO != null) _player = playerGO.transform;
            }
        }

        private void Update()
        {
            if (_player == null) return;

            Vector2Int playerChunk = WorldToChunk(_player.position);

            // Only recalculate if player moved to a new chunk
            if (playerChunk != _lastPlayerChunk)
            {
                _lastPlayerChunk = playerChunk;
                UpdateLoadQueues(playerChunk);
            }

            if (!_isProcessingQueues)
                StartCoroutine(ProcessQueues());
        }

        // ── Grid Initialization ───────────────────────────────

        private void InitializeGrid()
        {
            _grid = new WorldChunk[_worldGridWidth, _worldGridHeight];
            for (int x = 0; x < _worldGridWidth; x++)
            {
                for (int z = 0; z < _worldGridHeight; z++)
                {
                    _grid[x, z] = new WorldChunk
                    {
                        chunkX = x,
                        chunkZ = z,
                        addressableKey = $"Chunk_{x:00}_{z:00}",
                        isLoaded = false
                    };
                }
            }
        }

        // ── Queue Management ──────────────────────────────────

        private void UpdateLoadQueues(Vector2Int center)
        {
            _loadQueue.Clear();
            _unloadQueue.Clear();

            // Collect chunks sorted by distance (nearest first)
            var toLoad = new List<(WorldChunk chunk, float dist)>();

            for (int x = 0; x < _worldGridWidth; x++)
            {
                for (int z = 0; z < _worldGridHeight; z++)
                {
                    var chunk = _grid[x, z];
                    if (!chunk.IsValid) continue;

                    int dx = x - center.x;
                    int dz = z - center.y;
                    float dist = Mathf.Sqrt(dx * dx + dz * dz);

                    if (dist <= LOAD_RADIUS && !chunk.isLoaded)
                        toLoad.Add((chunk, dist));
                    else if (dist > UNLOAD_RADIUS && chunk.isLoaded)
                        _unloadQueue.Enqueue(chunk);
                }
            }

            // Sort by nearest first for priority loading
            toLoad.Sort((a, b) => a.dist.CompareTo(b.dist));
            foreach (var (chunk, _) in toLoad)
                _loadQueue.Enqueue(chunk);
        }

        private IEnumerator ProcessQueues()
        {
            _isProcessingQueues = true;

            // Process unloads first (free memory)
            while (_unloadQueue.Count > 0)
            {
                UnloadChunk(_unloadQueue.Dequeue());
                yield return null; // spread over frames
            }

            // Process loads
            while (_loadQueue.Count > 0)
            {
                var chunk = _loadQueue.Dequeue();
                if (!chunk.isLoaded)
                    yield return LoadChunkAsync(chunk);
                else
                    yield return null;
            }

            _isProcessingQueues = false;
        }

        // ── Load / Unload ────────────────────────────────────

        private IEnumerator LoadChunkAsync(WorldChunk chunk)
        {
            if (chunk.isLoaded) yield break;

            var handle = Addressables.InstantiateAsync(chunk.addressableKey);
            chunk.loadHandle = handle;
            chunk.isLoaded = true; // mark early to prevent duplicate loads

            yield return handle;

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                chunk.loadedObject = handle.Result;
                chunk.loadedObject.name = chunk.addressableKey;
                _loadedChunks.Add(chunk);
                OnChunkLoaded?.Invoke(chunk);

                // Apply biome settings if available
                if (chunk.biome != null)
                    ApplyBiomeAmbience(chunk.biome);
            }
            else
            {
                Debug.LogWarning($"[ChunkSystem] Failed to load: {chunk.addressableKey}");
                chunk.isLoaded = false;
            }
        }

        private void UnloadChunk(WorldChunk chunk)
        {
            if (!chunk.isLoaded) return;

            if (chunk.loadHandle.IsValid())
                Addressables.ReleaseInstance(chunk.loadHandle);

            chunk.isLoaded = false;
            chunk.loadedObject = null;
            _loadedChunks.Remove(chunk);
            OnChunkUnloaded?.Invoke(chunk);
        }

        // ── Helpers ───────────────────────────────────────────

        public Vector2Int WorldToChunk(Vector3 worldPos)
        {
            return new Vector2Int(
                Mathf.FloorToInt(worldPos.x / CHUNK_SIZE),
                Mathf.FloorToInt(worldPos.z / CHUNK_SIZE));
        }

        public WorldChunk GetChunkAt(Vector3 worldPos)
        {
            var coord = WorldToChunk(worldPos);
            if (coord.x < 0 || coord.x >= _worldGridWidth ||
                coord.y < 0 || coord.y >= _worldGridHeight) return null;
            return _grid[coord.x, coord.y];
        }

        public BiomeData GetBiomeAt(Vector3 worldPos) => GetChunkAt(worldPos)?.biome;

        private void ApplyBiomeAmbience(BiomeData biome)
        {
            if (biome == null) return;
            RenderSettings.fogColor = biome.fogColor;
            RenderSettings.fogDensity = biome.fogDensity;
            RenderSettings.ambientLight = biome.ambientLight;
        }

        // ── Gizmos ────────────────────────────────────────────
        private void OnDrawGizmos()
        {
            if (!_showGizmos || _grid == null) return;

            for (int x = 0; x < _worldGridWidth; x++)
            {
                for (int z = 0; z < _worldGridHeight; z++)
                {
                    var chunk = _grid[x, z];
                    Gizmos.color = chunk.isLoaded
                        ? new Color(0f, 1f, 0f, 0.15f)
                        : new Color(1f, 1f, 1f, 0.05f);

                    Vector3 center = chunk.WorldCenter;
                    Gizmos.DrawWireCube(center, new Vector3(CHUNK_SIZE, 1f, CHUNK_SIZE));
                }
            }

            if (_player == null) return;
            Gizmos.color = new Color(0f, 0.5f, 1f, 0.1f);
            Gizmos.DrawSphere(_player.position, CHUNK_SIZE * LOAD_RADIUS);
            Gizmos.color = new Color(1f, 0.3f, 0f, 0.05f);
            Gizmos.DrawSphere(_player.position, CHUNK_SIZE * UNLOAD_RADIUS);
        }
    }
}
