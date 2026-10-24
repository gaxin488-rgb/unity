#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using TMPro;
using EchoesOfTheLost.Characters.Player;
using EchoesOfTheLost.UI;

namespace EchoesOfTheLost.EditorTools
{
    public static class UICanvasCameraBuilder
    {
        private const string UISpriteDir = "Assets/_Project/Art/UI/Sprites";
        private const string UIAssetDir = "Assets/_Project/Art/UI";
        private const string MiniMapRTPath = "Assets/_Project/Art/UI/RT_MiniMap.renderTexture";

        [MenuItem("EotL/UI & Camera/1. Set Up the Canvas")]
        public static void Stage1_SetUpTheCanvasMenu()
        {
            SetupCanvasAndEventSystem();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        [MenuItem("EotL/UI & Camera/2. Using UI Elements on Screen")]
        public static void Stage2_UsingUIElementsOnScreenMenu()
        {
            var canvasGO = SetupCanvasAndEventSystem();
            PopulateScreenUIElements(canvasGO, forceRegenerateSprites: true);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        [MenuItem("EotL/UI & Camera/3. Animating UI Elements")]
        public static void Stage3_AnimatingUIElementsMenu()
        {
            var canvasGO = GameObject.Find("HUD_Canvas") ?? SetupCanvasAndEventSystem();
            if (canvasGO.transform.childCount == 0)
                PopulateScreenUIElements(canvasGO, forceRegenerateSprites: false);
            SetupUIAnimations(canvasGO);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        [MenuItem("EotL/UI & Camera/4. Adjusting the Game Camera View")]
        public static void Stage4_AdjustingGameCameraViewMenu()
        {
            var canvasGO = GameObject.Find("HUD_Canvas");
            AdjustGameCameraView(canvasGO);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        [MenuItem("EotL/UI & Camera/Run Complete 4-Stage UI & Camera Pipeline")]
        public static void RunCompletePipelineMenu()
        {
            BuildCompleteUIAndCamera();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            var miniCamGO = GameObject.Find("MiniMap_Camera");
            if (miniCamGO != null && miniCamGO.TryGetComponent<Camera>(out var miniCam))
                miniCam.Render();
        }

        public static GameObject BuildCompleteUIAndCamera()
        {
            GameObject canvasGO = SetupCanvasAndEventSystem();
            PopulateScreenUIElements(canvasGO, forceRegenerateSprites: true);
            SetupUIAnimations(canvasGO);
            AdjustGameCameraView(canvasGO);
            return canvasGO;
        }

        public static GameObject SetupCanvasAndEventSystem()
        {
            Directory.CreateDirectory(UISpriteDir);
            Directory.CreateDirectory(UIAssetDir);

            var existingES = Object.FindFirstObjectByType<EventSystem>();
            GameObject esGO = existingES != null ? existingES.gameObject : GameObject.Find("EventSystem");
            if (esGO == null)
                esGO = new GameObject("EventSystem");

            if (!esGO.TryGetComponent<EventSystem>(out _))
                esGO.AddComponent<EventSystem>();
            if (!esGO.TryGetComponent<InputSystemUIInputModule>(out _))
                esGO.AddComponent<InputSystemUIInputModule>();

            var existingCanvas = GameObject.Find("HUD_Canvas");
            if (existingCanvas != null)
                Object.DestroyImmediate(existingCanvas);

            var canvasGO = new GameObject("HUD_Canvas");
            canvasGO.layer = LayerMask.NameToLayer("UI") >= 0 ? LayerMask.NameToLayer("UI") : 5;

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;
            canvas.sortingOrder = 100;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();
            canvasGO.AddComponent<HUDController>();
            canvasGO.AddComponent<UIManager>();
            canvasGO.AddComponent<UIElementAnimator>();

            return canvasGO;
        }

        // ── Chế độ Map Exploration: Chỉ giữ La Bàn + Mini-Map + Điều khiển Camera ──
        public static void PopulateScreenUIElements(GameObject canvasGO, bool forceRegenerateSprites = false)
        {
            EnsureUISpritesGenerated(forceRegenerateSprites);

            Sprite sprPanel = LoadSprite("Spr_UIPanelFrame.png");
            Sprite sprArrow = LoadSprite("Spr_MiniMapArrow.png");

            for (int i = canvasGO.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(canvasGO.transform.GetChild(i).gameObject);

            // 1. TOP-CENTER: La bàn phương hướng (Compass) & Toast thông báo góc nhìn
            var compassBar = CreateSlicedPanel("CompassBar", canvasGO.transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -16f), new Vector2(540f, 34f),
                sprPanel, new Color(0.04f, 0.07f, 0.12f, 0.72f), addRimOutline: true);
            var compassText = CreateUIText("Compass_Text", compassBar,
                "W   •   NW   •   <color=#7CE8FF><b>[ N (014°) — VERDA WILDS ]</b></color>   •   NE   •   E",
                14.5f, TextAlignmentOptions.Center, new Color(0.90f, 0.95f, 1f));

            var toastPanel = CreateSlicedPanel("ToastBanner", canvasGO.transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -58f), new Vector2(580f, 40f),
                sprPanel, new Color(0.06f, 0.12f, 0.20f, 0.85f), addRimOutline: true);
            var toastCG = toastPanel.gameObject.AddComponent<CanvasGroup>();
            toastCG.alpha = 1f;
            var toastText = CreateUIText("Toast_Text", toastPanel,
                "Verda Wilds Map Explorer — [WASD] Move | [V] / [C] Camera View | [Scroll] Zoom",
                15f, TextAlignmentOptions.Center, new Color(1.0f, 0.94f, 0.68f));

            // 2. TOP-RIGHT: Mini-Map Radar
            var miniMapFrame = CreateSlicedPanel("MiniMapFrame", canvasGO.transform,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-20f, -18f), new Vector2(184f, 204f),
                sprPanel, new Color(0.04f, 0.07f, 0.12f, 0.82f), addRimOutline: true);

            var miniMapHeader = CreateUIText("MiniMap_Header", miniMapFrame, "<b>MINI-MAP — VERDA WILDS</b>", 11.5f,
                TextAlignmentOptions.Center, new Color(0.65f, 0.92f, 1f));
            SetRect(miniMapHeader.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(6f, -5f), new Vector2(-12f, 18f));

            var rawImgGO = new GameObject("MiniMap_RawImage");
            rawImgGO.transform.SetParent(miniMapFrame, false);
            var rawRT = rawImgGO.AddComponent<RectTransform>();
            rawRT.anchorMin = new Vector2(0.5f, 0.5f);
            rawRT.anchorMax = new Vector2(0.5f, 0.5f);
            rawRT.pivot = new Vector2(0.5f, 0.5f);
            rawRT.anchoredPosition = new Vector2(0f, -8f);
            rawRT.sizeDelta = new Vector2(164f, 164f);
            var rawImg = rawImgGO.AddComponent<RawImage>();
            rawImg.texture = EnsureMiniMapRenderTexture();
            rawImg.raycastTarget = false;

            var miniMapArrow = CreateIconObject("MiniMap_PlayerArrow", rawRT,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(24f, 24f),
                sprArrow, Color.white);

            // 3. BOTTOM-LEFT: Hướng dẫn phím điều khiển Camera & Thời tiết nhỏ gọn
            var legendPanel = CreateSlicedPanel("ControlsLegend", canvasGO.transform,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(18f, 16f), new Vector2(440f, 44f),
                sprPanel, new Color(0.04f, 0.07f, 0.12f, 0.72f), addRimOutline: true);
            CreateUIText("Legend_Text", legendPanel,
                "<color=#7CE8FF><b>[V]</b> Đổi góc Camera   <b>[C]</b> Góc nhìn 1</color>   <b>[Lăn chuột]</b> Zoom   <b>[Esc]</b> Chỉnh Giờ / Thời tiết",
                13f, TextAlignmentOptions.Center, new Color(0.88f, 0.94f, 1f));

            // Wire HUDController (chỉ gắn Toast, để trống HP/MP/Quest)
            if (!canvasGO.TryGetComponent<HUDController>(out var hud))
                hud = canvasGO.AddComponent<HUDController>();
            var soHud = new SerializedObject(hud);
            soHud.FindProperty("_toastRoot").objectReferenceValue = toastPanel.gameObject;
            soHud.FindProperty("_toastText").objectReferenceValue = toastText;
            soHud.ApplyModifiedPropertiesWithoutUndo();

            // Wire UIElementAnimator (chỉ gắn La bàn & Mũi tên Mini-Map)
            if (!canvasGO.TryGetComponent<UIElementAnimator>(out var uiAnim))
                uiAnim = canvasGO.AddComponent<UIElementAnimator>();
            var soAnim = new SerializedObject(uiAnim);
            soAnim.FindProperty("_compassBar").objectReferenceValue = compassBar;
            soAnim.FindProperty("_miniMapFrame").objectReferenceValue = miniMapFrame;
            soAnim.FindProperty("_miniMapArrow").objectReferenceValue = miniMapArrow;
            soAnim.FindProperty("_compassText").objectReferenceValue = compassText;
            soAnim.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetupUIAnimations(GameObject canvasGO)
        {
            // Không gắn Animator rườm rà trong chế độ ngắm Map
        }

        public static void AdjustGameCameraView(GameObject canvasGO)
        {
            var playerGO = GameObject.FindGameObjectWithTag("Player");
            if (playerGO == null) playerGO = GameObject.Find("Player");

            var mainCamGO = GameObject.FindGameObjectWithTag("MainCamera");
            if (mainCamGO == null) mainCamGO = GameObject.Find("Main Camera");
            if (mainCamGO == null)
            {
                mainCamGO = new GameObject("Main Camera") { tag = "MainCamera" };
                mainCamGO.AddComponent<Camera>();
                mainCamGO.AddComponent<AudioListener>();
            }

            if (!mainCamGO.TryGetComponent<Camera>(out var mainCam))
                mainCam = mainCamGO.AddComponent<Camera>();
            mainCam.clearFlags = CameraClearFlags.Skybox;
            mainCam.nearClipPlane = 0.15f;
            mainCam.farClipPlane = 450f;

            if (!mainCamGO.TryGetComponent<ThirdPersonCamera>(out var tpc))
                tpc = mainCamGO.AddComponent<ThirdPersonCamera>();

            if (playerGO != null)
            {
                // Ẩn thân nhân vật nếu còn bật
                var model = playerGO.transform.Find("Model");
                if (model != null) model.gameObject.SetActive(false);

                tpc.ApplyInitialFraming(playerGO.transform, yawDegrees: 8f, pitchDegrees: 12f, CameraViewPreset.Exploration);

                if (playerGO.TryGetComponent<PlayerController>(out var pc))
                {
                    var soPc = new SerializedObject(pc);
                    var camProp = soPc.FindProperty("_cameraTransform");
                    if (camProp != null)
                    {
                        camProp.objectReferenceValue = mainCamGO.transform;
                        soPc.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
            }

            if (canvasGO != null && canvasGO.TryGetComponent<Canvas>(out var canvas))
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
            }

            SetupOverheadMiniMapCamera(playerGO != null ? playerGO.transform : null, canvasGO);
        }

        private static void SetupOverheadMiniMapCamera(Transform playerTransform, GameObject canvasGO)
        {
            RenderTexture rt = EnsureMiniMapRenderTexture();

            var miniCamGO = GameObject.Find("MiniMap_Camera");
            if (miniCamGO == null)
                miniCamGO = new GameObject("MiniMap_Camera");

            Vector3 focusPos = playerTransform != null ? playerTransform.position : new Vector3(0f, 0f, 6f);
            miniCamGO.transform.position = new Vector3(focusPos.x, 65f, focusPos.z);
            miniCamGO.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            if (!miniCamGO.TryGetComponent<Camera>(out var miniCam))
                miniCam = miniCamGO.AddComponent<Camera>();
            miniCam.orthographic = true;
            miniCam.orthographicSize = 32f;
            miniCam.nearClipPlane = 1f;
            miniCam.farClipPlane = 200f;
            miniCam.clearFlags = CameraClearFlags.SolidColor;
            miniCam.backgroundColor = new Color(0.18f, 0.34f, 0.16f, 1f);
            miniCam.cullingMask = ~(1 << 5); // Render tất cả địa hình, cây cối, đường đi (trừ layer UI)
            miniCam.targetTexture = rt;
            miniCam.depth = -2;

            if (!miniCamGO.TryGetComponent<MiniMapSystem>(out var miniMapSys))
                miniMapSys = miniCamGO.AddComponent<MiniMapSystem>();
            var soMm = new SerializedObject(miniMapSys);
            soMm.FindProperty("_miniMapCamera").objectReferenceValue = miniCam;
            soMm.FindProperty("_cameraHeight").floatValue = 65f;
            soMm.FindProperty("_orthographicSize").floatValue = 32f;
            soMm.FindProperty("_renderTexture").objectReferenceValue = rt;
            soMm.FindProperty("_enableFogOfWar").boolValue = false;
            soMm.FindProperty("_rotateWithPlayer").boolValue = false;

            var layerProp = soMm.FindProperty("_miniMapLayer");
            if (layerProp != null)
            {
                var bits = layerProp.FindPropertyRelative("m_Bits");
                if (bits != null) bits.intValue = ~(1 << 5);
            }

            if (playerTransform != null)
                soMm.FindProperty("_player").objectReferenceValue = playerTransform;

            if (canvasGO != null)
            {
                var arrow = canvasGO.transform.Find("MiniMapFrame/MiniMap_RawImage/MiniMap_PlayerArrow") as RectTransform;
                if (arrow != null)
                    soMm.FindProperty("_playerIconRect").objectReferenceValue = arrow;
            }
            soMm.ApplyModifiedPropertiesWithoutUndo();

            miniCam.Render();
        }

        private static RenderTexture EnsureMiniMapRenderTexture()
        {
            Directory.CreateDirectory(UIAssetDir);
            var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(MiniMapRTPath);
            if (rt == null)
            {
                rt = new RenderTexture(256, 256, 16, RenderTextureFormat.ARGB32)
                {
                    name = "RT_MiniMap",
                    filterMode = FilterMode.Bilinear
                };
                AssetDatabase.CreateAsset(rt, MiniMapRTPath);
            }
            return rt;
        }

        private static void EnsureUISpritesGenerated(bool force = false)
        {
            Directory.CreateDirectory(UISpriteDir);

            BakePanelSprite($"{UISpriteDir}/Spr_UIPanelFrame.png", 64, 64, force);
            BakeArrowSprite($"{UISpriteDir}/Spr_MiniMapArrow.png", 64, force);

            AssetDatabase.Refresh();

            ConfigureSpriteImporter($"{UISpriteDir}/Spr_UIPanelFrame.png", new Vector4(12, 12, 12, 12));
            ConfigureSpriteImporter($"{UISpriteDir}/Spr_MiniMapArrow.png", Vector4.zero);
        }

        private static void BakePanelSprite(string path, int w, int h, bool force)
        {
            if (!force && File.Exists(path)) return;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            float cornerRadius = 8f;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = 0f;
                    if (x < cornerRadius) dx = cornerRadius - x;
                    else if (x > w - 1 - cornerRadius) dx = x - (w - 1 - cornerRadius);

                    float dy = 0f;
                    if (y < cornerRadius) dy = cornerRadius - y;
                    else if (y > h - 1 - cornerRadius) dy = y - (h - 1 - cornerRadius);

                    if (Mathf.Sqrt(dx * dx + dy * dy) > cornerRadius)
                    {
                        tex.SetPixel(x, y, Color.clear);
                        continue;
                    }

                    float grad = Mathf.Lerp(0.90f, 1.0f, (float)y / (h - 1));
                    tex.SetPixel(x, y, new Color(grad, grad, grad, 1f));
                }
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        private static void BakeArrowSprite(string path, int size, bool force)
        {
            if (!force && File.Exists(path)) return;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float cx = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                float ny = (float)y / (size - 1);
                for (int x = 0; x < size; x++)
                {
                    float nx = (x - cx) / (size * 0.5f);
                    bool inside = ny >= 0.18f && ny <= 0.88f && Mathf.Abs(nx) <= (0.90f - ny) * 0.72f;
                    bool notch = ny < 0.34f && Mathf.Abs(nx) < (0.34f - ny) * 0.85f;
                    if (inside && !notch)
                    {
                        Color col = Mathf.Abs(nx) > (0.90f - ny) * 0.52f
                            ? new Color(0.10f, 0.16f, 0.24f, 1f)
                            : new Color(0.30f, 0.95f, 1.0f, 1f);
                        tex.SetPixel(x, y, col);
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        private static void ConfigureSpriteImporter(string assetPath, Vector4 border)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;

            bool dirty = false;
            if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; dirty = true; }
            if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; dirty = true; }
            if (importer.spriteBorder != border) { importer.spriteBorder = border; dirty = true; }
            if (importer.alphaIsTransparency != true) { importer.alphaIsTransparency = true; dirty = true; }
            if (dirty) importer.SaveAndReimport();
        }

        private static Sprite LoadSprite(string fileName) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{UISpriteDir}/{fileName}");

        private static RectTransform CreateSlicedPanel(
            string name, Transform parent,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPos, Vector2 sizeDelta,
            Sprite sprite, Color color, bool addRimOutline = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;

            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.type = (sprite != null && sprite.border != Vector4.zero) ? Image.Type.Sliced : Image.Type.Simple;
            img.raycastTarget = false;

            if (addRimOutline)
            {
                var outline = go.AddComponent<Outline>();
                outline.effectColor = new Color(0.38f, 0.76f, 0.95f, 0.42f);
                outline.effectDistance = new Vector2(1.5f, -1.5f);
            }
            return rt;
        }

        private static RectTransform CreateIconObject(
            string name, Transform parent,
            Vector2 anchor, Vector2 pivot,
            Vector2 anchoredPos, Vector2 sizeDelta,
            Sprite sprite, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;

            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return rt;
        }

        private static TextMeshProUGUI CreateUIText(
            string name, RectTransform parent,
            string text, float fontSize,
            TextAlignmentOptions align, Color color)
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
            tmp.raycastTarget = false;

            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            shadow.effectDistance = new Vector2(1.2f, -1.2f);
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
    }
}
#endif
