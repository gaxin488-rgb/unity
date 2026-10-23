#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

// ============================================================
//  EnvironmentTextureGenerator — Procedural seamless PBR
//  Albedo + Normal Map generator, URP Material builder, and
//  TerrainLayer creator for:
//    1. Đất (Earth / Forest Soil)
//    2. Cỏ (Meadow Grass & 3D Grass Tufts)
//    3. Cây (Tree Bark & Canopy Foliage)
//    4. Núi (Craggy Mountain Cliff Strata)
//    5. Đá (Mossy Natural Rock & Ancient Solenne Ruin Stone)
//    6. Đường đi (Cobblestone & Dirt Trail Road)
// ============================================================
namespace EchoesOfTheLost.EditorTools
{
    public struct EnvironmentMaterialSet
    {
        public Material Grass;
        public Material GrassTuft;
        public Material Earth;
        public Material Road;
        public Material Rock;
        public Material RuinStone;
        public Material Mountain;
        public Material TreeTrunk;
        public Material TreeLeaves;
        public Material Crystal;
        public Material FlowerYellow;
        public Material FlowerRed;
        public Material FlowerBlue;
        public Material FlowerPurple;
        public Material FlowerStem;
        public TerrainLayer[] TerrainLayers;
    }

    public static class EnvironmentTextureGenerator
    {
        private const string TexDir = "Assets/_Project/Art/Textures/Environment";
        private const string MatDir = "Assets/_Project/Art/Materials";
        private const string LayerDir = "Assets/_Project/Art/TerrainLayers";
        private const int TexSize = 256;

        [MenuItem("EotL/Art/Generate Environment Textures & Materials")]
        public static void GenerateFromMenu()
        {
            GenerateAll(forceRegenerateTextures: true);
            Debug.Log("[EnvironmentTextureGenerator] Generated all 16 PBR textures, 10 URP materials, and 5 TerrainLayers!");
        }

