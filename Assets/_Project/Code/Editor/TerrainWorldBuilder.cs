#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// ============================================================
//  TerrainWorldBuilder — Complete 4-Stage Unity Terrain Pipeline:
//    Stage 1: BUILD A TERRAIN (TerrainData + URP Terrain/Lit + Heightmap)
//    Stage 2: PAINT TEXTURES ON THE TERRAIN (5-Layer Splatmap:
//             Grass, Earth, Cobble Road, Rock, Mountain Cliff)
//    Stage 3: ADD GRASS & TREES TO TERRAIN (TreePrototypes +
//             TreeInstances + 3D DetailPrototype Grass Layer)
//    Stage 4: EDIT THE TERRAIN (Sculpting, Road Carving, Plateau
//             Stamping, Hydraulic/Thermal Smoothing tools)
// ============================================================
namespace EchoesOfTheLost.EditorTools
{
    public static class TerrainWorldBuilder
    {
        private const string TerrainDir = "Assets/_Project/Data/Terrain";
        private const string PrefabDir = "Assets/_Project/Prefabs/Environment";
        private const string MatDir = "Assets/_Project/Art/Materials";
        private const string TerrainAssetPath = TerrainDir + "/TerrainData_VerdaWilds.asset";
        private const string TerrainMatPath = MatDir + "/Mat_Terrain_URP.mat";
        private const string OakPrefabPath = PrefabDir + "/PF_VerdaOakTree.prefab";
        private const string PinePrefabPath = PrefabDir + "/PF_VerdaPineTree.prefab";
        private const string GrassDetailPrefabPath = PrefabDir + "/PF_TerrainGrassDetail.prefab";
        private const string FlowerYellowPrefabPath = PrefabDir + "/PF_FlowerCluster_Yellow.prefab";
        private const string FlowerRedPrefabPath = PrefabDir + "/PF_FlowerCluster_Red.prefab";
        private const string FlowerBluePrefabPath = PrefabDir + "/PF_FlowerCluster_Blue.prefab";
        private const string FlowerPurplePrefabPath = PrefabDir + "/PF_FlowerCluster_Purple.prefab";

        private const int HeightRes = 257;
        private const int AlphaRes = 256;
        private const int DetailRes = 256;
        private static readonly Vector3 TerrainSize = new Vector3(256f, 42f, 256f);
        private static readonly Vector3 TerrainOrigin = new Vector3(-128f, 0f, -128f);

        // ── Editor Menu Items for Interactive Terrain Workflow ──

        [MenuItem("EotL/Terrain/Run Full Terrain Pipeline (All 4 Steps)")]
        public static void RunFullPipelineMenu()
        {
            var envMats = EnvironmentTextureGenerator.GenerateAll();
            BuildCompleteTerrain(null, envMats);
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
            Debug.Log("[TerrainWorldBuilder] Completed all 4 Terrain stages (Build -> Sculpt/Edit -> Paint 5 Splat Layers -> Populate Grass & Trees)!");
        }

        [MenuItem("EotL/Terrain/1. Build Terrain & Sculpt Heightmap")]
        public static void MenuStep1BuildAndSculpt()
        {
            var envMats = EnvironmentTextureGenerator.GenerateAll();
            var t = EnsureTerrainInScene(null, envMats);
            SculptVerdaWildsHeightmap(t.terrainData);
            Debug.Log("[TerrainWorldBuilder] Step 1 Complete: Built Terrain & sculpted Verda Wilds heightmap.");
        }

        [MenuItem("EotL/Terrain/2. Paint Textures on Terrain (5 Splat Layers)")]
        public static void MenuStep2PaintTextures()
        {
            var envMats = EnvironmentTextureGenerator.GenerateAll();
            var t = EnsureTerrainInScene(null, envMats);
            PaintTerrainSplatmap(t.terrainData, envMats.TerrainLayers);
            Debug.Log("[TerrainWorldBuilder] Step 2 Complete: Painted 5 PBR TerrainLayers (Grass, Earth, Road, Rock, Mountain).");
        }

        [MenuItem("EotL/Terrain/3. Add Grass & Trees to Terrain")]
        public static void MenuStep3AddGrassAndTrees()
        {
            var envMats = EnvironmentTextureGenerator.GenerateAll();
            var t = EnsureTerrainInScene(null, envMats);
            PopulateTreesAndGrassDetails(t.terrainData, envMats);
            t.Flush();
            Debug.Log("[TerrainWorldBuilder] Step 3 Complete: Added Oak/Pine TreeInstances and 3D Grass Detail layer to Terrain.");
        }

        [MenuItem("EotL/Terrain/4. Edit Terrain — Smooth & Re-Carve Winding Road")]
        public static void MenuStep4EditTerrain()
        {
            var envMats = EnvironmentTextureGenerator.GenerateAll();
            var t = EnsureTerrainInScene(null, envMats);
            ApplyTerrainEditsAndSmoothing(t.terrainData, smoothIterations: 2);
            PaintTerrainSplatmap(t.terrainData, envMats.TerrainLayers);
            t.Flush();
            Debug.Log("[TerrainWorldBuilder] Step 4 Complete: Applied terrain smoothing, terraced plateaus, and road bed carving.");
        }

        // ── Master Builder Called by SandboxSceneBuilder ──────

        public static Terrain BuildCompleteTerrain(Transform parent, EnvironmentMaterialSet envMats)
        {
            Directory.CreateDirectory(TerrainDir);
            Directory.CreateDirectory(PrefabDir);
            Directory.CreateDirectory(MatDir);

            // Stage 1: Build Terrain & Sculpt base heightmap
            Terrain terrain = EnsureTerrainInScene(parent, envMats);
            TerrainData td = terrain.terrainData;
            SculptVerdaWildsHeightmap(td);

            // Stage 4: Edit Terrain (carve winding road pass, terrace plateaus, smooth transitions)
            ApplyTerrainEditsAndSmoothing(td, smoothIterations: 1);

            // Stage 2: Paint 5 PBR Textures onto the Terrain splatmap
            PaintTerrainSplatmap(td, envMats.TerrainLayers);

            // Stage 3: Add 3D Grass Detail prototypes & Tree prototypes/instances
            PopulateTreesAndGrassDetails(td, envMats);

            terrain.drawHeightmap = true;
            terrain.drawTreesAndFoliage = true;
            terrain.detailObjectDistance = 140f;
            terrain.detailObjectDensity = 1.0f;
            terrain.treeDistance = 500f;
            terrain.treeBillboardDistance = 180f;
            terrain.Flush();

            EditorUtility.SetDirty(td);
            AssetDatabase.SaveAssets();
            return terrain;
        }

