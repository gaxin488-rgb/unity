#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using EchoesOfTheLost.Characters.Player;
using EchoesOfTheLost.Systems.Combat;
using EchoesOfTheLost.World;

// ============================================================================
//  GameplaySystemsBuilder — Automated Builder for:
//    1. C# Scripting Core Pipeline (SolenneAetherShrine & AetherCompanionDrone)
//    2. First Person Controller, Placeholders, Triggers & Player Footstep Audio
//    3. Attacking, Sound Effects & Animation System (CombatManager + Target Dummy)
//    4. Day/Night Cycle Lighting, Moon & Soft Shadows System
// ============================================================================
namespace EchoesOfTheLost.EditorTools
{
    public static class GameplaySystemsBuilder
    {
        private const string MatDir = "Assets/_Project/Art/Materials";
        private const string SfxFootsteps = "Assets/_Project/Art/Audio/SFX/Footsteps";
        private const string SfxCombat = "Assets/_Project/Art/Audio/SFX/Combat";
        private const string SfxEnv = "Assets/_Project/Art/Audio/SFX/Environment";

        [MenuItem("EotL/Scripting & Systems/Build All Gameplay & Scripting Systems")]
        public static void BuildAllFromMenu()
        {
            var envRoot = GameObject.Find("Environment_VerdaWilds")?.transform;
            if (envRoot == null)
            {
                var envGO = new GameObject("Environment_VerdaWilds");
                envRoot = envGO.transform;
            }

            var player = GameObject.FindWithTag("Player");
            var mainCam = Camera.main;
            var tpCam = mainCam != null ? mainCam.GetComponent<ThirdPersonCamera>() : null;

            BuildAllSystems(envRoot, player, mainCam, tpCam);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            Debug.Log("[GameplaySystemsBuilder] All gameplay, audio, triggers, placeholders, and camera systems built!");
        }

        public static void BuildAllSystems(
            Transform envRoot,
            GameObject player,
            Camera mainCam,
            ThirdPersonCamera tpCam)
        {
            // 0. Ensure all procedural audio WAV files exist
            GameAudioGenerator.GenerateAllAudio();

            // Materials
            var matGold = CreateOrLoadMaterial("Mat_SolenneGold.mat", new Color(0.95f, 0.78f, 0.22f), new Color(0.35f, 0.25f, 0.05f));
            var matAetherCyan = CreateOrLoadMaterial("Mat_AetherCyan.mat", new Color(0.18f, 0.85f, 0.98f), new Color(0.35f, 0.88f, 1.0f));
            var matStone = CreateOrLoadMaterial("Mat_RuinStone.mat", new Color(0.45f, 0.44f, 0.42f));
            var matWood = CreateOrLoadMaterial("Mat_PrototypingWood.mat", new Color(0.52f, 0.36f, 0.22f));
            var matGreybox = CreateOrLoadMaterial("Mat_PrototypingGreybox.mat", new Color(0.68f, 0.68f, 0.72f));
            var matTriggerGate = CreateOrLoadMaterial("Mat_TriggerGate.mat", new Color(0.20f, 0.90f, 0.65f), new Color(0.25f, 0.95f, 0.70f));

            // 1. Solenne Aether Wayshrine & Companion Drone
            var wayshrine = BuildSolenneWayshrine(envRoot, matStone, matGold, matAetherCyan, player);
            var drone = BuildCompanionDrone(envRoot, player, wayshrine.gameObject, matGold, matAetherCyan);

            // Re-bind drone reference into shrine
            wayshrine.BindGameObjectVariables(
                wayshrine.FloatingCoreObject,
                wayshrine.InnerRingObject,
                wayshrine.OuterRingObject,
                wayshrine.EnergyBeamObject,
                wayshrine.transform.Find("Shockwave_Ring")?.gameObject,
                GameObject.Find("04_Rocks_And_Ruins") ?? wayshrine.gameObject,
                drone.gameObject,
                player,
                wayshrine.GetComponentInChildren<Light>());

            // 2. Placeholder Greybox Prototyping Blockouts
            BuildPlaceholderBlockouts(envRoot, matGreybox, matWood, matStone);

            // 3. Gameplay Trigger Volumes
            BuildGameplayTriggerVolumes(envRoot, wayshrine.gameObject, matTriggerGate, matAetherCyan);

            // 4. Configure Player (Combat Audio, Footsteps Audio, First Person Controller)
            if (player != null)
            {
                ConfigurePlayerSystems(player, mainCam, tpCam, matAetherCyan, matGold);
            }
        }

