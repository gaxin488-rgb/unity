using UnityEngine;

// ============================================================================
//  PIPELINE B — STAGE 4: ADD FOOTSTEP SOUNDS TO THE PLAYER
//  Assets/_Project/Code/Characters/Player/PlayerFootstepAudio.cs
//
//  Detects surface material underneath Kael (Terrain Splatmap, Placeholder
//  blockout tags, Stone bridges, Ruins masonry, Wood platforms) and plays
//  synchronized footstep audio clips with dynamic walk/sprint cadence
//  and subtle pitch/volume variation.
// ============================================================================
namespace EchoesOfTheLost.Characters.Player
{
    public enum SurfaceAudioType
    {
        Grass = 0,
        Stone = 1,
        Earth = 2,
        Wood = 3
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public class PlayerFootstepAudio : MonoBehaviour
    {
        [Header("Footstep Audio Clips by Surface")]
        [SerializeField] private AudioClip[] _grassFootsteps;
        [SerializeField] private AudioClip[] _stoneFootsteps;
        [SerializeField] private AudioClip[] _earthFootsteps;
        [SerializeField] private AudioClip[] _woodFootsteps;

        [Header("Cadence & Velocity Settings")]
        [SerializeField] [Range(0.25f, 0.75f)] private float _walkStepInterval = 0.48f;
        [SerializeField] [Range(0.15f, 0.45f)] private float _sprintStepInterval = 0.31f;
        [SerializeField] private float _minMoveSpeedThreshold = 0.45f;
        [SerializeField] private LayerMask _groundLayerMask = -1;

        [Header("Audio Modulation")]
        [SerializeField] [Range(0.2f, 1.0f)] private float _baseVolume = 0.82f;
        [SerializeField] [Range(0.02f, 0.25f)] private float _pitchVariation = 0.08f;
        [SerializeField] [Range(0.02f, 0.2f)] private float _volumeVariation = 0.06f;

        private AudioSource _audioSource;
        private PlayerController _playerController;
        private Rigidbody _rigidbody;
        private float _stepTimer;
        private SurfaceAudioType _currentSurface = SurfaceAudioType.Grass;

        public SurfaceAudioType CurrentSurface => _currentSurface;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0.75f; // blend between 2D & 3D for player footsteps
            _audioSource.rolloffMode = AudioRolloffMode.Linear;
            _audioSource.minDistance = 1.5f;
            _audioSource.maxDistance = 25f;

            _playerController = GetComponent<PlayerController>();
            _rigidbody = GetComponent<Rigidbody>();
        }

        public void AssignClips(
            AudioClip[] grassClips,
            AudioClip[] stoneClips,
            AudioClip[] earthClips,
            AudioClip[] woodClips)
        {
            _grassFootsteps = grassClips;
            _stoneFootsteps = stoneClips;
            _earthFootsteps = earthClips;
            _woodFootsteps = woodClips;
        }

        private void Update()
        {
            if (!Application.isPlaying) return;

            bool isGrounded = _playerController != null ? _playerController.IsGrounded : true;
            if (!isGrounded)
            {
                _stepTimer = 0f;
                return;
            }

            Vector3 horizontalVelocity = _rigidbody != null
                ? new Vector3(_rigidbody.linearVelocity.x, 0f, _rigidbody.linearVelocity.z)
                : Vector3.zero;

            float speed = horizontalVelocity.magnitude;
            if (speed < _minMoveSpeedThreshold)
            {
                _stepTimer = 0f;
                return;
            }

            bool isSprinting = _playerController != null && _playerController.IsSprinting;
            float interval = isSprinting ? _sprintStepInterval : _walkStepInterval;

            _stepTimer += Time.deltaTime;
            if (_stepTimer >= interval)
            {
                _stepTimer = 0f;
                DetectGroundSurfaceAndPlayStep();
            }
        }

        /// <summary>
        /// Raycasts down from feet to sample terrain splatmap or collider audio tags.
        /// </summary>
        public void DetectGroundSurfaceAndPlayStep()
        {
            Vector3 rayOrigin = transform.position + Vector3.up * 0.25f;
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 1.4f, _groundLayerMask, QueryTriggerInteraction.Ignore))
            {
                _currentSurface = IdentifySurface(hit);
            }

