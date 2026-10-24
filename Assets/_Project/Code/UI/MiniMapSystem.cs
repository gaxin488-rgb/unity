using UnityEngine;
using System.Collections.Generic;

namespace EchoesOfTheLost.UI
{
    public class MiniMapSystem : Utils.Singleton<MiniMapSystem>
    {
        [Header("Camera")]
        [SerializeField] private Camera _miniMapCamera;
        [SerializeField] private float _cameraHeight = 65f;
        [SerializeField] private float _orthographicSize = 32f;
        [SerializeField] private LayerMask _miniMapLayer = ~0;

        [Header("Render Texture")]
        [SerializeField] private RenderTexture _renderTexture;
        [SerializeField] private int _textureResolution = 256;

        [Header("Fog of War")]
        [SerializeField] private float _worldSize = 5000f;
        [SerializeField] private bool _enableFogOfWar = false;
        [SerializeField] private Texture2D _fogTexture;
        [SerializeField] private float _revealRadius = 80f;
        [SerializeField] private Color _fogColor = new Color(0f, 0f, 0f, 0.85f);

        [Header("Player Icon")]
        [SerializeField] private Transform _player;
        [SerializeField] private RectTransform _playerIconRect;

        [Header("Rotation")]
        [SerializeField] private bool _rotateWithPlayer = false;

        private Dictionary<string, RectTransform> _mapMarkers = new Dictionary<string, RectTransform>();
        private HashSet<Vector2Int> _revealedCells = new HashSet<Vector2Int>();
        private const int FOG_RESOLUTION = 128;

        protected override void Awake()
        {
            base.Awake();

            if (_miniMapCamera == null)
                _miniMapCamera = GetComponent<Camera>() ?? GetComponentInChildren<Camera>();

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
        }

        private void LateUpdate()
        {
            if (_player == null) return;
            FollowPlayer();
            UpdateFogOfWar();
        }

        private void SetupMiniMapCamera()
        {
            if (_miniMapCamera == null) return;

            if (_renderTexture == null)
            {
                _renderTexture = new RenderTexture(_textureResolution, _textureResolution, 16);
                _renderTexture.filterMode = FilterMode.Bilinear;
            }

            // If _miniMapLayer was left unconfigured (0), render everything except UI (layer 5)
            int mask = _miniMapLayer.value == 0 ? ~(1 << 5) : (_miniMapLayer.value & ~(1 << 5));

            _miniMapCamera.targetTexture = _renderTexture;
            _miniMapCamera.orthographic = true;
            _miniMapCamera.orthographicSize = _orthographicSize;
            _miniMapCamera.nearClipPlane = 1f;
            _miniMapCamera.farClipPlane = 200f;
            _miniMapCamera.cullingMask = mask;
            _miniMapCamera.clearFlags = CameraClearFlags.SolidColor;
            _miniMapCamera.backgroundColor = new Color(0.18f, 0.34f, 0.16f, 1f);
        }

        private void FollowPlayer()
        {
            if (_miniMapCamera == null) return;

            Vector3 camPos = _player.position;
            camPos.y = _player.position.y + _cameraHeight;
            _miniMapCamera.transform.position = camPos;

            if (_rotateWithPlayer)
                _miniMapCamera.transform.rotation = Quaternion.Euler(90f, _player.eulerAngles.y, 0f);
            else
                _miniMapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            if (_playerIconRect != null)
            {
                float yaw = Camera.main != null ? Camera.main.transform.eulerAngles.y : _player.eulerAngles.y;
                _playerIconRect.localEulerAngles = _rotateWithPlayer ? Vector3.zero : new Vector3(0f, 0f, -yaw);
            }
        }

        private void SetupFogOfWar()
        {
            if (!_enableFogOfWar) return;

            _fogTexture = new Texture2D(FOG_RESOLUTION, FOG_RESOLUTION, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[FOG_RESOLUTION * FOG_RESOLUTION];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = _fogColor;
            _fogTexture.SetPixels(pixels);
            _fogTexture.Apply();
        }

        private void UpdateFogOfWar()
        {
            if (!_enableFogOfWar || _fogTexture == null || _player == null) return;

            Vector2Int cell = WorldToFogCell(_player.position);
            int revealRadius = Mathf.Clamp(Mathf.RoundToInt(_revealRadius / (_worldSize / FOG_RESOLUTION)), 1, 20);

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

        public RenderTexture GetMiniMapTexture() => _renderTexture;
        public void SetZoom(float size)
        {
            _orthographicSize = Mathf.Clamp(size, 18f, 150f);
            if (_miniMapCamera != null) _miniMapCamera.orthographicSize = _orthographicSize;
        }
    }
}
