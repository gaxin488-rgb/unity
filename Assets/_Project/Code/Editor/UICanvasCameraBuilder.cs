#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using TMPro;
using EchoesOfTheLost.Characters.Player;
using EchoesOfTheLost.UI;

// ============================================================
//  UICanvasCameraBuilder — Complete 4-Stage UI & Camera Pipeline:
//    Stage 1: SET UP THE CANVAS (Canvas + CanvasScaler 1920x1080 + GraphicRaycaster + EventSystem)
//    Stage 2: USING UI ELEMENTS ON SCREEN (Status Bars, Level Badge, Compass, Live Mini-Map, Quest Card, Hotbar, Reticle, Prompt, Toast)
//    Stage 3: ANIMATING UI ELEMENTS (Unity AnimationClips + AnimatorControllers + UIElementAnimator)
//    Stage 4: ADJUSTING THE GAME CAMERA VIEW (Over-the-Shoulder Framing + 3 Presets [V] + Collision + MiniMap Camera)
// ============================================================
namespace EchoesOfTheLost.EditorTools
{
    public static class UICanvasCameraBuilder
    {
        private const string UISpriteDir = "Assets/_Project/Art/UI/Sprites";
        private const string UIAssetDir = "Assets/_Project/Art/UI";
        private const string UIAnimDir = "Assets/_Project/Art/Animations/UI";
        private const string MiniMapRTPath = "Assets/_Project/Art/UI/RT_MiniMap.renderTexture";

        // ── Menu Commands for the 4 Stages ────────────────────

        [MenuItem("EotL/UI & Camera/1. Set Up the Canvas")]
        public static void Stage1_SetUpTheCanvasMenu()
        {
            SetupCanvasAndEventSystem();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[UICanvasCameraBuilder] Stage 1 Complete: Set Up the Canvas (HUD_Canvas + CanvasScaler 1920x1080 + EventSystem).");
        }

        [MenuItem("EotL/UI & Camera/2. Using UI Elements on Screen")]
        public static void Stage2_UsingUIElementsOnScreenMenu()
        {
            var canvasGO = SetupCanvasAndEventSystem();
            PopulateScreenUIElements(canvasGO);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[UICanvasCameraBuilder] Stage 2 Complete: Populated On-Screen UI Elements (Status, Compass, Mini-Map, Quests, Hotbar, Reticle, Prompts).");
        }

        [MenuItem("EotL/UI & Camera/3. Animating UI Elements")]
        public static void Stage3_AnimatingUIElementsMenu()
        {
            var canvasGO = GameObject.Find("HUD_Canvas") ?? SetupCanvasAndEventSystem();
            if (canvasGO.transform.childCount == 0)
                PopulateScreenUIElements(canvasGO);
            SetupUIAnimations(canvasGO);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[UICanvasCameraBuilder] Stage 3 Complete: Configured UI Animations (AnimationClips, AnimatorControllers & UIElementAnimator).");
        }

