using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections;
using System.Text;
using TMPro;

// ============================================================
//  HUDController — Complete runtime HUD & UI for Echoes of the Lost
//  Displays:
//    - Top-Left: HP bar, Stamina bar, Level/XP, Gold
//    - Top-Right: In-game Clock, Weather, Active Quest Tracker
//    - Bottom-Center: Interaction Prompt ([E] ...)
//    - Bottom Panel: Dialogue Box (Speaker + Typewriter text)
//    - Top-Center: Event Toast Notification (Quest / POI / Save)
//  Hotkeys:
//    - F5: Quick Save (Slot 1)
//    - F9: Quick Load (Slot 1)
// ============================================================
namespace EchoesOfTheLost.UI
{
    public class HUDController : Utils.Singleton<HUDController>
    {
        [Header("Top-Left (Player Status)")]
        [SerializeField] private Image _healthFill;
        [SerializeField] private TextMeshProUGUI _healthText;
        [SerializeField] private Image _staminaFill;
        [SerializeField] private TextMeshProUGUI _levelGoldText;

        [Header("Top-Right (World & Quest Tracker)")]
        [SerializeField] private TextMeshProUGUI _timeWeatherText;
        [SerializeField] private TextMeshProUGUI _questTrackerText;

        [Header("Bottom-Center (Interaction Prompt)")]
        [SerializeField] private GameObject _interactPromptRoot;
        [SerializeField] private TextMeshProUGUI _interactPromptText;

        [Header("Bottom (Dialogue Panel)")]
        [SerializeField] private GameObject _dialoguePanelRoot;
        [SerializeField] private TextMeshProUGUI _dialogueSpeakerText;
        [SerializeField] private TextMeshProUGUI _dialogueBodyText;

        [Header("Top-Center (Toast Banner)")]
        [SerializeField] private GameObject _toastRoot;
        [SerializeField] private TextMeshProUGUI _toastText;

        private Characters.Player.PlayerStats _playerStats;
        private Characters.Player.InteractionSystem _interactionSystem;
        private Coroutine _toastCoroutine;

        protected override void Awake()
        {
            base.Awake();
            Core.ServiceLocator.Register<HUDController>(this);
        }

        private void Start()
        {
            var playerGO = GameObject.FindGameObjectWithTag("Player");
            if (playerGO != null)
            {
                _playerStats = playerGO.GetComponent<Characters.Player.PlayerStats>();
                _interactionSystem = playerGO.GetComponent<Characters.Player.InteractionSystem>();
                if (_interactionSystem != null)
                {
                    _interactionSystem.OnTargetFound += ShowInteractionPrompt;
                    _interactionSystem.OnTargetLost += HideInteractionPrompt;
                }
            }

            // Subscribe to DialogueManager
            var dm = Systems.Dialogue.DialogueManager.Instance;
            if (dm != null)
            {
                dm.OnLineReady += OnDialogueLineReady;
                dm.OnTypewriterTick += OnDialogueTypewriterTick;
                dm.OnDialogueEnded += OnDialogueEnded;
            }

            // Subscribe to global events for toast notifications
            Core.EventBus.Subscribe<Core.QuestStartedEvent>(OnQuestStarted);
            Core.EventBus.Subscribe<Core.QuestCompletedEvent>(OnQuestCompleted);
            Core.EventBus.Subscribe<Core.PlayerLevelUpEvent>(OnPlayerLevelUp);
            Core.EventBus.Subscribe<Core.ItemPickedUpEvent>(OnItemPickedUp);

            if (Core.ServiceLocator.TryGet<World.POIManager>(out var poiMgr))
                poiMgr.OnPOIDiscovered += OnPOIDiscovered;

            HideInteractionPrompt();
            if (_dialoguePanelRoot != null) _dialoguePanelRoot.SetActive(false);
            if (_toastRoot != null) _toastRoot.SetActive(false);

            ShowToast("Welcome to Verda Wilds! Talk to Elder Maren [E] | Attack [LMB] | Save [F5]");
        }