        // ── 1. Solenne Wayshrine & Companion Drone ──────────────────────────

        public static SolenneAetherShrine BuildSolenneWayshrine(
            Transform parent,
            Material stoneMat,
            Material goldMat,
            Material glowMat,
            GameObject playerTarget)
        {
            var existing = GameObject.Find("Solenne_AetherWayshrine");
            if (existing != null) Object.DestroyImmediate(existing);

            var shrineGO = new GameObject("Solenne_AetherWayshrine");
            shrineGO.transform.SetParent(parent, false);
            shrineGO.transform.position = new Vector3(0f, 0.05f, -3.2f);

            // Spire Base Pedestal
            var basePedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            basePedestal.name = "Spire_Pedestal";
            basePedestal.transform.SetParent(shrineGO.transform, false);
            basePedestal.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            basePedestal.transform.localScale = new Vector3(2.4f, 0.35f, 2.4f);
            basePedestal.GetComponent<Renderer>().sharedMaterial = stoneMat;

            // 3 Surrounding Runestone Pillars
            for (int i = 0; i < 3; i++)
            {
                float ang = i * 120f * Mathf.Deg2Rad;
                var pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pillar.name = $"Pillar_{i + 1}";
                pillar.transform.SetParent(shrineGO.transform, false);
                pillar.transform.localPosition = new Vector3(Mathf.Cos(ang) * 1.55f, 1.6f, Mathf.Sin(ang) * 1.55f);
                pillar.transform.localScale = new Vector3(0.42f, 2.8f, 0.42f);
                pillar.transform.localRotation = Quaternion.Euler(0f, -i * 120f, 8f);
                pillar.GetComponent<Renderer>().sharedMaterial = goldMat;
            }

            // Floating Levitating Core (Stage 6 GameObject variable)
            var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.name = "Core_FloatingSphere";
            core.transform.SetParent(shrineGO.transform, false);
            core.transform.localPosition = new Vector3(0f, 2.05f, 0f);
            core.transform.localScale = Vector3.one * 0.72f;
            core.GetComponent<Renderer>().sharedMaterial = glowMat;
            Object.DestroyImmediate(core.GetComponent<Collider>());

            // Inner Rotating Gyroscope Ring (Stage 6 GameObject variable)
            var innerRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            innerRing.name = "Inner_GyroRing";
            innerRing.transform.SetParent(shrineGO.transform, false);
            innerRing.transform.localPosition = new Vector3(0f, 2.05f, 0f);
            innerRing.transform.localScale = new Vector3(1.35f, 0.04f, 1.35f);
            innerRing.transform.localRotation = Quaternion.Euler(35f, 0f, 25f);
            innerRing.GetComponent<Renderer>().sharedMaterial = goldMat;
            Object.DestroyImmediate(innerRing.GetComponent<Collider>());

            // Outer Counter-Rotating Runic Ring (Stage 6 GameObject variable)
            var outerRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            outerRing.name = "Outer_RunicRing";
            outerRing.transform.SetParent(shrineGO.transform, false);
            outerRing.transform.localPosition = new Vector3(0f, 2.05f, 0f);
            outerRing.transform.localScale = new Vector3(1.95f, 0.04f, 1.95f);
            outerRing.transform.localRotation = Quaternion.Euler(-25f, 45f, -15f);
            outerRing.GetComponent<Renderer>().sharedMaterial = glowMat;
            Object.DestroyImmediate(outerRing.GetComponent<Collider>());

            // High-Energy Aether Conduit Beam (Stage 6 GameObject variable)
            var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beam.name = "EnergyConduit_Beam";
            beam.transform.SetParent(shrineGO.transform, false);
            beam.transform.localPosition = new Vector3(0f, 2.05f, 8f);
            beam.transform.localScale = new Vector3(0.16f, 8f, 0.16f);
            beam.GetComponent<Renderer>().sharedMaterial = glowMat;
            beam.SetActive(false);
            Object.DestroyImmediate(beam.GetComponent<Collider>());

            // Expanding Shockwave Ring (Stage 6 GameObject variable)
            var shockwave = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shockwave.name = "Shockwave_Ring";
            shockwave.transform.SetParent(shrineGO.transform, false);
            shockwave.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            shockwave.transform.localScale = new Vector3(1.2f, 0.04f, 1.2f);
            shockwave.GetComponent<Renderer>().sharedMaterial = glowMat;
            shockwave.SetActive(false);
            Object.DestroyImmediate(shockwave.GetComponent<Collider>());

            // Point Light
            var lightGO = new GameObject("Wayshrine_PointLight");
            lightGO.transform.SetParent(shrineGO.transform, false);
            lightGO.transform.localPosition = new Vector3(0f, 2.3f, 0f);
            var pLight = lightGO.AddComponent<Light>();
            pLight.type = LightType.Point;
            pLight.color = new Color(0.20f, 0.85f, 0.98f);
            pLight.intensity = 2.4f;
            pLight.range = 10f;
            pLight.shadows = LightShadows.Soft;

            // Trigger / Interaction Collider
            var col = shrineGO.AddComponent<SphereCollider>();
            col.center = new Vector3(0f, 1.2f, 0f);
            col.radius = 2.8f;
            col.isTrigger = true;

            var shrine = shrineGO.AddComponent<SolenneAetherShrine>();
            shrine.BindGameObjectVariables(
                core,
                innerRing,
                outerRing,
                beam,
                shockwave,
                GameObject.Find("04_Rocks_And_Ruins") ?? shrineGO,
                null,
                playerTarget,
                pLight);

            return shrine;
        }

