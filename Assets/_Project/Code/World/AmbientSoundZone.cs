using UnityEngine;
using System.Collections;

// ============================================================
//  AmbientSoundZone — Spatial audio zones for environment
//  Attach to: Zone trigger colliders
// ============================================================
namespace EchoesOfTheLost.World
{
    [RequireComponent(typeof(Collider))]
    public class AmbientSoundZone : MonoBehaviour
    {
        [Header("Audio")]
        [SerializeField] private AudioClip _ambientClip;
        [SerializeField] private AudioClip[] _randomStingers;  // occasional random sounds
        [SerializeField] [Range(0f, 1f)] private float _volume = 0.5f;
        [SerializeField] private float _fadeTime = 2f;
        [SerializeField] private float _stingerMinInterval = 20f;
        [SerializeField] private float _stingerMaxInterval = 60f;

        [Header("3D Settings")]
        [SerializeField] private bool _is2D = true;       // 2D = global, 3D = positional
        [SerializeField] private float _spatialBlend = 0f; // 0=2D, 1=3D

        [Header("Exclusivity")]
        [SerializeField] private string _zoneTag = "ambient_default";
        [SerializeField] private bool _stopOtherAmbient = true;

        private AudioSource _audioSource;
        private bool _isPlayerInZone;
        private float _stingerTimer;
        private Coroutine _fadeCoroutine;

        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;

            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.clip = _ambientClip;
            _audioSource.loop = true;
            _audioSource.spatialBlend = _spatialBlend;
            _audioSource.volume = 0f;
            _audioSource.playOnAwake = false;

            ResetStingerTimer();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            _isPlayerInZone = true;

            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeAudio(0f, _volume, _fadeTime));

            if (!_audioSource.isPlaying) _audioSource.Play();
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            _isPlayerInZone = false;

            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeAndStop());
        }

        private void Update()
        {
            if (!_isPlayerInZone) return;
            if (_randomStingers == null || _randomStingers.Length == 0) return;

            _stingerTimer -= Time.deltaTime;
            if (_stingerTimer <= 0f)
            {
                PlayRandomStinger();
                ResetStingerTimer();
            }
        }

        private void PlayRandomStinger()
        {
            var clip = _randomStingers[Random.Range(0, _randomStingers.Length)];
            if (clip == null) return;

            var go = new GameObject("Stinger");
            go.transform.SetParent(transform);
            go.transform.localPosition = Vector3.zero;

            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.spatialBlend = _spatialBlend;
            src.volume = _volume * 0.6f;
            src.Play();

            Destroy(go, clip.length + 0.1f);
        }

        private IEnumerator FadeAudio(float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                _audioSource.volume = Mathf.Lerp(from, to, elapsed / duration);
                elapsed += Time.deltaTime;
                yield return null;
            }
            _audioSource.volume = to;
        }

        private IEnumerator FadeAndStop()
        {
            yield return FadeAudio(_audioSource.volume, 0f, _fadeTime);
            _audioSource.Stop();
        }

        private void ResetStingerTimer() =>
            _stingerTimer = Random.Range(_stingerMinInterval, _stingerMaxInterval);

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.1f);
            var col = GetComponent<Collider>();
            if (col != null) Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
        }
    }

    // ── Footstep Surface System ───────────────────────────────
    public enum FootstepSurface { Grass, Dirt, Stone, Wood, Metal, Sand, Snow, Water }

    [CreateAssetMenu(fileName = "FootstepData", menuName = "EotL/World/Footstep Data")]
    public class FootstepData : ScriptableObject
    {
        [System.Serializable]
        public class SurfaceClips
        {
            public FootstepSurface surface;
            public AudioClip[] clips;
        }

        public SurfaceClips[] surfaceSounds;

        public AudioClip GetClip(FootstepSurface surface)
        {
            foreach (var s in surfaceSounds)
            {
                if (s.surface == surface && s.clips != null && s.clips.Length > 0)
                    return s.clips[Random.Range(0, s.clips.Length)];
            }
            return null;
        }
    }

    public class FootstepPlayer : MonoBehaviour
    {
        [SerializeField] private FootstepData _footstepData;
        [SerializeField] private float _stepInterval = 0.45f;
        [SerializeField] private LayerMask _groundLayer;
        [SerializeField] private AudioSource _audioSource;

        private float _stepTimer;
        private Rigidbody _rb;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.spatialBlend = 0f;
        }

        private void Update()
        {
            if (_rb == null || _rb.linearVelocity.magnitude < 0.5f) return;

            _stepTimer -= Time.deltaTime;
            if (_stepTimer <= 0f)
            {
                _stepTimer = _stepInterval;
                PlayFootstep();
            }
        }

        private void PlayFootstep()
        {
            var surface = DetectSurface();
            var clip = _footstepData?.GetClip(surface);
            if (clip == null) return;

            _audioSource.pitch = Random.Range(0.9f, 1.1f);
            _audioSource.volume = Random.Range(0.7f, 1f);
            _audioSource.PlayOneShot(clip);
        }

        private FootstepSurface DetectSurface()
        {
            if (!Physics.Raycast(transform.position + Vector3.up * 0.2f,
                    Vector3.down, out var hit, 0.5f, _groundLayer))
                return FootstepSurface.Grass;

            // Read terrain texture or tag
            if (hit.collider.CompareTag("Water")) return FootstepSurface.Water;
            if (hit.collider.CompareTag("Wood")) return FootstepSurface.Wood;
            if (hit.collider.CompareTag("Stone")) return FootstepSurface.Stone;
            if (hit.collider.CompareTag("Metal")) return FootstepSurface.Metal;
            if (hit.collider.CompareTag("Sand")) return FootstepSurface.Sand;
            if (hit.collider.CompareTag("Snow")) return FootstepSurface.Snow;

            // Default terrain texture detection
            if (hit.collider.TryGetComponent<Terrain>(out var terrain))
                return DetectTerrainTexture(terrain, hit.point);

            return FootstepSurface.Grass;
        }

        private FootstepSurface DetectTerrainTexture(Terrain terrain, Vector3 hitPoint)
        {
            var td = terrain.terrainData;
            Vector3 normalized = hitPoint - terrain.transform.position;
            float x = normalized.x / td.size.x;
            float z = normalized.z / td.size.z;

            int mapX = Mathf.RoundToInt(x * td.alphamapWidth);
            int mapZ = Mathf.RoundToInt(z * td.alphamapHeight);
            mapX = Mathf.Clamp(mapX, 0, td.alphamapWidth - 1);
            mapZ = Mathf.Clamp(mapZ, 0, td.alphamapHeight - 1);

            float[,,] splatMap = td.GetAlphamaps(mapX, mapZ, 1, 1);

            // Find dominant texture
            int dominant = 0;
            float maxStrength = 0f;
            for (int i = 0; i < splatMap.GetLength(2); i++)
            {
                if (splatMap[0, 0, i] > maxStrength)
                {
                    maxStrength = splatMap[0, 0, i];
                    dominant = i;
                }
            }

            // Map texture index to surface (configure in editor per project)
            return dominant switch
            {
                0 => FootstepSurface.Grass,
                1 => FootstepSurface.Dirt,
                2 => FootstepSurface.Stone,
                3 => FootstepSurface.Sand,
                4 => FootstepSurface.Snow,
                _ => FootstepSurface.Grass
            };
        }
    }
}