        protected override void OnDestroy()
        {
            if (_interactionSystem != null)
            {
                _interactionSystem.OnTargetFound -= ShowInteractionPrompt;
                _interactionSystem.OnTargetLost -= HideInteractionPrompt;
            }

            var dm = Systems.Dialogue.DialogueManager.Instance;
            if (dm != null)
            {
                dm.OnLineReady -= OnDialogueLineReady;
                dm.OnTypewriterTick -= OnDialogueTypewriterTick;
                dm.OnDialogueEnded -= OnDialogueEnded;
            }

            Core.EventBus.Unsubscribe<Core.QuestStartedEvent>(OnQuestStarted);
            Core.EventBus.Unsubscribe<Core.QuestCompletedEvent>(OnQuestCompleted);
            Core.EventBus.Unsubscribe<Core.PlayerLevelUpEvent>(OnPlayerLevelUp);
            Core.EventBus.Unsubscribe<Core.ItemPickedUpEvent>(OnItemPickedUp);

            if (Core.ServiceLocator.TryGet<World.POIManager>(out var poiMgr))
                poiMgr.OnPOIDiscovered -= OnPOIDiscovered;

            base.OnDestroy();
        }

        private void Update()
        {
            UpdatePlayerStatusUI();
            UpdateWorldAndQuestUI();
            HandleQuickSaveLoadHotkeys();
        }

        // ── Status & Quest Updates ────────────────────────────

        private void UpdatePlayerStatusUI()
        {
            if (_playerStats == null) return;

            if (_healthFill != null) _healthFill.fillAmount = _playerStats.HealthPercent;
            if (_healthText != null)
                _healthText.text = $"HP {Mathf.CeilToInt(_playerStats.CurrentHealth)} / {Mathf.CeilToInt(_playerStats.MaxHealth.FinalValue)}";

            if (_staminaFill != null) _staminaFill.fillAmount = _playerStats.StaminaPercent;

            if (_levelGoldText != null)
            {
                float gold = Core.ServiceLocator.TryGet<Systems.Economy.EconomySystem>(out var econ) ? econ.PlayerGold : 0f;
                _levelGoldText.text = $"LV {_playerStats.Level} (XP {_playerStats.CurrentXP})   |   Gold: {gold:0}";
            }
        }

        private void UpdateWorldAndQuestUI()
        {
            if (_timeWeatherText != null)
            {
                string timeStr = Core.ServiceLocator.TryGet<World.TimeSystem>(out var ts) ? ts.FormattedTime : "08:00 AM";
                string weatherStr = Core.ServiceLocator.TryGet<World.WeatherSystem>(out var ws) ? ws.CurrentWeather.ToString() : "Clear";
                _timeWeatherText.text = $"{timeStr}  |  {weatherStr}\nVerda Wilds";
            }

            if (_questTrackerText != null && Core.ServiceLocator.TryGet<Systems.Quest.QuestManager>(out var qm))
            {
                if (qm.ActiveQuests.Count == 0)
                {
                    _questTrackerText.text = qm.CompletedQuests.Count > 0
                        ? "<color=#88FFAA>All Quests Completed!</color>\nReturn to Elder Maren"
                        : "<color=#FFD966>Objective:</color>\nSpeak with Elder Maren near the Ruins";
                }
                else
                {
                    var sb = new StringBuilder();
                    foreach (var q in qm.ActiveQuests)
                    {
                        sb.AppendLine($"<color=#FFD966>[{q.title}]</color>");
                        var objs = qm.GetActiveObjectives(q.questId);
                        if (objs != null)
                        {
                            foreach (var o in objs)
                            {
                                string mark = o.IsCompleted ? "<color=#7CFC00>[Done]</color>" : $"({o.currentCount}/{o.requiredCount})";
                                sb.AppendLine($" • {o.description} {mark}");
                            }
                        }
                    }
                    _questTrackerText.text = sb.ToString().TrimEnd();
                }
            }
        }