        public static AetherCompanionDrone BuildCompanionDrone(
            Transform parent,
            GameObject playerTarget,
            GameObject homeShrine,
            Material goldMat,
            Material glowMat)
        {
            var existing = GameObject.Find("Solenne_CompanionDrone");
            if (existing != null) Object.DestroyImmediate(existing);

            var droneGO = new GameObject("Solenne_CompanionDrone");
            droneGO.transform.SetParent(parent, false);
            droneGO.transform.position = new Vector3(1.2f, 2.1f, -2.2f);

            // Drone Chassis Core Sphere
            var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.name = "Drone_CoreEye";
            core.transform.SetParent(droneGO.transform, false);
            core.transform.localPosition = Vector3.zero;
            core.transform.localScale = Vector3.one * 0.38f;
            core.GetComponent<Renderer>().sharedMaterial = glowMat;
            Object.DestroyImmediate(core.GetComponent<Collider>());

            // Outer Gyroscope Stabilizer Ring
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Drone_StabilizerRing";
            ring.transform.SetParent(droneGO.transform, false);
            ring.transform.localPosition = Vector3.zero;
            ring.transform.localScale = new Vector3(0.68f, 0.025f, 0.68f);
            ring.transform.localRotation = Quaternion.Euler(25f, 0f, 35f);
            ring.GetComponent<Renderer>().sharedMaterial = goldMat;
            Object.DestroyImmediate(ring.GetComponent<Collider>());

            // Downward Scan Cone
            var scanCone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            scanCone.name = "Drone_ScanCone";
            scanCone.transform.SetParent(droneGO.transform, false);
            scanCone.transform.localPosition = new Vector3(0f, -0.65f, 0.45f);
            scanCone.transform.localScale = new Vector3(0.48f, 0.65f, 0.48f);
            scanCone.transform.localRotation = Quaternion.Euler(30f, 0f, 0f);
            scanCone.GetComponent<Renderer>().sharedMaterial = glowMat;
            scanCone.SetActive(false);
            Object.DestroyImmediate(scanCone.GetComponent<Collider>());

            // Point Light
            var lightGO = new GameObject("Drone_Light");
            lightGO.transform.SetParent(droneGO.transform, false);
            var dl = lightGO.AddComponent<Light>();
            dl.type = LightType.Point;
            dl.color = new Color(0.25f, 0.95f, 1.0f);
            dl.intensity = 1.4f;
            dl.range = 5.5f;

            var drone = droneGO.AddComponent<AetherCompanionDrone>();
            drone.BindGameObjectVariables(playerTarget, homeShrine, core, ring, scanCone);
            drone.SnapToOrbitPose(45f, enableScanCone: false);

            return drone;
        }

        // ── 2. Placeholder Blockouts & Combat Target Dummy ──────────────────

