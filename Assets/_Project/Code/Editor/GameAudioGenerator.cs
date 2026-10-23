#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// ============================================================================
//  GameAudioGenerator — Procedural 16-Bit PCM WAV Audio Synthesizer
//  Generates clean, authentic sound effect assets directly in Unity:
//    - Footstep SFX: Grass, Stone, Earth, Wood
//    - Combat SFX: Light Slash, Heavy Slash, Hit Impact, Parry Spark, Block Thud
//    - Environment SFX: Wayshrine Attune, Drone Chirp, Speed Gate Enter
// ============================================================================
namespace EchoesOfTheLost.EditorTools
{
    public static class GameAudioGenerator
    {
        private const string AudioRootDir = "Assets/_Project/Art/Audio";
        private const string FootstepsDir = "Assets/_Project/Art/Audio/SFX/Footsteps";
        private const string CombatDir = "Assets/_Project/Art/Audio/SFX/Combat";
        private const string EnvDir = "Assets/_Project/Art/Audio/SFX/Environment";
        private const int SampleRate = 44100;

        [MenuItem("EotL/Audio/Generate All Procedural SFX (.wav)")]
        public static void GenerateAllFromMenu()
        {
            GenerateAllAudio();
            AssetDatabase.Refresh();
            Debug.Log("[GameAudioGenerator] Successfully generated all procedural .wav sound effects!");
        }

        public static void GenerateAllAudio()
        {
            Directory.CreateDirectory(FootstepsDir);
            Directory.CreateDirectory(CombatDir);
            Directory.CreateDirectory(EnvDir);

            // 1. Footstep SFX (2 variations each for natural cadence)
            CreateWavFile(Path.Combine(FootstepsDir, "SFX_Step_Grass_01.wav"), SynthesizeFootstepGrass(110f, 0.16f, 1));
            CreateWavFile(Path.Combine(FootstepsDir, "SFX_Step_Grass_02.wav"), SynthesizeFootstepGrass(125f, 0.15f, 2));

            CreateWavFile(Path.Combine(FootstepsDir, "SFX_Step_Stone_01.wav"), SynthesizeFootstepStone(750f, 0.10f, 1));
            CreateWavFile(Path.Combine(FootstepsDir, "SFX_Step_Stone_02.wav"), SynthesizeFootstepStone(820f, 0.09f, 2));

            CreateWavFile(Path.Combine(FootstepsDir, "SFX_Step_Earth_01.wav"), SynthesizeFootstepEarth(95f, 0.18f, 1));
            CreateWavFile(Path.Combine(FootstepsDir, "SFX_Step_Earth_02.wav"), SynthesizeFootstepEarth(105f, 0.17f, 2));

            CreateWavFile(Path.Combine(FootstepsDir, "SFX_Step_Wood_01.wav"), SynthesizeFootstepWood(230f, 0.14f, 1));
            CreateWavFile(Path.Combine(FootstepsDir, "SFX_Step_Wood_02.wav"), SynthesizeFootstepWood(255f, 0.13f, 2));

            // 2. Combat SFX
            CreateWavFile(Path.Combine(CombatDir, "SFX_Sword_Light_Slash.wav"), SynthesizeSwordWhoosh(2400f, 420f, 0.22f, 0.85f));
            CreateWavFile(Path.Combine(CombatDir, "SFX_Sword_Heavy_Slash.wav"), SynthesizeSwordWhoosh(1600f, 190f, 0.38f, 1.0f));
            CreateWavFile(Path.Combine(CombatDir, "SFX_Hit_Impact.wav"), SynthesizeHitImpact(180f, 0.28f));
            CreateWavFile(Path.Combine(CombatDir, "SFX_Parry_Spark.wav"), SynthesizeParryClang(1920f, 0.55f));
            CreateWavFile(Path.Combine(CombatDir, "SFX_Block_Thud.wav"), SynthesizeBlockThud(140f, 0.25f));

            // 3. Environment & Wayshrine SFX
            CreateWavFile(Path.Combine(EnvDir, "SFX_Aether_Shrine_Attune.wav"), SynthesizeShrineAttunement(1.8f));
            CreateWavFile(Path.Combine(EnvDir, "SFX_Aether_Drone_Chirp.wav"), SynthesizeDroneChirp(0.42f));
            CreateWavFile(Path.Combine(EnvDir, "SFX_Trigger_Gate_Pass.wav"), SynthesizeGatePass(0.65f));

            AssetDatabase.SaveAssets();
        }