        // ── STAGE 1: BUILD A TERRAIN & SCULPT HEIGHTMAP ───────

        private static Terrain EnsureTerrainInScene(Transform parent, EnvironmentMaterialSet envMats)
        {
            var td = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainAssetPath);
            if (td == null)
            {
                td = new TerrainData();
                AssetDatabase.CreateAsset(td, TerrainAssetPath);
            }

            td.heightmapResolution = HeightRes;
            td.size = TerrainSize;
            td.alphamapResolution = AlphaRes;
            td.baseMapResolution = AlphaRes;
            td.SetDetailResolution(DetailRes, 16);

            if (envMats.TerrainLayers != null && envMats.TerrainLayers.Length > 0)
                td.terrainLayers = envMats.TerrainLayers;

            // Remove old flat ground plane if present
            var oldPlane = GameObject.Find("Valley_Grass_Floor");
            if (oldPlane != null) Object.DestroyImmediate(oldPlane);

            var existingTerrain = Object.FindAnyObjectByType<Terrain>();
            GameObject terrainGO;
            if (existingTerrain != null)
            {
                terrainGO = existingTerrain.gameObject;
                existingTerrain.terrainData = td;
                var col = terrainGO.GetComponent<TerrainCollider>() ?? terrainGO.AddComponent<TerrainCollider>();
                col.terrainData = td;
            }
            else
            {
                terrainGO = Terrain.CreateTerrainGameObject(td);
                terrainGO.name = "Terrain_VerdaWilds";
            }

            if (parent != null)
                terrainGO.transform.SetParent(parent, false);
            terrainGO.transform.position = TerrainOrigin;

            var terrain = terrainGO.GetComponent<Terrain>();
            terrain.materialTemplate = CreateOrUpdateURPTerrainMaterial();
            return terrain;
        }

        private static Material CreateOrUpdateURPTerrainMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(TerrainMatPath);
            var shader = Shader.Find("Universal Render Pipeline/Terrain/Lit")
                      ?? Shader.Find("Nature/Terrain/Standard")
                      ?? Shader.Find("Universal Render Pipeline/Lit");

            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, TerrainMatPath);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
                EditorUtility.SetDirty(mat);
            }
            return mat;
        }

        private static void SculptVerdaWildsHeightmap(TerrainData td)
        {
            int res = td.heightmapResolution;
            float[,] heights = new float[res, res];

            for (int y = 0; y < res; y++)
            {
                float normZ = (float)y / (res - 1);
                float worldZ = TerrainOrigin.z + normZ * TerrainSize.z;

                for (int x = 0; x < res; x++)
                {
                    float normX = (float)x / (res - 1);
                    float worldX = TerrainOrigin.x + normX * TerrainSize.x;

                    // Distance from central gameplay sanctuary (0, 10)
                    float dx = worldX;
                    float dz = worldZ - 10f;
                    float distFromSanctuary = Mathf.Sqrt(dx * dx + (dz * 0.85f) * (dz * 0.85f));

                    // 1. Undulating rolling meadow hills with high/low variation ("thảm cỏ trải dài cao thấp")
                    float primaryRolling = FBM(worldX * 0.020f + 11.3f, worldZ * 0.020f + 27.9f, 3);
                    float secondarySwells = Mathf.Sin(worldX * 0.042f + 1.7f) * Mathf.Cos(worldZ * 0.038f + 2.4f) * 0.5f + 0.5f;
                    float hillNoise = primaryRolling * 0.70f + secondarySwells * 0.30f;
                    float hillMask = SmoothRange(20f, 54f, distFromSanctuary);
                    float hillHeight = hillNoise * 0.14f * hillMask;

                    // 2. Outer Mountain Ring (r > 52m) with ridged alpine peaks
                    float mountainMask = SmoothRange(48f, 98f, distFromSanctuary);
                    // Keep a southern valley entrance pass narrower & lower so road enters from South
                    float southPassFactor = (worldZ < -10f && Mathf.Abs(worldX) < 28f)
                        ? SmoothRange(6f, 28f, Mathf.Abs(worldX))
                        : 1f;

                    float ridgeNoise = RidgedMF(worldX * 0.022f + 5.7f, worldZ * 0.022f + 19.4f, 4);
                    float mountainHeight = ridgeNoise * 0.62f * mountainMask * southPassFactor;

                    // 3. Extra northern sanctuary backdrop peak behind the Watchtower Ruins (z > 34)
                    float northBackdrop = SmoothRange(30f, 75f, worldZ) * (0.22f + 0.35f * ridgeNoise);

                    float totalNormalized = Mathf.Clamp01(hillHeight + mountainHeight + northBackdrop);
                    heights[y, x] = totalNormalized;
                }
            }

            td.SetHeights(0, 0, heights);
        }

        // ── STAGE 4: EDIT THE TERRAIN (Road Carving, Plateaus & Smoothing) ──

        public static void ApplyTerrainEditsAndSmoothing(TerrainData td, int smoothIterations = 1)
        {
            int res = td.heightmapResolution;
            float[,] heights = td.GetHeights(0, 0, res, res);

            for (int y = 0; y < res; y++)
            {
                float normZ = (float)y / (res - 1);
                float worldZ = TerrainOrigin.z + normZ * TerrainSize.z;

                for (int x = 0; x < res; x++)
                {
                    float normX = (float)x / (res - 1);
                    float worldX = TerrainOrigin.x + normX * TerrainSize.x;

                    float h = heights[y, x];

                    // Edit 1: Flatten central gameplay courtyard & ruin arena (x in [-16, 16], z in [-14, 26])
                    float boxDistX = Mathf.Max(0f, Mathf.Abs(worldX) - 15f);
                    float boxDistZ = worldZ < -14f ? (-14f - worldZ) : (worldZ > 25f ? (worldZ - 25f) : 0f);
                    float boxDist = Mathf.Sqrt(boxDistX * boxDistX + boxDistZ * boxDistZ);
                    float arenaFlatten = SmoothRange(0f, 12f, boxDist);
                    h *= arenaFlatten;

                    // Edit 2: Carve smooth winding southern road pass (z from -110 to 0)
                    float roadCenter = RoadCenterX(worldZ);
                    float distToRoad = Mathf.Abs(worldX - roadCenter);
                    if (worldZ >= -115f && worldZ <= 12f)
                    {
                        float roadFlatten = SmoothRange(3.5f, 14f, distToRoad);
                        float GentlySlopedRoadBed = Mathf.Clamp01((-worldZ - 14f) / 100f) * 0.04f;
                        h = Mathf.Lerp(GentlySlopedRoadBed, h, roadFlatten);
                    }

                    // Edit 3: Sculpt scenic West & East earthen overlooks (x ≈ ±24, z ≈ 10..22)
                    float westOverlook = OverlookMask(worldX, worldZ, -25f, 14f, 9f, 12f);
                    float eastOverlook = OverlookMask(worldX, worldZ, 25f, 14f, 9f, 12f);
                    float terraceTargetHeight = 3.2f / TerrainSize.y;
                    h = Mathf.Lerp(h, terraceTargetHeight, Mathf.Max(westOverlook, eastOverlook) * 0.85f);

                    heights[y, x] = Mathf.Clamp01(h);
                }
            }

            // Edit 4: Gentle 3x3 Gaussian/Box smoothing pass on outer terrain while keeping central arena crisp
            for (int iter = 0; iter < smoothIterations; iter++)
            {
                float[,] smoothed = (float[,])heights.Clone();
                for (int y = 1; y < res - 1; y++)
                {
                    float normZ = (float)y / (res - 1);
                    float worldZ = TerrainOrigin.z + normZ * TerrainSize.z;
                    for (int x = 1; x < res - 1; x++)
                    {
                        float normX = (float)x / (res - 1);
                        float worldX = TerrainOrigin.x + normX * TerrainSize.x;
                        if (Mathf.Abs(worldX) < 16f && worldZ > -14f && worldZ < 25f)
                            continue;

                        float sum =
                            heights[y - 1, x - 1] + heights[y - 1, x] * 2f + heights[y - 1, x + 1] +
                            heights[y, x - 1] * 2f + heights[y, x] * 4f + heights[y, x + 1] * 2f +
                            heights[y + 1, x - 1] + heights[y + 1, x] * 2f + heights[y + 1, x + 1];
                        smoothed[y, x] = sum / 16f;
                    }
                }
                heights = smoothed;
            }

            td.SetHeights(0, 0, heights);
        }

        // ── STAGE 2: PAINT TEXTURES ON THE TERRAIN (5 Splat Layers) ──
        // Layer 0: TL_Grass    (Cỏ thảo nguyên)
        // Layer 1: TL_Earth    (Đất rừng)
        // Layer 2: TL_Road     (Đường đi lát đá cuội)
        // Layer 3: TL_Rock     (Đá tảng tự nhiên)
        // Layer 4: TL_Mountain (Vách núi đá)

        public static void PaintTerrainSplatmap(TerrainData td, TerrainLayer[] layers)
        {
            if (layers != null && layers.Length > 0)
                td.terrainLayers = layers;

            int numLayers = td.alphamapLayers;
            if (numLayers < 5) return;

            int w = td.alphamapWidth;
            int h = td.alphamapHeight;
            float[,,] map = new float[h, w, numLayers];

            for (int y = 0; y < h; y++)
            {
                float normZ = (float)y / (h - 1);
                float worldZ = TerrainOrigin.z + normZ * TerrainSize.z;

                for (int x = 0; x < w; x++)
                {
                    float normX = (float)x / (w - 1);
                    float worldX = TerrainOrigin.x + normX * TerrainSize.x;

                    float altitude = td.GetInterpolatedHeight(normX, normZ);
                    float slope = td.GetSteepness(normX, normZ); // degrees 0..90
                    float detailNoise = FBM(worldX * 0.08f + 3.1f, worldZ * 0.08f + 7.7f, 2);

                    // 1. Road distance (Main South-North road + Camp East/West branches)
                    float distMainRoad = (worldZ >= -115f && worldZ <= 11f)
                        ? Mathf.Abs(worldX - RoadCenterX(worldZ))
                        : 999f;

                    float distBranchRoad = (Mathf.Abs(worldX) <= 13f && worldZ >= 3.5f && worldZ <= 8.0f)
                        ? Mathf.Abs(worldZ - 6.0f)
                        : 999f;

                    float roadDist = Mathf.Min(distMainRoad, distBranchRoad);
                    float wRoad = 1f - SmoothRange(1.8f, 3.4f, roadDist);

                    //Dirt shoulder along the road edge & around ruin base
                    float roadShoulder = (1f - SmoothRange(2.6f, 5.2f, roadDist)) * (1f - wRoad);
                    float ruinApron = (Mathf.Abs(worldX) < 10f && worldZ > 7f && worldZ < 24f) ? 0.55f : 0f;

                    // 2. Slope & Altitude weights for Grass, Earth, Rock, Mountain
                    float wMountain = SmoothRange(34f, 50f, slope) + SmoothRange(11f, 22f, altitude) * 0.85f;
                    wMountain = Mathf.Clamp01(wMountain);

                    float wRock = SmoothRange(20f, 34f, slope) * (1f - wMountain * 0.7f);
                    if (altitude > 5.5f && altitude < 14f)
                        wRock = Mathf.Max(wRock, (detailNoise - 0.45f) * 1.2f);
                    wRock = Mathf.Clamp01(wRock);

                    float wEarth = SmoothRange(10f, 24f, slope) * (1f - wMountain) * 0.75f
                                 + roadShoulder * 0.9f
                                 + ruinApron
                                 + SmoothRange(0.62f, 0.85f, detailNoise) * 0.30f;
                    wEarth = Mathf.Clamp01(wEarth * (1f - wRoad));

                    float wGrass = Mathf.Clamp01(1f - Mathf.Max(wRoad, Mathf.Max(wMountain, wRock * 0.85f)) - wEarth * 0.65f);

                    // Suppress non-road layers right on top of the cobblestone path
                    if (wRoad > 0.01f)
                    {
                        float nonRoadScale = 1f - wRoad;
                        wGrass *= nonRoadScale;
                        wEarth *= nonRoadScale;
                        wRock *= nonRoadScale;
                        wMountain *= nonRoadScale;
                    }

                    // Normalize weights so they sum to 1.0
                    float sum = wGrass + wEarth + wRoad + wRock + wMountain + 0.0001f;
                    map[y, x, 0] = wGrass / sum;
                    map[y, x, 1] = wEarth / sum;
                    map[y, x, 2] = wRoad / sum;
                    map[y, x, 3] = wRock / sum;
                    map[y, x, 4] = wMountain / sum;
                }
            }

            td.SetAlphamaps(0, 0, map);
        }

        // ── STAGE 3: ADD GRASS & TREES TO TERRAIN ─────────────

        public static void PopulateTreesAndGrassDetails(TerrainData td, EnvironmentMaterialSet envMats)
        {
            // 1. Create / Update Tree Prefabs (Single-Mesh Multi-Submesh for Unity Terrain TreePrototype)
            GameObject oakPrefab = CreateOrUpdateTerrainTreePrefab(
                OakPrefabPath, "Mesh_VerdaOakTree.asset", isPine: false, envMats.TreeTrunk, envMats.TreeLeaves);
            GameObject pinePrefab = CreateOrUpdateTerrainTreePrefab(
                PinePrefabPath, "Mesh_VerdaPineTree.asset", isPine: true, envMats.TreeTrunk, envMats.TreeLeaves);

            td.treePrototypes = new[]
            {
                new TreePrototype { prefab = oakPrefab, bendFactor = 0.15f },
                new TreePrototype { prefab = pinePrefab, bendFactor = 0.10f }
            };
            td.RefreshPrototypes();

            // Place 180+ Terrain TreeInstances across the valley hills & foothills
            var trees = new List<TreeInstance>(220);
            int gridSteps = 28;
            for (int gz = 0; gz < gridSteps; gz++)
            {
                for (int gx = 0; gx < gridSteps; gx++)
                {
                    float jitterX = Hash(gx * 37 + gz * 13, 101f) - 0.5f;
                    float jitterZ = Hash(gx * 53 + gz * 29, 202f) - 0.5f;

                    float normX = Mathf.Clamp01((gx + 0.5f + jitterX * 0.75f) / gridSteps);
                    float normZ = Mathf.Clamp01((gz + 0.5f + jitterZ * 0.75f) / gridSteps);

                    float worldX = TerrainOrigin.x + normX * TerrainSize.x;
                    float worldZ = TerrainOrigin.z + normZ * TerrainSize.z;

                    // Keep central ruin courtyard & road clear of trees
                    if (Mathf.Abs(worldX) < 13.5f && worldZ > -16f && worldZ < 26f)
                        continue;
                    if (worldZ <= 12f && Mathf.Abs(worldX - RoadCenterX(worldZ)) < 6.5f)
                        continue;

                    // Keep within a 110m radius around the valley so trees frame the playable region
                    float distFromCenter = Vector2.Distance(new Vector2(worldX, worldZ), new Vector2(0f, 8f));
                    if (distFromCenter > 105f)
                        continue;

                    float slope = td.GetSteepness(normX, normZ);
                    float altitude = td.GetInterpolatedHeight(normX, normZ);
                    if (slope > 32f || altitude > 16f)
                        continue;

                    // Forest density mask
                    float density = FBM(worldX * 0.04f + 9.2f, worldZ * 0.04f + 4.5f, 2);
                    if (density < 0.36f)
                        continue;

                    int protoIndex = (altitude > 4.2f || slope > 16f) ? 1 : 0; // Pines higher up, Oaks in valley
                    float scale = 0.85f + Hash(gx * 19 + gz * 41, 303f) * 0.45f;
                    float rot = Hash(gx * 71 + gz * 61, 404f) * Mathf.PI * 2f;

                    trees.Add(new TreeInstance
                    {
                        position = new Vector3(normX, altitude / TerrainSize.y, normZ),
                        widthScale = scale,
                        heightScale = scale,
                        rotation = rot,
                        color = Color.white,
                        lightmapColor = Color.white,
                        prototypeIndex = protoIndex
                    });
                }
            }
            td.SetTreeInstances(trees.ToArray(), snapToHeightmap: true);

            // 2. Create / Update 3D Grass Detail & 4 Distinct Flower Prototypes (Yellow, Red, Blue, Purple)
            GameObject grassDetailPrefab = CreateOrUpdateGrassDetailPrefab(GrassDetailPrefabPath, envMats.GrassTuft);
            GameObject flowerYellowPrefab = CreateOrUpdateFlowerDetailPrefab(FlowerYellowPrefabPath, envMats.FlowerYellow, envMats.FlowerStem);
            GameObject flowerRedPrefab = CreateOrUpdateFlowerDetailPrefab(FlowerRedPrefabPath, envMats.FlowerRed, envMats.FlowerStem);
            GameObject flowerBluePrefab = CreateOrUpdateFlowerDetailPrefab(FlowerBluePrefabPath, envMats.FlowerBlue, envMats.FlowerStem);
            GameObject flowerPurplePrefab = CreateOrUpdateFlowerDetailPrefab(FlowerPurplePrefabPath, envMats.FlowerPurple, envMats.FlowerStem);

            td.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
            td.SetDetailResolution(DetailRes, 16);

            var grassProto = new DetailPrototype
            {
                prototype = grassDetailPrefab,
                usePrototypeMesh = true,
                renderMode = DetailRenderMode.VertexLit,
                useInstancing = true,
                minWidth = 0.85f,
                maxWidth = 1.45f,
                minHeight = 0.70f,
                maxHeight = 1.30f,
                healthyColor = new Color(0.45f, 0.82f, 0.32f),
                dryColor = new Color(0.62f, 0.76f, 0.30f)
            };

            var flowerYellowProto = new DetailPrototype
            {
                prototype = flowerYellowPrefab,
                usePrototypeMesh = true,
                renderMode = DetailRenderMode.VertexLit,
                useInstancing = true,
                minWidth = 0.75f,
                maxWidth = 1.30f,
                minHeight = 0.70f,
                maxHeight = 1.25f,
                healthyColor = new Color(1.0f, 0.86f, 0.18f),
                dryColor = new Color(0.92f, 0.74f, 0.14f)
            };

            var flowerRedProto = new DetailPrototype
            {
                prototype = flowerRedPrefab,
                usePrototypeMesh = true,
                renderMode = DetailRenderMode.VertexLit,
                useInstancing = true,
                minWidth = 0.75f,
                maxWidth = 1.30f,
                minHeight = 0.70f,
                maxHeight = 1.25f,
                healthyColor = new Color(0.95f, 0.28f, 0.24f),
                dryColor = new Color(0.80f, 0.20f, 0.18f)
            };

            var flowerBlueProto = new DetailPrototype
            {
                prototype = flowerBluePrefab,
                usePrototypeMesh = true,
                renderMode = DetailRenderMode.VertexLit,
                useInstancing = true,
                minWidth = 0.75f,
                maxWidth = 1.30f,
                minHeight = 0.70f,
                maxHeight = 1.25f,
                healthyColor = new Color(0.28f, 0.62f, 0.98f),
                dryColor = new Color(0.20f, 0.48f, 0.82f)
            };

            var flowerPurpleProto = new DetailPrototype
            {
                prototype = flowerPurplePrefab,
                usePrototypeMesh = true,
                renderMode = DetailRenderMode.VertexLit,
                useInstancing = true,
                minWidth = 0.75f,
                maxWidth = 1.30f,
                minHeight = 0.70f,
                maxHeight = 1.25f,
                healthyColor = new Color(0.72f, 0.38f, 0.92f),
                dryColor = new Color(0.58f, 0.28f, 0.78f)
            };

            td.detailPrototypes = new[]
            {
                grassProto,
                flowerYellowProto,
                flowerRedProto,
                flowerBlueProto,
                flowerPurpleProto
            };
            td.RefreshPrototypes();

            int detW = td.detailWidth;
            int detH = td.detailHeight;
            int[,] grassMap = new int[detH, detW];
            int[,] yellowMap = new int[detH, detW];
            int[,] redMap = new int[detH, detW];
            int[,] blueMap = new int[detH, detW];
            int[,] purpleMap = new int[detH, detW];

            for (int y = 0; y < detH; y++)
            {
                float normZ = (float)y / (detH - 1);
                float worldZ = TerrainOrigin.z + normZ * TerrainSize.z;

                for (int x = 0; x < detW; x++)
                {
                    float normX = (float)x / (detW - 1);
                    float worldX = TerrainOrigin.x + normX * TerrainSize.x;

                    // Skip central ruin platform & cobblestone road
                    if (Mathf.Abs(worldX) < 9.5f && worldZ > 7.5f && worldZ < 24f)
                        continue;
                    if (worldZ <= 12f && Mathf.Abs(worldX - RoadCenterX(worldZ)) < 2.8f)
                        continue;
                    if (Mathf.Abs(worldX) <= 13f && Mathf.Abs(worldZ - 6.0f) < 2.0f)
                        continue;

                    float slope = td.GetSteepness(normX, normZ);
                    float altitude = td.GetInterpolatedHeight(normX, normZ);
                    if (slope > 28f || altitude > 13f)
                        continue;

                    // 1. Continuous rolling grass carpet ("thảm cỏ trải dài cao thấp")
                    float meadowNoise = FBM(worldX * 0.12f + 17.1f, worldZ * 0.12f + 31.4f, 2);
                    if (meadowNoise > 0.28f)
                    {
                        grassMap[y, x] = Mathf.Clamp(Mathf.RoundToInt((meadowNoise - 0.25f) * 15f), 3, 11);
                    }

                    // 2. Interspersed wildflower clusters (Yellow, Red, Blue, Purple)
                    float distRoad = (worldZ <= 12f) ? Mathf.Abs(worldX - RoadCenterX(worldZ)) : 999f;
                    float flowerNoise = FBM(worldX * 0.15f + 44.2f, worldZ * 0.15f + 88.6f, 2);

                    // Roadside verge flower strip (matching reference Slow Roads image with yellow wildflowers along road)
                    if (distRoad >= 2.9f && distRoad <= 6.8f && worldZ >= -110f && worldZ <= 10f)
                    {
                        if (flowerNoise > 0.38f)
                        {
                            int count = Mathf.Clamp(Mathf.RoundToInt((flowerNoise - 0.34f) * 16f), 3, 7);
                            float roadsideHue = Hash(x * 19 + y * 31, 88.5f);
                            if (roadsideHue < 0.65f)
                                yellowMap[y, x] = count;
                            else if (roadsideHue < 0.78f)
                                redMap[y, x] = count;
                            else if (roadsideHue < 0.89f)
                                blueMap[y, x] = count;
                            else
                                purpleMap[y, x] = count;
                        }
                    }
                    else if (flowerNoise > 0.46f)
                    {
                        // Open rolling meadow flower carpets: organic colored ribbons & zones
                        int count = Mathf.Clamp(Mathf.RoundToInt((flowerNoise - 0.42f) * 15f), 2, 6);
                        float zone = FBM(worldX * 0.055f + 14.2f, worldZ * 0.055f + 27.8f, 2);

                        if (zone < 0.28f)
                            yellowMap[y, x] = count;
                        else if (zone < 0.52f)
                            redMap[y, x] = count;
                        else if (zone < 0.76f)
                            blueMap[y, x] = count;
                        else
                            purpleMap[y, x] = count;
                    }
                }
            }

            td.SetDetailLayer(0, 0, 0, grassMap);
            td.SetDetailLayer(0, 0, 1, yellowMap);
            td.SetDetailLayer(0, 0, 2, redMap);
            td.SetDetailLayer(0, 0, 3, blueMap);
            td.SetDetailLayer(0, 0, 4, purpleMap);
        }

        // ── Procedural Mesh Prefab Builders for Terrain Prototypes ──

        private static GameObject CreateOrUpdateTerrainTreePrefab(
            string prefabPath, string meshFileName, bool isPine, Material trunkMat, Material leafMat)
        {
            string meshPath = $"{TerrainDir}/{meshFileName}";
            Mesh treeMesh = BuildCombinedTreeMesh(isPine);
            treeMesh.name = Path.GetFileNameWithoutExtension(meshFileName);

            var existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existingMesh == null)
            {
                AssetDatabase.CreateAsset(treeMesh, meshPath);
            }
            else
            {
                EditorUtility.CopySerialized(treeMesh, existingMesh);
                Object.DestroyImmediate(treeMesh);
                treeMesh = existingMesh;
            }

            var tempGO = new GameObject(Path.GetFileNameWithoutExtension(prefabPath));
            var mf = tempGO.AddComponent<MeshFilter>();
            mf.sharedMesh = treeMesh;

            var mr = tempGO.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { trunkMat, leafMat };

            var col = tempGO.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 2.2f, 0f);
            col.radius = 0.38f;
            col.height = 4.4f;

            var prefab = PrefabUtility.SaveAsPrefabAsset(tempGO, prefabPath);
            Object.DestroyImmediate(tempGO);
            return prefab;
        }

        private static GameObject CreateOrUpdateGrassDetailPrefab(string prefabPath, Material grassTuftMat)
        {
            string meshPath = $"{TerrainDir}/Mesh_GrassDetailCluster.asset";
            Mesh grassMesh = BuildCrossedGrassMesh();
            grassMesh.name = "Mesh_GrassDetailCluster";

            var existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existingMesh == null)
            {
                AssetDatabase.CreateAsset(grassMesh, meshPath);
            }
            else
            {
                EditorUtility.CopySerialized(grassMesh, existingMesh);
                Object.DestroyImmediate(grassMesh);
                grassMesh = existingMesh;
            }

            var tempGO = new GameObject("PF_TerrainGrassDetail");
            var mf = tempGO.AddComponent<MeshFilter>();
            mf.sharedMesh = grassMesh;

            var mr = tempGO.AddComponent<MeshRenderer>();
            mr.sharedMaterial = grassTuftMat;
            mr.shadowCastingMode = ShadowCastingMode.Off;

            var prefab = PrefabUtility.SaveAsPrefabAsset(tempGO, prefabPath);
            Object.DestroyImmediate(tempGO);
            return prefab;
        }

        private static GameObject CreateOrUpdateFlowerDetailPrefab(string prefabPath, Material flowerMat, Material stemMat)
        {
            string meshPath = $"{TerrainDir}/Mesh_FlowerCluster.asset";
            Mesh flowerMesh = BuildFlowerClusterMesh();
            flowerMesh.name = "Mesh_FlowerCluster";

            var existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existingMesh == null)
            {
                AssetDatabase.CreateAsset(flowerMesh, meshPath);
            }
            else
            {
                EditorUtility.CopySerialized(flowerMesh, existingMesh);
                Object.DestroyImmediate(flowerMesh);
                flowerMesh = existingMesh;
            }

            string goName = Path.GetFileNameWithoutExtension(prefabPath);
            var tempGO = new GameObject(goName);
            var mf = tempGO.AddComponent<MeshFilter>();
            mf.sharedMesh = flowerMesh;

            var mr = tempGO.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { flowerMat, stemMat };
            mr.shadowCastingMode = ShadowCastingMode.Off;

            var prefab = PrefabUtility.SaveAsPrefabAsset(tempGO, prefabPath);
            Object.DestroyImmediate(tempGO);
            return prefab;
        }

        private static void AddLimbToCombines(Vector3 start, Vector3 end, float radius, List<CombineInstance> combines, Mesh cylMesh)
        {
            Vector3 delta = end - start;
            float len = delta.magnitude;
            if (len < 0.005f) return;
            Vector3 mid = (start + end) * 0.5f;
            Quaternion rot = Quaternion.FromToRotation(Vector3.up, delta.normalized);
            combines.Add(new CombineInstance
            {
                mesh = cylMesh,
                transform = Matrix4x4.TRS(mid, rot, new Vector3(radius * 2f, len * 0.5f, radius * 2f))
            });
        }

        private static Mesh BuildCombinedTreeMesh(bool isPine)
        {
            var cylGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            var quadGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Mesh cylMesh = cylGO.GetComponent<MeshFilter>().sharedMesh;
            Mesh quadMesh = quadGO.GetComponent<MeshFilter>().sharedMesh;

            var trunkCombines = new List<CombineInstance>();
            var canopyCombines = new List<CombineInstance>();

            if (isPine)
            {
                // Pine trunk (tall tapering cylinder)
                AddLimbToCombines(new Vector3(0f, 0f, 0f), new Vector3(0f, 3.8f, 0f), 0.42f, trunkCombines, cylMesh);
                AddLimbToCombines(new Vector3(0f, 3.8f, 0f), new Vector3(0f, 7.2f, 0f), 0.26f, trunkCombines, cylMesh);

                // Pine tiered foliage cards (graduating smaller upwards)
                float[] tierHeights = { 3.0f, 4.2f, 5.3f, 6.3f, 7.3f };
                float[] tierScales = { 3.2f, 2.6f, 2.0f, 1.4f, 0.9f };

                for (int t = 0; t < tierHeights.Length; t++)
                {
                    float th = tierHeights[t];
                    float ts = tierScales[t];
                    int cardCount = 6;
                    for (int c = 0; c < cardCount; c++)
                    {
                        float ang = c * (360f / cardCount);
                        Quaternion rot = Quaternion.Euler(32f, ang, 0f);
                        Vector3 offset = Quaternion.Euler(0f, ang, 0f) * Vector3.forward * (ts * 0.32f);
                        canopyCombines.Add(new CombineInstance
                        {
                            mesh = quadMesh,
                            transform = Matrix4x4.TRS(new Vector3(0f, th, 0f) + offset, rot, Vector3.one * ts)
                        });
                    }
                }
            }
            else
            {
                // Recursive Branching Deciduous Oak Tree with Crossed Leaf Quads
                var rng = new System.Random(42);

                void BuildBranchToCombines(
                    Vector3 startPos, Quaternion rotation, float length, float radius,
                    int depth, int maxDepth)
                {
                    Vector3 dir = rotation * Vector3.up;
                    Vector3 mid = startPos + dir * (length * 0.5f);

                    trunkCombines.Add(new CombineInstance
                    {
                        mesh = cylMesh,
                        transform = Matrix4x4.TRS(mid, rotation, new Vector3(radius * 2f, length * 0.5f, radius * 2f))
                    });

                    Vector3 tipPos = startPos + dir * length;

                    void AddLeafCluster(Vector3 pos, float scale)
                    {
                        for (int i = 0; i < 6; i++)
                        {
                            Vector3 offset = new Vector3(
                                (float)(rng.NextDouble() * 2.0 - 1.0),
                                (float)(rng.NextDouble() * 2.0 - 1.0),
                                (float)(rng.NextDouble() * 2.0 - 1.0)
                            ) * (scale * 0.2f);

                            Quaternion rot = Quaternion.Euler(
                                (float)(rng.NextDouble() * 110.0 - 55.0),
                                i * 60f + (float)(rng.NextDouble() * 30.0 - 15.0),
                                (float)(rng.NextDouble() * 70.0 - 35.0)
                            );

                            float cardScale = scale * (0.85f + (float)rng.NextDouble() * 0.40f);

                            canopyCombines.Add(new CombineInstance
                            {
                                mesh = quadMesh,
                                transform = Matrix4x4.TRS(pos + offset, rot, Vector3.one * cardScale)
                            });
                        }
                    }

                    if (depth >= maxDepth)
                    {
                        AddLeafCluster(tipPos, radius * 18f);
                        return;
                    }

                    if (depth >= maxDepth - 1)
                    {
                        AddLeafCluster(tipPos, radius * 14f);
                    }

                    int subBranches = (depth == 0) ? 3 : rng.Next(2, 4);
                    for (int i = 0; i < subBranches; i++)
                    {
                        float yaw = (360f / subBranches) * i + (float)(rng.NextDouble() * 50.0 - 25.0);
                        float pitch = (i == 0 && depth < 2)
                            ? 8f + (float)rng.NextDouble() * 10f
                            : 28f + (float)rng.NextDouble() * 24f;

                        Quaternion nextRot = rotation * Quaternion.Euler(pitch, yaw, 0f);
                        float nextLength = length * (0.68f + (float)rng.NextDouble() * 0.14f);
                        float nextRadius = radius * 0.65f;

                        BuildBranchToCombines(tipPos, nextRot, nextLength, nextRadius, depth + 1, maxDepth);
                    }
                }

                BuildBranchToCombines(Vector3.zero, Quaternion.identity, length: 3.8f, radius: 0.42f,
                    depth: 0, maxDepth: 3);
            }

            var trunkSubMesh = new Mesh();
            trunkSubMesh.CombineMeshes(trunkCombines.ToArray(), true, true);

            var canopySubMesh = new Mesh();
            canopySubMesh.CombineMeshes(canopyCombines.ToArray(), true, true);

            var finalCombine = new[]
            {
                new CombineInstance { mesh = trunkSubMesh, transform = Matrix4x4.identity },
                new CombineInstance { mesh = canopySubMesh, transform = Matrix4x4.identity }
            };

            var finalMesh = new Mesh();
            finalMesh.CombineMeshes(finalCombine, mergeSubMeshes: false, useMatrices: true);
            finalMesh.RecalculateBounds();
            finalMesh.RecalculateNormals();

            Object.DestroyImmediate(trunkSubMesh);
            Object.DestroyImmediate(canopySubMesh);
            Object.DestroyImmediate(cylGO);
            Object.DestroyImmediate(quadGO);

            return finalMesh;
        }

        private static Mesh BuildFlowerClusterMesh()
        {
            var cylGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            var sphGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var cubeGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh cylMesh = cylGO.GetComponent<MeshFilter>().sharedMesh;
            Mesh sphMesh = sphGO.GetComponent<MeshFilter>().sharedMesh;
            Mesh cubeMesh = cubeGO.GetComponent<MeshFilter>().sharedMesh;

            var stemCombines = new List<CombineInstance>();
            var petalCombines = new List<CombineInstance>();

            // 1. Center blossom
            AddLimbToCombines(new Vector3(0f, 0f, 0f), new Vector3(0f, 0.40f, 0f), 0.024f, stemCombines, cylMesh);
            petalCombines.Add(new CombineInstance
            {
                mesh = sphMesh,
                transform = Matrix4x4.TRS(new Vector3(0f, 0.42f, 0f), Quaternion.identity, new Vector3(0.24f, 0.12f, 0.24f))
            });

            // 2. 4 Surrounding angled blossoms
            Vector3[] flowerOffsets =
            {
                new Vector3(-0.16f, 0.36f, 0.08f),
                new Vector3(0.18f, 0.38f, 0.06f),
                new Vector3(0.05f, 0.33f, -0.17f),
                new Vector3(-0.10f, 0.35f, -0.14f)
            };

            for (int i = 0; i < flowerOffsets.Length; i++)
            {
                Vector3 top = flowerOffsets[i];
                AddLimbToCombines(new Vector3(top.x * 0.15f, 0.02f, top.z * 0.15f), top, 0.020f, stemCombines, cylMesh);
                petalCombines.Add(new CombineInstance
                {
                    mesh = sphMesh,
                    transform = Matrix4x4.TRS(top + new Vector3(0f, 0.02f, 0f), Quaternion.identity, new Vector3(0.20f, 0.10f, 0.20f))
                });
            }

            // 3. Basal leaf blades (3 angled green slabs around base)
            for (int i = 0; i < 3; i++)
            {
                float angle = i * 120f + 25f;
                stemCombines.Add(new CombineInstance
                {
                    mesh = cubeMesh,
                    transform = Matrix4x4.TRS(
                        new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad) * 0.10f, 0.09f, Mathf.Sin(angle * Mathf.Deg2Rad) * 0.10f),
                        Quaternion.Euler(22f, angle, 0f),
                        new Vector3(0.18f, 0.03f, 0.30f))
                });
            }

            var stemSubMesh = new Mesh();
            stemSubMesh.CombineMeshes(stemCombines.ToArray(), true, true);

            var petalSubMesh = new Mesh();
            petalSubMesh.CombineMeshes(petalCombines.ToArray(), true, true);

            // Submesh 0 = Petals (Vivid Flower Colors), Submesh 1 = Stems (Green)
            var finalCombine = new[]
            {
                new CombineInstance { mesh = petalSubMesh, transform = Matrix4x4.identity },
                new CombineInstance { mesh = stemSubMesh, transform = Matrix4x4.identity }
            };

            var finalMesh = new Mesh();
            finalMesh.CombineMeshes(finalCombine, mergeSubMeshes: false, useMatrices: true);
            finalMesh.RecalculateBounds();
            finalMesh.RecalculateNormals();

            Object.DestroyImmediate(stemSubMesh);
            Object.DestroyImmediate(petalSubMesh);
            Object.DestroyImmediate(cylGO);
            Object.DestroyImmediate(sphGO);
            Object.DestroyImmediate(cubeGO);

            return finalMesh;
        }

        public static Mesh BuildCrossedGrassMesh()
        {
            var mesh = new Mesh();
            mesh.name = "Mesh_GrassDetailCluster";

            // 3 intersecting vertical Quads (Cards) at 0°, 60°, 120°
            int quadCount = 3;
            float width = 0.92f;
            float height = 0.82f;

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            for (int q = 0; q < quadCount; q++)
            {
                float angleDeg = q * 60f + 14f;
                float angleRad = angleDeg * Mathf.Deg2Rad;
                Vector3 right = new Vector3(Mathf.Cos(angleRad), 0f, Mathf.Sin(angleRad));
                Vector3 cardNormal = new Vector3(-right.z, 0f, right.x);

                int baseIdx = vertices.Count;

                // Bottom vertices (pinned to ground at y = 0)
                Vector3 bl = -right * (width * 0.46f);
                Vector3 br = right * (width * 0.46f);

                // Top vertices (flared slightly outward for natural spreading tuft silhouette)
                Vector3 tl = -right * (width * 0.52f) + Vector3.up * height;
                Vector3 tr = right * (width * 0.52f) + Vector3.up * height;

                vertices.Add(bl);
                vertices.Add(br);
                vertices.Add(tl);
                vertices.Add(tr);

                // Soft upward foliage normal trick: blends horizontal card normal with upward vector
                Vector3 softNormal = (cardNormal * 0.35f + Vector3.up * 0.85f).normalized;
                normals.Add(softNormal);
                normals.Add(softNormal);
                normals.Add(softNormal);
                normals.Add(softNormal);

                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(1f, 0f));
                uvs.Add(new Vector2(0f, 1f));
                uvs.Add(new Vector2(1f, 1f));

                // Front face
                triangles.Add(baseIdx + 0);
                triangles.Add(baseIdx + 2);
                triangles.Add(baseIdx + 1);

                triangles.Add(baseIdx + 2);
                triangles.Add(baseIdx + 3);
                triangles.Add(baseIdx + 1);

                // Back face
                triangles.Add(baseIdx + 0);
                triangles.Add(baseIdx + 1);
                triangles.Add(baseIdx + 2);

                triangles.Add(baseIdx + 2);
                triangles.Add(baseIdx + 1);
                triangles.Add(baseIdx + 3);
            }

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();

            return mesh;
        }

        // ── Terrain Math & Spline Helpers ─────────────────────

        public static float RoadCenterX(float worldZ)
        {
            if (worldZ >= -12f) return 0f;
            float t = (-worldZ - 12f) * 0.08f;
            return Mathf.Sin(t) * 6.5f * Mathf.Clamp01((-worldZ - 12f) / 18f);
        }

        private static float OverlookMask(float x, float z, float cx, float cz, float rx, float rz)
        {
            float dx = (x - cx) / rx;
            float dz = (z - cz) / rz;
            float d = Mathf.Sqrt(dx * dx + dz * dz);
            return 1f - SmoothRange(0.65f, 1.15f, d);
        }

        private static float SmoothRange(float edge0, float edge1, float x)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(edge0, edge1, x));
        }

        private static float FBM(float x, float y, int octaves)
        {
            float sum = 0f;
            float amp = 0.5f;
            float freq = 1f;
            float max = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Mathf.PerlinNoise(x * freq, y * freq) * amp;
                max += amp;
                freq *= 2.05f;
                amp *= 0.5f;
            }
            return sum / max;
        }

        private static float RidgedMF(float x, float y, int octaves)
        {
            float sum = 0f;
            float amp = 0.5f;
            float freq = 1f;
            float max = 0f;
            for (int i = 0; i < octaves; i++)
            {
                float n = Mathf.PerlinNoise(x * freq, y * freq);
                n = 1f - Mathf.Abs(n * 2f - 1f);
                n *= n;
                sum += n * amp;
                max += amp;
                freq *= 2.1f;
                amp *= 0.5f;
            }
            return sum / max;
        }

        private static float Hash(int n, float seed)
        {
            return Mathf.Repeat(Mathf.Sin(n * 12.9898f + seed * 78.233f) * 43758.5453f, 1f);
        }
    }
}
#endif