        public static void BuildPlaceholderBlockouts(
            Transform parent,
            Material greyboxMat,
            Material woodMat,
            Material stoneMat)
        {
            var existing = GameObject.Find("Prototyping_Placeholders");
            if (existing != null) Object.DestroyImmediate(existing);

            var root = new GameObject("Prototyping_Placeholders").transform;
            root.SetParent(parent, false);

            // 1. Traversal Ramp to West Overlook (Jump & slope testing)
            var ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.name = "Blockout_TraversalRamp";
            ramp.transform.SetParent(root, false);
            ramp.transform.position = new Vector3(-16f, 0.65f, 8f);
            ramp.transform.rotation = Quaternion.Euler(-11f, 35f, 0f);
            ramp.transform.localScale = new Vector3(3.2f, 0.35f, 7.5f);
            ramp.GetComponent<Renderer>().sharedMaterial = greyboxMat;
            var rampMarker = ramp.AddComponent<PlaceholderObjectMarker>();
            rampMarker.ConfigurePlaceholder("RAMP_WEST_01", PlaceholderArchetype.TraversalRamp, ramp.transform.localScale, "Earth", Color.yellow);

            // 2. Stepping Pillars (Acrobatic jump arcs testing)
            Vector3[] pillarOffsets = { new Vector3(-8.5f, 0.4f, 10f), new Vector3(-7.2f, 0.75f, 11.5f), new Vector3(-5.8f, 1.15f, 12.8f) };
            for (int i = 0; i < pillarOffsets.Length; i++)
            {
                var step = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                step.name = $"Blockout_SteppingPillar_{i + 1}";
                step.transform.SetParent(root, false);
                step.transform.position = pillarOffsets[i];
                step.transform.localScale = new Vector3(1.2f, pillarOffsets[i].y, 1.2f);
                step.GetComponent<Renderer>().sharedMaterial = stoneMat;
                var marker = step.AddComponent<PlaceholderObjectMarker>();
                marker.ConfigurePlaceholder($"STEP_PILLAR_{i + 1}", PlaceholderArchetype.SteppingPillar, step.transform.localScale, "Stone", Color.cyan);
            }

            // 3. Tactical Cover Wall (Combat barrier)
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Blockout_TacticalCoverWall";
            wall.transform.SetParent(root, false);
            wall.transform.position = new Vector3(3.8f, 0.55f, 14f);
            wall.transform.rotation = Quaternion.Euler(0f, -22f, 0f);
            wall.transform.localScale = new Vector3(3.4f, 1.1f, 0.45f);
            wall.GetComponent<Renderer>().sharedMaterial = stoneMat;
            var wallMarker = wall.AddComponent<PlaceholderObjectMarker>();
            wallMarker.ConfigurePlaceholder("COVER_WALL_01", PlaceholderArchetype.TacticalCoverWall, wall.transform.localScale, "Stone", Color.blue);
            wall.SetActive(false); // [Tập trung map] Tạm ẩn chướng ngại vật combat để tầm nhìn quang đãng

            // 4. Wooden Supply Crates (Loot & obstacle testing)
            var crateCluster = new GameObject("Blockout_SupplyCrates").transform;
            crateCluster.SetParent(root, false);
            crateCluster.position = new Vector3(-3.8f, 0.05f, 11.2f);

            var c1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            c1.name = "Crate_01";
            c1.transform.SetParent(crateCluster, false);
            c1.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            c1.transform.localScale = Vector3.one * 1.0f;
            c1.GetComponent<Renderer>().sharedMaterial = woodMat;
            c1.AddComponent<PlaceholderObjectMarker>().ConfigurePlaceholder("CRATE_01", PlaceholderArchetype.InteractableCrate, Vector3.one, "Wood", Color.green);

            var c2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            c2.name = "Crate_02";
            c2.transform.SetParent(crateCluster, false);
            c2.transform.localPosition = new Vector3(0.75f, 0.38f, 0.25f);
            c2.transform.localScale = Vector3.one * 0.76f;
            c2.GetComponent<Renderer>().sharedMaterial = woodMat;
            c2.AddComponent<PlaceholderObjectMarker>().ConfigurePlaceholder("CRATE_02", PlaceholderArchetype.InteractableCrate, Vector3.one * 0.76f, "Wood", Color.green);

            // 5. Sparring Combat Target Dummy (Testing Combo Attacks, Sound Effects & Wobble Animation)
            var dummyGO = new GameObject("Blockout_CombatTargetDummy");
            dummyGO.transform.SetParent(root, false);
            dummyGO.transform.position = new Vector3(1.6f, 0.05f, 13.5f);

            var dummyStand = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            dummyStand.name = "Dummy_BaseStand";
            dummyStand.transform.SetParent(dummyGO.transform, false);
            dummyStand.transform.localPosition = new Vector3(0f, 0.12f, 0f);
            dummyStand.transform.localScale = new Vector3(1.1f, 0.12f, 1.1f);
            dummyStand.GetComponent<Renderer>().sharedMaterial = stoneMat;

            var dummyBody = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            dummyBody.name = "Dummy_Torso";
            dummyBody.transform.SetParent(dummyGO.transform, false);
            dummyBody.transform.localPosition = new Vector3(0f, 1.15f, 0f);
            dummyBody.transform.localScale = new Vector3(0.55f, 0.85f, 0.55f);
            dummyBody.GetComponent<Renderer>().sharedMaterial = woodMat;
            Object.DestroyImmediate(dummyBody.GetComponent<Collider>());

            var dummyCrossbar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dummyCrossbar.name = "Dummy_Crossarms";
            dummyCrossbar.transform.SetParent(dummyBody.transform, false);
            dummyCrossbar.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            dummyCrossbar.transform.localScale = new Vector3(2.4f, 0.25f, 0.35f);
            dummyCrossbar.GetComponent<Renderer>().sharedMaterial = woodMat;
            Object.DestroyImmediate(dummyCrossbar.GetComponent<Collider>());

            var col = dummyGO.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 1.15f, 0f);
            col.height = 2.1f;
            col.radius = 0.65f;

