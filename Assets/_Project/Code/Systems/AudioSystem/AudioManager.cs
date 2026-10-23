using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// ============================================================
//  AudioManager — Pooled audio with music transitions
//  Attach to: _Boot scene persistent GameObject
// ============================================================
namespace EchoesOfTheLost.Systems
{
    public class AudioManager : Utils.Singleton<AudioManager>
    {
        [Header("Audio Sources")]
        [SerializeField] private AudioSource _musicSourceA;
        [SerializeField] private AudioSource _musicSourceB;
        [SerializeField] private AudioSource _ambienceSource;
        [SerializeField] private int _sfxPoolSize = 10;

        [Header("Default Volumes")]
        [SerializeField] [Range(0f, 1f)] private float _masterVolume = 1f;
        [SerializeField] [Range(0f, 1f)] private float _musicVolume = 0.7f;
        [SerializeField] [Range(0f, 1f)] private float _sfxVolume = 1f;
        [SerializeField] [Range(0f, 1f)] private float _ambienceVolume = 0.5f;

        [Header("Transition")]
        [SerializeField] private float _musicCrossfadeDuration = 1.5f;

        private List<AudioSource> _sfxPool = new List<AudioSource>();
        private AudioSource _activeMusicSource;
        private AudioSource _inactiveMusicSource;
        private Coroutine _crossfadeCoroutine;

        protected override void Awake()
        {
            base.Awake();

            // Create music sources if not assigned
            if (_musicSourceA == null) _musicSourceA = CreateAudioSource("Music_A");
            if (_musicSourceB == null) _musicSourceB = CreateAudioSource("Music_B");
            if (_ambienceSource == null) _ambienceSource = CreateAudioSource("Ambience");

            _activeMusicSource = _musicSourceA;
            _inactiveMusicSource = _musicSourceB;

            // Warm up SFX pool
            for (int i = 0; i < _sfxPoolSize; i++)
                _sfxPool.Add(CreateAudioSource($"SFX_{i:00}"));

            // Register with service locator
            Core.ServiceLocator.Register<AudioManager>(this);
        }

        // ── Music ───────────────────────────────────────────

        /// <summary>Play music with smooth crossfade.</summary>
        public void PlayMusic(AudioClip clip, bool loop = true, float? overrideVolume = null)
        {
            if (_activeMusicSource.clip == clip && _activeMusicSource.isPlaying) return;

            if (_crossfadeCoroutine != null) StopCoroutine(_crossfadeCoroutine);
            _crossfadeCoroutine = StartCoroutine(CrossfadeMusic(clip, loop, overrideVolume ?? _musicVolume * _masterVolume));
        }

        /// <summary>Stop music with fade out.</summary>
        public void StopMusic(float fadeTime = 1f)
        {
            if (_crossfadeCoroutine != null) StopCoroutine(_crossfadeCoroutine);
            StartCoroutine(FadeOut(_activeMusicSource, fadeTime));
        }

        // ── SFX ─────────────────────────────────────────────

        /// <summary>Play a 2D SFX (UI, global).</summary>
        public void PlaySFX(AudioClip clip, float volumeScale = 1f)
        {
            var source = GetFreeSFXSource();
            if (source == null) return;
            source.spatialBlend = 0f;
            source.PlayOneShot(clip, _sfxVolume * _masterVolume * volumeScale);
        }

        /// <summary>Play a 3D SFX at world position.</summary>
        public void PlaySFX(AudioClip clip, Vector3 position, float volumeScale = 1f)
        {
            AudioSource.PlayClipAtPoint(clip, position, _sfxVolume * _masterVolume * volumeScale);
        }

        // ── Ambience ─────────────────────────────────────────

        public void PlayAmbience(AudioClip clip, bool loop = true)
        {
            _ambienceSource.clip = clip;
            _ambienceSource.loop = loop;
            _ambienceSource.volume = _ambienceVolume * _masterVolume;
            _ambienceSource.Play();
        }

        public void StopAmbience() => _ambienceSource.Stop();

        // ── Volume Control ──────────────────────────────────

        public void SetMasterVolume(float v)
        {
            _masterVolume = Mathf.Clamp01(v);
            ApplyVolumes();
        }

        public void SetMusicVolume(float v)
        {
            _musicVolume = Mathf.Clamp01(v);
            _activeMusicSource.volume = _musicVolume * _masterVolume;
        }

        public void SetSFXVolume(float v) => _sfxVolume = Mathf.Clamp01(v);

        public void SetAmbienceVolume(float v)
        {
            _ambienceVolume = Mathf.Clamp01(v);
            _ambienceSource.volume = _ambienceVolume * _masterVolume;
        }

        // ── Private ─────────────────────────────────────────

        private IEnumerator CrossfadeMusic(AudioClip newClip, bool loop, float targetVolume)
        {
            _inactiveMusicSource.clip = newClip;
            _inactiveMusicSource.loop = loop;
            _inactiveMusicSource.volume = 0f;
            _inactiveMusicSource.Play();

            float elapsed = 0f;
            float startVolume = _activeMusicSource.volume;

            while (elapsed < _musicCrossfadeDuration)
            {
                float t = elapsed / _musicCrossfadeDuration;
                _activeMusicSource.volume = Mathf.Lerp(startVolume, 0f, t);
                _inactiveMusicSource.volume = Mathf.Lerp(0f, targetVolume, t);
                elapsed += Time.deltaTime;
                yield return null;
            }

            _activeMusicSource.Stop();
            (_activeMusicSource, _inactiveMusicSource) = (_inactiveMusicSource, _activeMusicSource);
        }

        private IEnumerator FadeOut(AudioSource source, float duration)
        {
            float startVolume = source.volume;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                source.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
                elapsed += Time.deltaTime;
                yield return null;
            }
            source.Stop();
        }

        private AudioSource GetFreeSFXSource()
        {
            foreach (var s in _sfxPool)
                if (!s.isPlaying) return s;
            // All busy — use oldest (just overwrite)
            return _sfxPool[0];
        }

        private AudioSource CreateAudioSource(string sourceName)
        {
            var go = new GameObject(sourceName);
            go.transform.SetParent(transform);
            return go.AddComponent<AudioSource>();
        }

        private void ApplyVolumes()
        {
            _activeMusicSource.volume = _musicVolume * _masterVolume;
            _ambienceSource.volume = _ambienceVolume * _masterVolume;
        }
    }
}
