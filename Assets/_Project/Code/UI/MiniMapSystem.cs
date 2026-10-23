using UnityEngine;
using System.Collections.Generic;

// ============================================================
//  MiniMapSystem — Render-texture minimap with fog of war
//  Attach to: Dedicated minimap camera GameObject
// ============================================================
namespace EchoesOfTheLost.UI
{
    public class MiniMapSystem : Utils.Singleton<MiniMapSystem>
    {
        [Header("Camera")]
        [SerializeField] private Camera _miniMapCamera;
        [SerializeField] private float _cameraHeight = 200f;
        [SerializeField] private float _orthographicSize = 75f;   // visible radius
        [SerializeField] private LayerMask _miniMapLayer;

        [Header("Render Texture")]
        [SerializeField] private RenderTexture _renderTexture;
        [SerializeField] private int _textureResolution = 256;

        [Header("Fog of War")]
        [Tooltip("Total world edge length in units (grid width * chunk size). Must be > 0.")]
        [SerializeField] private float _worldSize = 5000f;
        [SerializeField] private bool _enableFogOfWar = true;
        [SerializeField] private Texture2D _fogTexture;
        [SerializeField] private float _revealRadius = 80f;
        [SerializeField] private Color _fogColor = new Color(0f, 0f, 0f, 0.85f);

        [Header("Player Icon")]
        [SerializeField] private Transform _player;
        [SerializeField] private RectTransform _playerIconRect;

        [Header("Rotation")]
        [SerializeField] private bool _rotateWithPlayer = true;

        // ── State ─────────────────────────────────────────────
        private Dictionary<string, RectTransform> _mapMarkers = new Dictionary<string, RectTransform>();
        private HashSet<Vector2Int> _revealedCells = new HashSet<Vector2Int>();
        private const int FOG_RESOLUTION = 128;

        protected override void Awake()
        {
            base.Awake();

            if (_miniMapCamera == null)
                _miniMapCamera = GetComponentInChildren<Camera>();

            SetupMiniMapCamera();
            SetupFogOfWar();

            Core.ServiceLocator.Register<MiniMapSystem>(this);
        }

        private void Start()
        {
            if (_player == null)
            {
                var go = GameObject.FindGameObjectWithTag("Player");
                if (go != null) _player = go.transform;
            }

            // Subscribe to POI discovery
            if (Core.ServiceLocator.TryGet<World.POIManager>(out var poi))
                poi.OnPOIDiscovered += OnPOIDiscovered;
        }

        private void LateUpdate()
        {
            if (_player == null) return;
            FollowPlayer();
            UpdateFogOfWar();
        }

        // ── Camera Setup ─────────────────────────────────────

        private void SetupMiniMapCamera()
        {
            if (_miniMapCamera == null) return;

            if (_renderTexture == null)
            {
                _renderTexture = new RenderTexture(_textureResolution, _textureResolution, 16);
                _renderTexture.filterMode = FilterMode.Bilinear;
            }

            _miniMapCamera.targetTexture = _renderTexture;
            _miniMapCamera.orthographic = true;
            _miniMapCamera.orthographicSize = _orthographicSize;
            _miniMapCamera.cullingMask = _miniMapLayer;
            _miniMapCamera.clearFlags = CameraClearFlags.SolidColor;
            _miniMapCamera.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 1f);
        }

        private void FollowPlayer()
        {
            if (_miniMapCamera == null) return;

            // Follow player XZ
            Vector3 camPos = _player.position;
            camPos.y = _player.position.y + _cameraHeight;
            _miniMapCamera.transform.position = camPos;

            // Rotate with player (or keep north-up)
            if (_rotateWithPlayer)
                _miniMapCamera.transform.rotation = Quaternion.Euler(90f, _player.eulerAngles.y, 0f);
            else
                _miniMapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            // Update player icon direction
            if (_playerIconRect != null && _rotateWithPlayer)
                _playerIconRect.localEulerAngles = Vector3.zero;
            else if (_playerIconRect != null)
                _playerIconRect.localEulerAngles = new Vector3(0f, 0f, -_player.eulerAngles.y);
        }

        // ── Fog of War ────────────────────────────────────────

        private void SetupFogOfWar()
        {
            if (!_enableFogOfWar) return;

            _fogTexture = new Texture2D(FOG_RESOLUTION, FOG_RESOLUTION, TextureFormat.RGBA32, false);

            // Fill with fog
            Color[] pixels = new Color[FOG_RESOLUTION * FOG_RESOLUTION];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = _fogColor;
            _fogTexture.SetPixels(pixels);
            _fogTexture.Apply();
        }

        private void UpdateFogOfWar()
        {
            if (!_enableFogOfWar || _fogTexture == null || _player == null) return;

            // Convert world pos to fog texture cell
            Vector2Int cell = WorldToFogCell(_player.position);
            int revealRadius = Mathf.RoundToInt(_revealRadius / (_worldSize / FOG_RESOLUTION));
            revealRadius = Mathf.Clamp(revealRadius, 1, 20);

            bool changed = false;
            for (int x = cell.x - revealRadius; x <= cell.x + revealRadius; x++)
            {
                for (int y = cell.y - revealRadius; y <= cell.y + revealRadius; y++)
                {
                    var c = new Vector2Int(
                        Mathf.Clamp(x, 0, FOG_RESOLUTION - 1),
                        Mathf.Clamp(y, 0, FOG_RESOLUTION - 1));

                    if (!_revealedCells.Contains(c))
                    {
                        _revealedCells.Add(c);
                        _fogTexture.SetPixel(c.x, c.y, Color.clear);
                        changed = true;
                    }
                }
            }

            if (changed) _fogTexture.Apply();
        }

        private Vector2Int WorldToFogCell(Vector3 worldPos) =>
            new Vector2Int(
                Mathf.Clamp(Mathf.FloorToInt(worldPos.x / _worldSize * FOG_RESOLUTION), 0, FOG_RESOLUTION - 1),
                Mathf.Clamp(Mathf.FloorToInt(worldPos.z / _worldSize * FOG_RESOLUTION), 0, FOG_RESOLUTION - 1));

        // ── Markers ──────────────────────────────────────────

        public void AddMarker(string markerId, RectTransform iconPrefab, Vector3 worldPos)
        {
            if (_mapMarkers.ContainsKey(markerId)) return;
            _mapMarkers[markerId] = iconPrefab;
        }

        public void RemoveMarker(string markerId) => _mapMarkers.Remove(markerId);

        private void OnPOIDiscovered(World.PointOfInterest poi)
        {
            // Add POI icon to minimap
            Debug.Log($"[MiniMap] POI discovered: {poi.displayName}");
        }

        // ── Public API ───────────────────────────────────────

        public RenderTexture GetMiniMapTexture() => _renderTexture;
        public void SetZoom(float size)
        {
            _orthographicSize = Mathf.Clamp(size, 30f, 200f);
            if (_miniMapCamera != null) _miniMapCamera.orthographicSize = _orthographicSize;
        }

        public void ZoomIn() => SetZoom(_orthographicSize - 10f);
        public void ZoomOut() => SetZoom(_orthographicSize + 10f);
    }
}