            var impactSfx = AssetDatabase.LoadAssetAtPath<AudioClip>(Path.Combine(SfxCombat, "SFX_Hit_Impact.wav"));
            var dummyComp = dummyGO.AddComponent<CombatTargetDummy>();
            dummyComp.ConfigureDummy(dummyBody.transform, dummyBody.GetComponent<Renderer>(), impactSfx, new Color(0.52f, 0.36f, 0.22f));

            var dummyMarker = dummyGO.AddComponent<PlaceholderObjectMarker>();
            dummyMarker.ConfigurePlaceholder("TARGET_DUMMY_01", PlaceholderArchetype.CombatTargetDummy, new Vector3(1.2f, 2.1f, 1.2f), "Wood", Color.red);
            dummyGO.SetActive(false); // [Tập trung map] Tạm ẩn bia tập combat để quang đãng map
        }

        // ── 3. Gameplay Trigger Volumes ─────────────────────────────────────

        public static void BuildGameplayTriggerVolumes(
            Transform parent,
            GameObject wayshrineGO,
            Material gateMat,
            Material glowMat)
        {
            var existing = GameObject.Find("Gameplay_TriggerVolumes");
            if (existing != null) Object.DestroyImmediate(existing);

            var root = new GameObject("Gameplay_TriggerVolumes").transform;
            root.SetParent(parent, false);

            // 1. Aether Speed Boost Gate at the Stone Bridge Entrance
            var gateGO = new GameObject("Trigger_SpeedBoostGate");
            gateGO.transform.SetParent(root, false);
            gateGO.transform.position = new Vector3(0f, 0.05f, -6.5f);

            var gateRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            gateRing.name = "Gate_BoundaryRing";
            gateRing.transform.SetParent(gateGO.transform, false);
            gateRing.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            gateRing.transform.localScale = new Vector3(4.2f, 0.05f, 2.2f);
            gateRing.GetComponent<Renderer>().sharedMaterial = gateMat;
            Object.DestroyImmediate(gateRing.GetComponent<Collider>());

            var gateLightGO = new GameObject("Gate_Light");
            gateLightGO.transform.SetParent(gateGO.transform, false);
            gateLightGO.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            var gLight = gateLightGO.AddComponent<Light>();
            gLight.type = LightType.Point;
            gLight.color = new Color(0.20f, 0.90f, 0.65f);
            gLight.intensity = 1.8f;
            gLight.range = 6f;

            var gCol = gateGO.AddComponent<BoxCollider>();
            gCol.isTrigger = true;
            gCol.center = new Vector3(0f, 1.2f, 0f);
            gCol.size = new Vector3(4.5f, 2.4f, 2.4f);

            var gTrigger = gateGO.AddComponent<GameplayTriggerVolume>();
            gTrigger.ConfigureTrigger("trigger_bridge_gate", "Aether Speed Boost Gate", TriggerVolumeType.SpeedBoostGate, gateRing, wayshrineGO, gLight);

            // 2. Healing Sanctuary inside the Wayshrine Courtyard
            var healGO = new GameObject("Trigger_HealingSanctuary");
            healGO.transform.SetParent(root, false);
            healGO.transform.position = new Vector3(0f, 0.05f, -3.2f);

            var healRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            healRing.name = "Sanctuary_Ring";
            healRing.transform.SetParent(healGO.transform, false);
            healRing.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            healRing.transform.localScale = new Vector3(5.5f, 0.04f, 5.5f);
            healRing.GetComponent<Renderer>().sharedMaterial = glowMat;
            Object.DestroyImmediate(healRing.GetComponent<Collider>());

            var hCol = healGO.AddComponent<SphereCollider>();
            hCol.isTrigger = true;
            hCol.center = new Vector3(0f, 1.0f, 0f);
            hCol.radius = 2.8f;

            var hTrigger = healGO.AddComponent<GameplayTriggerVolume>();
            hTrigger.ConfigureTrigger("trigger_wayshrine_sanctuary", "Solenne Healing Sanctuary", TriggerVolumeType.HealingSanctuary, healRing, wayshrineGO, null);

            // 3. Ruins Ambush Alarm near the Watchtower Steps
            var alarmGO = new GameObject("Trigger_RuinsAmbushAlarm");
            alarmGO.transform.SetParent(root, false);
            alarmGO.transform.position = new Vector3(0f, 0.25f, 9.5f);

            var alarmRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            alarmRing.name = "Alarm_Ring";
            alarmRing.transform.SetParent(alarmGO.transform, false);
            alarmRing.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            alarmRing.transform.localScale = new Vector3(6.5f, 0.04f, 3.2f);
            alarmRing.GetComponent<Renderer>().sharedMaterial = gateMat;
            Object.DestroyImmediate(alarmRing.GetComponent<Collider>());

            var aCol = alarmGO.AddComponent<BoxCollider>();
            aCol.isTrigger = true;
            aCol.center = new Vector3(0f, 1.2f, 0f);
            aCol.size = new Vector3(6.8f, 2.4f, 3.4f);

            var aTrigger = alarmGO.AddComponent<GameplayTriggerVolume>();
            aTrigger.ConfigureTrigger("trigger_watchtower_alarm", "Ruins Ambush Perimeter", TriggerVolumeType.RuinsAmbushAlarm, alarmRing, null, null);
        }

