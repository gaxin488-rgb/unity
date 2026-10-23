#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using EchoesOfTheLost.Characters.Enemies;
using EchoesOfTheLost.Characters.NPCs;
using EchoesOfTheLost.Characters.Player;
using EchoesOfTheLost.Core;
using EchoesOfTheLost.Systems;
using EchoesOfTheLost.Systems.Combat;
using EchoesOfTheLost.Systems.Dialogue;
using EchoesOfTheLost.Systems.Economy;
using EchoesOfTheLost.Systems.Inventory;
using EchoesOfTheLost.Systems.Progression;
using EchoesOfTheLost.Systems.Quest;
using EchoesOfTheLost.UI;
using EchoesOfTheLost.World;

// ============================================================
//  SandboxSceneBuilder (v2) — Builds the playable "Verda Wilds"
//  vertical slice in Assets/_Project/Scenes/Sandbox.unity:
//    - Core, World, Quest, Dialogue, Economy, Time & Weather systems
//    - Stylized URP materials + ScriptableObject QuestData & BiomeData
//    - Ancient Solenne Ruins, 2 Echo Crystals, Elder Maren NPC,
//      2 patrolling Overgrown Guardians with floating health bars
//    - Full runtime HUD Canvas (HP/Stamina/XP/Gold, Clock, Quest
//      Tracker, Interaction Prompt, Dialogue Box, Toast Banner)
// ============================================================
namespace EchoesOfTheLost.EditorTools
{
    public static class SandboxSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Sandbox.unity";
        private const string StampPath = "ProjectSettings/SandboxVersion.txt";
        private const string BuilderVersion = "v6_scripting_gameplay_systems";
        private const string ControllerPath = "Assets/_Project/Art/Characters/Player/PlayerAnimator.controller";
        private const string InputPath = "Assets/_Project/Settings/Input/PlayerInputActions.inputactions";
        private const string MatDir = "Assets/_Project/Art/Materials";
        private const string QuestAssetPath = "Assets/_Project/Data/ScriptableObjects/Quests/Quest_FirstEcho.asset";
        private const string BiomeAssetPath = "Assets/_Project/Data/ScriptableObjects/Biomes/Biome_VerdaWilds.asset";

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool PostMessageW(System.IntPtr hWnd, uint Msg, System.IntPtr wParam, System.IntPtr lParam);

        private const uint WM_COMMAND = 0x0111;
        private const uint WM_ACTIVATEAPP = 0x001C;
        private const int CMD_ASSETS_REFRESH = 40323;

        private static double _nextWatchTime;
        private static System.DateTime _lastMaxWriteUtc;
        private static System.IntPtr _unityMainHwnd;
        private static System.Threading.Timer _bgWatchTimer;
        private static System.Action _signalTick;

        [InitializeOnLoadMethod]
        private static void AutoBuildOnce()
        {
            _lastMaxWriteUtc = GetMaxScriptWriteUtc();
            Application.runInBackground = true;

            // Keep Unity Pipeline AutoTick enabled across domain reloads even when minimized
            SessionState.SetBool("Unity.Pipeline.AutoTick.Enabled", true);
            SessionState.SetInt("Unity.Pipeline.AutoTick.IntervalMs", 16);

            try
            {
                var sigMethod = typeof(EditorApplication).GetMethod(
                    "SignalTick",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                if (sigMethod != null)
                {
                    _signalTick = (System.Action)System.Delegate.CreateDelegate(typeof(System.Action), sigMethod);
                }
            }
            catch
            {
                _signalTick = null;
            }

            try
            {
                _unityMainHwnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
            }
            catch
            {
                _unityMainHwnd = System.IntPtr.Zero;
            }

            EditorApplication.update -= BackgroundWatchScripts;
            EditorApplication.update += BackgroundWatchScripts;

            // Background ThreadPool timer so minimized or unfocused Unity Editor wakes up continuously
            _bgWatchTimer?.Dispose();
            _bgWatchTimer = new System.Threading.Timer(BackgroundTimerCallback, null, 1000, 1000);
            AssemblyReloadEvents.beforeAssemblyReload += () =>
            {
                _bgWatchTimer?.Dispose();
                _bgWatchTimer = null;
            };

            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (_unityMainHwnd == System.IntPtr.Zero)
                {
                    try { _unityMainHwnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle; } catch { }
                }
                string currentStamp = File.Exists(StampPath) ? File.ReadAllText(StampPath).Trim() : "";
                if (!File.Exists(ScenePath) || currentStamp != BuilderVersion)
                {
                    Build(silent: true);
                }
                UnityEditor.PackageManager.Client.Resolve();
            };
        }

        private static void BackgroundTimerCallback(object state)
        {
            try
            {
                // Always signal a tick so Unity's main thread stays responsive even when minimized (IsIconic)
                _signalTick?.Invoke();

                var latest = GetMaxScriptWriteUtc();
                if (latest <= _lastMaxWriteUtc) return;
                _lastMaxWriteUtc = latest;

                EditorApplication.delayCall += () =>
                {
                    if (!EditorApplication.isCompiling && !EditorApplication.isUpdating && !EditorApplication.isPlayingOrWillChangePlaymode)
                        AssetDatabase.Refresh();
                };
                _signalTick?.Invoke();

                if (_unityMainHwnd != System.IntPtr.Zero)
                {
                    PostMessageW(_unityMainHwnd, WM_ACTIVATEAPP, new System.IntPtr(1), System.IntPtr.Zero);
                    PostMessageW(_unityMainHwnd, WM_COMMAND, new System.IntPtr(CMD_ASSETS_REFRESH), System.IntPtr.Zero);
                }
            }
            catch
            {
                // Ignore background thread exceptions
            }
        }

        private static void BackgroundWatchScripts()
        {
            if (EditorApplication.timeSinceStartup < _nextWatchTime) return;
            _nextWatchTime = EditorApplication.timeSinceStartup + 2.0;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            var latest = GetMaxScriptWriteUtc();
            if (latest > _lastMaxWriteUtc)
            {
                _lastMaxWriteUtc = latest;
                AssetDatabase.Refresh();
            }
        }

        private static System.DateTime GetMaxScriptWriteUtc()
        {
            System.DateTime max = System.DateTime.MinValue;
            const string codeDir = "Assets/_Project/Code";
            if (!Directory.Exists(codeDir)) return max;
            foreach (string f in Directory.GetFiles(codeDir, "*.cs", SearchOption.AllDirectories))
            {
                var t = File.GetLastWriteTimeUtc(f);
                if (t > max) max = t;
            }
            return max;
        }

        [MenuItem("EotL/Setup/Build Sandbox Scene (Verda Wilds)")]
        public static void BuildFromMenu() => Build(silent: false);

        public static void Build(bool silent = true)
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;

            AssetDatabase.ImportAsset(InputPath, ImportAssetOptions.ForceUpdate);
            var inputAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
            if (inputAsset == null)
            {
                Debug.LogWarning("[SandboxSceneBuilder] Could not load " + InputPath + " as InputActionAsset yet.");
                return;
            }

            var controller = CreateOrLoadPlayerAnimator();

