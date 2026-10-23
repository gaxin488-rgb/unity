using UnityEngine;
using System.Collections;

// ============================================================
//  WaterSystem — Water physics, buoyancy, swim detection
//  Attach to: Water GameObject (with mesh + trigger collider)
// ============================================================
namespace EchoesOfTheLost.World
{
    // ── Buoyancy Physics ─────────────────────────────────────
    public class BuoyancyObject : MonoBehaviour
    {
        [Header("Buoyancy Settings")]
        [SerializeField] private float _buoyancyForce = 15f;
        [SerializeField] private float _waterDrag = 3f;
        [SerializeField] private float _waterAngularDrag = 1f;
        [SerializeField] private float _waterLevel = 0f;

        [Header("Float Points")]
        [SerializeField] private Transform[] _floatPoints;  // corners of the object

        private Rigidbody _rb;
        private float _defaultDrag;
        private float _defaultAngularDrag;
        private bool _isSubmerged;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            if (_rb != null)
            {
                _defaultDrag = _rb.linearDamping;
                _defaultAngularDrag = _rb.angularDamping;
            }
        }

        private void FixedUpdate()
        {
            if (_rb == null) return;

            bool anySubmerged = false;

            if (_floatPoints != null && _floatPoints.Length > 0)
            {
                foreach (var point in _floatPoints)
                {
                    if (point == null) continue;
                    float depth = _waterLevel - point.position.y;
                    if (depth > 0f)
                    {
                        anySubmerged = true;
                        float factor = Mathf.Clamp01(depth / 0.5f);
                        _rb.AddForceAtPosition(Vector3.up * _buoyancyForce * factor, point.position, ForceMode.Force);
                    }
                }
            }
            else
            {
                // Simple center-based buoyancy
                float depth = _waterLevel - transform.position.y;
                if (depth > 0f)
                {
                    anySubmerged = true;
                    _rb.AddForce(Vector3.up * _buoyancyForce * Mathf.Clamp01(depth), ForceMode.Force);
                }
            }

            // Apply water drag
            if (anySubmerged != _isSubmerged)
            {
                _isSubmerged = anySubmerged;
                _rb.linearDamping = anySubmerged ? _waterDrag : _defaultDrag;
                _rb.angularDamping = anySubmerged ? _waterAngularDrag : _defaultAngularDrag;
            }
        }
    }

    // ── Water Zone (Swim Trigger) ─────────────────────────────
    [RequireComponent(typeof(Collider))]
    public class WaterZone : MonoBehaviour
    {
        [Header("Water Properties")]
        [SerializeField] private float _waterLevel = 0f;
        [SerializeField] private bool _isOcean = false;
        [SerializeField] private float _currentStrength = 0f;
        [SerializeField] private Vector3 _currentDirection = Vector3.forward;

        [Header("Gameplay")]
        [SerializeField] private bool _isDeep = false;  // requires swim skill
        [SerializeField] private float _coldDamagePerSecond = 0f;  // for icy water

        [Header("Post-Processing")]
        [SerializeField] private Color _underwaterFogColor = new Color(0.1f, 0.4f, 0.6f, 1f);
        [SerializeField] private float _underwaterFogDensity = 0.08f;
        [SerializeField] private float _underwaterFogStartDistance = 5f;

        private Color _defaultFogColor;
        private float _defaultFogDensity;
        private float _defaultFogStart;
        private bool _playerIsUnderwater;
        private Transform _playerTransform;
        private Characters.Player.PlayerStats _playerStats;

        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
            _defaultFogColor = RenderSettings.fogColor;
            _defaultFogDensity = RenderSettings.fogDensity;
            _defaultFogStart = RenderSettings.fogStartDistance;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            _playerTransform = other.transform;
            _playerStats = other.GetComponent<Characters.Player.PlayerStats>();

            // Notify player controller about swimming state
            // PlayerController handles swim animation internally via physics detection
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            _playerTransform = null;
            _playerStats = null;

            if (_playerIsUnderwater)
                ExitUnderwater();
        }

        private void Update()
        {
            if (_playerTransform == null) return;

            bool isUnderwater = _playerTransform.position.y < _waterLevel - 0.5f;

            if (isUnderwater != _playerIsUnderwater)
            {
                _playerIsUnderwater = isUnderwater;
                if (isUnderwater) EnterUnderwater();
                else ExitUnderwater();
            }

            // Cold damage
            if (_coldDamagePerSecond > 0f && _playerStats != null)
                _playerStats.TakeDamage(_coldDamagePerSecond * Time.deltaTime, ignoreDefense: true);

            // Water current
            if (_currentStrength > 0f && _playerTransform.TryGetComponent<Rigidbody>(out var rb))
                rb.AddForce(_currentDirection.normalized * _currentStrength, ForceMode.Acceleration);
        }

        private void EnterUnderwater()
        {
            RenderSettings.fogColor = _underwaterFogColor;
            RenderSettings.fogDensity = _underwaterFogDensity;
            RenderSettings.fogStartDistance = _underwaterFogStartDistance;
            // TODO: Enable underwater post-processing (color grading, caustics)
        }

        private void ExitUnderwater()
        {
            RenderSettings.fogColor = _defaultFogColor;
            RenderSettings.fogDensity = _defaultFogDensity;
            RenderSettings.fogStartDistance = _defaultFogStart;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 0.5f, 1f, 0.2f);
            var col = GetComponent<Collider>();
            if (col != null) Gizmos.DrawCube(col.bounds.center, col.bounds.size);

            // Water level plane
            Gizmos.color = new Color(0f, 0.8f, 1f, 0.3f);
            Gizmos.DrawWireCube(
                new Vector3(transform.position.x, _waterLevel, transform.position.z),
                new Vector3(col?.bounds.size.x ?? 10f, 0.05f, col?.bounds.size.z ?? 10f));
        }
    }

    // ── Water Shader Properties ───────────────────────────────
    public class WaterShaderController : MonoBehaviour
    {
        [Header("Wave Settings")]
        [SerializeField] private float _waveSpeed = 0.5f;
        [SerializeField] private float _waveHeight = 0.3f;
        [SerializeField] private Vector2 _waveDirection = new Vector2(1f, 0.5f);
        [SerializeField] private float _waveFrequency = 0.3f;

        [Header("Visual")]
        [SerializeField] private Color _shallowColor = new Color(0.2f, 0.7f, 0.9f, 0.8f);
        [SerializeField] private Color _deepColor = new Color(0.05f, 0.2f, 0.5f, 0.95f);
        [SerializeField] private float _depthFadeDistance = 5f;
        [SerializeField] private float _foamThreshold = 0.4f;

        [Header("Reflection / Refraction")]
        [SerializeField] private float _reflectionStrength = 0.6f;
        [SerializeField] private float _refractionStrength = 0.1f;
        [SerializeField] private float _refractionDistortion = 0.05f;

        private Material _waterMaterial;
        private Renderer _renderer;

        private static readonly int _ShallowColor = Shader.PropertyToID("_ShallowColor");
        private static readonly int _DeepColor = Shader.PropertyToID("_DeepColor");
        private static readonly int _WaveSpeed = Shader.PropertyToID("_WaveSpeed");
        private static readonly int _WaveHeight = Shader.PropertyToID("_WaveHeight");
        private static readonly int _WaveDir = Shader.PropertyToID("_WaveDirection");
        private static readonly int _WaveFreq = Shader.PropertyToID("_WaveFrequency");
        private static readonly int _FoamThreshold = Shader.PropertyToID("_FoamThreshold");
        private static readonly int _DepthFade = Shader.PropertyToID("_DepthFadeDistance");
        private static readonly int _ReflStr = Shader.PropertyToID("_ReflectionStrength");

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            if (_renderer != null)
                _waterMaterial = _renderer.material; // instance
        }

        private void Start() => ApplyProperties();

        private void Update()
        {
            if (_waterMaterial == null) return;
            _waterMaterial.SetFloat(_WaveSpeed, _waveSpeed);
        }

        private void ApplyProperties()
        {
            if (_waterMaterial == null) return;
            _waterMaterial.SetColor(_ShallowColor, _shallowColor);
            _waterMaterial.SetColor(_DeepColor, _deepColor);
            _waterMaterial.SetFloat(_WaveSpeed, _waveSpeed);
            _waterMaterial.SetFloat(_WaveHeight, _waveHeight);
            _waterMaterial.SetVector(_WaveDir, _waveDirection);
            _waterMaterial.SetFloat(_WaveFreq, _waveFrequency);
            _waterMaterial.SetFloat(_FoamThreshold, _foamThreshold);
            _waterMaterial.SetFloat(_DepthFade, _depthFadeDistance);
            _waterMaterial.SetFloat(_ReflStr, _reflectionStrength);
        }
    }
}
