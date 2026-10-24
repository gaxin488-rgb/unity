#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using EchoesOfTheLost.Characters.Player;
using EchoesOfTheLost.World;

namespace EchoesOfTheLost.EditorTools
{
    public static class GameplaySystemsBuilder
    {
        private const string MatDir = "Assets/_Project/Art/Materials";
        private const string SfxFootsteps = "Assets/_Project/Art/Audio/SFX/Footsteps";

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
        }

        public static void BuildAllSystems(
            Transform envRoot,
            GameObject player,
            Camera mainCam,
            ThirdPersonCamera tpCam)
        {
            GameAudioGenerator.GenerateAllAudio();

            // Dọn sạch các vật thể prototyping/trụ/drone cũ nếu còn tồn tại trong Scene
            string[] clutterObjects =
            {
                "Solenne_AetherWayshrine",
                "Solenne_CompanionDrone",
                "Prototyping_Placeholders",
                "Gameplay_TriggerVolumes"
            };
            foreach (string objName in clutterObjects)
            {
                var old = GameObject.Find(objName);
                if (old != null) Object.DestroyImmediate(old);
            }

            if (player != null)
            {
                var matAetherCyan = CreateOrLoadMaterial("Mat_AetherCyan.mat", new Color(0.18f, 0.85f, 0.98f));
                var matGold = CreateOrLoadMaterial("Mat_SolenneGold.mat", new Color(0.95f, 0.78f, 0.22f));
                ConfigurePlayerSystems(player, mainCam, tpCam, matAetherCyan, matGold);
            }
        }

        public static void ConfigurePlayerSystems(
            GameObject player,
            Camera mainCam,
            ThirdPersonCamera tpCam,
            Material bladeGlowMat,
            Material hiltMat)
        {
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

            var eyeMount = player.transform.Find("FirstPerson_EyeMount");
            if (eyeMount == null)
            {
                var eyeGO = new GameObject("FirstPerson_EyeMount");
                eyeMount = eyeGO.transform;
                eyeMount.SetParent(player.transform, false);
                eyeMount.localPosition = new Vector3(0f, 1.68f, 0.12f);
            }

            // Xóa bỏ kiếm Viewmodel nếu có
            var fpViewModel = eyeMount.Find("FP_ViewModel_AetherBlade")?.gameObject;
            if (fpViewModel != null)
            {
                Object.DestroyImmediate(fpViewModel);
                fpViewModel = null;
            }

            // Ẩn hoàn toàn Model nhân vật để chỉ tập trung vào Map
            var modelTransform = player.transform.Find("Model");
            var bodyModel = modelTransform != null ? modelTransform.gameObject : null;
            if (bodyModel != null)
            {
                bodyModel.SetActive(false);
            }

            if (!player.TryGetComponent<FirstPersonController>(out var fpc))
            {
                fpc = player.AddComponent<FirstPersonController>();
            }

            fpc.ConfigureReferences(eyeMount, mainCam, tpCam, null, null);
            fpc.SetFirstPersonMode(false);
        }

        private static Material CreateOrLoadMaterial(string fileName, Color baseColor)
        {
            string path = Path.Combine(MatDir, fileName).Replace("\\", "/");
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Directory.CreateDirectory(MatDir);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
            if (mat.HasProperty("_Color")) mat.color = baseColor;
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
    }
}
#endif
