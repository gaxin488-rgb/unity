using UnityEngine;

// ============================================================
//  EnemySpawner — Manages spawn points, caps, respawn timers
//  Attach to: Spawn zone GameObjects in each chunk
// ============================================================
namespace EchoesOfTheLost.Characters.Enemies
{
    [System.Serializable]
    public class SpawnEntry
    {
        public GameObject enemyPrefab;
        [Range(0f, 1f)] public float weight = 1f;  // relative spawn probability
        public int maxPerSpawner = 3;
    }

    public class EnemySpawner : MonoBehaviour
    {
        [Header("Spawn Config")]
        [SerializeField] private SpawnEntry[] _spawnPool;
        [SerializeField] private int _maxActiveEnemies = 5;
        [SerializeField] private float _spawnRadius = 8f;
        [SerializeField] private float _spawnInterval = 30f;   // seconds between spawn checks
        [SerializeField] private float _respawnDelay = 120f;    // seconds before respawn

        [Header("Conditions")]
        [SerializeField] private float _minPlayerDistance = 20f;  // don't spawn if player too close
        [SerializeField] private float _maxPlayerDistance = 150f; // don't spawn if player too far
        [SerializeField] private bool _spawnOnlyAtNight;
        [SerializeField] private int _minPlayerLevel = 1;

        [Header("Debug")]
        [SerializeField] private bool _showGizmos = true;

        private System.Collections.Generic.List<GameObject> _activeEnemies
            = new System.Collections.Generic.List<GameObject>();
        private float _spawnTimer;
        private Transform _playerTransform;

        private void Start()
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) _playerTransform = go.transform;

            _spawnTimer = Random.Range(0f, _spawnInterval); // stagger spawners
        }

        private void Update()
        {
            CleanupDeadEnemies();

            _spawnTimer -= Time.deltaTime;
            if (_spawnTimer <= 0f)
            {
                _spawnTimer = _spawnInterval;
                TrySpawn();
            }
        }

        private void TrySpawn()
        {
            if (_activeEnemies.Count >= _maxActiveEnemies) return;
            if (!IsPlayerInRange()) return;
            if (_spawnOnlyAtNight && IsDay()) return;

            // Spawn 1–2 enemies per cycle
            int toSpawn = Random.Range(1, 3);
            for (int i = 0; i < toSpawn && _activeEnemies.Count < _maxActiveEnemies; i++)
                SpawnEnemy();
        }

        private void SpawnEnemy()
        {
            var entry = PickWeightedEntry();
            if (entry == null || entry.enemyPrefab == null) return;

            // Find valid NavMesh position
            Vector3 spawnPos = GetRandomSpawnPosition();
            if (spawnPos == Vector3.zero) return;

            var enemy = Instantiate(entry.enemyPrefab, spawnPos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            _activeEnemies.Add(enemy);
        }

        private SpawnEntry PickWeightedEntry()
        {
            if (_spawnPool == null || _spawnPool.Length == 0) return null;

            float totalWeight = 0f;
            foreach (var e in _spawnPool) totalWeight += e.weight;

            float roll = Random.Range(0f, totalWeight);
            float cumulative = 0f;

            foreach (var e in _spawnPool)
            {
                cumulative += e.weight;
                if (roll <= cumulative) return e;
            }

            return _spawnPool[_spawnPool.Length - 1];
        }

        private Vector3 GetRandomSpawnPosition()
        {
            for (int attempt = 0; attempt < 10; attempt++)
            {
                Vector2 rand = Random.insideUnitCircle * _spawnRadius;
                Vector3 candidate = transform.position + new Vector3(rand.x, 0f, rand.y);

                if (UnityEngine.AI.NavMesh.SamplePosition(candidate, out var hit, 2f, UnityEngine.AI.NavMesh.AllAreas))
                    return hit.position;
            }
            return Vector3.zero;
        }

        private bool IsPlayerInRange()
        {
            if (_playerTransform == null) return false;
            float dist = Vector3.Distance(transform.position, _playerTransform.position);
            return dist >= _minPlayerDistance && dist <= _maxPlayerDistance;
        }

        private bool IsDay()
        {
            if (Core.ServiceLocator.TryGet<World.TimeSystem>(out var ts))
                return ts.IsDaytime;
            return true;
        }

        private void CleanupDeadEnemies()
        {
            _activeEnemies.RemoveAll(e => e == null);
        }

        private void OnDrawGizmos()
        {
            if (!_showGizmos) return;
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.15f);
            Gizmos.DrawWireSphere(transform.position, _spawnRadius);
            Gizmos.color = new Color(1f, 1f, 0f, 0.05f);
            Gizmos.DrawWireSphere(transform.position, _minPlayerDistance);
        }
    }
}