            PlayFootstepSound(_currentSurface);
        }

        private SurfaceAudioType IdentifySurface(RaycastHit hit)
        {
            // 1. Check if hit has PlaceholderObjectMarker
            if (hit.collider.TryGetComponent<World.PlaceholderObjectMarker>(out var marker))
            {
                string tag = marker.SurfaceAudioTag.ToLowerInvariant();
                if (tag.Contains("stone") || tag.Contains("rock")) return SurfaceAudioType.Stone;
                if (tag.Contains("wood") || tag.Contains("crate")) return SurfaceAudioType.Wood;
                if (tag.Contains("earth") || tag.Contains("dirt")) return SurfaceAudioType.Earth;
                if (tag.Contains("grass")) return SurfaceAudioType.Grass;
            }

            // 2. Check if hit is Unity Terrain — query alphamap weights
            if (hit.collider.TryGetComponent<Terrain>(out var terrain))
            {
                return GetDominantTerrainSurface(terrain, hit.point);
            }

            // 3. Check name or material hints
            string name = hit.collider.gameObject.name.ToLowerInvariant();
            if (name.Contains("stone") || name.Contains("rock") || name.Contains("bridge") || name.Contains("ruin") || name.Contains("pillar"))
                return SurfaceAudioType.Stone;
            if (name.Contains("wood") || name.Contains("deck") || name.Contains("crate"))
                return SurfaceAudioType.Wood;
            if (name.Contains("earth") || name.Contains("dirt") || name.Contains("mud"))
                return SurfaceAudioType.Earth;

            return SurfaceAudioType.Grass;
        }

        private SurfaceAudioType GetDominantTerrainSurface(Terrain terrain, Vector3 worldPos)
        {
            var td = terrain.terrainData;
            if (td == null || td.alphamapLayers == 0) return SurfaceAudioType.Grass;

            Vector3 tPos = terrain.transform.position;
            float normX = Mathf.Clamp01((worldPos.x - tPos.x) / td.size.x);
            float normZ = Mathf.Clamp01((worldPos.z - tPos.z) / td.size.z);

            int mapX = Mathf.Clamp((int)(normX * td.alphamapWidth), 0, td.alphamapWidth - 1);
            int mapZ = Mathf.Clamp((int)(normZ * td.alphamapHeight), 0, td.alphamapHeight - 1);

            float[,,] splat = td.GetAlphamaps(mapX, mapZ, 1, 1);
            int bestLayer = 0;
            float maxWeight = 0f;

            for (int i = 0; i < td.alphamapLayers; i++)
            {
                if (splat[0, 0, i] > maxWeight)
                {
                    maxWeight = splat[0, 0, i];
                    bestLayer = i;
                }
            }

            // In our TerrainWorldBuilder:
            // Layer 0 = TL_Grass
            // Layer 1 = TL_Earth
            // Layer 2 = TL_Road (Cobblestone)
            // Layer 3 = TL_Rock
            // Layer 4 = TL_Mountain (Stone)
            switch (bestLayer)
            {
                case 0: return SurfaceAudioType.Grass;
                case 1: return SurfaceAudioType.Earth;
                case 2:
                case 3:
                case 4: return SurfaceAudioType.Stone;
                default: return SurfaceAudioType.Grass;
            }
        }

        private void PlayFootstepSound(SurfaceAudioType surface)
        {
            AudioClip[] clipPool = null;
            switch (surface)
            {
                case SurfaceAudioType.Grass: clipPool = _grassFootsteps; break;
                case SurfaceAudioType.Stone: clipPool = _stoneFootsteps; break;
                case SurfaceAudioType.Earth: clipPool = _earthFootsteps; break;
                case SurfaceAudioType.Wood: clipPool = _woodFootsteps; break;
            }

            if (clipPool == null || clipPool.Length == 0) return;

            int index = Random.Range(0, clipPool.Length);
            AudioClip clip = clipPool[index];
            if (clip == null) return;

            float pitch = 1.0f + Random.Range(-_pitchVariation, _pitchVariation);
            float volume = _baseVolume + Random.Range(-_volumeVariation, _volumeVariation);

            _audioSource.pitch = pitch;
            _audioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
        }
    }
}