        [MenuItem("EotL/UI & Camera/4. Adjusting the Game Camera View")]
        public static void Stage4_AdjustingGameCameraViewMenu()
        {
            var canvasGO = GameObject.Find("HUD_Canvas");
            AdjustGameCameraView(canvasGO);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[UICanvasCameraBuilder] Stage 4 Complete: Adjusted Main Camera View (Over-the-Shoulder Framing, 3 Presets [V] & MiniMap Camera).");
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

        // ── Master Entry Point Called by SandboxSceneBuilder ──

        public static GameObject BuildCompleteUIAndCamera()
        {
            // Stage 1: Set Up the Canvas & EventSystem
            GameObject canvasGO = SetupCanvasAndEventSystem();

            // Stage 2: Using UI Elements on Screen
            PopulateScreenUIElements(canvasGO);

            // Stage 3: Animating UI Elements
            SetupUIAnimations(canvasGO);

            // Stage 4: Adjusting the Game Camera View & Binding ScreenSpaceCamera + MiniMap
            AdjustGameCameraView(canvasGO);

            return canvasGO;
        }

        // ============================================================
        //  STAGE 1: SET UP THE CANVAS
        // ============================================================
        public static GameObject SetupCanvasAndEventSystem()
        {
            Directory.CreateDirectory(UISpriteDir);
            Directory.CreateDirectory(UIAssetDir);
            Directory.CreateDirectory(UIAnimDir);

            // 1. Ensure EventSystem + InputSystemUIInputModule exists
            var existingES = Object.FindFirstObjectByType<EventSystem>();
            GameObject esGO = existingES != null ? existingES.gameObject : GameObject.Find("EventSystem");
            if (esGO == null)
                esGO = new GameObject("EventSystem");

            if (!esGO.TryGetComponent<EventSystem>(out _))
                esGO.AddComponent<EventSystem>();
            if (!esGO.TryGetComponent<InputSystemUIInputModule>(out _))
                esGO.AddComponent<InputSystemUIInputModule>();

            // 2. Create or Reconfigure HUD_Canvas
            var existingCanvas = GameObject.Find("HUD_Canvas");
            if (existingCanvas != null)
                Object.DestroyImmediate(existingCanvas);

            var canvasGO = new GameObject("HUD_Canvas");
            canvasGO.layer = LayerMask.NameToLayer("UI") >= 0 ? LayerMask.NameToLayer("UI") : 5;

            var canvas = canvasGO.AddComponent<Canvas>();
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                var mcGO = GameObject.Find("Main Camera");
                if (mcGO != null) mcGO.TryGetComponent<Camera>(out mainCam);
            }
            if (mainCam != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = mainCam;
                canvas.planeDistance = 0.5f;
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }
            canvas.pixelPerfect = false;
            canvas.sortingOrder = 10;

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

        // ============================================================
        //  STAGE 2: USING UI ELEMENTS ON SCREEN
        // ============================================================
        public static void PopulateScreenUIElements(GameObject canvasGO)
        {
            EnsureUISpritesGenerated();

            Sprite sprPanel = LoadSprite("Spr_UIPanelFrame.png");
            Sprite sprBar = LoadSprite("Spr_UIBarFill.png");
            Sprite sprCrest = LoadSprite("Spr_BadgeCrest.png");
            Sprite sprReticle = LoadSprite("Spr_Reticle.png");
            Sprite sprArrow = LoadSprite("Spr_MiniMapArrow.png");
            Sprite sprSword = LoadSprite("Spr_IconSword.png");
            Sprite sprShield = LoadSprite("Spr_IconShield.png");
            Sprite sprPotion = LoadSprite("Spr_IconPotion.png");
            Sprite sprEcho = LoadSprite("Spr_IconEcho.png");

            // Clear old children if rebuilding
            for (int i = canvasGO.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(canvasGO.transform.GetChild(i).gameObject);

            // ── 1. TOP-LEFT: Player Status Panel (Level Crest, HP + Damage Trail, Stamina, XP, Gold) ──
            var statusPanel = CreateSlicedPanel("StatusPanel", canvasGO.transform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(22f, -20f), new Vector2(410f, 146f),
                sprPanel, new Color(0.06f, 0.09f, 0.14f, 0.88f));

            // Level Crest Badge (Left emblem)
            var levelBadge = CreateIconObject("LevelBadge", statusPanel,
                new Vector2(0f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(44f, -48f), new Vector2(60f, 60f),
                sprCrest, Color.white);
            var badgeText = CreateUIText("BadgeLevel_Text", levelBadge, "LV\n<size=130%><b>1</b></size>", 14,
                TextAlignmentOptions.Center, new Color(0.12f, 0.08f, 0.02f));

            // HP Bar with Delayed Damage Trail
            var hpBg = CreateSlicedPanel("HP_BG", statusPanel,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(82f, -18f), new Vector2(310f, 28f),
                sprPanel, new Color(0.18f, 0.06f, 0.07f, 0.94f));
            var hpDamageTrail = CreateUIFillBar("HP_DamageTrail", hpBg, sprBar, new Color(1.0f, 0.72f, 0.22f, 0.92f), 1.0f);
            var hpFill = CreateUIFillBar("HP_Fill", hpBg, sprBar, new Color(0.88f, 0.22f, 0.24f, 1f), 1.0f);
            var hpText = CreateUIText("HP_Text", hpBg, "HP 100 / 100", 15, TextAlignmentOptions.Center, Color.white);

            // Stamina Bar
            var stBg = CreateSlicedPanel("Stamina_BG", statusPanel,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(82f, -52f), new Vector2(310f, 18f),
                sprPanel, new Color(0.06f, 0.16f, 0.10f, 0.94f));
            var stFill = CreateUIFillBar("Stamina_Fill", stBg, sprBar, new Color(0.24f, 0.84f, 0.45f, 1f), 1.0f);

            // XP Progress Bar
            var xpBg = CreateSlicedPanel("XP_BG", statusPanel,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(82f, -75f), new Vector2(310f, 10f),
                sprPanel, new Color(0.06f, 0.10f, 0.20f, 0.94f));
            var xpFill = CreateUIFillBar("XP_Fill", xpBg, sprBar, new Color(0.32f, 0.78f, 1.0f, 1f), 0.35f);

            // Level / XP / Gold Text
            var lvGoldText = CreateUIText("LevelGold_Text", statusPanel,
                "KAEL — LV 1 (XP 0/100)   |   <color=#FFD966>Gold: 0</color>", 16,
                TextAlignmentOptions.Left, new Color(0.92f, 0.96f, 1f));
            SetRect(lvGoldText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -96f), new Vector2(374f, 36f));

            // ── 2. TOP-CENTER: Sky Compass Strip & Animated Toast Banner ──
            var compassBar = CreateSlicedPanel("CompassBar", canvasGO.transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -18f), new Vector2(640f, 38f),
                sprPanel, new Color(0.05f, 0.08f, 0.13f, 0.86f));
            var compassText = CreateUIText("Compass_Text", compassBar,
                "W   •   NW   •   <color=#7CE8FF>[ N (014°) — SOLENNE RUINS ]</color>   •   NE   •   E",
                15, TextAlignmentOptions.Center, new Color(0.86f, 0.92f, 0.98f));

            var toastPanel = CreateSlicedPanel("ToastBanner", canvasGO.transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -64f), new Vector2(680f, 48f),
                sprPanel, new Color(0.08f, 0.15f, 0.22f, 0.94f));
            var toastCG = toastPanel.gameObject.AddComponent<CanvasGroup>();
            toastCG.alpha = 1f;
            var toastText = CreateUIText("Toast_Text", toastPanel,
                "Welcome to Verda Wilds! Talk to Elder Maren [E] | Switch Camera [V]",
                17, TextAlignmentOptions.Center, new Color(1.0f, 0.94f, 0.68f));

            // ── 3. TOP-RIGHT: Live Mini-Map Radar & Active Quest Tracker Card ──
            var miniMapFrame = CreateSlicedPanel("MiniMapFrame", canvasGO.transform,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-22f, -20f), new Vector2(184f, 204f),
                sprPanel, new Color(0.05f, 0.08f, 0.13f, 0.92f));

