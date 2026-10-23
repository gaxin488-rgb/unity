#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace EchoesOfTheLost.EditorTools
{
    public static class TerrainWorldBuilder
    {
        private const string TerrainDir = "Assets/_Project/Data/Terrain";
        private const string PrefabDir = "Assets/_Project/Prefabs/Environment";
        private const string MatDir = "Assets/_Project/Art/Materials";
        private const string TerrainAssetPath = TerrainDir + "/TerrainData_VerdaWilds.asset";
        private const string TerrainMatPath = MatDir + "/Mat_Terrain_URP.mat";

        private const string OakPrefabPathA = PrefabDir + "/PF_VerdaOakTree.prefab";
        private const string OakPrefabPathB = PrefabDir + "/PF_VerdaOakTree_VariantB.prefab";
        private const string OakPrefabPathC = PrefabDir + "/PF_VerdaOakTree_VariantC.prefab";
        private const string PinePrefabPathA = PrefabDir + "/PF_VerdaPineTree.prefab";
        private const string PinePrefabPathB = PrefabDir + "/PF_VerdaPineTree_VariantB.prefab";

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
            var envMats = EnvironmentTextureGenerator.GenerateAll(forceRegenerateTextures: true);
            BuildCompleteTerrain(null, envMats);
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
            Debug.Log("[TerrainWorldBuilder] Completed all 4 Terrain stages with realistic organic trees!");
        }

        [MenuItem("EotL/Terrain/1. Build Terrain & Sculpt Heightmap")]
        public static void MenuStep1BuildAndSculpt()
        {
            var envMats = EnvironmentTextureGenerator.GenerateAll(forceRegenerateTextures: false);
            var t = EnsureTerrainInScene(null, envMats);
            SculptVerdaWildsHeightmap(t.terrainData);
            Debug.Log("[TerrainWorldBuilder] Step 1 Complete: Built Terrain & sculpted Verda Wilds heightmap.");
        }

        [MenuItem("EotL/Terrain/2. Paint Textures on Terrain (5 Splat Layers)")]
        public static void MenuStep2PaintTextures()
        {
            var envMats = EnvironmentTextureGenerator.GenerateAll(forceRegenerateTextures: false);
            var t = EnsureTerrainInScene(null, envMats);
            PaintTerrainSplatmap(t.terrainData, envMats.TerrainLayers);
            Debug.Log("[TerrainWorldBuilder] Step 2 Complete: Painted 5 PBR TerrainLayers.");
        }

        [MenuItem("EotL/Terrain/3. Add Grass & Trees to Terrain")]
        public static void MenuStep3AddGrassAndTrees()
        {
            var envMats = EnvironmentTextureGenerator.GenerateAll(forceRegenerateTextures: true);
            var t = EnsureTerrainInScene(null, envMats);
            PopulateTreesAndGrassDetails(t.terrainData, envMats);
            t.Flush();
            Debug.Log("[TerrainWorldBuilder] Step 3 Complete: Updated realistic organic Oak/Pine trees and grass.");
        }

        [MenuItem("EotL/Terrain/4. Edit Terrain - Smooth & Re-Carve Winding Road")]
        public static void MenuStep4EditTerrain()
        {
            var envMats = EnvironmentTextureGenerator.GenerateAll(forceRegenerateTextures: false);
            var t = EnsureTerrainInScene(null, envMats);
            ApplyTerrainEditsAndSmoothing(t.terrainData, smoothIterations: 2);
            PaintTerrainSplatmap(t.terrainData, envMats.TerrainLayers);
            t.Flush();
            Debug.Log("[TerrainWorldBuilder] Step 4 Complete: Applied terrain smoothing and road carving.");
        }

        // ── Master Builder Called by SandboxSceneBuilder ──────

        public static Terrain BuildCompleteTerrain(Transform parent, EnvironmentMaterialSet envMats)
        {
            Directory.CreateDirectory(TerrainDir);
            Directory.CreateDirectory(PrefabDir);
            Directory.CreateDirectory(MatDir);

            Terrain terrain = EnsureTerrainInScene(parent, envMats);
            TerrainData td = terrain.terrainData;
            SculptVerdaWildsHeightmap(td);
            ApplyTerrainEditsAndSmoothing(td, smoothIterations: 1);
            PaintTerrainSplatmap(td, envMats.TerrainLayers);
            PopulateTreesAndGrassDetails(td, envMats);

            terrain.drawHeightmap = true;
            terrain.drawTreesAndFoliage = true;
            terrain.detailObjectDistance = 140f;
            terrain.detailObjectDensity = 1.0f;
            terrain.treeDistance = 500f;
            terrain.treeBillboardDistance = 250f;
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

                    float dx = worldX;
                    float dz = worldZ - 10f;
                    float distFromSanctuary = Mathf.Sqrt(dx * dx + (dz * 0.85f) * (dz * 0.85f));

                    float primaryRolling = FBM(worldX * 0.020f + 11.3f, worldZ * 0.020f + 27.9f, 3);
                    float secondarySwells = Mathf.Sin(worldX * 0.042f + 1.7f) * Mathf.Cos(worldZ * 0.038f + 2.4f) * 0.5f + 0.5f;
                    float hillNoise = primaryRolling * 0.70f + secondarySwells * 0.30f;
                    float hillMask = SmoothRange(20f, 54f, distFromSanctuary);
                    float hillHeight = hillNoise * 0.14f * hillMask;

                    float mountainMask = SmoothRange(48f, 98f, distFromSanctuary);
                    float southPassFactor = (worldZ < -10f && Mathf.Abs(worldX) < 28f)
                        ? SmoothRange(6f, 28f, Mathf.Abs(worldX))
                        : 1f;

                    float ridgeNoise = RidgedMF(worldX * 0.022f + 5.7f, worldZ * 0.022f + 19.4f, 4);
                    float mountainHeight = ridgeNoise * 0.62f * mountainMask * southPassFactor;

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

                    float boxDistX = Mathf.Max(0f, Mathf.Abs(worldX) - 15f);
                    float boxDistZ = worldZ < -14f ? (-14f - worldZ) : (worldZ > 25f ? (worldZ - 25f) : 0f);
                    float boxDist = Mathf.Sqrt(boxDistX * boxDistX + boxDistZ * boxDistZ);
                    float arenaFlatten = SmoothRange(0f, 12f, boxDist);
                    h *= arenaFlatten;

                    float roadCenter = RoadCenterX(worldZ);
                    float distToRoad = Mathf.Abs(worldX - roadCenter);
                    if (worldZ >= -115f && worldZ <= 12f)
                    {
                        float roadFlatten = SmoothRange(3.5f, 14f, distToRoad);
                        float gentlySlopedRoadBed = Mathf.Clamp01((-worldZ - 14f) / 100f) * 0.04f;
                        h = Mathf.Lerp(gentlySlopedRoadBed, h, roadFlatten);
                    }

                    float westOverlook = OverlookMask(worldX, worldZ, -25f, 14f, 9f, 12f);
                    float eastOverlook = OverlookMask(worldX, worldZ, 25f, 14f, 9f, 12f);
                    float terraceTargetHeight = 3.2f / TerrainSize.y;
                    h = Mathf.Lerp(h, terraceTargetHeight, Mathf.Max(westOverlook, eastOverlook) * 0.85f);

                    heights[y, x] = Mathf.Clamp01(h);
                }
            }

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
                    float slope = td.GetSteepness(normX, normZ);
                    float detailNoise = FBM(worldX * 0.08f + 3.1f, worldZ * 0.08f + 7.7f, 2);

                    float distMainRoad = (worldZ >= -115f && worldZ <= 11f)
                        ? Mathf.Abs(worldX - RoadCenterX(worldZ))
                        : 999f;

                    float distBranchRoad = (Mathf.Abs(worldX) <= 13f && worldZ >= 3.5f && worldZ <= 8.0f)
                        ? Mathf.Abs(worldZ - 6.0f)
                        : 999f;

                    float roadDist = Mathf.Min(distMainRoad, distBranchRoad);
                    float wRoad = 1f - SmoothRange(1.8f, 3.4f, roadDist);

                    float roadShoulder = (1f - SmoothRange(2.6f, 5.2f, roadDist)) * (1f - wRoad);
                    float ruinApron = (Mathf.Abs(worldX) < 10f && worldZ > 7f && worldZ < 24f) ? 0.55f : 0f;

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

                    if (wRoad > 0.01f)
                    {
                        float nonRoadScale = 1f - wRoad;
                        wGrass *= nonRoadScale;
                        wEarth *= nonRoadScale;
                        wRock *= nonRoadScale;
                        wMountain *= nonRoadScale;
                    }

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
            td.treeInstances = new TreeInstance[0];
            td.treePrototypes = new TreePrototype[0];

            // Build 3 distinct Oak variants + 2 Pine variants for a natural, non-repetitive forest
            GameObject oakA = CreateOrUpdateTerrainTreePrefab(
                OakPrefabPathA, "Mesh_VerdaOakTree_A.asset", isPine: false, variantSeed: 101, envMats.TreeTrunk, envMats.TreeLeaves);
            GameObject oakB = CreateOrUpdateTerrainTreePrefab(
                OakPrefabPathB, "Mesh_VerdaOakTree_B.asset", isPine: false, variantSeed: 207, envMats.TreeTrunk, envMats.TreeLeaves);
            GameObject oakC = CreateOrUpdateTerrainTreePrefab(
                OakPrefabPathC, "Mesh_VerdaOakTree_C.asset", isPine: false, variantSeed: 319, envMats.TreeTrunk, envMats.TreeLeaves);

            GameObject pineA = CreateOrUpdateTerrainTreePrefab(
                PinePrefabPathA, "Mesh_VerdaPineTree_A.asset", isPine: true, variantSeed: 405, envMats.TreeTrunk, envMats.TreeLeaves);
            GameObject pineB = CreateOrUpdateTerrainTreePrefab(
                PinePrefabPathB, "Mesh_VerdaPineTree_B.asset", isPine: true, variantSeed: 512, envMats.TreeTrunk, envMats.TreeLeaves);

            td.treePrototypes = new[]
            {
                new TreePrototype { prefab = oakA, bendFactor = 0.15f },
                new TreePrototype { prefab = oakB, bendFactor = 0.15f },
                new TreePrototype { prefab = oakC, bendFactor = 0.15f },
                new TreePrototype { prefab = pineA, bendFactor = 0.10f },
                new TreePrototype { prefab = pineB, bendFactor = 0.10f }
            };
            td.RefreshPrototypes();

            var trees = new List<TreeInstance>(240);
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

                    if (Mathf.Abs(worldX) < 13.5f && worldZ > -16f && worldZ < 26f)
                        continue;
                    if (worldZ <= 12f && Mathf.Abs(worldX - RoadCenterX(worldZ)) < 6.5f)
                        continue;

                    float distFromCenter = Vector2.Distance(new Vector2(worldX, worldZ), new Vector2(0f, 8f));
                    if (distFromCenter > 105f)
                        continue;

                    float slope = td.GetSteepness(normX, normZ);
                    float altitude = td.GetInterpolatedHeight(normX, normZ);
                    if (slope > 32f || altitude > 16f)
                        continue;

                    float density = FBM(worldX * 0.04f + 9.2f, worldZ * 0.04f + 4.5f, 2);
                    if (density < 0.36f)
                        continue;

                    bool usePine = (altitude > 4.5f || slope > 17f);
                    int protoIndex = usePine
                        ? 3 + ((gx + gz) % 2)
                        : (gx * 7 + gz * 13) % 3;

                    float widthScale = 0.82f + Hash(gx * 19 + gz * 41, 303f) * 0.52f;
                    float heightScale = widthScale * (0.88f + Hash(gx * 23 + gz * 59, 606f) * 0.28f);
                    float rot = Hash(gx * 71 + gz * 61, 404f) * Mathf.PI * 2f;

                    // Subtle canopy tint variation between trees
                    float tintShift = 0.90f + Hash(gx * 31 + gz * 17, 707f) * 0.14f;
                    Color treeTint = new Color(tintShift, Mathf.Clamp01(tintShift + 0.03f), tintShift * 0.95f, 1f);

                    trees.Add(new TreeInstance
                    {
                        position = new Vector3(normX, altitude / TerrainSize.y, normZ),
                        widthScale = widthScale,
                        heightScale = heightScale,
                        rotation = rot,
                        color = treeTint,
                        lightmapColor = Color.white,
                        prototypeIndex = protoIndex
                    });
                }
            }
            td.SetTreeInstances(trees.ToArray(), snapToHeightmap: true);

            // 2. Create / Update Grass & Wildflower Detail Prototypes
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
                minHeight = 0.65f,
                maxHeight = 1.25f,
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

                    float meadowNoise = FBM(worldX * 0.12f + 17.1f, worldZ * 0.12f + 31.4f, 2);
                    if (meadowNoise > 0.28f)
                    {
                        grassMap[y, x] = Mathf.Clamp(Mathf.RoundToInt((meadowNoise - 0.25f) * 15f), 3, 11);
                    }

                    float distRoad = (worldZ <= 12f) ? Mathf.Abs(worldX - RoadCenterX(worldZ)) : 999f;
                    float flowerNoise = FBM(worldX * 0.15f + 44.2f, worldZ * 0.15f + 88.6f, 2);

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

            UpgradeStandaloneSceneTrees(envMats);
        }

        // Tự động tìm và nâng cấp mọi GameObject cây rời ven đường / quanh di tích trong Scene
        public static void UpgradeStandaloneSceneTrees(EnvironmentMaterialSet envMats)
        {
            Mesh[] oakMeshes =
            {
                AssetDatabase.LoadAssetAtPath<Mesh>($"{TerrainDir}/Mesh_VerdaOakTree_A.asset"),
                AssetDatabase.LoadAssetAtPath<Mesh>($"{TerrainDir}/Mesh_VerdaOakTree_B.asset"),
                AssetDatabase.LoadAssetAtPath<Mesh>($"{TerrainDir}/Mesh_VerdaOakTree_C.asset")
            };
            Mesh pineMesh = AssetDatabase.LoadAssetAtPath<Mesh>($"{TerrainDir}/Mesh_VerdaPineTree_A.asset");

            if (oakMeshes[0] == null) return;

            var allRenderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            var upgradedRoots = new HashSet<Transform>();
            int counter = 0;

            foreach (var mr in allRenderers)
            {
                if (mr == null || mr.gameObject == null) continue;
                var mats = mr.sharedMaterials;
                if (mats == null || mats.Length == 0) continue;

                // Trường hợp 1: Cây rời đã gộp thành 1 Mesh có 2 Material (Trunk + Leaves)
                if (mats.Length == 2 && mats[0] != null && mats[1] != null &&
                    mats[0].name.Contains("TreeTrunk") && mats[1].name.Contains("TreeLeaves"))
                {
                    var mf = mr.GetComponent<MeshFilter>();
                    if (mf != null)
                    {
                        bool isPine = mr.gameObject.name.ToLowerInvariant().Contains("pine");
                        mf.sharedMesh = isPine ? pineMesh : oakMeshes[counter % oakMeshes.Length];
                        mr.sharedMaterials = new[] { envMats.TreeTrunk, envMats.TreeLeaves };
                        counter++;
                    }
                    continue;
                }

                // Trường hợp 2: Cây rời tạo từ nhiều khối Cylinder / Quad con ghép trong 1 GameObject cha
                if (mats.Length == 1 && mats[0] != null &&
                    (mats[0].name.Contains("TreeTrunk") || mats[0].name.Contains("TreeLeaves")))
                {
                    Transform candidate = mr.transform;
                    while (candidate.parent != null &&
                           (candidate.parent.name.ToLowerInvariant().Contains("tree") ||
                            candidate.parent.name.ToLowerInvariant().Contains("oak") ||
                            candidate.parent.name.ToLowerInvariant().Contains("pine") ||
                            candidate.parent.name.ToLowerInvariant().Contains("branch") ||
                            candidate.parent.name.ToLowerInvariant().Contains("canopy")) &&
                           candidate.parent.childCount < 40)
                    {
                        candidate = candidate.parent;
                    }

                    if (candidate != mr.transform && !upgradedRoots.Contains(candidate))
                    {
                        upgradedRoots.Add(candidate);
                        for (int c = candidate.childCount - 1; c >= 0; c--)
                        {
                            Object.DestroyImmediate(candidate.GetChild(c).gameObject);
                        }

                        var mf = candidate.GetComponent<MeshFilter>() ?? candidate.gameObject.AddComponent<MeshFilter>();
                        var rootMr = candidate.GetComponent<MeshRenderer>() ?? candidate.gameObject.AddComponent<MeshRenderer>();
                        bool isPine = candidate.name.ToLowerInvariant().Contains("pine");
                        mf.sharedMesh = isPine ? pineMesh : oakMeshes[counter % oakMeshes.Length];
                        rootMr.sharedMaterials = new[] { envMats.TreeTrunk, envMats.TreeLeaves };
                        counter++;
                    }
                }
            }
        }

        // ── Asset Persistence Helper ─────────────────────────

        private static Mesh SaveOrUpdateMeshAsset(Mesh newMesh, string meshPath)
        {
            var existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existingMesh == null)
            {
                AssetDatabase.CreateAsset(newMesh, meshPath);
                return newMesh;
            }
            else
            {
                existingMesh.Clear();
                EditorUtility.CopySerialized(newMesh, existingMesh);
                EditorUtility.SetDirty(existingMesh);
                Object.DestroyImmediate(newMesh);
                return existingMesh;
            }
        }

        private static GameObject CreateOrUpdateTerrainTreePrefab(
            string prefabPath, string meshFileName, bool isPine, int variantSeed, Material trunkMat, Material leafMat)
        {
            string meshPath = $"{TerrainDir}/{meshFileName}";
            Mesh treeMesh = BuildCombinedTreeMesh(isPine, variantSeed);
            treeMesh.name = Path.GetFileNameWithoutExtension(meshFileName);
            treeMesh = SaveOrUpdateMeshAsset(treeMesh, meshPath);

            var tempGO = new GameObject(Path.GetFileNameWithoutExtension(prefabPath));
            var mf = tempGO.AddComponent<MeshFilter>();
            mf.sharedMesh = treeMesh;

            var mr = tempGO.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { trunkMat, leafMat };

            var col = tempGO.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 3.0f, 0f);
            col.radius = 0.42f;
            col.height = 6.0f;

            var prefab = PrefabUtility.SaveAsPrefabAsset(tempGO, prefabPath);
            Object.DestroyImmediate(tempGO);
            return prefab;
        }

        private static GameObject CreateOrUpdateGrassDetailPrefab(string prefabPath, Material grassTuftMat)
        {
            string meshPath = $"{TerrainDir}/Mesh_GrassDetailCluster.asset";
            Mesh grassMesh = BuildCrossedGrassMesh();
            grassMesh.name = "Mesh_GrassDetailCluster";
            grassMesh = SaveOrUpdateMeshAsset(grassMesh, meshPath);

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
            flowerMesh = SaveOrUpdateMeshAsset(flowerMesh, meshPath);

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

        // ── Realistic Organic Multi-Segment Tree & Spherical-Normal Canopy Builder ──

        // Builds a multi-segment curved & smoothly tapering branch tube (with optional root flare at base)
        private static void AppendCurvedTaperedBranch(
            Vector3 startPos, Vector3 initialDir, float totalLength,
            float rStart, float rEnd, int rings, int sides,
            Vector3 curveBias, bool flareRoot, float vOffset,
            List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris,
            out Vector3[] spinePositions, out Vector3[] spineTangents)
        {
            spinePositions = new Vector3[rings + 1];
            spineTangents = new Vector3[rings + 1];

            Vector3 pos = startPos;
            Vector3 dir = initialDir.normalized;
            float stepLen = totalLength / rings;

            int baseVertIdx = verts.Count;

            for (int r = 0; r <= rings; r++)
            {
                float t = (float)r / rings;
                spinePositions[r] = pos;
                spineTangents[r] = dir;

                // Smooth exponential taper + natural flared root buttress near ground
                float radius = Mathf.Lerp(rStart, rEnd, Mathf.Pow(t, 0.85f));
                if (flareRoot && t < 0.35f)
                {
                    float flareT = 1f - (t / 0.35f);
                    radius += rStart * 0.55f * flareT * flareT;
                }

                Quaternion ringRot = Quaternion.FromToRotation(Vector3.up, dir);

                for (int s = 0; s <= sides; s++)
                {
                    float u = (float)s / sides;
                    float ang = u * Mathf.PI * 2f;

                    // Subtle fluted root ridges at the base of the trunk
                    float flute = (flareRoot && t < 0.4f)
                        ? 1f + 0.14f * (1f - t / 0.4f) * Mathf.Sin(ang * 5f)
                        : 1f;

                    Vector3 localRadial = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                    Vector3 worldRadial = ringRot * localRadial;

                    verts.Add(pos + worldRadial * (radius * flute));
                    norms.Add(worldRadial.normalized);
                    uvs.Add(new Vector2(u * 2f, vOffset + t * totalLength * 0.45f));
                }

                if (r < rings)
                {
                    // Gently curve the branch along curveBias as it grows
                    dir = (dir + curveBias * (1f / rings)).normalized;
                    pos += dir * stepLen;
                }
            }

            int stride = sides + 1;
            for (int r = 0; r < rings; r++)
            {
                int row0 = baseVertIdx + r * stride;
                int row1 = baseVertIdx + (r + 1) * stride;
                for (int s = 0; s < sides; s++)
                {
                    tris.Add(row0 + s);
                    tris.Add(row1 + s);
                    tris.Add(row0 + s + 1);

                    tris.Add(row0 + s + 1);
                    tris.Add(row1 + s);
                    tris.Add(row1 + s + 1);
                }
            }
        }

        // Appends a slightly arched 3D leaf card whose normals point spherically outward from canopyCenter.
        // This eliminates flat X-plane shading seams and gives the tree a lush, volumetric 3D crown!
        private static void AppendArchedFoliageCard(
            Vector3 cardCenter, Quaternion cardRot, float scale, Vector3 canopyCenter,
            List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris)
        {
            int idx = verts.Count;
            float half = scale * 0.5f;
            float arch = scale * 0.14f; // 5-vertex arched dome card so it is never a flat plane

            Vector3[] localPts =
            {
                new Vector3(-half, -half,  arch * 0.4f), // 0: bottom-left
                new Vector3( half, -half,  arch * 0.4f), // 1: bottom-right
                new Vector3(-half,  half,  arch * 0.4f), // 2: top-left
                new Vector3( half,  half,  arch * 0.4f), // 3: top-right
                new Vector3( 0f,    0f,   -arch)         // 4: arched center apex
            };

            Vector2[] localUVs =
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 0.5f)
            };

            for (int i = 0; i < 5; i++)
            {
                Vector3 wp = cardCenter + cardRot * localPts[i];
                verts.Add(wp);

                // SPHERICAL OUTWARD NORMAL: makes overlapping cards shade as a cohesive 3D leaf cloud
                Vector3 outward = (wp - canopyCenter).normalized;
                Vector3 sphericalNormal = (outward * 0.78f + Vector3.up * 0.35f).normalized;
                norms.Add(sphericalNormal);
                uvs.Add(localUVs[i]);
            }

            // 4 triangles front + 4 triangles back
            int[] cardTris =
            {
                0, 2, 4,  2, 3, 4,  3, 1, 4,  1, 0, 4,
                0, 4, 2,  2, 4, 3,  3, 4, 1,  1, 4, 0
            };

            for (int t = 0; t < cardTris.Length; t++)
                tris.Add(idx + cardTris[t]);
        }

        // Creates a lush dome-shaped sub-crown of overlapping arched leaf cards at the end of a branch
        private static void AppendCanopyCloudDome(
            Vector3 domeCenter, float domeRadius, Vector3 treeCrownCenter, System.Random rng,
            List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris)
        {
            // Fibonacci hemisphere distribution around the branch tip so leaves form a natural billowing puff
            int cardCount = 14;
            float goldenAngle = Mathf.PI * (3f - Mathf.Sqrt(5f));

            for (int i = 0; i < cardCount; i++)
            {
                float t = (float)i / (cardCount - 1);
                // Bias towards upper hemisphere (y from -0.25 to +0.95) for an umbrella-like oak bough
                float y = Mathf.Lerp(-0.22f, 0.95f, t);
                float r = Mathf.Sqrt(Mathf.Max(0.01f, 1f - y * y));
                float theta = i * goldenAngle + (float)(rng.NextDouble() * 0.4 - 0.2);

                Vector3 dir = new Vector3(Mathf.Cos(theta) * r, y, Mathf.Sin(theta) * r).normalized;
                float dist = domeRadius * (0.38f + 0.32f * (float)rng.NextDouble());
                Vector3 cardPos = domeCenter + dir * dist;

                // Orient each arched card tangential to the dome surface with slight organic flutter
                Quaternion lookOut = Quaternion.LookRotation(dir, Vector3.up);
                Quaternion flutter = Quaternion.Euler(
                    (float)(rng.NextDouble() * 36.0 - 18.0),
                    (float)(rng.NextDouble() * 36.0 - 18.0),
                    (float)(rng.NextDouble() * 180.0));

                float cardSize = domeRadius * (0.95f + (float)rng.NextDouble() * 0.32f);

                // Blend normal center between local dome and overall tree crown
                Vector3 shadingCenter = Vector3.Lerp(domeCenter - Vector3.up * (domeRadius * 0.35f), treeCrownCenter, 0.35f);
                AppendArchedFoliageCard(cardPos, lookOut * flutter, cardSize, shadingCenter, verts, norms, uvs, tris);
            }
        }

        private static Mesh BuildCombinedTreeMesh(bool isPine, int variantSeed = 42)
        {
            var trunkVerts = new List<Vector3>();
            var trunkNorms = new List<Vector3>();
            var trunkUVs = new List<Vector2>();
            var trunkTris = new List<int>();

            var leafVerts = new List<Vector3>();
            var leafNorms = new List<Vector3>();
            var leafUVs = new List<Vector2>();
            var leafTris = new List<int>();

            var rng = new System.Random(variantSeed);

            if (isPine)
            {
                // Tall natural conifer (9.8m) with slight organic lean, tapering trunk, wooden boughs & drooping needle tiers
                Vector3 lean = new Vector3(
                    (float)(rng.NextDouble() - 0.5) * 0.08f,
                    0f,
                    (float)(rng.NextDouble() - 0.5) * 0.08f);

                AppendCurvedTaperedBranch(
                    Vector3.zero, Vector3.up, totalLength: 9.6f,
                    rStart: 0.40f, rEnd: 0.03f, rings: 8, sides: 8,
                    curveBias: lean, flareRoot: true, vOffset: 0f,
                    trunkVerts, trunkNorms, trunkUVs, trunkTris,
                    out Vector3[] trunkPts, out _);

                Vector3 pineCrownCenter = new Vector3(0f, 5.8f, 0f);
                int tiers = 7;

                for (int t = 0; t < tiers; t++)
                {
                    float tierFrac = (float)t / (tiers - 1);
                    float height = Mathf.Lerp(2.3f, 8.8f, tierFrac);
                    float maxReach = Mathf.Lerp(2.45f, 0.60f, tierFrac);
                    int boughs = (t < 4) ? 6 : 5;

                    Vector3 tierOrigin = Vector3.Lerp(trunkPts[1], trunkPts[trunkPts.Length - 1], tierFrac * 0.92f);
                    tierOrigin.y = height;

                    for (int b = 0; b < boughs; b++)
                    {
                        float yaw = b * (360f / boughs) + t * 29f + (float)(rng.NextDouble() * 18.0 - 9.0);
                        Quaternion bRot = Quaternion.Euler(18f + tierFrac * 8f, yaw, 0f);
                        Vector3 bDir = bRot * Vector3.forward;
                        float bLen = maxReach * (0.85f + (float)rng.NextDouble() * 0.25f);

                        AppendCurvedTaperedBranch(
                            tierOrigin, bDir, bLen,
                            rStart: 0.08f * (1f - tierFrac * 0.55f), rEnd: 0.015f,
                            rings: 3, sides: 5,
                            curveBias: new Vector3(0f, 0.12f, 0f), flareRoot: false, vOffset: 1f,
                            trunkVerts, trunkNorms, trunkUVs, trunkTris,
                            out Vector3[] bPts, out _);

                        // Layered drooping pine foliage pads along the bough
                        for (int p = 1; p < bPts.Length; p++)
                        {
                            Vector3 padPos = bPts[p];
                            float padScale = maxReach * (0.68f - p * 0.08f);
                            for (int c = -1; c <= 1; c++)
                            {
                                Quaternion cardRot = Quaternion.Euler(28f, yaw + c * 32f, c * 12f);
                                AppendArchedFoliageCard(
                                    padPos + Vector3.up * 0.08f, cardRot, padScale, pineCrownCenter,
                                    leafVerts, leafNorms, leafUVs, leafTris);
                            }
                        }
                    }
                }

                // Pointed conifer top leader tuft
                Vector3 topTip = trunkPts[trunkPts.Length - 1];
                for (int c = 0; c < 5; c++)
                {
                    Quaternion rot = Quaternion.Euler(15f, c * 72f, 0f);
                    AppendArchedFoliageCard(topTip, rot, 0.95f, topTip - Vector3.up * 0.5f,
                        leafVerts, leafNorms, leafUVs, leafTris);
                }
            }
            else
            {
                // ── Realistic Multi-Tiered Gnarled Oak Tree ──
                // 1. Main sinuous trunk from flared roots up into the crown (no flat cylinder joints!)
                Vector3 trunkSway = new Vector3(
                    (float)(rng.NextDouble() * 0.28 - 0.14),
                    0f,
                    (float)(rng.NextDouble() * 0.28 - 0.14));

                float mainTrunkLen = 4.6f + (float)rng.NextDouble() * 0.7f;
                AppendCurvedTaperedBranch(
                    Vector3.zero, Vector3.up, mainTrunkLen,
                    rStart: 0.44f, rEnd: 0.18f, rings: 7, sides: 9,
                    curveBias: trunkSway, flareRoot: true, vOffset: 0f,
                    trunkVerts, trunkNorms, trunkUVs, trunkTris,
                    out Vector3[] trunkSpine, out Vector3[] trunkDirs);

                Vector3 overallCrownCenter = new Vector3(0f, 5.8f, 0f);

                // Recursive secondary & tertiary branch generator
                void GrowOakBough(
                    Vector3 startPos, Vector3 startDir, float length, float rStart, float rEnd,
                    int depth, int maxDepth)
                {
                    // Natural upward phototropism (branches arch outward then curve gently toward the sky)
                    Vector3 gnarl = new Vector3(
                        (float)(rng.NextDouble() * 0.36 - 0.18),
                        0.22f + depth * 0.08f,
                        (float)(rng.NextDouble() * 0.36 - 0.18));

                    int rings = Mathf.Max(3, 5 - depth);
                    int sides = Mathf.Max(5, 8 - depth);

                    AppendCurvedTaperedBranch(
                        startPos, startDir, length, rStart, rEnd, rings, sides,
                        curveBias: gnarl, flareRoot: false, vOffset: depth * 1.3f,
                        trunkVerts, trunkNorms, trunkUVs, trunkTris,
                        out Vector3[] spine, out Vector3[] tangents);

                    Vector3 tipPos = spine[spine.Length - 1];
                    Vector3 tipDir = tangents[tangents.Length - 1];

                    if (depth >= maxDepth)
                    {
                        // Outer twig tip: place a lush volumetric leaf cloud dome hiding the fine twig tip
                        float domeRadius = 1.45f + (float)rng.NextDouble() * 0.35f;
                        AppendCanopyCloudDome(tipPos, domeRadius, overallCrownCenter, rng,
                            leafVerts, leafNorms, leafUVs, leafTris);
                        return;
                    }

                    // Mid-bough foliage cluster so the interior canopy is full and continuous
                    if (depth >= maxDepth - 1)
                    {
                        Vector3 midBranchPos = spine[spine.Length / 2];
                        AppendCanopyCloudDome(midBranchPos + Vector3.up * 0.25f, 1.25f, overallCrownCenter, rng,
                            leafVerts, leafNorms, leafUVs, leafTris);
                    }

                    // Spawn 2 to 3 sub-branches at staggered points (not all at the exact same point!)
                    int subCount = (depth == 1) ? 3 : 2;
                    for (int i = 0; i < subCount; i++)
                    {
                        // Fork from 65%..100% along the parent branch for staggered natural joints
                        int spawnRing = (i == 0) ? spine.Length - 1 : Mathf.Max(2, spine.Length - 2);
                        Vector3 forkPos = spine[spawnRing];
                        Vector3 parentDir = tangents[spawnRing];

                        float yaw = i * (360f / subCount) + depth * 57f + (float)(rng.NextDouble() * 44.0 - 22.0);
                        float spreadAngle = (i == 0)
                            ? 18f + (float)rng.NextDouble() * 14f
                            : 34f + (float)rng.NextDouble() * 18f;

                        Quaternion forkRot = Quaternion.FromToRotation(Vector3.up, parentDir)
                                           * Quaternion.Euler(spreadAngle, yaw, 0f);
                        Vector3 childDir = (forkRot * Vector3.up).normalized;

                        float childLen = length * (0.72f + (float)rng.NextDouble() * 0.14f);
                        float childRStart = Mathf.Lerp(rStart, rEnd, (float)spawnRing / (spine.Length - 1)) * 0.82f;
                        // Outer twigs taper down to 0.02m so no blunt wooden cylinders ever poke out!
                        float childREnd = (depth + 1 >= maxDepth) ? 0.022f : childRStart * 0.56f;

                        GrowOakBough(forkPos, childDir, childLen, childRStart, childREnd, depth + 1, maxDepth);
                    }
                }

                // 2. Sprout 5 primary boughs at DIFFERENT heights along the upper half of the trunk (rings 3, 4, 5, 6)
                int[] boughRings = { 3, 4, 5, 5, 6 };
                float[] boughYaws = { 0f, 135f, 255f, 65f, 195f };
                float[] boughPitches = { 52f, 48f, 42f, 38f, 20f };
                float[] boughLengths = { 2.4f, 2.5f, 2.6f, 2.4f, 2.2f };

                float yawOffset = (float)(rng.NextDouble() * 360.0);

                for (int b = 0; b < boughRings.Length; b++)
                {
                    int rIdx = Mathf.Clamp(boughRings[b], 2, trunkSpine.Length - 1);
                    Vector3 origin = trunkSpine[rIdx];
                    Vector3 tDir = trunkDirs[rIdx];

                    float tFrac = (float)rIdx / (trunkSpine.Length - 1);
                    float trunkRadAtNode = Mathf.Lerp(0.44f, 0.18f, tFrac);

                    float yaw = boughYaws[b] + yawOffset + (float)(rng.NextDouble() * 24.0 - 12.0);
                    float pitch = boughPitches[b] + (float)(rng.NextDouble() * 10.0 - 5.0);

                    Quaternion bRot = Quaternion.FromToRotation(Vector3.up, tDir) * Quaternion.Euler(pitch, yaw, 0f);
                    Vector3 bDir = (bRot * Vector3.up).normalized;

                    GrowOakBough(
                        origin, bDir,
                        length: boughLengths[b],
                        rStart: trunkRadAtNode * 0.72f,
                        rEnd: trunkRadAtNode * 0.36f,
                        depth: 1, maxDepth: 3);
                }

                // 3. Central crown top dome so the tree silhouette is full and rounded at the apex
                Vector3 apex = trunkSpine[trunkSpine.Length - 1] + Vector3.up * 2.1f;
                AppendCanopyCloudDome(apex, 1.75f, overallCrownCenter, rng,
                    leafVerts, leafNorms, leafUVs, leafTris);
            }

            // Assemble Multi-Submesh WITHOUT calling RecalculateNormals() so our custom spherical
            // foliage normals and smooth cylindrical bark normals are preserved 100%!
            var finalMesh = new Mesh();
            int trunkVertCount = trunkVerts.Count;

            var allVerts = new List<Vector3>(trunkVertCount + leafVerts.Count);
            allVerts.AddRange(trunkVerts);
            allVerts.AddRange(leafVerts);

            var allNorms = new List<Vector3>(trunkVertCount + leafNorms.Count);
            allNorms.AddRange(trunkNorms);
            allNorms.AddRange(leafNorms);

            var allUVs = new List<Vector2>(trunkVertCount + leafUVs.Count);
            allUVs.AddRange(trunkUVs);
            allUVs.AddRange(leafUVs);

            var shiftedLeafTris = new List<int>(leafTris.Count);
            for (int i = 0; i < leafTris.Count; i++)
                shiftedLeafTris.Add(leafTris[i] + trunkVertCount);

            finalMesh.SetVertices(allVerts);
            finalMesh.SetNormals(allNorms);
            finalMesh.SetUVs(0, allUVs);
            finalMesh.subMeshCount = 2;
            finalMesh.SetTriangles(trunkTris, 0);
            finalMesh.SetTriangles(shiftedLeafTris, 1);
            finalMesh.RecalculateBounds();

            return finalMesh;
        }

        // ── 3D Multi-Petal Wildflower Cluster ────────────────

        private static Mesh BuildFlowerClusterMesh()
        {
            var petalVerts = new List<Vector3>();
            var petalNorms = new List<Vector3>();
            var petalUVs = new List<Vector2>();
            var petalTris = new List<int>();

            var stemVerts = new List<Vector3>();
            var stemNorms = new List<Vector3>();
            var stemUVs = new List<Vector2>();
            var stemTris = new List<int>();

            Vector3[] tops =
            {
                new Vector3( 0.00f, 0.42f,  0.00f),
                new Vector3(-0.16f, 0.36f,  0.08f),
                new Vector3( 0.18f, 0.38f,  0.06f),
                new Vector3( 0.05f, 0.33f, -0.17f),
                new Vector3(-0.10f, 0.35f, -0.14f)
            };

            for (int f = 0; f < tops.Length; f++)
            {
                Vector3 top = tops[f];
                Vector3 basePos = new Vector3(top.x * 0.25f, 0f, top.z * 0.25f);
                AppendCurvedTaperedBranch(
                    basePos, (top - basePos).normalized, Vector3.Distance(basePos, top),
                    0.018f, 0.010f, 2, 5, Vector3.zero, false, 0f,
                    stemVerts, stemNorms, stemUVs, stemTris, out _, out _);

                int petals = 6;
                for (int p = 0; p < petals; p++)
                {
                    float yaw = p * (360f / petals) + f * 17f;
                    Quaternion rot = Quaternion.Euler(25f, yaw, 0f);
                    int idx = petalVerts.Count;

                    Vector3 p0 = top;
                    Vector3 p1 = top + rot * new Vector3(-0.045f, 0.02f, 0.06f);
                    Vector3 p2 = top + rot * new Vector3( 0.045f, 0.02f, 0.06f);
                    Vector3 p3 = top + rot * new Vector3( 0.000f, 0.01f, 0.13f);

                    petalVerts.Add(p0); petalVerts.Add(p1); petalVerts.Add(p2); petalVerts.Add(p3);
                    for (int n = 0; n < 4; n++) petalNorms.Add(Vector3.up);
                    petalUVs.Add(new Vector2(0.5f, 0f));
                    petalUVs.Add(new Vector2(0f, 0.5f));
                    petalUVs.Add(new Vector2(1f, 0.5f));
                    petalUVs.Add(new Vector2(0.5f, 1f));

                    petalTris.Add(idx + 0); petalTris.Add(idx + 1); petalTris.Add(idx + 2);
                    petalTris.Add(idx + 1); petalTris.Add(idx + 3); petalTris.Add(idx + 2);
                    petalTris.Add(idx + 0); petalTris.Add(idx + 2); petalTris.Add(idx + 1);
                    petalTris.Add(idx + 1); petalTris.Add(idx + 2); petalTris.Add(idx + 3);
                }
            }

            var finalMesh = new Mesh();
            int petalVertCount = petalVerts.Count;

            var allVerts = new List<Vector3>(petalVertCount + stemVerts.Count);
            allVerts.AddRange(petalVerts);
            allVerts.AddRange(stemVerts);

            var allNorms = new List<Vector3>(petalVertCount + stemNorms.Count);
            allNorms.AddRange(petalNorms);
            allNorms.AddRange(stemNorms);

            var allUVs = new List<Vector2>(petalVertCount + stemUVs.Count);
            allUVs.AddRange(petalUVs);
            allUVs.AddRange(stemUVs);

            var shiftedStemTris = new List<int>(stemTris.Count);
            for (int i = 0; i < stemTris.Count; i++)
                shiftedStemTris.Add(stemTris[i] + petalVertCount);

            finalMesh.SetVertices(allVerts);
            finalMesh.SetNormals(allNorms);
            finalMesh.SetUVs(0, allUVs);
            finalMesh.subMeshCount = 2;
            finalMesh.SetTriangles(petalTris, 0);
            finalMesh.SetTriangles(shiftedStemTris, 1);
            finalMesh.RecalculateBounds();

            return finalMesh;
        }

        // ── Crossed Grass Tuft Mesh ──────────────────────────

        public static Mesh BuildCrossedGrassMesh()
        {
            var mesh = new Mesh();
            mesh.name = "Mesh_GrassDetailCluster";

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

                Vector3 bl = -right * (width * 0.46f);
                Vector3 br = right * (width * 0.46f);
                Vector3 tl = -right * (width * 0.52f) + Vector3.up * height;
                Vector3 tr = right * (width * 0.52f) + Vector3.up * height;

                vertices.Add(bl);
                vertices.Add(br);
                vertices.Add(tl);
                vertices.Add(tr);

                Vector3 softNormal = (cardNormal * 0.35f + Vector3.up * 0.85f).normalized;
                normals.Add(softNormal);
                normals.Add(softNormal);
                normals.Add(softNormal);
                normals.Add(softNormal);

                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(1f, 0f));
                uvs.Add(new Vector2(0f, 1f));
                uvs.Add(new Vector2(1f, 1f));

                triangles.Add(baseIdx + 0);
                triangles.Add(baseIdx + 2);
                triangles.Add(baseIdx + 1);

                triangles.Add(baseIdx + 2);
                triangles.Add(baseIdx + 3);
                triangles.Add(baseIdx + 1);

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

        // ── Terrain Math & Spline Helpers ────────────────────

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