        // ── 4. Configure Player (Combat Audio, Footsteps, First-Person) ─────

        public static void ConfigurePlayerSystems(
            GameObject player,
            Camera mainCam,
            ThirdPersonCamera tpCam,
            Material bladeGlowMat,
            Material hiltMat)
        {
            // A. Configure Combat Audio on CombatManager
            if (player.TryGetComponent<CombatManager>(out var combat))
            {
                var lightSlash = AssetDatabase.LoadAssetAtPath<AudioClip>(Path.Combine(SfxCombat, "SFX_Sword_Light_Slash.wav"));
                var heavySlash = AssetDatabase.LoadAssetAtPath<AudioClip>(Path.Combine(SfxCombat, "SFX_Sword_Heavy_Slash.wav"));
                var hitImpact = AssetDatabase.LoadAssetAtPath<AudioClip>(Path.Combine(SfxCombat, "SFX_Hit_Impact.wav"));
                var parrySpark = AssetDatabase.LoadAssetAtPath<AudioClip>(Path.Combine(SfxCombat, "SFX_Parry_Spark.wav"));
                var blockThud = AssetDatabase.LoadAssetAtPath<AudioClip>(Path.Combine(SfxCombat, "SFX_Block_Thud.wav"));
                combat.ConfigureCombatAudio(lightSlash, heavySlash, hitImpact, parrySpark, blockThud);
            }

            // B. Add PlayerFootstepAudio
            if (!player.TryGetComponent<PlayerFootstepAudio>(out var footstepAudio))
            {
                footstepAudio = player.AddComponent<PlayerFootstepAudio>();
            }

            var grass1 = AssetDatabase.LoadAssetAtPath<AudioClip>(Path.Combine(SfxFootsteps, "SFX_Step_Grass_01.wav"));
            var grass2 = AssetDatabase.LoadAssetAtPath<AudioClip>(Path.Combine(SfxFootsteps, "SFX_Step_Grass_02.wav"));
            var stone1 = AssetDatabase.LoadAssetAtPath<AudioClip>(Path.Combine(SfxFootsteps, "SFX_Step_Stone_01.wav"));
            var stone2 = AssetDatabase.LoadAssetAtPath<AudioClip>(Path.Combine(SfxFootsteps, "SFX_Step_Stone_02.wav"));
            var earth1 = AssetDatabase.LoadAssetAtPath<AudioClip>(Path.Combine(SfxFootsteps, "SFX_Step_Earth_01.wav"));
            var earth2 = AssetDatabase.LoadAssetAtPath<AudioClip>(Path.Combine(SfxFootsteps, "SFX_Step_Earth_02.wav"));
            var wood1 = AssetDatabase.LoadAssetAtPath<AudioClip>(Path.Combine(SfxFootsteps, "SFX_Step_Wood_01.wav"));
            var wood2 = AssetDatabase.LoadAssetAtPath<AudioClip>(Path.Combine(SfxFootsteps, "SFX_Step_Wood_02.wav"));

            footstepAudio.AssignClips(
                new[] { grass1, grass2 },
                new[] { stone1, stone2 },
                new[] { earth1, earth2 },
                new[] { wood1, wood2 });

            // C. Add FirstPersonController & Eye Mount
            var eyeMount = player.transform.Find("FirstPerson_EyeMount");
            if (eyeMount == null)
            {
                var eyeGO = new GameObject("FirstPerson_EyeMount");
                eyeMount = eyeGO.transform;
                eyeMount.SetParent(player.transform, false);
                eyeMount.localPosition = new Vector3(0f, 1.68f, 0.12f);
            }

            // First-Person Viewmodel Blade & Gauntlet
            var fpViewModel = eyeMount.Find("FP_ViewModel_AetherBlade")?.gameObject;
            if (fpViewModel == null)
            {
                fpViewModel = new GameObject("FP_ViewModel_AetherBlade");
                fpViewModel.transform.SetParent(eyeMount, false);
                fpViewModel.transform.localPosition = new Vector3(0.32f, -0.28f, 0.58f);
                fpViewModel.transform.localRotation = Quaternion.Euler(15f, -12f, 8f);

                // Blade
                var blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
                blade.name = "BladeMesh";
                blade.transform.SetParent(fpViewModel.transform, false);
                blade.transform.localPosition = new Vector3(0f, 0.35f, 0f);
                blade.transform.localScale = new Vector3(0.06f, 0.85f, 0.035f);
                blade.GetComponent<Renderer>().sharedMaterial = bladeGlowMat;
                Object.DestroyImmediate(blade.GetComponent<Collider>());

                // Hilt
                var hilt = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                hilt.name = "HiltMesh";
                hilt.transform.SetParent(fpViewModel.transform, false);
                hilt.transform.localPosition = new Vector3(0f, -0.15f, 0f);
                hilt.transform.localScale = new Vector3(0.045f, 0.15f, 0.045f);
                hilt.GetComponent<Renderer>().sharedMaterial = hiltMat;
                Object.DestroyImmediate(hilt.GetComponent<Collider>());

                fpViewModel.SetActive(false); // [Tập trung map] Tạm ẩn blade viewmodel
            }
            else
            {
                fpViewModel.SetActive(false);
            }

            var modelTransform = player.transform.Find("Model");
            var bodyModel = modelTransform != null ? modelTransform.gameObject : null;
            if (bodyModel != null)
            {
                bodyModel.SetActive(false); // [Tập trung map] Tạm ẩn model nhân vật
            }

            if (!player.TryGetComponent<FirstPersonController>(out var fpc))
            {
                fpc = player.AddComponent<FirstPersonController>();
            }

            // Truyền null cho bodyModel và fpViewModel để khi chuyển đổi camera không bị hiện lại
            fpc.ConfigureReferences(eyeMount, mainCam, tpCam, null, null);
        }

        private static Material CreateOrLoadMaterial(string fileName, Color baseColor, Color? emission = null)
        {
            string path = Path.Combine(MatDir, fileName).Replace("\\", "/");
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Directory.CreateDirectory(MatDir);
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
    }
}
#endif