        // ── WAV Synthesizers ────────────────────────────────────────────────

        private static float[] SynthesizeFootstepGrass(float basePitch, float duration, int seed)
        {
            int numSamples = Mathf.CeilToInt(duration * SampleRate);
            float[] samples = new float[numSamples];
            var rng = new System.Random(seed * 777);

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-t * 22f);

                // Low thud + soft rustle noise
                float thud = Mathf.Sin(2f * Mathf.PI * basePitch * t) * Mathf.Exp(-t * 28f);
                float noise = ((float)rng.NextDouble() * 2f - 1f) * 0.45f;
                // Soft filter noise
                samples[i] = (thud * 0.55f + noise * env) * env * 0.75f;
            }
            return samples;
        }

        private static float[] SynthesizeFootstepStone(float clickPitch, float duration, int seed)
        {
            int numSamples = Mathf.CeilToInt(duration * SampleRate);
            float[] samples = new float[numSamples];
            var rng = new System.Random(seed * 999);

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-t * 42f);

                float click = Mathf.Sin(2f * Mathf.PI * (clickPitch - t * 3000f) * t);
                float sub = Mathf.Sin(2f * Mathf.PI * 180f * t) * Mathf.Exp(-t * 35f);
                float grit = ((float)rng.NextDouble() * 2f - 1f) * 0.25f;

                samples[i] = (click * 0.6f + sub * 0.35f + grit) * env * 0.85f;
            }
            return samples;
        }

        private static float[] SynthesizeFootstepEarth(float basePitch, float duration, int seed)
        {
            int numSamples = Mathf.CeilToInt(duration * SampleRate);
            float[] samples = new float[numSamples];
            var rng = new System.Random(seed * 456);

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-t * 18f);

                float thud = Mathf.Sin(2f * Mathf.PI * basePitch * t) * Mathf.Exp(-t * 16f);
                float crumble = ((float)rng.NextDouble() * 2f - 1f) * 0.35f;

                samples[i] = (thud * 0.7f + crumble * env) * env * 0.78f;
            }
            return samples;
        }

        private static float[] SynthesizeFootstepWood(float knockPitch, float duration, int seed)
        {
            int numSamples = Mathf.CeilToInt(duration * SampleRate);
            float[] samples = new float[numSamples];

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-t * 26f);

                float knock1 = Mathf.Sin(2f * Mathf.PI * knockPitch * t);
                float knock2 = Mathf.Sin(2f * Mathf.PI * (knockPitch * 1.55f) * t) * 0.4f;

                samples[i] = (knock1 + knock2) * env * 0.8f;
            }
            return samples;
        }

        private static float[] SynthesizeSwordWhoosh(float startFreq, float endFreq, float duration, float volume)
        {
            int numSamples = Mathf.CeilToInt(duration * SampleRate);
            float[] samples = new float[numSamples];
            var rng = new System.Random(1337);

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / duration;
                float currentFreq = Mathf.Lerp(startFreq, endFreq, t * t);
                float env = Mathf.Sin(t * Mathf.PI);

                float noise = (float)rng.NextDouble() * 2f - 1f;
                float tone = Mathf.Sin(2f * Mathf.PI * currentFreq * ((float)i / SampleRate));

                samples[i] = (noise * 0.45f + tone * 0.55f) * env * volume;
            }
            return samples;
        }

        private static float[] SynthesizeHitImpact(float basePitch, float duration)
        {
            int numSamples = Mathf.CeilToInt(duration * SampleRate);
            float[] samples = new float[numSamples];
            var rng = new System.Random(2026);

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-t * 16f);

                float punch = Mathf.Sin(2f * Mathf.PI * (basePitch - t * 400f) * t) * Mathf.Exp(-t * 20f);
                float crunch = ((float)rng.NextDouble() * 2f - 1f) * 0.5f;

                samples[i] = (punch * 0.7f + crunch) * env * 0.95f;
            }
            return samples;
        }

        private static float[] SynthesizeParryClang(float clangFreq, float duration)
        {
            int numSamples = Mathf.CeilToInt(duration * SampleRate);
            float[] samples = new float[numSamples];

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-t * 7.5f);

                // High bell-like metallic chime with 3 non-harmonic overtones
                float tone1 = Mathf.Sin(2f * Mathf.PI * clangFreq * t);
                float tone2 = Mathf.Sin(2f * Mathf.PI * (clangFreq * 1.34f) * t) * 0.65f;
                float tone3 = Mathf.Sin(2f * Mathf.PI * (clangFreq * 2.18f) * t) * 0.35f;

                samples[i] = (tone1 + tone2 + tone3) * env * 0.68f;
            }
            return samples;
        }

        private static float[] SynthesizeBlockThud(float thudPitch, float duration)
        {
            int numSamples = Mathf.CeilToInt(duration * SampleRate);
            float[] samples = new float[numSamples];

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-t * 15f);

                float tone = Mathf.Sin(2f * Mathf.PI * thudPitch * t);
                samples[i] = tone * env * 0.9f;
            }
            return samples;
        }

        private static float[] SynthesizeShrineAttunement(float duration)
        {
            int numSamples = Mathf.CeilToInt(duration * SampleRate);
            float[] samples = new float[numSamples];

            // Harmonic chord: Aether Major Chord (C4=261.63Hz, E4=329.63Hz, G4=392.00Hz, C5=523.25Hz)
            float[] chord = { 261.63f, 329.63f, 392.00f, 523.25f };

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / SampleRate;
                float progress = (float)i / numSamples;
                float envelope = Mathf.Sin(progress * Mathf.PI);

                float sum = 0f;
                for (int c = 0; c < chord.Length; c++)
                {
                    float vibrato = Mathf.Sin(t * 5.5f + c) * 2.5f;
                    sum += Mathf.Sin(2f * Mathf.PI * (chord[c] + vibrato) * t);
                }

                samples[i] = (sum / chord.Length) * envelope * 0.72f;
            }
            return samples;
        }

        private static float[] SynthesizeDroneChirp(float duration)
        {
            int numSamples = Mathf.CeilToInt(duration * SampleRate);
            float[] samples = new float[numSamples];

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / duration;
                float env = Mathf.Sin(t * Mathf.PI);
                float freq = Mathf.Lerp(1200f, 2400f, Mathf.Sin(t * Mathf.PI));
                samples[i] = Mathf.Sin(2f * Mathf.PI * freq * ((float)i / SampleRate)) * env * 0.65f;
            }
            return samples;
        }

        private static float[] SynthesizeGatePass(float duration)
        {
            int numSamples = Mathf.CeilToInt(duration * SampleRate);
            float[] samples = new float[numSamples];

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / duration;
                float env = Mathf.Sin(t * Mathf.PI);
                float freq = Mathf.Lerp(300f, 1400f, t);
                samples[i] = Mathf.Sin(2f * Mathf.PI * freq * ((float)i / SampleRate)) * env * 0.7f;
            }
            return samples;
        }

        // ── Standard RIFF WAVE 16-Bit PCM Writer ────────────────────────────

        private static void CreateWavFile(string fullPath, float[] samples)
        {
            byte[] wavBytes = EncodeToWav(samples, SampleRate, 1);
            File.WriteAllBytes(fullPath, wavBytes);
            AssetDatabase.ImportAsset(fullPath, ImportAssetOptions.ForceUpdate);
        }

        private static byte[] EncodeToWav(float[] samples, int sampleRate, int channels)
        {
            int byteCount = samples.Length * channels * 2;
            using (var stream = new MemoryStream(44 + byteCount))
            using (var writer = new BinaryWriter(stream))
            {
                // RIFF chunk
                writer.Write(new char[] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + byteCount);
                writer.Write(new char[] { 'W', 'A', 'V', 'E' });

                // fmt chunk
                writer.Write(new char[] { 'f', 'm', 't', ' ' });
                writer.Write(16); // subchunk1 size
                writer.Write((short)1); // PCM
                writer.Write((short)channels);
                writer.Write(sampleRate);
                writer.Write(sampleRate * channels * 2); // byte rate
                writer.Write((short)(channels * 2)); // block align
                writer.Write((short)16); // bits per sample

                // data chunk
                writer.Write(new char[] { 'd', 'a', 't', 'a' });
                writer.Write(byteCount);

                for (int i = 0; i < samples.Length; i++)
                {
                    short sampleInt = (short)(Mathf.Clamp(samples[i], -1f, 1f) * 32767f);
                    writer.Write(sampleInt);
                }

                return stream.ToArray();
            }
        }
    }
}
#endif
