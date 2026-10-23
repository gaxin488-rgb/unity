using UnityEngine;
using System.Collections.Generic;

// ============================================================
//  BiomeData — ScriptableObject defining a biome's properties
// ============================================================
namespace EchoesOfTheLost.World
{
    public enum WeatherType { Clear, Cloudy, Raining, Thunderstorm, Snowing, Foggy, Dusty, ResonanceStorm }

    [CreateAssetMenu(fileName = "Biome_New", menuName = "EotL/World/Biome Data")]
    public class BiomeData : ScriptableObject
    {
        [Header("Identity")]
        public string biomeId;
        public string biomeName;
        [TextArea(2, 3)] public string description;
        public Color minimapColor = Color.green;

        [Header("Environment")]
        public Material skyboxOverride;
        public Color fogColor = Color.white;
        public float fogDensity = 0.01f;
        public float fogStartDistance = 50f;
        public Color ambientLight = Color.white;

        [Header("Post-Processing")]
        public string colorGradingLUTPath; // path to LUT texture for this biome

        [Header("Terrain")]
        public TerrainLayer[] terrainLayers;
        public float heightScale = 50f;
        public float noiseScale = 0.005f;

        [Header("Vegetation")]
        public GameObject[] treePrefabs;
        public GameObject[] grassPrefabs;
        public GameObject[] rockPrefabs;
        public float vegetationDensity = 0.7f;

        [Header("Weather")]
        [Range(0f, 1f)] public float[] weatherProbabilities = new float[] { 0.5f, 0.3f, 0.2f };
        public WeatherType[] possibleWeather = new WeatherType[] { WeatherType.Clear, WeatherType.Cloudy, WeatherType.Raining };

        [Header("Audio")]
        public AudioClip[] ambientSounds;
        public AudioClip[] musicVariants;

        [Header("Gameplay")]
        public float temperatureModifier = 0f; // affects stamina regen in extreme biomes
        public bool allowFastTravel = true;
    }
}