        public static EnvironmentMaterialSet GenerateAll(bool forceRegenerateTextures = false)
        {
            Directory.CreateDirectory(TexDir);
            Directory.CreateDirectory(MatDir);
            Directory.CreateDirectory(LayerDir);

            EnsureURPPipelineAsset();

            // 1. Grass (Cỏ thảo nguyên)
            EnsureTexturePair("Tex_Grass", forceRegenerateTextures, SampleGrass, normalStrength: 2.2f);
            // 2. Earth (Khối đất rừng)
            EnsureTexturePair("Tex_Earth", forceRegenerateTextures, SampleEarth, normalStrength: 2.8f);
            // 3. Road (Đường đi lát đá cuội xen đất)
            EnsureTexturePair("Tex_Road", forceRegenerateTextures, SampleRoad, normalStrength: 3.4f);
            // 4. Rock (Đá tảng tự nhiên bám rêu)
            EnsureTexturePair("Tex_Rock", forceRegenerateTextures, SampleRock, normalStrength: 3.6f);
            // 5. RuinStone (Đá cổ vật Solenne)
            EnsureTexturePair("Tex_RuinStone", forceRegenerateTextures, SampleRuinStone, normalStrength: 3.2f);
            // 6. Mountain (Vách núi đá phân tầng)
            EnsureTexturePair("Tex_Mountain", forceRegenerateTextures, SampleMountain, normalStrength: 4.0f);
            // 7. TreeBark (Vỏ thân cây)
            EnsureTexturePair("Tex_TreeBark", forceRegenerateTextures, SampleTreeBark, normalStrength: 3.5f);
            // 8. Foliage (Tán lá cây)
            EnsureTexturePair("Tex_Foliage", forceRegenerateTextures, SampleFoliage, normalStrength: 2.4f);
            // 9. GrassBlade (Bụi cỏ với ngọn nhọn và nền Alpha trong suốt)
            EnsureGrassBladeTextures(forceRegenerateTextures);
            // 10. LeafCluster (Tán lá cây với viền răng cưa và nền Alpha trong suốt)
            EnsureLeafClusterTextures(forceRegenerateTextures);

            AssetDatabase.Refresh();

            // Configure importers (Normal map flag, wrap mode, filtering)
            string[] names =
            {
                "Tex_Grass", "Tex_Earth", "Tex_Road", "Tex_Rock",
                "Tex_RuinStone", "Tex_Mountain", "Tex_TreeBark", "Tex_Foliage"
            };
            foreach (string n in names)
            {
                ConfigureTextureImporter($"{TexDir}/{n}_Albedo.png", isNormal: false);
                ConfigureTextureImporter($"{TexDir}/{n}_Normal.png", isNormal: true);
            }
            ConfigureTextureImporter($"{TexDir}/Tex_GrassBlade_Albedo.png", isNormal: false, isAlphaCutout: true);
            ConfigureTextureImporter($"{TexDir}/Tex_GrassBlade_Normal.png", isNormal: true, isAlphaCutout: true);
            ConfigureTextureImporter($"{TexDir}/Tex_LeafCluster_Albedo.png", isNormal: false, isAlphaCutout: true);
            ConfigureTextureImporter($"{TexDir}/Tex_LeafCluster_Normal.png", isNormal: true, isAlphaCutout: true);

            // Build URP Materials
            var set = new EnvironmentMaterialSet
            {
                Grass = CreateOrUpdatePBRMaterial("Mat_Grass.mat", "Tex_Grass",
                    Color.white, smoothness: 0.16f, bumpScale: 1.15f, tiling: new Vector2(24f, 24f)),

                // Bụi cỏ Alpha Cutout: 2 mặt (Cull Off), ngọn cỏ nhọn, nền trong suốt
                GrassTuft = CreateOrUpdateCutoutMaterial("Mat_GrassTuft.mat", "Tex_GrassBlade",
                    Color.white, cutoff: 0.35f),

                Earth = CreateOrUpdatePBRMaterial("Mat_Earth.mat", "Tex_Earth",
                    Color.white, smoothness: 0.12f, bumpScale: 1.3f, tiling: new Vector2(5f, 2f)),

                Road = CreateOrUpdatePBRMaterial("Mat_Road.mat", "Tex_Road",
                    Color.white, smoothness: 0.26f, bumpScale: 1.45f, tiling: new Vector2(2f, 6f)),

                Rock = CreateOrUpdatePBRMaterial("Mat_Rock.mat", "Tex_Rock",
                    Color.white, smoothness: 0.22f, bumpScale: 1.45f, tiling: new Vector2(3f, 3f)),

                RuinStone = CreateOrUpdatePBRMaterial("Mat_RuinStone.mat", "Tex_RuinStone",
                    Color.white, smoothness: 0.28f, bumpScale: 1.35f, tiling: new Vector2(6f, 6f)),

                Mountain = CreateOrUpdatePBRMaterial("Mat_Mountain.mat", "Tex_Mountain",
                    Color.white, smoothness: 0.15f, bumpScale: 1.5f, tiling: new Vector2(5f, 4f)),

                TreeTrunk = CreateOrUpdatePBRMaterial("Mat_TreeTrunk.mat", "Tex_TreeBark",
                    Color.white, smoothness: 0.12f, bumpScale: 1.4f, tiling: new Vector2(2f, 3f)),

                // Tán lá cây Alpha Cutout: 2 mặt (Cull Off), viền răng cưa tự nhiên, không bị tròn đặc
                TreeLeaves = CreateOrUpdateCutoutMaterial("Mat_TreeLeaves.mat", "Tex_LeafCluster",
                    Color.white, cutoff: 0.35f),

                Crystal = CreateOrUpdateEmissiveMaterial("Mat_EchoCrystal.mat",
                    new Color(0.18f, 0.92f, 1.0f), new Color(0.12f, 0.78f, 0.95f) * 2.4f),

                // 4 Distinct Wildflower Materials: Yellow, Red, Blue, Purple + Green Stem
                FlowerYellow = CreateOrUpdateVibrantFlowerMaterial("Mat_Flower_Yellow.mat", new Color(1.0f, 0.88f, 0.08f)),
                FlowerRed = CreateOrUpdateVibrantFlowerMaterial("Mat_Flower_Red.mat", new Color(0.96f, 0.16f, 0.14f)),
                FlowerBlue = CreateOrUpdateVibrantFlowerMaterial("Mat_Flower_Blue.mat", new Color(0.18f, 0.62f, 1.0f)),
                FlowerPurple = CreateOrUpdateVibrantFlowerMaterial("Mat_Flower_Purple.mat", new Color(0.78f, 0.32f, 0.96f)),
                FlowerStem = CreateOrUpdatePBRMaterial("Mat_Flower_Stem.mat", "Tex_Foliage",
                    new Color(0.28f, 0.58f, 0.18f), smoothness: 0.14f, bumpScale: 0.9f, tiling: new Vector2(2f, 2f))
            };

            // Build Unity TerrainLayers for Terrain painting & BiomeData
            set.TerrainLayers = new[]
            {
                CreateOrUpdateTerrainLayer("TL_Grass.terrainlayer", "Tex_Grass", new Vector2(8f, 8f), 1.0f),
                CreateOrUpdateTerrainLayer("TL_Earth.terrainlayer", "Tex_Earth", new Vector2(6f, 6f), 1.2f),
                CreateOrUpdateTerrainLayer("TL_Road.terrainlayer", "Tex_Road", new Vector2(4f, 4f), 1.3f),
                CreateOrUpdateTerrainLayer("TL_Rock.terrainlayer", "Tex_Rock", new Vector2(7f, 7f), 1.4f),
                CreateOrUpdateTerrainLayer("TL_Mountain.terrainlayer", "Tex_Mountain", new Vector2(12f, 12f), 1.5f)
            };

            AssetDatabase.SaveAssets();
            return set;
        }

        private static void EnsureURPPipelineAsset()
        {
            const string rpDir = "Assets/_Project/Settings/Rendering";
            Directory.CreateDirectory(rpDir);
            const string assetPath = rpDir + "/EotL_URP_Asset.asset";
            const string rendererPath = rpDir + "/EotL_URP_Renderer.asset";

            var urpAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>(assetPath);
            if (urpAsset == null || urpAsset.scriptableRenderer == null)
            {
                if (urpAsset != null)
                    AssetDatabase.DeleteAsset(assetPath);
                if (AssetDatabase.LoadAssetAtPath<Object>(rendererPath) != null)
                    AssetDatabase.DeleteAsset(rendererPath);

                urpAsset = UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset.Create();
                var so = new SerializedObject(urpAsset);
                var prop = so.FindProperty("m_RendererDataList");
                if (prop != null && prop.arraySize > 0)
                {
                    var rendererObj = prop.GetArrayElementAtIndex(0).objectReferenceValue;
                    if (rendererObj != null && !EditorUtility.IsPersistent(rendererObj))
                    {
                        rendererObj.name = "EotL_URP_Renderer";
                        AssetDatabase.CreateAsset(rendererObj, rendererPath);
                    }
                }
                AssetDatabase.CreateAsset(urpAsset, assetPath);
                AssetDatabase.SaveAssets();
            }

            if (UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline != urpAsset)
                UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = urpAsset;

            int qCount = QualitySettings.names.Length;
            int origQ = QualitySettings.GetQualityLevel();
            for (int i = 0; i < qCount; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                if (QualitySettings.renderPipeline != urpAsset)
                    QualitySettings.renderPipeline = urpAsset;
            }
            QualitySettings.SetQualityLevel(origQ, false);
        }