        private void HandleQuickSaveLoadHotkeys()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.f5Key.wasPressedThisFrame)
            {
                if (Systems.SaveSystem.Instance != null && Systems.SaveSystem.Instance.SaveGame(1))
                    ShowToast("Quick Saved to Slot 1 [F5]");
            }
            else if (kb.f9Key.wasPressedThisFrame)
            {
                if (Systems.SaveSystem.Instance != null && Systems.SaveSystem.Instance.LoadGame(1) != null)
                    ShowToast("Quick Loaded Slot 1 [F9]");
            }
        }

        // ── Interaction Prompt ───────────────────────────────

        private void ShowInteractionPrompt(string prompt)
        {
            if (_interactPromptRoot != null) _interactPromptRoot.SetActive(true);
            if (_interactPromptText != null) _interactPromptText.text = prompt;
        }

        private void HideInteractionPrompt()
        {
            if (_interactPromptRoot != null) _interactPromptRoot.SetActive(false);
        }

        // ── Dialogue UI ──────────────────────────────────────

        private void OnDialogueLineReady(Systems.Dialogue.DialogueLine line)
        {
            HideInteractionPrompt();
            if (_dialoguePanelRoot != null) _dialoguePanelRoot.SetActive(true);
            if (_dialogueSpeakerText != null) _dialogueSpeakerText.text = line.speaker;
            if (_dialogueBodyText != null) _dialogueBodyText.text = "";
        }

        private void OnDialogueTypewriterTick(string textSoFar)
        {
            if (_dialogueBodyText != null)
                _dialogueBodyText.text = textSoFar + "\n<size=70%><color=#AAAAAA>[E / Space / Click to continue]</color></size>";
        }

        private void OnDialogueEnded()
        {
            if (_dialoguePanelRoot != null) _dialoguePanelRoot.SetActive(false);
        }

        // ── Toast Notifications ──────────────────────────────

        public void ShowToast(string message, float duration = 3.5f)
        {
            if (_toastRoot == null || _toastText == null) return;
            if (_toastCoroutine != null) StopCoroutine(_toastCoroutine);
            _toastCoroutine = StartCoroutine(ToastRoutine(message, duration));
        }

        private IEnumerator ToastRoutine(string message, float duration)
        {
            _toastRoot.SetActive(true);
            _toastText.text = message;

            var cg = _toastRoot.GetComponent<CanvasGroup>();
            var rt = _toastRoot.GetComponent<RectTransform>();
            Vector2 basePos = rt != null ? new Vector2(0f, -64f) : Vector2.zero;

            // Slide-in & fade-in animation (0.25s)
            float elapsed = 0f;
            while (elapsed < 0.25f)
            {
                elapsed += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0f, 1f, elapsed / 0.25f);
                if (cg != null) cg.alpha = k;
                if (rt != null) rt.anchoredPosition = Vector2.Lerp(basePos + new Vector2(0f, 28f), basePos, k);
                yield return null;
            }
            if (cg != null) cg.alpha = 1f;
            if (rt != null) rt.anchoredPosition = basePos;

            yield return new WaitForSecondsRealtime(Mathf.Max(0.2f, duration - 0.5f));

            // Fade-out animation (0.25s)
            elapsed = 0f;
            while (elapsed < 0.25f)
            {
                elapsed += Time.unscaledDeltaTime;
                float k = 1f - Mathf.SmoothStep(0f, 1f, elapsed / 0.25f);
                if (cg != null) cg.alpha = k;
                yield return null;
            }

            _toastRoot.SetActive(false);
        }

        private void OnQuestStarted(Core.QuestStartedEvent e) =>
            ShowToast($"Quest Started: {e.QuestId}");

        private void OnQuestCompleted(Core.QuestCompletedEvent e) =>
            ShowToast($"Quest Completed! Rewards Granted ({e.QuestId})", 4.5f);

        private void OnPlayerLevelUp(Core.PlayerLevelUpEvent e) =>
            ShowToast($"LEVEL UP! Reached Level {e.NewLevel}", 4f);

        private void OnItemPickedUp(Core.ItemPickedUpEvent e) =>
            ShowToast($"Recorded: {e.ItemId} (+{e.Quantity})");

        private void OnPOIDiscovered(World.PointOfInterest poi) =>
            ShowToast($"Discovered Location: {poi.displayName} (+50 XP)");
    }
}