            var miniMapHeader = CreateUIText("MiniMap_Header", miniMapFrame, "RADAR — VERDA WILDS", 12,
                TextAlignmentOptions.Center, new Color(0.55f, 0.90f, 1f));
            SetRect(miniMapHeader.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(6f, -6f), new Vector2(-12f, 20f));

            var rawImgGO = new GameObject("MiniMap_RawImage");
            rawImgGO.transform.SetParent(miniMapFrame, false);
            var rawRT = rawImgGO.AddComponent<RectTransform>();
            rawRT.anchorMin = new Vector2(0.5f, 0.5f);
            rawRT.anchorMax = new Vector2(0.5f, 0.5f);
            rawRT.pivot = new Vector2(0.5f, 0.5f);
            rawRT.anchoredPosition = new Vector2(0f, -8f);
            rawRT.sizeDelta = new Vector2(160f, 160f);
            var rawImg = rawImgGO.AddComponent<RawImage>();
            rawImg.texture = EnsureMiniMapRenderTexture();

            var miniMapArrow = CreateIconObject("MiniMap_PlayerArrow", rawRT,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(24f, 24f),
                sprArrow, Color.white);

            // Quest Tracker Panel (positioned beside/below Mini-Map on Top-Right)
            var rightPanel = CreateSlicedPanel("QuestTrackerPanel", canvasGO.transform,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-218f, -20f), new Vector2(360f, 204f),
                sprPanel, new Color(0.06f, 0.09f, 0.14f, 0.88f));

            var timeText = CreateUIText("TimeWeather_Text", rightPanel, "08:00 AM  |  Clear\nVerda Wilds", 15,
                TextAlignmentOptions.TopRight, new Color(0.72f, 0.92f, 1f));
            SetRect(timeText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(14f, -12f), new Vector2(-28f, 44f));

