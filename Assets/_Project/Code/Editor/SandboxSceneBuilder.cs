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
        private const string BuilderVersion = "v2_verda_wilds";
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

        private static void Build(bool silent)
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

            // ── Create Materials ──
            Directory.CreateDirectory(MatDir);
            var matGrass = CreateOrLoadMaterial("Mat_Grass.mat", new Color(0.24f, 0.48f, 0.26f));
            var matStone = CreateOrLoadMaterial("Mat_RuinStone.mat", new Color(0.52f, 0.54f, 0.50f));
            var matDarkStone = CreateOrLoadMaterial("Mat_RuinDark.mat", new Color(0.36f, 0.38f, 0.36f));
            var matCrystal = CreateOrLoadMaterial("Mat_EchoCrystal.mat", new Color(0.20f, 0.92f, 0.98f), emission: new Color(0.1f, 0.7f, 0.85f));
            var matPlayer = CreateOrLoadMaterial("Mat_Player.mat", new Color(0.20f, 0.55f, 0.90f));
            var matNpc = CreateOrLoadMaterial("Mat_ElderMaren.mat", new Color(0.90f, 0.72f, 0.22f));
            var matEnemy = CreateOrLoadMaterial("Mat_Guardian.mat", new Color(0.82f, 0.24f, 0.24f));
            var matTreeTrunk = CreateOrLoadMaterial("Mat_TreeTrunk.mat", new Color(0.42f, 0.28f, 0.16f));
            var matTreeLeaves = CreateOrLoadMaterial("Mat_TreeLeaves.mat", new Color(0.18f, 0.58f, 0.28f));

            // ── Create ScriptableObject Assets ──
            var questFirstEcho = CreateOrLoadQuestAsset();
            var biomeVerda = CreateOrLoadBiomeAsset();

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

            // ── 2. Lighting & Time/Weather ──
            var lightGO = new GameObject("Directional Light (Sun)");
            var sunLight = lightGO.AddComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.intensity = 1.25f;
            sunLight.color = new Color(1f, 0.96f, 0.88f);
            sunLight.shadows = LightShadows.Soft;
            lightGO.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            RenderSettings.sun = sunLight;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.004f;
            RenderSettings.fogColor = new Color(0.68f, 0.82f, 0.90f);

            var timeGO = new GameObject("TimeSystem");
            var timeSys = timeGO.AddComponent<TimeSystem>();
            SetObjectRef(timeSys, "_sunLight", sunLight);

            var weatherGO = new GameObject("WeatherSystem");
            var weatherSys = weatherGO.AddComponent<WeatherSystem>();
            SetObjectRef(weatherSys, "_sunLight", sunLight);

            new GameObject("WorldEventSystem").AddComponent<WorldEventSystem>();

            // ── 3. Environment: Verda Wilds & Solenne Watchtower Ruins ──
            var envRoot = new GameObject("Environment_VerdaWilds").transform;

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground_VerdaWilds";
            ground.transform.SetParent(envRoot, false);
            ground.transform.localScale = new Vector3(30f, 1f, 30f);
            ground.GetComponent<Renderer>().sharedMaterial = matGrass;

            // Ruin Courtyard Platform & Steps
            CreateBox("Ruin_Platform", envRoot, new Vector3(0f, 0.25f, 16f), new Vector3(16f, 0.5f, 14f), matStone);
            CreateBox("Ruin_Step1", envRoot, new Vector3(0f, 0.12f, 8.5f), new Vector3(6f, 0.25f, 1.2f), matDarkStone);
            CreateBox("Ruin_Step2", envRoot, new Vector3(0f, 0.25f, 9.5f), new Vector3(6f, 0.5f, 1.2f), matDarkStone);

            // Ancient Pillars & Archway
            CreateBox("Pillar_NW", envRoot, new Vector3(-6.5f, 2.5f, 21f), new Vector3(1.2f, 4.5f, 1.2f), matDarkStone);
            CreateBox("Pillar_NE", envRoot, new Vector3(6.5f, 2.5f, 21f), new Vector3(1.2f, 4.5f, 1.2f), matDarkStone);
            CreateBox("Pillar_SW", envRoot, new Vector3(-6.5f, 2.0f, 11f), new Vector3(1.2f, 3.5f, 1.2f), matDarkStone);
            CreateBox("Pillar_SE", envRoot, new Vector3(6.5f, 2.0f, 11f), new Vector3(1.2f, 3.5f, 1.2f), matDarkStone);
            CreateBox("Arch_Top", envRoot, new Vector3(0f, 5.0f, 21f), new Vector3(14.5f, 0.9f, 1.4f), matStone);

            // Stylized Trees around the clearing
            CreateStylizedTree("Tree_01", envRoot, new Vector3(-12f, 0f, 6f), matTreeTrunk, matTreeLeaves);
            CreateStylizedTree("Tree_02", envRoot, new Vector3(12f, 0f, 7f), matTreeTrunk, matTreeLeaves);
            CreateStylizedTree("Tree_03", envRoot, new Vector3(-14f, 0f, 20f), matTreeTrunk, matTreeLeaves);
            CreateStylizedTree("Tree_04", envRoot, new Vector3(14f, 0f, 22f), matTreeTrunk, matTreeLeaves);

            // ── 4. Solenne Echo Crystals (Collectibles / Lore Nodes) ──
            CreateEchoCrystal("EchoCrystal_West", envRoot, new Vector3(-4.5f, 1.6f, 15f), matDarkStone, matCrystal,
                new[]
                {
                    "[ECHO RECORD #012 - VERDA WATCHTOWER]: The biological guardians have overgrown their neural limiters.",
                    "[ECHO RECORD #012]: Anyone reading this—do not let the Void Seekers reach the Central Spire!"
                });

            CreateEchoCrystal("EchoCrystal_East", envRoot, new Vector3(4.5f, 1.6f, 18f), matDarkStone, matCrystal,
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
            SetLayerMask(combat, "_hitLayerMask", -1);

            // ── 9. Main Camera ──
            var camGO = new GameObject("Main Camera") { tag = "MainCamera" };
            camGO.AddComponent<Camera>();
            camGO.AddComponent<AudioListener>();
            var follow = camGO.AddComponent<ThirdPersonCamera>();
            SetObjectRef(follow, "_target", player.transform);
            camGO.transform.position = new Vector3(0f, 3f, -6f);
            SetObjectRef(pc, "_cameraTransform", camGO.transform);

            // ── 10. Runtime HUD Canvas ──
            BuildHUDCanvas();

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

        private static void CreateBox(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static void CreateStylizedTree(string name, Transform parent, Vector3 pos, Material trunkMat, Material leafMat)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.position = pos;

            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Trunk";
            trunk.transform.SetParent(root, false);
            trunk.transform.localPosition = new Vector3(0f, 1.8f, 0f);
            trunk.transform.localScale = new Vector3(0.6f, 1.8f, 0.6f);
            trunk.GetComponent<Renderer>().sharedMaterial = trunkMat;

            var crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            crown.name = "Canopy";
            crown.transform.SetParent(root, false);
            crown.transform.localPosition = new Vector3(0f, 4.2f, 0f);
            crown.transform.localScale = new Vector3(3.2f, 2.6f, 3.2f);
            crown.GetComponent<Renderer>().sharedMaterial = leafMat;
            Object.DestroyImmediate(crown.GetComponent<Collider>());
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

        private static BiomeData CreateOrLoadBiomeAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<BiomeData>(BiomeAssetPath);
            if (existing != null) return existing;

            Directory.CreateDirectory(Path.GetDirectoryName(BiomeAssetPath));
            var b = ScriptableObject.CreateInstance<BiomeData>();
            b.biomeId = "biome_verda_wilds";
            b.biomeName = "Verda Wilds";
            b.description = "Lush emerald forests and ancient Solenne watchtower ruins.";
            b.fogColor = new Color(0.68f, 0.82f, 0.90f);
            b.fogDensity = 0.004f;
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
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) return;
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectArrayRef(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) return;
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
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