            // ── Create PBR Textures, Materials & TerrainLayers ──
            Directory.CreateDirectory(MatDir);
            var envMats = EnvironmentTextureGenerator.GenerateAll();
            var matGrass = envMats.Grass;
            var matGrassTuft = envMats.GrassTuft;
            var matEarth = envMats.Earth;
            var matRoad = envMats.Road;
            var matRock = envMats.Rock;
            var matStone = envMats.RuinStone;
            var matMountain = envMats.Mountain;
            var matTreeTrunk = envMats.TreeTrunk;
            var matTreeLeaves = envMats.TreeLeaves;
            var matCrystal = envMats.Crystal;

            var matPlayer = CreateOrLoadMaterial("Mat_Player.mat", new Color(0.20f, 0.55f, 0.90f));
            var matNpc = CreateOrLoadMaterial("Mat_ElderMaren.mat", new Color(0.90f, 0.72f, 0.22f));
            var matEnemy = CreateOrLoadMaterial("Mat_Guardian.mat", new Color(0.82f, 0.24f, 0.24f));

            // ── Create ScriptableObject Assets ──
            var questFirstEcho = CreateOrLoadQuestAsset();
            var biomeVerda = CreateOrLoadBiomeAsset(envMats.TerrainLayers);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ── 1. Core & World Managers ──
            new GameObject("GameManager").AddComponent<GameManager>();

            var inputGO = new GameObject("InputManager");
            var inputMgr = inputGO.AddComponent<InputManager>();
            SetObjectRef(inputMgr, "_inputAsset", inputAsset);

            new GameObject("AudioManager").AddComponent<AudioManager>();
            new GameObject("SaveSystem").AddComponent<SaveSystem>();
            new GameObject("SceneLoader").AddComponent<SceneLoader>();
            new GameObject("DialogueManager").AddComponent<DialogueManager>();
            new GameObject("EconomySystem").AddComponent<EconomySystem>();

            var questGO = new GameObject("QuestManager");
            var questMgr = questGO.AddComponent<QuestManager>();
            SetObjectArrayRef(questMgr, "_allQuests", new Object[] { questFirstEcho });

            // ── 2. Lighting & Time/Weather (Day/Night Cycle, Moon & Soft Shadows) ──
            var lightGO = new GameObject("Directional Light (Sun)");
            var sunLight = lightGO.AddComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.intensity = 1.25f;
            sunLight.color = new Color(1f, 0.96f, 0.88f);
            sunLight.shadows = LightShadows.Soft;
            lightGO.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            RenderSettings.sun = sunLight;
            var skyMat = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
            if (skyMat != null) RenderSettings.skybox = skyMat;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.004f;
            RenderSettings.fogColor = new Color(0.68f, 0.82f, 0.90f);

            var moonGO = new GameObject("Directional Light (Moon)");
            var moonLight = moonGO.AddComponent<Light>();
            moonLight.type = LightType.Directional;
            moonLight.intensity = 0.35f;
            moonLight.color = new Color(0.65f, 0.78f, 1.0f);
            moonLight.shadows = LightShadows.Soft;
            moonGO.transform.rotation = Quaternion.Euler(228f, -35f, 0f);

            var timeGO = new GameObject("TimeSystem");
            var timeSys = timeGO.AddComponent<TimeSystem>();
            timeSys.ConfigureLightingDefaults(sunLight, moonLight);

            var weatherGO = new GameObject("WeatherSystem");
            var weatherSys = weatherGO.AddComponent<WeatherSystem>();
            SetObjectRef(weatherSys, "_sunLight", sunLight);

            new GameObject("WorldEventSystem").AddComponent<WorldEventSystem>();

            // ── 3. Environment: Verda Wilds Terrain & Solenne Watchtower Ruins ──
            var envRoot = new GameObject("Environment_VerdaWilds").transform;
            BuildRichEnvironment(envRoot, envMats, matGrass, matGrassTuft, matEarth, matRoad, matRock, matStone, matMountain, matTreeTrunk, matTreeLeaves);

            // ── 4. Solenne Echo Crystals (Collectibles / Lore Nodes) ──
            CreateEchoCrystal("EchoCrystal_West", envRoot, new Vector3(-4.5f, 1.6f, 15f), matRock, matCrystal,
                new[]
                {
                    "[ECHO RECORD #012 - VERDA WATCHTOWER]: The biological guardians have overgrown their neural limiters.",
                    "[ECHO RECORD #012]: Anyone reading this—do not let the Void Seekers reach the Central Spire!"
                });

            CreateEchoCrystal("EchoCrystal_East", envRoot, new Vector3(4.5f, 1.6f, 18f), matRock, matCrystal,
                new[]
                {
                    "[ECHO RECORD #019 - ARCHIVIST SOLENNE]: We built the Unraveling protocol as a shield, not a weapon.",
                    "[ECHO RECORD #019]: Seek the three Resonance Cores if you wish to restore Aethara."
                });

            // ── 5. POI Manager with Watchtower Ruin POI ──
            var poiGO = new GameObject("POIManager");
            var poiMgr = poiGO.AddComponent<POIManager>();
            ConfigureWatchtowerPOI(poiMgr);

            // ── 6. NPC: Elder Maren (Quest Giver) ──
            CreateElderMarenNPC(new Vector3(-2.5f, 0.05f, 5.5f), controller, matNpc);

            // ── 7. Enemies: 2 Overgrown Guardians with Patrol Waypoints ──
            CreateGuardianEnemy("OvergrownGuardian_01", new Vector3(-3.5f, 0.55f, 19f),
                new[] { new Vector3(-5f, 0.55f, 19f), new Vector3(0f, 0.55f, 19f) },
                controller, matEnemy, envRoot);

            CreateGuardianEnemy("OvergrownGuardian_02", new Vector3(5.5f, 0.55f, 14f),
                new[] { new Vector3(5.5f, 0.55f, 12f), new Vector3(5.5f, 0.55f, 19f) },
                controller, matEnemy, envRoot);

            // ── 8. Player (Kael) ──
            var player = new GameObject("Player") { tag = "Player" };
            player.transform.position = new Vector3(0f, 0.05f, 0f);

            var model = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            model.name = "Model";
            model.transform.SetParent(player.transform, false);
            model.transform.localPosition = new Vector3(0f, 1f, 0f);
            model.GetComponent<Renderer>().sharedMaterial = matPlayer;
            Object.DestroyImmediate(model.GetComponent<Collider>());
            model.SetActive(false); // [Tập trung map] Tạm ẩn model nhân vật để quan sát cảnh quan thoáng đãng