        private static float SmoothRange(float edge0, float edge1, float x)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(edge0, edge1, x));
        }

        // ── Procedural Surface Samplers (Seamless Toroidal UV) ──

        private delegate void SurfaceSampler(float u, float v, out Color albedo, out float height);

        // 1. CỎ (Lush stylized meadow grass with clover/moss variation & blades)
        private static void SampleGrass(float u, float v, out Color albedo, out float height)
        {
            float macro = TileFBM(u, v, 3, 3, 21.7f);
            float mid = TileFBM(u, v, 8, 8, 53.1f);
            float blades = TileFBM(u, v * 2.5f, 20, 32, 91.4f);

            Color deepGreen = new Color(0.16f, 0.36f, 0.14f);
            Color lushGreen = new Color(0.28f, 0.56f, 0.21f);
            Color sunTip = new Color(0.48f, 0.72f, 0.26f);
            Color cloverSoil = new Color(0.24f, 0.30f, 0.15f);

            Color c = Color.Lerp(deepGreen, lushGreen, SmoothRange(0.2f, 0.8f, macro));
            c = Color.Lerp(c, sunTip, SmoothRange(0.55f, 0.92f, blades * 0.7f + mid * 0.3f));
            c = Color.Lerp(c, cloverSoil, SmoothRange(0.72f, 0.95f, 1f - mid) * 0.35f);

            height = Mathf.Clamp01(blades * 0.55f + mid * 0.30f + macro * 0.15f);
            albedo = new Color(c.r, c.g, c.b, 0.18f);
        }

        // 2. ĐẤT (Rich forest soil with organic humus, small pebbles, and root flecks)
        private static void SampleEarth(float u, float v, out Color albedo, out float height)
        {
            float baseSoil = TileFBM(u, v, 4, 4, 14.3f);
            float crumbs = TileFBM(u, v, 14, 14, 67.8f);
            float pebbles = CellularStone(u, v, 10, 10, 112.5f, out float edgeDist);

            Color darkLoam = new Color(0.24f, 0.17f, 0.12f);
            Color warmDirt = new Color(0.42f, 0.30f, 0.20f);
            Color dryClay = new Color(0.54f, 0.41f, 0.28f);
            Color pebbleCol = new Color(0.52f, 0.49f, 0.45f);

            Color c = Color.Lerp(darkLoam, warmDirt, baseSoil);
            c = Color.Lerp(c, dryClay, SmoothRange(0.45f, 0.85f, crumbs) * 0.6f);

            float pebbleMask = SmoothRange(0.74f, 0.88f, pebbles) * SmoothRange(0.05f, 0.20f, edgeDist);
            c = Color.Lerp(c, pebbleCol, pebbleMask * 0.7f);

            height = Mathf.Clamp01(baseSoil * 0.3f + crumbs * 0.45f + pebbleMask * 0.25f);
            albedo = new Color(c.r, c.g, c.b, 0.14f);
        }

        // 3. ĐƯỜNG ĐI (Cobblestone & compacted dirt trail road with distinct stone pavers)
        private static void SampleRoad(float u, float v, out Color albedo, out float height)
        {
            float cellId = CellularStone(u, v, 8, 8, 305.2f, out float mortarDist);
            float stoneGrain = TileFBM(u, v, 12, 12, 411.9f);
            float dirtWear = TileFBM(u, v, 4, 4, 509.1f);

            float stoneMask = SmoothRange(0.05f, 0.15f, mortarDist);

            Color mortarDirt = new Color(0.24f, 0.18f, 0.13f);
            Color warmCobble = new Color(0.68f, 0.57f, 0.44f);
            Color coolCobble = new Color(0.55f, 0.49f, 0.42f);
            Color goldCobble = new Color(0.76f, 0.65f, 0.50f);

            Color stoneCol = Color.Lerp(warmCobble, coolCobble, cellId);
            stoneCol = Color.Lerp(stoneCol, goldCobble, SmoothRange(0.5f, 0.9f, stoneGrain) * 0.6f);
            stoneCol *= (0.88f + 0.24f * stoneGrain);

            Color c = Color.Lerp(mortarDirt, stoneCol, stoneMask);

            height = Mathf.Clamp01(stoneMask * (0.70f + 0.25f * stoneGrain) + (1f - stoneMask) * 0.12f * dirtWear);
            albedo = new Color(c.r, c.g, c.b, 0.26f);
        }

        // 4. ĐÁ TỰ NHIÊN (Cracked blue-grey granite boulder with subtle moss crevices)
        private static void SampleRock(float u, float v, out Color albedo, out float height)
        {
            float facets = CellularStone(u, v, 6, 6, 612.4f, out float crackDist);
            float detail = TileFBM(u, v, 12, 12, 719.0f);
            float mossNoise = TileFBM(u, v, 4, 4, 823.6f);

            float crackMask = SmoothRange(0.03f, 0.12f, crackDist);

            Color crevice = new Color(0.16f, 0.18f, 0.21f);
            Color darkSlate = new Color(0.42f, 0.45f, 0.50f);
            Color lightGranite = new Color(0.66f, 0.68f, 0.71f);
            Color lichenMoss = new Color(0.30f, 0.48f, 0.25f);

            Color rockCol = Color.Lerp(darkSlate, lightGranite, facets * 0.55f + detail * 0.45f);
            Color c = Color.Lerp(crevice, rockCol, crackMask);

            float mossMask = SmoothRange(0.72f, 0.92f, mossNoise) * (1f - crackMask);
            c = Color.Lerp(c, lichenMoss, mossMask * 0.5f);

            height = Mathf.Clamp01(crackMask * (0.55f + 0.40f * detail));
            albedo = new Color(c.r, c.g, c.b, 0.22f);
        }

        // 5. ĐÁ DI TÍCH CỔ SOLENNE (Carved ashlar limestone blocks with crisp masonry joints)
        private static void SampleRuinStone(float u, float v, out Color albedo, out float height)
        {
            float rows = 4f;
            float rowIndex = Mathf.Floor(v * rows);
            float rowV = Mathf.Repeat(v * rows, 1f);
            float offsetU = Mathf.Repeat(u * 2f + (rowIndex % 2f) * 0.5f, 1f);

            float bevelU = SmoothRange(0.0f, 0.06f, offsetU) * SmoothRange(0.0f, 0.06f, 1f - offsetU);
            float bevelV = SmoothRange(0.0f, 0.08f, rowV) * SmoothRange(0.0f, 0.08f, 1f - rowV);
            float blockMask = bevelU * bevelV;

            float weathering = TileFBM(u, v, 8, 8, 915.3f);
            float finePores = TileFBM(u, v, 18, 18, 991.7f);

            Color mortar = new Color(0.24f, 0.25f, 0.23f);
            Color baseStone = new Color(0.66f, 0.64f, 0.58f);
            Color warmLimestone = new Color(0.79f, 0.76f, 0.68f);

            Color blockCol = Color.Lerp(baseStone, warmLimestone, weathering * 0.65f + finePores * 0.35f);
            Color c = Color.Lerp(mortar, blockCol, blockMask);

            height = Mathf.Clamp01(blockMask * (0.75f + 0.20f * finePores) + (1f - blockMask) * 0.08f);
            albedo = new Color(c.r, c.g, c.b, 0.28f);
        }

        // 6. NÚI (Layered geological cliff strata + angular rock fractures + snow/moss ledges)
        private static void SampleMountain(float u, float v, out Color albedo, out float height)
        {
            float warp = TileFBM(u, v, 4, 4, 1102.4f) * 0.18f;
            float strata = Mathf.Sin((v + warp) * Mathf.PI * 10f) * 0.5f + 0.5f;
            float crags = TileFBM(u, v, 9, 6, 1245.8f);
            float ridges = Mathf.Abs(TileFBM(u, v, 14, 14, 1389.1f) * 2f - 1f);

            Color deepBasalt = new Color(0.24f, 0.26f, 0.29f);
            Color cliffGrey = new Color(0.44f, 0.46f, 0.49f);
            Color ochreLayer = new Color(0.52f, 0.47f, 0.41f);
            Color alpineMoss = new Color(0.30f, 0.43f, 0.27f);

            Color c = Color.Lerp(deepBasalt, cliffGrey, crags);
            c = Color.Lerp(c, ochreLayer, SmoothRange(0.55f, 0.85f, strata) * 0.55f);
            c = Color.Lerp(c, alpineMoss, SmoothRange(0.70f, 0.92f, ridges * strata) * 0.40f);

            height = Mathf.Clamp01(strata * 0.35f + crags * 0.40f + (1f - ridges) * 0.25f);
            albedo = new Color(c.r, c.g, c.b, 0.16f);
        }

        // 7. VỎ CÂY (Vertical fibrous bark ridges & dark fissures)
        private static void SampleTreeBark(float u, float v, out Color albedo, out float height)
        {
            float warp = TileFBM(u, v, 3, 2, 1510.2f) * 0.12f;
            float verticalGrooves = TileFBM(u + warp, v, 14, 3, 1620.7f);
            float plates = TileFBM(u, v, 8, 6, 1730.5f);

            Color deepFissure = new Color(0.18f, 0.11f, 0.07f);
            Color midBark = new Color(0.36f, 0.23f, 0.14f);
            Color sunlitRidge = new Color(0.49f, 0.34f, 0.21f);
            Color barkMoss = new Color(0.25f, 0.39f, 0.19f);

            float ridgeMask = SmoothRange(0.25f, 0.78f, verticalGrooves);
            Color c = Color.Lerp(deepFissure, midBark, ridgeMask);
            c = Color.Lerp(c, sunlitRidge, SmoothRange(0.6f, 0.9f, plates * ridgeMask));
            c = Color.Lerp(c, barkMoss, SmoothRange(0.75f, 0.95f, plates) * 0.25f);

            height = Mathf.Clamp01(ridgeMask * 0.75f + plates * 0.25f);
            albedo = new Color(c.r, c.g, c.b, 0.12f);
        }

        // 8. TÁN LÁ CÂY (Stylized layered leaf clusters for canopies & bushes)
        private static void SampleFoliage(float u, float v, out Color albedo, out float height)
        {
            float cluster = CellularStone(u, v, 8, 8, 1840.3f, out float leafEdge);
            float subLeaf = TileFBM(u, v, 16, 16, 1950.9f);

            Color shadowCanopy = new Color(0.11f, 0.28f, 0.13f);
            Color midEmerald = new Color(0.20f, 0.50f, 0.22f);
            Color sunlitLeaf = new Color(0.42f, 0.70f, 0.28f);

            float leafMask = SmoothRange(0.05f, 0.35f, leafEdge);
            Color c = Color.Lerp(shadowCanopy, midEmerald, leafMask);
            c = Color.Lerp(c, sunlitLeaf, SmoothRange(0.45f, 0.88f, cluster * 0.6f + subLeaf * 0.4f) * leafMask);

            height = Mathf.Clamp01(leafMask * (0.6f + 0.4f * subLeaf));
            albedo = new Color(c.r, c.g, c.b, 0.16f);
        }

        // ── Texture Baking & Sobel Normal Map Generation ──────

        private static void EnsureTexturePair(string baseName, bool force, SurfaceSampler sampler, float normalStrength)
        {
            string albedoPath = $"{TexDir}/{baseName}_Albedo.png";
            string normalPath = $"{TexDir}/{baseName}_Normal.png";

            if (!force && File.Exists(albedoPath) && File.Exists(normalPath))
                return;

            var albedoColors = new Color[TexSize * TexSize];
            var heights = new float[TexSize * TexSize];

            for (int y = 0; y < TexSize; y++)
            {
                float v = (float)y / TexSize;
                for (int x = 0; x < TexSize; x++)
                {
                    float u = (float)x / TexSize;
                    sampler(u, v, out Color col, out float h);
                    int idx = y * TexSize + x;
                    albedoColors[idx] = col;
                    heights[idx] = h;
                }
            }

            // Compute seamless tangent-space Normal Map via Sobel filter
            var normalColors = new Color[TexSize * TexSize];
            for (int y = 0; y < TexSize; y++)
            {
                int yUp = (y + 1) % TexSize;
                int yDown = (y - 1 + TexSize) % TexSize;
                for (int x = 0; x < TexSize; x++)
                {
                    int xRight = (x + 1) % TexSize;
                    int xLeft = (x - 1 + TexSize) % TexSize;

                    float hL = heights[y * TexSize + xLeft];
                    float hR = heights[y * TexSize + xRight];
                    float hD = heights[yDown * TexSize + x];
                    float hU = heights[yUp * TexSize + x];

                    float dx = (hL - hR) * normalStrength;
                    float dy = (hD - hU) * normalStrength;
                    Vector3 n = new Vector3(dx, dy, 1f).normalized;

                    normalColors[y * TexSize + x] = new Color(
                        n.x * 0.5f + 0.5f,
                        n.y * 0.5f + 0.5f,
                        n.z * 0.5f + 0.5f,
                        1f);
                }
            }

            WritePng(albedoPath, albedoColors);
            WritePng(normalPath, normalColors);
        }

        private static void WritePng(string assetPath, Color[] pixels)
        {
            var tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();
            byte[] bytes = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            File.WriteAllBytes(assetPath, bytes);
        }

        private static void WritePngCustom(string assetPath, Color[] pixels, int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();
            byte[] bytes = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            File.WriteAllBytes(assetPath, bytes);
        }

        private static void EnsureGrassBladeTextures(bool force)
        {
            string albedoPath = $"{TexDir}/Tex_GrassBlade_Albedo.png";
            string normalPath = $"{TexDir}/Tex_GrassBlade_Normal.png";

            if (!force && File.Exists(albedoPath) && File.Exists(normalPath)) return;

            int w = 128;
            int h = 128;
            var pixels = new Color[w * h];
            var heights = new float[w * h];

            // 1. Xóa toàn bộ nền thành trong suốt (Color.clear)
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.clear;
                heights[i] = 0f;
            }

            // 2. Vẽ 36 cọng cỏ mọc từ đáy (y = 0) lên trên, dày ở gốc và nhọn dần ở ngọn
            var rng = new System.Random(42);
            int bladeCount = 36;

            for (int b = 0; b < bladeCount; b++)
            {
                int startX = rng.Next(6, w - 6);
                int bladeHeight = rng.Next((int)(h * 0.45f), (int)(h * 0.96f));
                float lean = (float)(rng.NextDouble() * 2.0 - 1.0) * (w * 0.16f);
                float curve = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.40f;

                Color rootColor = new Color(0.10f, 0.32f, 0.06f, 1f);
                Color midColor = new Color(0.28f, 0.65f, 0.18f, 1f);
                Color tipColor = new Color(0.55f, 0.82f, 0.28f, 1f);

                for (int y = 0; y < bladeHeight; y++)
                {
                    float t = (float)y / bladeHeight;
                    float currentX = startX + lean * t + curve * Mathf.Sin(t * Mathf.PI) * (w * 0.09f);
                    int centerX = Mathf.RoundToInt(currentX);

                    // Cọng cỏ dày ở gốc (3-4 pixel), nhọn dần ở ngọn (1 pixel)
                    int halfW = Mathf.Max(0, Mathf.RoundToInt((1f - t) * 3.4f));

                    // Màu chuyển từ xanh đậm ở gốc sang xanh sáng tươi ở ngọn
                    Color bladeColor = t < 0.5f
                        ? Color.Lerp(rootColor, midColor, t * 2f)
                        : Color.Lerp(midColor, tipColor, (t - 0.5f) * 2f);

                    for (int dx = -halfW; dx <= halfW; dx++)
                    {
                        int px = Mathf.Clamp(centerX + dx, 0, w - 1);
                        int idx = y * w + px;

                        float distNorm = halfW > 0 ? Mathf.Abs((float)dx / halfW) : 0f;
                        float alpha = Mathf.Clamp01(1.25f - distNorm * 0.45f);

                        if (pixels[idx].a > 0.01f)
                        {
                            pixels[idx] = Color.Lerp(pixels[idx], bladeColor, alpha);
                            pixels[idx].a = Mathf.Max(pixels[idx].a, alpha);
                        }
                        else
                        {
                            pixels[idx] = new Color(bladeColor.r, bladeColor.g, bladeColor.b, alpha);
                        }
                        heights[idx] = Mathf.Max(heights[idx], (1f - distNorm * 0.5f) * (1f - t * 0.25f));
                    }
                }
            }

            WritePngCustom(albedoPath, pixels, w, h);

            // 3. Tính toán Normal Map để các cọng cỏ bắt sáng và nổi khối 3D mềm mại
            var normals = new Color[w * h];
            float normalStrength = 2.6f;
            for (int y = 0; y < h; y++)
            {
                int yUp = Mathf.Min(h - 1, y + 1);
                int yDown = Mathf.Max(0, y - 1);
                for (int x = 0; x < w; x++)
                {
                    int xRight = Mathf.Min(w - 1, x + 1);
                    int xLeft = Mathf.Max(0, x - 1);

                    float hL = heights[y * w + xLeft];
                    float hR = heights[y * w + xRight];
                    float hD = heights[yDown * w + x];
                    float hU = heights[yUp * w + x];

                    float dx = (hL - hR) * normalStrength;
                    float dy = (hD - hU) * normalStrength;
                    Vector3 n = new Vector3(dx, dy, 1f).normalized;

                    normals[y * w + x] = new Color(
                        n.x * 0.5f + 0.5f,
                        n.y * 0.5f + 0.5f,
                        n.z * 0.5f + 0.5f,
                        pixels[y * w + x].a);
                }
            }
            WritePngCustom(normalPath, normals, w, h);
        }

        private static void EnsureLeafClusterTextures(bool force)
        {
            string albedoPath = $"{TexDir}/Tex_LeafCluster_Albedo.png";
            string normalPath = $"{TexDir}/Tex_LeafCluster_Normal.png";

            if (!force && File.Exists(albedoPath) && File.Exists(normalPath)) return;

            int size = 256;
            var pixels = new Color[size * size];
            var heights = new float[size * size];

            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.clear;
                heights[i] = 0f;
            }

            // Vẽ 95 chiếc lá hình mũi mác (lanceolate leaves) xếp chồng từ lớp lá tối bên trong ra lớp lá sáng ngoài rìa
            var rng = new System.Random(1337);
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            int totalLeaves = 95;

            Color deepShadowGreen = new Color(0.10f, 0.27f, 0.08f, 1f);
            Color midForestGreen  = new Color(0.20f, 0.48f, 0.14f, 1f);
            Color sunlitTipGreen  = new Color(0.42f, 0.70f, 0.22f, 1f);

            for (int i = 0; i < totalLeaves; i++)
            {
                float layerT = (float)i / (totalLeaves - 1);
                float angle = i * 2.39996f + (float)(rng.NextDouble() * 0.35 - 0.175);
                // Các lá phân bố tỏa tròn, chừa phần rìa ngoài thành từng chóp lá nhọn tự nhiên
                float radius = Mathf.Sqrt((float)rng.NextDouble()) * (size * 0.36f);

                Vector2 leafPos = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                float leafDirAngle = angle + (float)(rng.NextDouble() * 0.7 - 0.35);
                float cosA = Mathf.Cos(leafDirAngle);
                float sinA = Mathf.Sin(leafDirAngle);

                float leafLen = size * (0.11f + (float)rng.NextDouble() * 0.05f);
                float leafWid = leafLen * (0.48f + (float)rng.NextDouble() * 0.12f);

                Color baseLeafCol = Color.Lerp(deepShadowGreen, midForestGreen, layerT * 0.85f);
                Color tipLeafCol  = Color.Lerp(midForestGreen, sunlitTipGreen, layerT);

                int minX = Mathf.Max(0, Mathf.FloorToInt(leafPos.x - leafLen - 2));
                int maxX = Mathf.Min(size - 1, Mathf.CeilToInt(leafPos.x + leafLen + 2));
                int minY = Mathf.Max(0, Mathf.FloorToInt(leafPos.y - leafLen - 2));
                int maxY = Mathf.Min(size - 1, Mathf.CeilToInt(leafPos.y + leafLen + 2));

                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        float dx = x - leafPos.x;
                        float dy = y - leafPos.y;

                        // Tọa độ cục bộ dọc theo thân chiếc lá (lx: dọc gân lá, ly: ngang phiến lá)
                        float lx = (dx * cosA + dy * sinA) / leafLen;
                        float ly = (-dx * sinA + dy * cosA) / leafWid;

                        if (lx < -0.85f || lx > 1.0f) continue;

                        // Đường viền chiếc lá nhọn ở đầu (lx = 1) và bầu ở gốc
                        float normL = (lx + 0.85f) / 1.85f; // 0..1
                        float leafProfile = Mathf.Sin(normL * Mathf.PI) * (1.15f - 0.35f * normL);

                        if (Mathf.Abs(ly) <= leafProfile)
                        {
                            float edgeDist = 1f - Mathf.Abs(ly) / Mathf.Max(0.001f, leafProfile);
                            float centerVein = Mathf.Clamp01(1f - Mathf.Abs(ly) * 2.5f);

                            Color col = Color.Lerp(baseLeafCol, tipLeafCol, normL * 0.75f + centerVein * 0.25f);
                            // Viền tối nhẹ (Ambient Occlusion giữa các phiến lá) để tách biệt từng chiếc lá
                            col *= (0.82f + 0.22f * edgeDist);
                            col.a = 1f;

                            int idx = y * size + x;
                            pixels[idx] = col;
                            heights[idx] = Mathf.Clamp01(0.3f + layerT * 0.4f + edgeDist * 0.3f);
                        }
                    }
                }
            }

            WritePngCustom(albedoPath, pixels, size, size);

            // Tạo Normal Map nổi khối nhẹ cho từng phiến lá
            var normals = new Color[size * size];
            float normalStrength = 2.0f;
            for (int y = 0; y < size; y++)
            {
                int yUp = Mathf.Min(size - 1, y + 1);
                int yDown = Mathf.Max(0, y - 1);
                for (int x = 0; x < size; x++)
                {
                    int xRight = Mathf.Min(size - 1, x + 1);
                    int xLeft = Mathf.Max(0, x - 1);

                    float hL = heights[y * size + xLeft];
                    float hR = heights[y * size + xRight];
                    float hD = heights[yDown * size + x];
                    float hU = heights[yUp * size + x];

                    float dx = (hL - hR) * normalStrength;
                    float dy = (hD - hU) * normalStrength;
                    Vector3 n = new Vector3(dx, dy, 1f).normalized;

                    normals[y * size + x] = new Color(
                        n.x * 0.5f + 0.5f,
                        n.y * 0.5f + 0.5f,
                        n.z * 0.5f + 0.5f,
                        pixels[y * size + x].a);
                }
            }
            WritePngCustom(normalPath, normals, size, size);
        }

        private static void ConfigureTextureImporter(string assetPath, bool isNormal, bool isAlphaCutout = false)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;

            bool dirty = false;
            var targetType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (importer.textureType != targetType) { importer.textureType = targetType; dirty = true; }
            var wrap = isAlphaCutout ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            if (importer.wrapMode != wrap) { importer.wrapMode = wrap; dirty = true; }
            if (importer.filterMode != FilterMode.Bilinear && isAlphaCutout) { importer.filterMode = FilterMode.Bilinear; dirty = true; }
            else if (importer.filterMode != FilterMode.Trilinear && !isAlphaCutout) { importer.filterMode = FilterMode.Trilinear; dirty = true; }
            if (importer.anisoLevel != 8) { importer.anisoLevel = 8; dirty = true; }

            if (isAlphaCutout && !isNormal)
            {
                if (importer.alphaSource != TextureImporterAlphaSource.FromInput) { importer.alphaSource = TextureImporterAlphaSource.FromInput; dirty = true; }
                if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; dirty = true; }
            }

            if (dirty) importer.SaveAndReimport();
        }

        // ── Material & TerrainLayer Builders ──────────────────

        private static Material CreateOrUpdatePBRMaterial(
            string matFileName, string texBaseName,
            Color tintColor, float smoothness, float bumpScale, Vector2 tiling)
        {
            string matPath = $"{MatDir}/{matFileName}";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }

            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{texBaseName}_Albedo.png");
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{texBaseName}_Normal.png");

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tintColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", tintColor);

            if (albedo != null)
            {
                if (mat.HasProperty("_BaseMap"))
                {
                    mat.SetTexture("_BaseMap", albedo);
                    mat.SetTextureScale("_BaseMap", tiling);
                }
                mat.mainTexture = albedo;
                mat.mainTextureScale = tiling;
            }

            if (normal != null && mat.HasProperty("_BumpMap"))
            {
                mat.EnableKeyword("_NORMALMAP");
                mat.SetTexture("_BumpMap", normal);
                mat.SetTextureScale("_BumpMap", tiling);
                if (mat.HasProperty("_BumpScale")) mat.SetFloat("_BumpScale", bumpScale);
            }

            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.0f);
            mat.enableInstancing = true;

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material CreateOrUpdateCutoutMaterial(
            string matFileName, string texBaseName,
            Color tintColor, float cutoff = 0.35f)
        {
            string matPath = $"{MatDir}/{matFileName}";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }

            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{texBaseName}_Albedo.png");
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{texBaseName}_Normal.png");

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tintColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", tintColor);

            if (albedo != null)
            {
                if (mat.HasProperty("_BaseMap"))
                {
                    mat.SetTexture("_BaseMap", albedo);
                    mat.SetTextureScale("_BaseMap", Vector2.one);
                }
                mat.mainTexture = albedo;
                mat.mainTextureScale = Vector2.one;
            }

            if (normal != null && mat.HasProperty("_BumpMap"))
            {
                mat.EnableKeyword("_NORMALMAP");
                mat.SetTexture("_BumpMap", normal);
                mat.SetTextureScale("_BumpMap", Vector2.one);
                if (mat.HasProperty("_BumpScale")) mat.SetFloat("_BumpScale", 0.85f);
            }

            // Cấu hình Alpha Cutout (Alpha Clipping) & hiển thị cả 2 mặt (Cull Off)
            if (mat.HasProperty("_AlphaClip")) mat.SetFloat("_AlphaClip", 1f);
            if (mat.HasProperty("_Cutoff")) mat.SetFloat("_Cutoff", cutoff);
            if (mat.HasProperty("_Cull")) mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 0f); // Opaque with alpha test
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.15f);

            // Standard Shader compatibility flags
            mat.SetFloat("_Mode", 1f); // 1 = Cutout
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            mat.SetInt("_ZWrite", 1);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            mat.enableInstancing = true;

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material CreateOrUpdateVibrantFlowerMaterial(string matFileName, Color vibrantColor)
        {
            string matPath = $"{MatDir}/{matFileName}";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }

            // Remove any dark texture so the pure vibrant blossom tint shines brilliantly
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", null);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", null);
            mat.mainTexture = null;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", vibrantColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", vibrantColor);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.35f);

            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/Tex_Foliage_Normal.png");
            if (normal != null && mat.HasProperty("_BumpMap"))
            {
                mat.EnableKeyword("_NORMALMAP");
                mat.SetTexture("_BumpMap", normal);
                mat.SetFloat("_BumpScale", 0.65f);
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material CreateOrUpdateEmissiveMaterial(string matFileName, Color baseColor, Color emissionColor)
        {
            string matPath = $"{MatDir}/{matFileName}";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", baseColor);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.85f);
            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", emissionColor);

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static TerrainLayer CreateOrUpdateTerrainLayer(string fileName, string texBaseName, Vector2 tileSize, float normalScale)
        {
            string path = $"{LayerDir}/{fileName}";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, path);
            }

            layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{texBaseName}_Albedo.png");
            layer.normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{texBaseName}_Normal.png");
            layer.tileSize = tileSize;
            layer.normalScale = normalScale;
            layer.smoothness = 0.15f;
            layer.metallic = 0f;

            EditorUtility.SetDirty(layer);
            return layer;
        }

        // ── Seamless Math Helpers ─────────────────────────────

        private static float TileFBM(float u, float v, int freqX, int freqY, float seed)
        {
            float sum = 0f;
            float amp = 0.5f;
            float totalAmp = 0f;
            int fx = freqX;
            int fy = freqY;

            for (int o = 0; o < 3; o++)
            {
                sum += TileNoise(u, v, fx, fy, seed + o * 37.3f) * amp;
                totalAmp += amp;
                amp *= 0.5f;
                fx *= 2;
                fy *= 2;
            }
            return sum / totalAmp;
        }

        private static float TileNoise(float u, float v, int periodX, int periodY, float seed)
        {
            float x = u * periodX;
            float y = v * periodY;
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            float tx = x - x0;
            float ty = y - y0;

            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);

            int ix0 = ((x0 % periodX) + periodX) % periodX;
            int ix1 = (ix0 + 1) % periodX;
            int iy0 = ((y0 % periodY) + periodY) % periodY;
            int iy1 = (iy0 + 1) % periodY;

            float n00 = Hash2D(ix0, iy0, seed);
            float n10 = Hash2D(ix1, iy0, seed);
            float n01 = Hash2D(ix0, iy1, seed);
            float n11 = Hash2D(ix1, iy1, seed);

            return Mathf.Lerp(Mathf.Lerp(n00, n10, tx), Mathf.Lerp(n01, n11, tx), ty);
        }

        // Seamless Voronoi / Worley cellular distance for cobblestones, pebbles & rock cracks
        private static float CellularStone(float u, float v, int cellsX, int cellsY, float seed, out float edgeDistance)
        {
            float px = u * cellsX;
            float py = v * cellsY;
            int cx = Mathf.FloorToInt(px);
            int cy = Mathf.FloorToInt(py);

            float minDist1 = 99f;
            float minDist2 = 99f;
            float closestCellHash = 0f;

            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = cx + dx;
                    int ny = cy + dy;
                    int wx = ((nx % cellsX) + cellsX) % cellsX;
                    int wy = ((ny % cellsY) + cellsY) % cellsY;

                    float jx = Hash2D(wx, wy, seed);
                    float jy = Hash2D(wx, wy, seed + 71.9f);

                    float fx = nx + 0.15f + jx * 0.70f;
                    float fy = ny + 0.15f + jy * 0.70f;

                    float dist = Vector2.Distance(new Vector2(px, py), new Vector2(fx, fy));
                    if (dist < minDist1)
                    {
                        minDist2 = minDist1;
                        minDist1 = dist;
                        closestCellHash = jx;
                    }
                    else if (dist < minDist2)
                    {
                        minDist2 = dist;
                    }
                }
            }

            edgeDistance = Mathf.Clamp01(minDist2 - minDist1);
            return closestCellHash;
        }

        private static float Hash2D(int x, int y, float seed)
        {
            float h = x * 127.1f + y * 311.7f + seed * 74.7f;
            return Mathf.Repeat(Mathf.Sin(h) * 43758.5453f, 1f);
        }
    }
}
#endif