            var questText = CreateUIText("QuestTracker_Text", rightPanel,
                "<color=#FFD966>[Echoes of Verda Wilds]</color>\n • Speak with Elder Maren near the Ruins\n • Examine Solenne Echo Crystals (0/2)",
                15, TextAlignmentOptions.TopLeft, Color.white);
            SetRect(questText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -60f), new Vector2(-32f, 132f));

            // ── 4. CENTER: Aim Crosshair Reticle & Interaction Prompt Pill ──
            var reticleRT = CreateIconObject("CrosshairReticle", canvasGO.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 16f), new Vector2(34f, 34f),
                sprReticle, new Color(0.65f, 0.95f, 1f, 0.80f));

            var promptPanel = CreateSlicedPanel("InteractPrompt", canvasGO.transform,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 148f), new Vector2(440f, 46f),
                sprPanel, new Color(0.05f, 0.09f, 0.14f, 0.92f));

            var keycapRT = CreateSlicedPanel("Keycap_E", promptPanel,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(30f, 0f), new Vector2(34f, 34f),
                sprPanel, new Color(0.18f, 0.78f, 0.96f, 0.96f));
            CreateUIText("Keycap_Label", keycapRT, "<b>E</b>", 17, TextAlignmentOptions.Center, new Color(0.04f, 0.08f, 0.12f));

            var promptText = CreateUIText("Prompt_Text", promptPanel, "Talk to Elder Maren / Examine Echo", 18,
                TextAlignmentOptions.Left, new Color(0.65f, 0.96f, 1f));
            SetRect(promptText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(58f, -6f), new Vector2(-68f, 34f));

            // ── 5. BOTTOM-CENTER: 4-Slot Action Skill & Item Hotbar ──
            var hotbarPanel = CreateSlicedPanel("ActionHotbar", canvasGO.transform,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 18f), new Vector2(364f, 84f),
                sprPanel, new Color(0.05f, 0.08f, 0.13f, 0.90f));

            string[] slotNames = { "Slot_0_Slash", "Slot_1_Heavy", "Slot_2_Tonic", "Slot_3_Echo" };
            string[] slotKeys = { "LMB", "RMB", "1", "E" };
            string[] slotLabels = { "Slash", "Heavy", "Tonic", "Echo" };
            Sprite[] slotIcons = { sprSword, sprShield, sprPotion, sprEcho };

            var hotbarSlots = new RectTransform[4];
            var hotbarHighlights = new Image[4];

            for (int i = 0; i < 4; i++)
            {
                float xPos = -126f + i * 84f;
                var slotRT = CreateSlicedPanel(slotNames[i], hotbarPanel,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(xPos, 2f), new Vector2(72f, 68f),
                    sprPanel, new Color(0.14f, 0.22f, 0.32f, 0.92f));

                hotbarSlots[i] = slotRT;
                hotbarHighlights[i] = slotRT.GetComponent<Image>();

                CreateIconObject("Slot_Icon", slotRT,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0f, 5f), new Vector2(42f, 42f),
                    slotIcons[i], Color.white);

                var keyBadge = CreateUIText("Key_Text", slotRT, $"<b>[{slotKeys[i]}]</b> {slotLabels[i]}", 11,
                    TextAlignmentOptions.Bottom, new Color(0.95f, 0.90f, 0.65f));
                SetRect(keyBadge.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(2f, -46f), new Vector2(-4f, 20f));
            }

            // ── 6. BOTTOM DIALOGUE PANEL (Hidden until dialogue starts) ──
            var dlgPanel = CreateSlicedPanel("DialoguePanel", canvasGO.transform,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 108f), new Vector2(920f, 144f),
                sprPanel, new Color(0.04f, 0.06f, 0.11f, 0.95f));

            var speakerText = CreateUIText("Speaker_Text", dlgPanel, "Elder Maren", 21,
                TextAlignmentOptions.TopLeft, new Color(1f, 0.85f, 0.35f));
            SetRect(speakerText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -12f), new Vector2(-48f, 30f));

            var bodyText = CreateUIText("Body_Text", dlgPanel, "...", 18,
                TextAlignmentOptions.TopLeft, Color.white);
            SetRect(bodyText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -46f), new Vector2(-48f, 86f));
            dlgPanel.gameObject.SetActive(false);

            // ── 7. BOTTOM-LEFT: Controls & Camera View Legend ──
            var legendPanel = CreateSlicedPanel("ControlsLegend", canvasGO.transform,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(22f, 18f), new Vector2(460f, 64f),
                sprPanel, new Color(0.05f, 0.08f, 0.13f, 0.84f));
            CreateUIText("Legend_Text", legendPanel,
                "<color=#7CE8FF>[V] Switch Camera View</color>   [Scroll] Zoom   [Tab] Bag   [J] Quests\n[E] Interact   [LMB/RMB] Attack   [K] Character   [Esc] Sandbox",
                13.5f, TextAlignmentOptions.Center, new Color(0.84f, 0.92f, 0.98f));

            // ── Wire HUDController References ──
            if (!canvasGO.TryGetComponent<HUDController>(out var hud))
                hud = canvasGO.AddComponent<HUDController>();
            var soHud = new SerializedObject(hud);
            soHud.FindProperty("_healthFill").objectReferenceValue = hpFill;
            soHud.FindProperty("_healthText").objectReferenceValue = hpText;
            soHud.FindProperty("_staminaFill").objectReferenceValue = stFill;
            soHud.FindProperty("_levelGoldText").objectReferenceValue = lvGoldText;
            soHud.FindProperty("_timeWeatherText").objectReferenceValue = timeText;
            soHud.FindProperty("_questTrackerText").objectReferenceValue = questText;
            soHud.FindProperty("_interactPromptRoot").objectReferenceValue = promptPanel.gameObject;
            soHud.FindProperty("_interactPromptText").objectReferenceValue = promptText;
            soHud.FindProperty("_dialoguePanelRoot").objectReferenceValue = dlgPanel.gameObject;
            soHud.FindProperty("_dialogueSpeakerText").objectReferenceValue = speakerText;
            soHud.FindProperty("_dialogueBodyText").objectReferenceValue = bodyText;
            soHud.FindProperty("_toastRoot").objectReferenceValue = toastPanel.gameObject;
            soHud.FindProperty("_toastText").objectReferenceValue = toastText;
            soHud.ApplyModifiedPropertiesWithoutUndo();

            // ── Wire UIElementAnimator References ──
            if (!canvasGO.TryGetComponent<UIElementAnimator>(out var uiAnim))
                uiAnim = canvasGO.AddComponent<UIElementAnimator>();
            var soAnim = new SerializedObject(uiAnim);
            soAnim.FindProperty("_statusPanel").objectReferenceValue = statusPanel;
            soAnim.FindProperty("_compassBar").objectReferenceValue = compassBar;
            soAnim.FindProperty("_miniMapFrame").objectReferenceValue = miniMapFrame;
            soAnim.FindProperty("_questTrackerPanel").objectReferenceValue = rightPanel;
            soAnim.FindProperty("_actionHotbar").objectReferenceValue = hotbarPanel;
            soAnim.FindProperty("_levelBadge").objectReferenceValue = levelBadge;
            soAnim.FindProperty("_reticleTransform").objectReferenceValue = reticleRT;
            soAnim.FindProperty("_reticleImage").objectReferenceValue = reticleRT.GetComponent<Image>();
            soAnim.FindProperty("_interactPromptTransform").objectReferenceValue = promptPanel;
            soAnim.FindProperty("_interactKeycapImage").objectReferenceValue = keycapRT.GetComponent<Image>();
            soAnim.FindProperty("_miniMapArrow").objectReferenceValue = miniMapArrow;
            soAnim.FindProperty("_hpMainFill").objectReferenceValue = hpFill;
            soAnim.FindProperty("_hpDamageTrailFill").objectReferenceValue = hpDamageTrail;
            soAnim.FindProperty("_xpFill").objectReferenceValue = xpFill;
            soAnim.FindProperty("_compassText").objectReferenceValue = compassText;

            var slotsProp = soAnim.FindProperty("_hotbarSlots");
            slotsProp.arraySize = 4;
            var hiProp = soAnim.FindProperty("_hotbarSlotHighlights");
            hiProp.arraySize = 4;
            for (int i = 0; i < 4; i++)
            {
                slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = hotbarSlots[i];
                hiProp.GetArrayElementAtIndex(i).objectReferenceValue = hotbarHighlights[i];
            }
            soAnim.ApplyModifiedPropertiesWithoutUndo();
        }

        // ============================================================
        //  STAGE 3: ANIMATING UI ELEMENTS
        // ============================================================
        public static void SetupUIAnimations(GameObject canvasGO)
        {
            Directory.CreateDirectory(UIAnimDir);

            // 1. Create Pulse Loop AnimationClip & AnimatorController for Badges & Prompts
            string pulseClipPath = $"{UIAnimDir}/Anim_UI_PulseLoop.anim";
            string pulseCtrlPath = $"{UIAnimDir}/AC_UI_PulseLoop.controller";
            AnimationClip pulseClip = CreateOrUpdatePulseClip(pulseClipPath);
            AnimatorController pulseCtrl = CreateOrUpdateSingleStateController(pulseCtrlPath, "PulseLoop", pulseClip);

            // 2. Create Hotbar Slot Idle + Pop AnimationClips & AnimatorController
            string slotIdlePath = $"{UIAnimDir}/Anim_UI_SlotIdle.anim";
            string slotPopPath = $"{UIAnimDir}/Anim_UI_SlotPop.anim";
            string slotCtrlPath = $"{UIAnimDir}/AC_UI_HotbarSlot.controller";
            AnimationClip slotIdleClip = CreateOrUpdateSlotIdleClip(slotIdlePath);
            AnimationClip slotPopClip = CreateOrUpdateSlotPopClip(slotPopPath);
            AnimatorController slotCtrl = CreateOrUpdateHotbarController(slotCtrlPath, slotIdleClip, slotPopClip);

            if (canvasGO == null) return;

            // Attach Pulse Animator to LevelBadge & InteractPrompt
            AttachAnimator(canvasGO.transform.Find("StatusPanel/LevelBadge"), pulseCtrl);
            AttachAnimator(canvasGO.transform.Find("InteractPrompt"), pulseCtrl);

            // Attach HotbarPop Animator to each of the 4 ActionHotbar slots
            var hotbar = canvasGO.transform.Find("ActionHotbar");
            if (hotbar != null)
            {
                for (int i = 0; i < hotbar.childCount; i++)
                {
                    AttachAnimator(hotbar.GetChild(i), slotCtrl);
                }
            }
        }

        // ============================================================
        //  STAGE 4: ADJUSTING THE GAME CAMERA VIEW
        // ============================================================
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
                // Position Kael slightly left of center and angle camera (yaw = 10°, pitch = 13°)
                // so the player, stone bridge, Elder Maren, Watchtower Ruins, Echo Crystals & Mountains
                // are all framed in a balanced rule-of-thirds composition.
                tpc.ApplyInitialFraming(playerGO.transform, yawDegrees: 8f, pitchDegrees: 13.5f, CameraViewPreset.Exploration);

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

            // Bind HUD_Canvas to Main Camera in ScreenSpaceCamera mode so Game View & Camera Renders show the full HUD
            if (canvasGO != null)
            {
                if (canvasGO.TryGetComponent<Canvas>(out var canvas))
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = mainCam;
                    canvas.planeDistance = 0.5f;
                    canvas.sortingOrder = 10;
                }
            }

            // Configure Overhead MiniMap Camera & RenderTexture
            SetupOverheadMiniMapCamera(playerGO != null ? playerGO.transform : null, canvasGO);

            // Sync Editor SceneView camera to match the adjusted Game Camera view
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.AlignViewToObject(mainCamGO.transform);
                SceneView.lastActiveSceneView.Repaint();
            }
        }

        private static void SetupOverheadMiniMapCamera(Transform playerTransform, GameObject canvasGO)
        {
            RenderTexture rt = EnsureMiniMapRenderTexture();

            var miniCamGO = GameObject.Find("MiniMap_Camera");
            if (miniCamGO == null)
                miniCamGO = new GameObject("MiniMap_Camera");

            Vector3 focusPos = playerTransform != null ? playerTransform.position : new Vector3(0f, 0f, 6f);
            miniCamGO.transform.position = new Vector3(focusPos.x, 52f, focusPos.z + 6f);
            miniCamGO.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            if (!miniCamGO.TryGetComponent<Camera>(out var miniCam))
                miniCam = miniCamGO.AddComponent<Camera>();
            miniCam.orthographic = true;
            miniCam.orthographicSize = 26f;
            miniCam.clearFlags = CameraClearFlags.SolidColor;
            miniCam.backgroundColor = new Color(0.12f, 0.22f, 0.14f, 1f);
            miniCam.cullingMask = ~(1 << 5); // Exclude UI layer from Mini-Map camera
            miniCam.targetTexture = rt;
            miniCam.depth = -2;

            if (!miniCamGO.TryGetComponent<MiniMapSystem>(out var miniMapSys))
                miniMapSys = miniCamGO.AddComponent<MiniMapSystem>();
            var soMm = new SerializedObject(miniMapSys);
            soMm.FindProperty("_miniMapCamera").objectReferenceValue = miniCam;
            soMm.FindProperty("_cameraHeight").floatValue = 52f;
            soMm.FindProperty("_orthographicSize").floatValue = 26f;
            soMm.FindProperty("_renderTexture").objectReferenceValue = rt;
            soMm.FindProperty("_enableFogOfWar").boolValue = false;
            if (playerTransform != null)
                soMm.FindProperty("_player").objectReferenceValue = playerTransform;

            if (canvasGO != null)
            {
                var arrow = canvasGO.transform.Find("MiniMapFrame/MiniMap_RawImage/MiniMap_PlayerArrow") as RectTransform;
                if (arrow != null)
                    soMm.FindProperty("_playerIconRect").objectReferenceValue = arrow;
            }
            soMm.ApplyModifiedPropertiesWithoutUndo();

            // Render immediately so the MiniMap RawImage is populated in Edit Mode too
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

        // ── AnimationClip & Controller Builders ───────────────

        private static AnimationClip CreateOrUpdatePulseClip(string path)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            bool isNew = clip == null;
            if (isNew) clip = new AnimationClip { name = Path.GetFileNameWithoutExtension(path) };

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            var scaleCurve = new AnimationCurve(
                new Keyframe(0f, 1.0f),
                new Keyframe(0.9f, 1.05f),
                new Keyframe(1.8f, 1.0f));

            clip.SetCurve("", typeof(RectTransform), "m_LocalScale.x", scaleCurve);
            clip.SetCurve("", typeof(RectTransform), "m_LocalScale.y", scaleCurve);
            clip.SetCurve("", typeof(RectTransform), "m_LocalScale.z", AnimationCurve.Constant(0f, 1.8f, 1f));

            if (isNew) AssetDatabase.CreateAsset(clip, path);
            else EditorUtility.SetDirty(clip);
            return clip;
        }

        private static AnimationClip CreateOrUpdateSlotIdleClip(string path)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            bool isNew = clip == null;
            if (isNew) clip = new AnimationClip { name = Path.GetFileNameWithoutExtension(path) };

            clip.SetCurve("", typeof(RectTransform), "m_LocalScale.x", AnimationCurve.Constant(0f, 1f, 1f));
            clip.SetCurve("", typeof(RectTransform), "m_LocalScale.y", AnimationCurve.Constant(0f, 1f, 1f));
            clip.SetCurve("", typeof(RectTransform), "m_LocalScale.z", AnimationCurve.Constant(0f, 1f, 1f));

            if (isNew) AssetDatabase.CreateAsset(clip, path);
            else EditorUtility.SetDirty(clip);
            return clip;
        }

        private static AnimationClip CreateOrUpdateSlotPopClip(string path)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            bool isNew = clip == null;
            if (isNew) clip = new AnimationClip { name = Path.GetFileNameWithoutExtension(path) };

            var popCurve = new AnimationCurve(
                new Keyframe(0.0f, 1.0f),
                new Keyframe(0.08f, 1.22f),
                new Keyframe(0.22f, 1.0f));

            clip.SetCurve("", typeof(RectTransform), "m_LocalScale.x", popCurve);
            clip.SetCurve("", typeof(RectTransform), "m_LocalScale.y", popCurve);
            clip.SetCurve("", typeof(RectTransform), "m_LocalScale.z", AnimationCurve.Constant(0f, 0.22f, 1f));

            if (isNew) AssetDatabase.CreateAsset(clip, path);
            else EditorUtility.SetDirty(clip);
            return clip;
        }

        private static AnimatorController CreateOrUpdateSingleStateController(string path, string stateName, AnimationClip clip)
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (ctrl != null) return ctrl;

            ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            var state = ctrl.layers[0].stateMachine.AddState(stateName);
            state.motion = clip;
            return ctrl;
        }

        private static AnimatorController CreateOrUpdateHotbarController(string path, AnimationClip idleClip, AnimationClip popClip)
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (ctrl != null) return ctrl;

            ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            ctrl.AddParameter("Pop", AnimatorControllerParameterType.Trigger);
            var sm = ctrl.layers[0].stateMachine;
            var idle = sm.AddState("Idle");
            idle.motion = idleClip;
            var pop = sm.AddState("Pop");
            pop.motion = popClip;

            var toPop = idle.AddTransition(pop);
            toPop.AddCondition(AnimatorConditionMode.If, 0f, "Pop");
            toPop.duration = 0.02f;
            toPop.hasExitTime = false;

            var toIdle = pop.AddTransition(idle);
            toIdle.hasExitTime = true;
            toIdle.exitTime = 0.95f;
            toIdle.duration = 0.05f;
            return ctrl;
        }

        private static void AttachAnimator(Transform target, AnimatorController controller)
        {
            if (target == null || controller == null) return;
            if (!target.gameObject.TryGetComponent<Animator>(out var anim))
                anim = target.gameObject.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;
            anim.updateMode = AnimatorUpdateMode.UnscaledTime;
        }

        // ── Procedural UI Sprite Generator ────────────────────

        private static void EnsureUISpritesGenerated()
        {
            Directory.CreateDirectory(UISpriteDir);

            BakePanelSprite($"{UISpriteDir}/Spr_UIPanelFrame.png", 64, 64);
            BakeBarSprite($"{UISpriteDir}/Spr_UIBarFill.png", 64, 32);
            BakeCrestSprite($"{UISpriteDir}/Spr_BadgeCrest.png", 64);
            BakeReticleSprite($"{UISpriteDir}/Spr_Reticle.png", 64);
            BakeArrowSprite($"{UISpriteDir}/Spr_MiniMapArrow.png", 64);
            BakeSkillIconSprite($"{UISpriteDir}/Spr_IconSword.png", 64, iconType: 0);
            BakeSkillIconSprite($"{UISpriteDir}/Spr_IconShield.png", 64, iconType: 1);
            BakeSkillIconSprite($"{UISpriteDir}/Spr_IconPotion.png", 64, iconType: 2);
            BakeSkillIconSprite($"{UISpriteDir}/Spr_IconEcho.png", 64, iconType: 3);

            AssetDatabase.Refresh();

            ConfigureSpriteImporter($"{UISpriteDir}/Spr_UIPanelFrame.png", new Vector4(10, 10, 10, 10));
            ConfigureSpriteImporter($"{UISpriteDir}/Spr_UIBarFill.png", Vector4.zero);
            ConfigureSpriteImporter($"{UISpriteDir}/Spr_BadgeCrest.png", Vector4.zero);
            ConfigureSpriteImporter($"{UISpriteDir}/Spr_Reticle.png", Vector4.zero);
            ConfigureSpriteImporter($"{UISpriteDir}/Spr_MiniMapArrow.png", Vector4.zero);
            ConfigureSpriteImporter($"{UISpriteDir}/Spr_IconSword.png", Vector4.zero);
            ConfigureSpriteImporter($"{UISpriteDir}/Spr_IconShield.png", Vector4.zero);
            ConfigureSpriteImporter($"{UISpriteDir}/Spr_IconPotion.png", Vector4.zero);
            ConfigureSpriteImporter($"{UISpriteDir}/Spr_IconEcho.png", Vector4.zero);
        }

        private static void BakePanelSprite(string path, int w, int h)
        {
            if (File.Exists(path)) return;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int edge = Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y));
                    Color c;
                    if (edge == 0)
                        c = new Color(0.05f, 0.08f, 0.12f, 0.0f);
                    else if (edge <= 2)
                        c = new Color(0.55f, 0.85f, 0.98f, 0.95f); // Solenne cyan-gold outer rim
                    else if (edge == 3)
                        c = new Color(0.18f, 0.30f, 0.44f, 0.95f);
                    else
                    {
                        float grad = (float)y / (h - 1);
                        c = Color.Lerp(new Color(0.75f, 0.82f, 0.92f, 0.96f), new Color(0.95f, 0.98f, 1.0f, 0.96f), grad);
                    }
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        private static void BakeBarSprite(string path, int w, int h)
        {
            if (File.Exists(path)) return;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
            {
                float ny = (float)y / (h - 1);
                float gloss = ny > 0.55f ? Mathf.Lerp(0.88f, 1.0f, (ny - 0.55f) / 0.45f) : Mathf.Lerp(0.68f, 0.92f, ny / 0.55f);
                for (int x = 0; x < w; x++)
                {
                    tex.SetPixel(x, y, new Color(gloss, gloss, gloss, 1f));
                }
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        private static void BakeCrestSprite(string path, int size)
        {
            if (File.Exists(path)) return;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float maxR = size * 0.46f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 d = new Vector2(x, y) - center;
                    float r = d.magnitude;
                    if (r > maxR)
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                    else if (r > maxR - 3.2f)
                    {
                        tex.SetPixel(x, y, new Color(1.0f, 0.92f, 0.55f, 1f));
                    }
                    else
                    {
                        float t = r / maxR;
                        Color fill = Color.Lerp(new Color(0.98f, 0.82f, 0.32f, 1f), new Color(0.75f, 0.52f, 0.14f, 1f), t);
                        tex.SetPixel(x, y, fill);
                    }
                }
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        private static void BakeReticleSprite(string path, int size)
        {
            if (File.Exists(path)) return;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x - c);
                    float dy = Mathf.Abs(y - c);
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float diamond = dx + dy;

                    bool centerDot = dist <= 2.4f;
                    bool ring = Mathf.Abs(diamond - 16f) <= 1.6f;
                    bool ticks = (dx <= 1.2f && dy >= 9f && dy <= 22f) || (dy <= 1.2f && dx >= 9f && dx <= 22f);

                    if (centerDot || ring || ticks)
                        tex.SetPixel(x, y, Color.white);
                    else
                        tex.SetPixel(x, y, Color.clear);
                }
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        private static void BakeArrowSprite(string path, int size)
        {
            if (File.Exists(path)) return;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float cx = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                float ny = (float)y / (size - 1); // 0 bottom .. 1 top
                for (int x = 0; x < size; x++)
                {
                    float nx = (x - cx) / (size * 0.5f); // -1 .. 1
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

        private static void BakeSkillIconSprite(string path, int size, int iconType)
        {
            if (File.Exists(path)) return;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - c) / c;
                    float dy = (y - c) / c;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    Color pixel = Color.clear;

                    if (iconType == 0) // Sword (Diagonal glowing blade)
                    {
                        float diag = Mathf.Abs(dx - dy);
                        float along = (dx + dy) * 0.5f;
                        if (diag < 0.14f && along > -0.55f && along < 0.75f)
                            pixel = new Color(0.85f, 0.95f, 1.0f, 1f);
                        else if (Mathf.Abs(along + 0.35f) < 0.09f && diag < 0.42f)
                            pixel = new Color(0.95f, 0.78f, 0.30f, 1f); // Crossguard
                    }
                    else if (iconType == 1) // Shield (Aether crest)
                    {
                        if (Mathf.Abs(dx) < 0.55f * (1f - Mathf.Max(0f, -dy * 0.65f)) && dy > -0.72f && dy < 0.62f)
                        {
                            pixel = r > 0.45f
                                ? new Color(0.95f, 0.80f, 0.32f, 1f)
                                : new Color(0.24f, 0.62f, 0.92f, 1f);
                        }
                    }
                    else if (iconType == 2) // Potion (Crimson/Emerald flask)
                    {
                        if (r < 0.52f && dy < 0.15f)
                            pixel = dy < -0.02f ? new Color(0.28f, 0.92f, 0.48f, 1f) : new Color(0.82f, 0.95f, 0.98f, 0.85f);
                        else if (Mathf.Abs(dx) < 0.18f && dy >= 0.12f && dy < 0.62f)
                            pixel = new Color(0.85f, 0.72f, 0.45f, 1f);
                    }
                    else // Echo Crystal (Cyan diamond)
                    {
                        float diamond = Mathf.Abs(dx * 1.3f) + Mathf.Abs(dy * 0.95f);
                        if (diamond < 0.72f)
                            pixel = Color.Lerp(new Color(0.75f, 1.0f, 1.0f, 1f), new Color(0.12f, 0.78f, 0.98f, 1f), diamond / 0.72f);
                    }

                    tex.SetPixel(x, y, pixel);
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

        // ── UI Element Construction Helpers ───────────────────

        private static RectTransform CreateSlicedPanel(
            string name, Transform parent,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPos, Vector2 sizeDelta,
            Sprite sprite, Color color)
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

        private static Image CreateUIFillBar(string name, RectTransform parent, Sprite barSprite, Color color, float fill)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(3f, 3f);
            rt.offsetMax = new Vector2(-3f, -3f);

            var img = go.AddComponent<Image>();
            img.sprite = barSprite;
            img.color = color;
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillAmount = fill;
            return img;
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