            var rb = player.AddComponent<Rigidbody>();
            rb.mass = 1f;
            var capsule = player.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 1f, 0f);
            capsule.height = 2f;
            capsule.radius = 0.5f;

            var animator = player.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;

            player.AddComponent<PlayerStats>();
            var pc = player.AddComponent<PlayerController>();
            SetLayerMask(pc, "_groundLayer", -1);
            SetLayerMask(pc, "_interactLayer", -1);

            var interaction = player.AddComponent<InteractionSystem>();
            SetLayerMask(interaction, "_interactLayerMask", -1);

            player.AddComponent<InventoryManager>();
            player.AddComponent<ProgressionSystem>();

            var combat = player.AddComponent<CombatManager>();
            combat.enabled = false; // [Tập trung map] Tạm tắt combat để tập trung khám phá thế giới
            SetLayerMask(combat, "_hitLayerMask", -1);

            // ── 9. Main Camera & Orbit Follow ──
            var camGO = new GameObject("Main Camera") { tag = "MainCamera" };
            camGO.AddComponent<Camera>();
            camGO.AddComponent<AudioListener>();
            var follow = camGO.AddComponent<ThirdPersonCamera>();
            SetObjectRef(follow, "_target", player.transform);
            SetObjectRef(pc, "_cameraTransform", camGO.transform);

            // ── 10. Gameplay Systems: Wayshrine, Drone, Placeholders, Triggers & First Person ──
            GameplaySystemsBuilder.BuildAllSystems(envRoot, player, camGO.GetComponent<Camera>(), follow);

            // ── 11. Complete 4-Stage HUD Canvas & Camera Presets ──
            UICanvasCameraBuilder.BuildCompleteUIAndCamera();

            // ── Save Scene & Version Stamp ──
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            File.WriteAllText(StampPath, BuilderVersion);
            AssetDatabase.SaveAssets();
            CoreSystemsVerifier.RunAllChecks();

            Debug.Log("[SandboxSceneBuilder] Built Verda Wilds slice in " + ScenePath +
                      "! Press Play: Talk to Elder Maren [E], Examine Echo Crystals [E], Attack Guardians [LMB/RMB], Quick Save [F5].");
            if (!silent) EditorUtility.DisplayDialog("Verda Wilds Sandbox", "Scene built: " + ScenePath, "OK");
        }

        // ── World & Character Helpers ─────────────────────────

        private static void BuildRichEnvironment(
            Transform envRoot,
            EnvironmentMaterialSet envMats,
            Material matGrass, Material matGrassTuft, Material matEarth,
            Material matRoad, Material matRock, Material matStone,
            Material matMountain, Material matTreeTrunk, Material matTreeLeaves)
        {
            // 0. UNITY TERRAIN (Sculpted Heightmap + 5 Splat Layers + TreeInstances + 3D Grass Details)
            TerrainWorldBuilder.BuildCompleteTerrain(envRoot, envMats);

            // 1. KHỐI ĐẤT & THẢM CỎ (Scenic Earthen Overlooks with Grass Caps)
            var earthGroup = new GameObject("01_Earth_And_Grass_Blocks").transform;
            earthGroup.SetParent(envRoot, false);

            CreateTerracedEarthBlock("EarthBlock_WestOverlook", earthGroup, new Vector3(-22f, 1.3f, 14f), new Vector3(9f, 2.6f, 16f), matEarth, matGrass);
            CreateTerracedEarthBlock("EarthBlock_EastOverlook", earthGroup, new Vector3(22f, 1.4f, 14f), new Vector3(9f, 2.8f, 16f), matEarth, matGrass);

            // 2. CẦU ĐÁ CỔ TRÊN ĐƯỜNG MÒN TERRAIN (Stone Bridge & Curbs along Painted Terrain Road)
            var roadGroup = new GameObject("02_Road_And_StoneBridge").transform;
            roadGroup.SetParent(envRoot, false);

            CreateBox("Bridge_CobbleDeck", roadGroup, new Vector3(0f, 0.04f, -3f), new Vector3(4.2f, 0.08f, 5.6f), matRoad);
            CreateBox("Bridge_Parapet_W", roadGroup, new Vector3(-2.3f, 0.45f, -3f), new Vector3(0.45f, 0.8f, 5.5f), matStone);
            CreateBox("Bridge_Parapet_E", roadGroup, new Vector3(2.3f, 0.45f, -3f), new Vector3(0.45f, 0.8f, 5.5f), matStone);
            CreateBox("Bridge_Post_SW", roadGroup, new Vector3(-2.3f, 0.75f, -5.8f), new Vector3(0.7f, 1.4f, 0.7f), matRock);
            CreateBox("Bridge_Post_SE", roadGroup, new Vector3(2.3f, 0.75f, -5.8f), new Vector3(0.7f, 1.4f, 0.7f), matRock);
            CreateBox("Bridge_Post_NW", roadGroup, new Vector3(-2.3f, 0.75f, -0.2f), new Vector3(0.7f, 1.4f, 0.7f), matRock);
            CreateBox("Bridge_Post_NE", roadGroup, new Vector3(2.3f, 0.75f, -0.2f), new Vector3(0.7f, 1.4f, 0.7f), matRock);

            // 3. VÁCH NÚI ĐÁ (Alpine Cliff Outcrops accenting the Sculpted Terrain Mountains)
            var mountainGroup = new GameObject("03_Mountains_And_Cliffs").transform;
            mountainGroup.SetParent(envRoot, false);

            CreateMountainPeak("CliffOutcrop_NorthWest", mountainGroup, new Vector3(-42f, 6f, 52f), new Vector3(18f, 18f, 14f), matMountain);
            CreateMountainPeak("CliffOutcrop_NorthEast", mountainGroup, new Vector3(42f, 6f, 52f), new Vector3(18f, 19f, 14f), matMountain);

            // 4. ĐÁ TẢNG & PHẾ TÍCH CỔ SOLENNE (Mossy Boulders & Ruin Masonry)
            var rockGroup = new GameObject("04_Rocks_And_Ruins").transform;
            rockGroup.SetParent(envRoot, false);

            // Ruin Courtyard Platform & Steps
            CreateBox("Ruin_Platform", rockGroup, new Vector3(0f, 0.25f, 16f), new Vector3(16f, 0.5f, 14f), matStone);
            CreateBox("Ruin_Step1", rockGroup, new Vector3(0f, 0.12f, 8.5f), new Vector3(6f, 0.25f, 1.2f), matRock);
            CreateBox("Ruin_Step2", rockGroup, new Vector3(0f, 0.25f, 9.5f), new Vector3(6f, 0.5f, 1.2f), matRock);

            // Ancient Pillars & Archway
            CreateBox("Pillar_NW", rockGroup, new Vector3(-6.5f, 2.5f, 21f), new Vector3(1.2f, 4.5f, 1.2f), matStone);
            CreateBox("Pillar_NE", rockGroup, new Vector3(6.5f, 2.5f, 21f), new Vector3(1.2f, 4.5f, 1.2f), matStone);
            CreateBox("Pillar_SW", rockGroup, new Vector3(-6.5f, 2.0f, 11f), new Vector3(1.2f, 3.5f, 1.2f), matStone);
            CreateBox("Pillar_SE", rockGroup, new Vector3(6.5f, 2.0f, 11f), new Vector3(1.2f, 3.5f, 1.2f), matStone);
            CreateBox("Arch_Top", rockGroup, new Vector3(0f, 5.0f, 21f), new Vector3(14.5f, 0.9f, 1.4f), matStone);

            // Natural Mossy Rock Clusters scattered around the valley
            CreateRockCluster("RockCluster_01", rockGroup, new Vector3(-9.5f, 0f, 4.5f), 1.4f, matRock);
            CreateRockCluster("RockCluster_02", rockGroup, new Vector3(9.5f, 0f, 5.0f), 1.6f, matRock);
            CreateRockCluster("RockCluster_03", rockGroup, new Vector3(-11.5f, 0f, 15.5f), 1.8f, matRock);
            CreateRockCluster("RockCluster_04", rockGroup, new Vector3(11.5f, 0f, 16.5f), 1.7f, matRock);
            CreateRockCluster("RockCluster_05", rockGroup, new Vector3(-6.5f, 0f, -6.5f), 1.3f, matRock);
            CreateRockCluster("RockCluster_06", rockGroup, new Vector3(7.0f, 0f, -5.5f), 1.5f, matRock);

            // 5. CÂY RỪNG (Multi-Layered Trees with Bark & Canopy Foliage)
            var treeGroup = new GameObject("05_Trees_And_Canopies").transform;
            treeGroup.SetParent(envRoot, false);

            CreateStylizedTree("Tree_01", treeGroup, new Vector3(-11.5f, 0f, 6.5f), matTreeTrunk, matTreeLeaves);
            CreateStylizedTree("Tree_02", treeGroup, new Vector3(11.5f, 0f, 7.5f), matTreeTrunk, matTreeLeaves);
            CreateStylizedTree("Tree_03", treeGroup, new Vector3(-13.5f, 0f, 20f), matTreeTrunk, matTreeLeaves);
            CreateStylizedTree("Tree_04", treeGroup, new Vector3(13.5f, 0f, 21.5f), matTreeTrunk, matTreeLeaves);
            CreateStylizedTree("Tree_05", treeGroup, new Vector3(-8.5f, 0f, -8.5f), matTreeTrunk, matTreeLeaves);
            CreateStylizedTree("Tree_06", treeGroup, new Vector3(8.5f, 0f, -7.5f), matTreeTrunk, matTreeLeaves);
            CreateStylizedTree("Tree_07", treeGroup, new Vector3(-17f, 2.4f, 12f), matTreeTrunk, matTreeLeaves);
            CreateStylizedTree("Tree_08", treeGroup, new Vector3(17f, 2.8f, 14f), matTreeTrunk, matTreeLeaves);
            CreateStylizedTree("Tree_09", treeGroup, new Vector3(-7f, 3.6f, 30f), matTreeTrunk, matTreeLeaves);
            CreateStylizedTree("Tree_10", treeGroup, new Vector3(7f, 3.6f, 30f), matTreeTrunk, matTreeLeaves);

            // 6. BỤI CỎ 3D (3D Lush Grass Tufts & Shrubs)
            var grassGroup = new GameObject("06_Grass_Tufts_And_Shrubs").transform;
            grassGroup.SetParent(envRoot, false);

            Vector3[] tuftPositions =
            {
                new Vector3(-3.2f, 0f, 2.2f), new Vector3(3.2f, 0f, 2.6f),
                new Vector3(-4.0f, 0f, 7.2f), new Vector3(4.2f, 0f, 7.4f),
                new Vector3(-8.2f, 0f, 5.2f), new Vector3(8.2f, 0f, 5.8f),
                new Vector3(-9.8f, 0f, 12.0f), new Vector3(9.8f, 0f, 12.5f),
                new Vector3(-3.5f, 0f, -4.5f), new Vector3(3.6f, 0f, -4.2f),
                new Vector3(-5.2f, 0f, -9.0f), new Vector3(5.4f, 0f, -8.8f)
            };
            for (int i = 0; i < tuftPositions.Length; i++)
            {
                CreateGrassTuft($"GrassTuft_{i + 1:00}", grassGroup, tuftPositions[i], matGrassTuft);
            }

            // 7. RỘNG ĐƯỜNG MÒN: TƯỜNG ĐÁ & THẢM HOA DẠI SẶC SỠ (Rustic Dry-Stone Wall & 4-Color Wildflowers along curved road)
            CreateRoadsideWallAndFlowers(
                envRoot, matStone, matRock,
                matGrassTuft, envMats.FlowerStem, envMats.FlowerYellow, envMats.FlowerRed, envMats.FlowerBlue, envMats.FlowerPurple,
                matTreeTrunk, matTreeLeaves);

            // 8. Tự động nâng cấp & đồng bộ toàn bộ cây trong cảnh với Mesh Sồi uốn cong & tán lá dăm nhỏ mới
            TerrainWorldBuilder.UpgradeStandaloneSceneTrees(envMats);
        }

        private static void CreateTerracedEarthBlock(string name, Transform parent, Vector3 pos, Vector3 scale, Material earthMat, Material grassMat)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.position = pos;

            // Main earthen cliff body
            var earth = GameObject.CreatePrimitive(PrimitiveType.Cube);
            earth.name = "Earth_Body";
            earth.transform.SetParent(root, false);
            earth.transform.localPosition = Vector3.zero;
            earth.transform.localScale = scale;
            earth.GetComponent<Renderer>().sharedMaterial = earthMat;

            // Lush grass top cap overhanging slightly
            var grassTop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            grassTop.name = "Grass_TopCap";
            grassTop.transform.SetParent(root, false);
            grassTop.transform.localPosition = new Vector3(0f, scale.y * 0.5f + 0.12f, 0f);
            grassTop.transform.localScale = new Vector3(scale.x + 0.35f, 0.28f, scale.z + 0.35f);
            grassTop.GetComponent<Renderer>().sharedMaterial = grassMat;
        }

        private static void CreateMountainPeak(string name, Transform parent, Vector3 basePos, Vector3 size, Material mountainMat)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.position = basePos;

            // Base cliff block
            var lower = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lower.name = "Cliff_Base";
            lower.transform.SetParent(root, false);
            lower.transform.localPosition = new Vector3(0f, size.y * 0.28f, 0f);
            lower.transform.localScale = new Vector3(size.x, size.y * 0.56f, size.z);
            lower.transform.localRotation = Quaternion.Euler(0f, 8f, 0f);
            lower.GetComponent<Renderer>().sharedMaterial = mountainMat;

            // Mid crag
            var mid = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mid.name = "Cliff_MidCrag";
            mid.transform.SetParent(root, false);
            mid.transform.localPosition = new Vector3(size.x * 0.05f, size.y * 0.62f, -size.z * 0.04f);
            mid.transform.localScale = new Vector3(size.x * 0.72f, size.y * 0.48f, size.z * 0.74f);
            mid.transform.localRotation = Quaternion.Euler(4f, -12f, 3f);
            mid.GetComponent<Renderer>().sharedMaterial = mountainMat;

            // Ridge summit
            var peak = GameObject.CreatePrimitive(PrimitiveType.Cube);
            peak.name = "Cliff_Summit";
            peak.transform.SetParent(root, false);
            peak.transform.localPosition = new Vector3(-size.x * 0.04f, size.y * 0.88f, size.z * 0.03f);
            peak.transform.localScale = new Vector3(size.x * 0.44f, size.y * 0.36f, size.z * 0.46f);
            peak.transform.localRotation = Quaternion.Euler(-5f, 18f, -4f);
            peak.GetComponent<Renderer>().sharedMaterial = mountainMat;
        }

        private static void CreateRockCluster(string name, Transform parent, Vector3 pos, float scale, Material rockMat)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.position = pos;

            var mainRock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            mainRock.name = "Boulder_Main";
            mainRock.transform.SetParent(root, false);
            mainRock.transform.localPosition = new Vector3(0f, 0.45f * scale, 0f);
            mainRock.transform.localScale = new Vector3(1.5f * scale, 1.05f * scale, 1.35f * scale);
            mainRock.transform.localRotation = Quaternion.Euler(12f, 28f, -8f);
            mainRock.GetComponent<Renderer>().sharedMaterial = rockMat;

            var sideRock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sideRock.name = "Boulder_Slab";
            sideRock.transform.SetParent(root, false);
            sideRock.transform.localPosition = new Vector3(0.75f * scale, 0.30f * scale, 0.35f * scale);
            sideRock.transform.localScale = new Vector3(0.95f * scale, 0.65f * scale, 0.85f * scale);
            sideRock.transform.localRotation = Quaternion.Euler(-8f, -35f, 14f);
            sideRock.GetComponent<Renderer>().sharedMaterial = rockMat;
        }

        private static void CreateGrassTuft(string name, Transform parent, Vector3 pos, Material tuftMat, float scale = 1.0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * scale;

            var mf = go.AddComponent<MeshFilter>();
            string meshPath = "Assets/_Project/Data/Terrain/Mesh_GrassDetailCluster.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (mesh == null)
            {
                mesh = TerrainWorldBuilder.BuildCrossedGrassMesh();
            }
            mf.sharedMesh = mesh;

            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = tuftMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static void CreateBox(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        // 2. Tạo chùm cành lá từ các tấm Quad đan chéo (thay cho PrimitiveType.Sphere)
        private static void CreateLeafCluster(Transform parent, Vector3 position, float scale, Material leafMat)
        {
            var cluster = new GameObject("LeafCluster");
            cluster.transform.SetParent(parent, false);
            cluster.transform.localPosition = position;

            // Tạo 6 mặt phẳng lá xoay các góc khác nhau tạo độ phồng 3D tự nhiên
            for (int i = 0; i < 6; i++)
            {
                var card = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(card.GetComponent<Collider>());
                card.name = $"LeafCard_{i}";
                card.transform.SetParent(cluster.transform, false);
                card.transform.localPosition = Random.insideUnitSphere * (scale * 0.2f);
                card.transform.localRotation = Quaternion.Euler(
                    Random.Range(-55f, 55f),
                    i * 60f + Random.Range(-15f, 15f),
                    Random.Range(-35f, 35f)
                );
                card.transform.localScale = Vector3.one * scale * Random.Range(0.85f, 1.25f);
                card.GetComponent<Renderer>().sharedMaterial = leafMat;
            }
        }

        // 3. Hàm đệ quy tạo cây cao nhiều tầng nhánh và cành con
        private static void BuildBranch(Transform parent, Vector3 startPos, Quaternion rotation, float length, float radius, int depth, int maxDepth, Material barkMat, Material leafMat)
        {
            // Tạo đoạn thân/cành hiện tại
            var segment = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            segment.name = $"Branch_D{depth}";
            if (depth > 0) Object.DestroyImmediate(segment.GetComponent<Collider>()); // Giữ collider ở thân chính
            segment.transform.SetParent(parent, false);

            Vector3 dir = rotation * Vector3.up;
            segment.transform.localPosition = startPos + dir * (length * 0.5f);
            segment.transform.localRotation = rotation;
            segment.transform.localScale = new Vector3(radius * 2f, length * 0.5f, radius * 2f);
            segment.GetComponent<Renderer>().sharedMaterial = barkMat;

            Vector3 tipPos = startPos + dir * length;

            // Nếu đã đến nhánh ngoài cùng -> gắn chùm lá xum xuê
            if (depth >= maxDepth)
            {
                CreateLeafCluster(parent, tipPos, radius * 18f, leafMat);
                return;
            }

            // Nếu ở tầng cành trung gian cũng thêm một ít lá phụ tạo độ dầy tán
            if (depth >= maxDepth - 1)
            {
                CreateLeafCluster(parent, tipPos, radius * 14f, leafMat);
            }

            // Phân nhánh: Thân chính đi tiếp lên cao + đẻ ra 2 đến 4 nhánh con tỏa ra xung quanh
            int subBranches = (depth == 0) ? 3 : Random.Range(2, 4);
            for (int i = 0; i < subBranches; i++)
            {
                float yaw = (360f / subBranches) * i + Random.Range(-25f, 25f);
                float pitch = (i == 0 && depth < 2) ? Random.Range(8f, 18f) : Random.Range(28f, 52f); // 1 nhánh vươn cao, các nhánh khác tỏa rộng

                Quaternion nextRot = rotation * Quaternion.Euler(pitch, yaw, 0f);
                float nextLength = length * Random.Range(0.68f, 0.82f);
                float nextRadius = radius * 0.65f;

                BuildBranch(parent, tipPos, nextRot, nextLength, nextRadius, depth + 1, maxDepth, barkMat, leafMat);
            }
        }

        private static void CreateStylizedTree(string name, Transform parent, Vector3 pos, Material trunkMat, Material leafMat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;

            int seed = Mathf.Abs(name.GetHashCode() ^ Mathf.RoundToInt(pos.x * 73f + pos.z * 137f));
            string[] oakNames = { "Mesh_VerdaOakTree_A.asset", "Mesh_VerdaOakTree_B.asset", "Mesh_VerdaOakTree_C.asset" };
            string chosenMesh = oakNames[seed % oakNames.Length];
            if (name.ToLowerInvariant().Contains("pine")) chosenMesh = "Mesh_VerdaPineTree_A.asset";

            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>($"Assets/_Project/Data/Terrain/{chosenMesh}");
            if (mesh != null)
            {
                var mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterials = new[] { trunkMat, leafMat };

                float yaw = (seed % 360);
                float scale = 0.95f + (seed % 20) * 0.015f;
                go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                go.transform.localScale = Vector3.one * scale;

                var col = go.AddComponent<CapsuleCollider>();
                col.center = new Vector3(0f, 2.5f, 0f);
                col.height = 5f;
                col.radius = 0.45f;
            }
            else
            {
                // Fallback nếu chưa bake mesh asset
                Random.InitState(seed);
                BuildBranch(go.transform, Vector3.zero, Quaternion.identity, length: 3.8f, radius: 0.42f, depth: 0, maxDepth: 3, trunkMat, leafMat);
            }
        }

        private static void CreateRoadsideWallAndFlowers(
            Transform parent,
            Material stoneMat, Material rockMat,
            Material grassTuftMat, Material stemMat,
            Material flowerYellow, Material flowerRed, Material flowerBlue, Material flowerPurple,
            Material trunkMat, Material leafMat)
        {
            var wallGroup = new GameObject("07_Scenic_Roadside_Wall_And_Flowers").transform;
            wallGroup.SetParent(parent, false);

            var terrain = Terrain.activeTerrain;

            // Follow curved winding road from z = -105 to z = -6
            float stepZ = 3.2f;
            for (float z = -105f; z <= -6f; z += stepZ)
            {
                float cx0 = TerrainWorldBuilder.RoadCenterX(z);
                float cx1 = TerrainWorldBuilder.RoadCenterX(z + stepZ);
                Vector3 p0 = new Vector3(cx0, 0f, z);
                Vector3 p1 = new Vector3(cx1, 0f, z + stepZ);
                Vector3 fwd = (p1 - p0).normalized;
                Vector3 left = new Vector3(-fwd.z, 0f, fwd.x);

                // Wall center on left shoulder (3.6m to left of road centerline)
                Vector3 wallPos = (p0 + p1) * 0.5f + left * 3.6f;
                float groundY = terrain != null ? terrain.SampleHeight(wallPos) : 0f;
                wallPos.y = groundY + 0.42f;

                Quaternion wallRot = Quaternion.LookRotation(fwd, Vector3.up);

                // 1. Dry-stone wall block (rustic fieldstone rock texture)
                var wallBlock = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wallBlock.name = $"StoneWall_{Mathf.RoundToInt(z)}";
                wallBlock.transform.SetParent(wallGroup, false);
                wallBlock.transform.position = wallPos;
                wallBlock.transform.rotation = wallRot;
                wallBlock.transform.localScale = new Vector3(0.58f, 0.85f, stepZ * 1.05f);
                wallBlock.GetComponent<Renderer>().sharedMaterial = rockMat;

                // Wall cap slab with slight rustic overhang
                var wallCap = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wallCap.name = $"WallCap_{Mathf.RoundToInt(z)}";
                wallCap.transform.SetParent(wallGroup, false);
                wallCap.transform.position = wallPos + new Vector3(0f, 0.45f, 0f);
                wallCap.transform.rotation = wallRot;
                wallCap.transform.localScale = new Vector3(0.72f, 0.14f, stepZ * 1.04f);
                wallCap.GetComponent<Renderer>().sharedMaterial = rockMat;

                // 2. Dense foreground wildflowers & tapered grass tufts hugging the stone wall
                int flowerTuftsPerSegment = 4;
                for (int f = 0; f < flowerTuftsPerSegment; f++)
                {
                    float t = (float)f / flowerTuftsPerSegment;
                    Vector3 segPos = Vector3.Lerp(p0, p1, t) + left * (3.6f - 0.70f - (f % 2) * 0.45f);
                    float fgY = terrain != null ? terrain.SampleHeight(segPos) : 0f;
                    segPos.y = fgY;

                    // Choose flower color: 60% Yellow (matching Slow Roads), 15% Red, 15% Blue, 10% Purple
                    float roll = Mathf.Repeat(Mathf.Sin(z * 13.7f + f * 41.2f) * 43758.54f, 1f);
                    Material flowerMat = flowerYellow;
                    if (roll > 0.60f && roll <= 0.75f) flowerMat = flowerRed;
                    else if (roll > 0.75f && roll <= 0.90f) flowerMat = flowerBlue;
                    else if (roll > 0.90f) flowerMat = flowerPurple;

                    CreateRoadsideFlowerCluster($"RoadsideFlower_{Mathf.RoundToInt(z)}_{f}", wallGroup, segPos, stemMat, flowerMat);
                    CreateGrassTuft($"RoadsideGrass_{Mathf.RoundToInt(z)}_{f}", wallGroup, segPos + left * 0.20f, grassTuftMat, 0.92f);
                }

                // Also place natural grass tufts along the right verge
                if (Mathf.RoundToInt(z) % 6 == 0)
                {
                    Vector3 rightVerge = (p0 + p1) * 0.5f - left * 3.2f;
                    if (terrain != null) rightVerge.y = terrain.SampleHeight(rightVerge);
                    CreateGrassTuft($"RightVergeGrass_{Mathf.RoundToInt(z)}", wallGroup, rightVerge, grassTuftMat, 1.05f);
                }
            }

            // 3. Majestic roadside branching trees along left verge framing the view
            float[] roadsideTreeZs = { -22f, -52f, -82f };
            for (int i = 0; i < roadsideTreeZs.Length; i++)
            {
                float tz = roadsideTreeZs[i];
                float tcx = TerrainWorldBuilder.RoadCenterX(tz);
                Vector3 treePos = new Vector3(tcx - 6.2f, 0f, tz);
                if (terrain != null) treePos.y = terrain.SampleHeight(treePos);
                CreateStylizedTree($"RoadsideTree_{i + 1}", wallGroup, treePos, trunkMat, leafMat);
            }
        }

        private static void CreateRoadsideFlowerCluster(
            string name, Transform parent, Vector3 pos, Material stemMat, Material blossomMat)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.position = pos;

            Vector3[] offsets =
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(-0.20f, 0f, 0.10f),
                new Vector3(0.22f, 0f, -0.08f),
                new Vector3(0.06f, 0f, 0.20f),
                new Vector3(-0.14f, 0f, -0.16f),
                new Vector3(0.18f, 0f, 0.14f)
            };

            for (int i = 0; i < offsets.Length; i++)
            {
                float h = 0.32f + (i * 0.035f);
                var stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                stem.name = $"Stem_{i}";
                stem.transform.SetParent(root, false);
                stem.transform.localPosition = offsets[i] + new Vector3(0f, h * 0.5f, 0f);
                stem.transform.localScale = new Vector3(0.038f, h * 0.5f, 0.038f);
                stem.GetComponent<Renderer>().sharedMaterial = stemMat;
                Object.DestroyImmediate(stem.GetComponent<Collider>());

                var blossom = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                blossom.name = $"Blossom_{i}";
                blossom.transform.SetParent(root, false);
                blossom.transform.localPosition = offsets[i] + new Vector3(0f, h + 0.06f, 0f);
                blossom.transform.localScale = new Vector3(0.28f, 0.15f, 0.28f);
                blossom.GetComponent<Renderer>().sharedMaterial = blossomMat;
                Object.DestroyImmediate(blossom.GetComponent<Collider>());
            }
        }

        private static void CreateEchoCrystal(string name, Transform parent, Vector3 pos, Material pedestalMat, Material crystalMat, string[] lines)
        {
            CreateBox(name + "_Pedestal", parent, new Vector3(pos.x, pos.y - 0.7f, pos.z), new Vector3(0.9f, 0.8f, 0.9f), pedestalMat);

            var crystal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crystal.name = name;
            crystal.transform.SetParent(parent, false);
            crystal.transform.position = pos;
            crystal.transform.rotation = Quaternion.Euler(35f, 45f, 35f);
            crystal.transform.localScale = new Vector3(0.55f, 0.95f, 0.55f);
            crystal.GetComponent<Renderer>().sharedMaterial = crystalMat;

            var echo = crystal.AddComponent<EchoCrystalInteractable>();
            var so = new SerializedObject(echo);
            so.FindProperty("_echoId").stringValue = "echo_crystal";
            so.FindProperty("_displayName").stringValue = "Solenne Echo Crystal";
            var linesProp = so.FindProperty("_memoryLines");
            linesProp.arraySize = lines.Length;
            for (int i = 0; i < lines.Length; i++)
                linesProp.GetArrayElementAtIndex(i).stringValue = lines[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateElderMarenNPC(Vector3 pos, AnimatorController controller, Material mat)
        {
            var npc = new GameObject("NPC_ElderMaren");
            npc.transform.position = pos;
            npc.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            var model = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            model.name = "Model";
            model.transform.SetParent(npc.transform, false);
            model.transform.localPosition = new Vector3(0f, 1f, 0f);
            model.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(model.GetComponent<Collider>());

            var col = npc.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 1f, 0f);
            col.height = 2f;
            col.radius = 0.5f;

            npc.AddComponent<NavMeshAgent>();
            var anim = npc.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;

            var ctrl = npc.AddComponent<NPCController>();
            var so = new SerializedObject(ctrl);
            so.FindProperty("_npcId").stringValue = "npc_elder_maren";
            so.FindProperty("_displayName").stringValue = "Elder Maren";
            so.FindProperty("_questToOfferId").stringValue = "quest_first_echo";
            so.ApplyModifiedPropertiesWithoutUndo();
            npc.SetActive(false); // [Tập trung map] Tạm ẩn NPC để tập trung vào cảnh quan map
        }

        private static void CreateGuardianEnemy(string name, Vector3 pos, Vector3[] waypoints, AnimatorController controller, Material mat, Transform envRoot)
        {
            var enemy = new GameObject(name);
            enemy.transform.position = pos;

            var model = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            model.name = "Model";
            model.transform.SetParent(enemy.transform, false);
            model.transform.localPosition = new Vector3(0f, 1.05f, 0f);
            model.transform.localScale = new Vector3(1.15f, 1.1f, 1.15f);
            model.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(model.GetComponent<Collider>());

            var col = enemy.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 1.05f, 0f);
            col.height = 2.1f;
            col.radius = 0.55f;

            enemy.AddComponent<NavMeshAgent>();
            var anim = enemy.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;

            var eb = enemy.AddComponent<EnemyBase>();
            enemy.AddComponent<WorldHealthBar>();

            // Create waypoint transforms
            var wpTransforms = new Object[waypoints.Length];
            for (int i = 0; i < waypoints.Length; i++)
            {
                var wp = new GameObject($"{name}_WP{i}");
                wp.transform.SetParent(envRoot, false);
                wp.transform.position = waypoints[i];
                wpTransforms[i] = wp.transform;
            }

            var so = new SerializedObject(eb);
            so.FindProperty("_enemyId").stringValue = "enemy_guardian";
            so.FindProperty("_displayName").stringValue = "Overgrown Guardian";
            var pts = so.FindProperty("_patrolPoints");
            pts.arraySize = wpTransforms.Length;
            for (int i = 0; i < wpTransforms.Length; i++)
                pts.GetArrayElementAtIndex(i).objectReferenceValue = wpTransforms[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            enemy.SetActive(false); // [Tập trung map] Tạm ẩn kẻ địch để tập trung vào cảnh quan map
        }

        private static void ConfigureWatchtowerPOI(POIManager poiMgr)
        {
            var so = new SerializedObject(poiMgr);
            var arr = so.FindProperty("_allPOIs");
            arr.arraySize = 1;
            var elem = arr.GetArrayElementAtIndex(0);
            elem.FindPropertyRelative("poiId").stringValue = "poi_solenne_watchtower";
            elem.FindPropertyRelative("displayName").stringValue = "Solenne Watchtower Ruins";
            elem.FindPropertyRelative("description").stringValue = "An ancient Solenne outpost humming with Echo Crystals.";
            elem.FindPropertyRelative("worldPosition").vector3Value = new Vector3(0f, 0.5f, 16f);
            elem.FindPropertyRelative("recommendedLevel").intValue = 1;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── HUD Builder ───────────────────────────────────────

        private static void BuildHUDCanvas()
        {
            var canvasGO = new GameObject("HUD_Canvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGO.AddComponent<GraphicRaycaster>();

            var hud = canvasGO.AddComponent<HUDController>();
            canvasGO.AddComponent<UIManager>();

            // 1. Top-Left Status Panel
            var statusPanel = CreateUIPanel("StatusPanel", canvasGO.transform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -20f), new Vector2(360f, 130f),
                new Color(0.06f, 0.08f, 0.12f, 0.82f));

            var hpBg = CreateUIPanel("HP_BG", statusPanel,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -16f), new Vector2(328f, 28f),
                new Color(0.2f, 0.08f, 0.08f, 0.9f));
            var hpFill = CreateUIFillBar("HP_Fill", hpBg, new Color(0.88f, 0.22f, 0.24f, 1f));
            var hpText = CreateUIText("HP_Text", hpBg, "HP 100 / 100", 16, TextAlignmentOptions.Center, Color.white);

            var stBg = CreateUIPanel("Stamina_BG", statusPanel,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -52f), new Vector2(328f, 18f),
                new Color(0.08f, 0.18f, 0.10f, 0.9f));
            var stFill = CreateUIFillBar("Stamina_Fill", stBg, new Color(0.25f, 0.82f, 0.42f, 1f));

            var lvGoldText = CreateUIText("LevelGold_Text", statusPanel, "LV 1 (XP 0)   |   Gold: 0", 17,
                TextAlignmentOptions.Left, new Color(1f, 0.92f, 0.60f));
            SetRect(lvGoldText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -82f), new Vector2(328f, 34f));

            // 2. Top-Right World & Quest Tracker Panel
            var rightPanel = CreateUIPanel("QuestTrackerPanel", canvasGO.transform,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -20f), new Vector2(400f, 210f),
                new Color(0.06f, 0.08f, 0.12f, 0.82f));
            rightPanel.pivot = new Vector2(1f, 1f);

            var timeText = CreateUIText("TimeWeather_Text", rightPanel, "08:00 AM  |  Clear\nVerda Wilds", 16,
                TextAlignmentOptions.TopRight, new Color(0.75f, 0.92f, 1f));
            SetRect(timeText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -12f), new Vector2(-16f, 48f));

            var questText = CreateUIText("QuestTracker_Text", rightPanel, "Objective:\nSpeak with Elder Maren", 16,
                TextAlignmentOptions.TopLeft, Color.white);
            SetRect(questText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -68f), new Vector2(-16f, 130f));

            // 3. Bottom-Center Interaction Prompt
            var promptPanel = CreateUIPanel("InteractPrompt", canvasGO.transform,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 195f), new Vector2(420f, 48f),
                new Color(0.05f, 0.08f, 0.12f, 0.88f));
            promptPanel.pivot = new Vector2(0.5f, 0.5f);
            var promptText = CreateUIText("Prompt_Text", promptPanel, "[E] Interact", 20,
                TextAlignmentOptions.Center, new Color(0.45f, 0.95f, 1f));

            // 4. Bottom Dialogue Panel
            var dlgPanel = CreateUIPanel("DialoguePanel", canvasGO.transform,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(980f, 150f),
                new Color(0.04f, 0.06f, 0.10f, 0.94f));
            dlgPanel.pivot = new Vector2(0.5f, 0f);

            var speakerText = CreateUIText("Speaker_Text", dlgPanel, "Elder Maren", 22,
                TextAlignmentOptions.TopLeft, new Color(1f, 0.85f, 0.35f));
            SetRect(speakerText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -14f), new Vector2(-24f, 32f));

            var bodyText = CreateUIText("Body_Text", dlgPanel, "...", 19,
                TextAlignmentOptions.TopLeft, Color.white);
            SetRect(bodyText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -50f), new Vector2(-24f, 88f));

            // 5. Top-Center Toast Notification
            var toastPanel = CreateUIPanel("ToastBanner", canvasGO.transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(680f, 52f),
                new Color(0.08f, 0.14f, 0.20f, 0.92f));
            toastPanel.pivot = new Vector2(0.5f, 1f);
            var toastText = CreateUIText("Toast_Text", toastPanel, "Notification", 19,
                TextAlignmentOptions.Center, new Color(0.95f, 0.95f, 0.70f));

            // 6. Bottom-Left Controls Legend
            var legendPanel = CreateUIPanel("ControlsLegend", canvasGO.transform,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(20f, 20f), new Vector2(440f, 54f),
                new Color(0.05f, 0.07f, 0.11f, 0.78f));
            legendPanel.pivot = new Vector2(0f, 0f);
            CreateUIText("Legend_Text", legendPanel,
                "[Tab] Inventory   [J] Quests   [K] Character   [Esc] Sandbox\n[E] Interact   [LMB/RMB] Attack   [F5/F9] Save/Load",
                14, TextAlignmentOptions.Center, new Color(0.78f, 0.88f, 0.96f));

            // Wire HUDController serialized fields
            var so = new SerializedObject(hud);
            so.FindProperty("_healthFill").objectReferenceValue = hpFill;
            so.FindProperty("_healthText").objectReferenceValue = hpText;
            so.FindProperty("_staminaFill").objectReferenceValue = stFill;
            so.FindProperty("_levelGoldText").objectReferenceValue = lvGoldText;
            so.FindProperty("_timeWeatherText").objectReferenceValue = timeText;
            so.FindProperty("_questTrackerText").objectReferenceValue = questText;
            so.FindProperty("_interactPromptRoot").objectReferenceValue = promptPanel.gameObject;
            so.FindProperty("_interactPromptText").objectReferenceValue = promptText;
            so.FindProperty("_dialoguePanelRoot").objectReferenceValue = dlgPanel.gameObject;
            so.FindProperty("_dialogueSpeakerText").objectReferenceValue = speakerText;
            so.FindProperty("_dialogueBodyText").objectReferenceValue = bodyText;
            so.FindProperty("_toastRoot").objectReferenceValue = toastPanel.gameObject;
            so.FindProperty("_toastText").objectReferenceValue = toastText;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static RectTransform CreateUIPanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta, Color bgColor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            var img = go.AddComponent<Image>();
            img.color = bgColor;
            return rt;
        }

        private static Image CreateUIFillBar(string name, RectTransform parent, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = color;
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillAmount = 1f;
            return img;
        }

        private static TextMeshProUGUI CreateUIText(string name, RectTransform parent, string text, float fontSize, TextAlignmentOptions align, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = align;
            tmp.color = color;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            return tmp;
        }

        private static void SetRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
        }

        // ── Asset Generators ──────────────────────────────────

        private static Material CreateOrLoadMaterial(string fileName, Color baseColor, Color? emission = null)
        {
            string path = Path.Combine(MatDir, fileName).Replace("\\", "/");
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
            if (mat.HasProperty("_Color")) mat.color = baseColor;
            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", emission.Value);
            }
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static QuestData CreateOrLoadQuestAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<QuestData>(QuestAssetPath);
            if (existing != null) return existing;

            Directory.CreateDirectory(Path.GetDirectoryName(QuestAssetPath));
            var q = ScriptableObject.CreateInstance<QuestData>();
            q.questId = "quest_first_echo";
            q.title = "Echoes of Verda Wilds";
            q.description = "Examine the ancient Solenne Echo Crystals in the Watchtower Ruins and defeat an Overgrown Guardian.";
            q.questType = QuestType.Main;
            q.isMainQuest = true;
            q.objectives = new[]
            {
                new QuestObjective
                {
                    objectiveId = "obj_examine_echoes",
                    description = "Examine Solenne Echo Crystals",
                    type = ObjectiveType.CollectItem,
                    targetId = "echo_crystal",
                    requiredCount = 2
                },
                new QuestObjective
                {
                    objectiveId = "obj_kill_guardian",
                    description = "Defeat an Overgrown Guardian",
                    type = ObjectiveType.KillEnemy,
                    targetId = "enemy_guardian",
                    requiredCount = 1
                }
            };
            q.reward = new QuestReward
            {
                experiencePoints = 150,
                goldAmount = 50f
            };
            AssetDatabase.CreateAsset(q, QuestAssetPath);
            return q;
        }

        private static BiomeData CreateOrLoadBiomeAsset(TerrainLayer[] terrainLayers = null)
        {
            var existing = AssetDatabase.LoadAssetAtPath<BiomeData>(BiomeAssetPath);
            if (existing != null)
            {
                if (terrainLayers != null && terrainLayers.Length > 0)
                {
                    existing.terrainLayers = terrainLayers;
                    EditorUtility.SetDirty(existing);
                }
                return existing;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(BiomeAssetPath));
            var b = ScriptableObject.CreateInstance<BiomeData>();
            b.biomeId = "biome_verda_wilds";
            b.biomeName = "Verda Wilds";
            b.description = "Lush emerald forests and ancient Solenne watchtower ruins.";
            b.fogColor = new Color(0.68f, 0.82f, 0.90f);
            b.fogDensity = 0.004f;
            b.terrainLayers = terrainLayers;
            AssetDatabase.CreateAsset(b, BiomeAssetPath);
            return b;
        }

        private static AnimatorController CreateOrLoadPlayerAnimator()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (existing != null) return existing;

            Directory.CreateDirectory(Path.GetDirectoryName(ControllerPath));
            var c = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            c.AddParameter("Speed", AnimatorControllerParameterType.Float);
            c.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
            c.AddParameter("IsCrouching", AnimatorControllerParameterType.Bool);
            c.AddParameter("IsSwimming", AnimatorControllerParameterType.Bool);
            c.AddParameter("IsBlocking", AnimatorControllerParameterType.Bool);
            c.AddParameter("ComboStep", AnimatorControllerParameterType.Int);
            c.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
            c.AddParameter("Roll", AnimatorControllerParameterType.Trigger);
            c.AddParameter("LightAttack", AnimatorControllerParameterType.Trigger);
            c.AddParameter("HeavyAttack", AnimatorControllerParameterType.Trigger);
            c.AddParameter("Parry", AnimatorControllerParameterType.Trigger);
            c.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            c.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            c.AddParameter("Die", AnimatorControllerParameterType.Trigger);

            c.layers[0].stateMachine.AddState("Idle");
            return c;
        }

        private static void SetObjectRef(Object target, string field, Object value)
        {
            if (target == null) return;
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) return;
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetObjectArrayRef(Object target, string field, Object[] values)
        {
            if (target == null) return;
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) return;
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetLayerMask(Object target, string field, int mask)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            var bits = prop?.FindPropertyRelative("m_Bits");
            if (bits == null) return;
            bits.intValue = mask;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
