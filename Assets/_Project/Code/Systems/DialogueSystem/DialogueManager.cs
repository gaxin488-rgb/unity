using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// ============================================================
//  DialogueManager — Narrative & dialogue orchestrator
//  Supports both direct string[] dialogue lines AND Ink JSON assets.
//  Attach to: World_Persistent / Sandbox scene
// ============================================================
namespace EchoesOfTheLost.Systems.Dialogue
{
    [System.Serializable]
    public class DialogueLine
    {
        public string speaker;
        public string text;
        public string portraitKey;
    }

    [System.Serializable]
    public class DialogueChoice
    {
        public int index;
        public string text;
        public bool isAvailable;
    }

    public class DialogueManager : Utils.Singleton<DialogueManager>
    {
        [Header("Settings")]
        [SerializeField] private float _typewriterSpeed = 0.025f;  // seconds per char

        // ── Events ────────────────────────────────────────────
        public event System.Action<DialogueLine> OnLineReady;
        public event System.Action<List<DialogueChoice>> OnChoicesReady;
        public event System.Action OnDialogueEnded;
        public event System.Action<string> OnTypewriterTick;

        // ── State ─────────────────────────────────────────────
        private bool _isInDialogue;
        private bool _isTyping;
        private string _currentNpcId;
        private string _currentSpeaker;
        private Queue<string> _lineQueue = new Queue<string>();
        private string _currentFullLine = "";
        private Coroutine _typewriterCoroutine;

        public bool IsInDialogue => _isInDialogue;

        protected override void Awake()
        {
            base.Awake();
            Core.ServiceLocator.Register<DialogueManager>(this);
        }

        private void OnEnable()
        {
            if (Core.InputManager.Instance != null)
                Core.InputManager.Instance.OnInteractPressed += HandleAdvanceInput;
        }

        private void OnDisable()
        {
            if (Core.InputManager.Instance != null)
                Core.InputManager.Instance.OnInteractPressed -= HandleAdvanceInput;
        }

        private void Update()
        {
            if (!_isInDialogue) return;

            // Allow Space / Enter / Left Click to advance dialogue even while player action map is disabled
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if ((kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame)) ||
                (mouse != null && mouse.leftButton.wasPressedThisFrame))
            {
                AdvanceDialogue();
            }
        }

        private void HandleAdvanceInput()
        {
            if (_isInDialogue) AdvanceDialogue();
        }

        // ── Public API ──────────────────────────────────────

        /// <summary>Start a multi-line dialogue sequence directly from string array.</summary>
        public void StartDialogue(string npcId, string speakerName, string[] lines)
        {
            if (_isInDialogue || lines == null || lines.Length == 0) return;

            _isInDialogue = true;
            _currentNpcId = npcId;
            _currentSpeaker = string.IsNullOrEmpty(speakerName) ? npcId : speakerName;

            _lineQueue.Clear();
            foreach (var l in lines)
            {
                if (!string.IsNullOrWhiteSpace(l))
                    _lineQueue.Enqueue(l.Trim());
            }

            Core.InputManager.Instance?.DisablePlayerInput();
            Core.GameManager.Instance?.SetGameState(Core.GameState.Dialogue);
            Core.EventBus.Publish(new Core.DialogueStartedEvent { NpcId = npcId });

            ShowNextQueuedLine();
        }

        /// <summary>Start dialogue with an NPC using their Ink JSON or text asset.</summary>
        public void StartDialogue(string npcId, TextAsset inkJson)
        {
            if (inkJson == null)
            {
                StartDialogue(npcId, npcId, new[] { "..." });
                return;
            }

            var rawLines = inkJson.text.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
            StartDialogue(npcId, npcId, rawLines);
        }

        /// <summary>Call when player taps/clicks to advance.</summary>
        public void AdvanceDialogue()
        {
            if (!_isInDialogue) return;

            if (_isTyping)
            {
                SkipTypewriter();
                return;
            }

            ShowNextQueuedLine();
        }

        public void SelectChoice(int choiceIndex)
        {
            OnChoicesReady?.Invoke(null);
            ShowNextQueuedLine();
        }

        public void EndDialogue()
        {
            if (!_isInDialogue) return;
            if (_typewriterCoroutine != null) StopCoroutine(_typewriterCoroutine);

            _isInDialogue = false;
            _isTyping = false;
            _lineQueue.Clear();

            Core.InputManager.Instance?.EnablePlayerInput();
            Core.GameManager.Instance?.SetGameState(Core.GameState.Playing);

            Core.EventBus.Publish(new Core.DialogueEndedEvent { NpcId = _currentNpcId });
            OnDialogueEnded?.Invoke();
        }

        // ── Internal Flow ────────────────────────────────────

        private void ShowNextQueuedLine()
        {
            if (_lineQueue.Count == 0)
            {
                EndDialogue();
                return;
            }

            _currentFullLine = _lineQueue.Dequeue();
            var line = new DialogueLine
            {
                speaker = _currentSpeaker,
                text = _currentFullLine,
                portraitKey = ""
            };

            if (_typewriterCoroutine != null) StopCoroutine(_typewriterCoroutine);
            _typewriterCoroutine = StartCoroutine(TypewriterEffect(line));
        }

        private IEnumerator TypewriterEffect(DialogueLine line)
        {
            _isTyping = true;
            OnLineReady?.Invoke(new DialogueLine
            {
                speaker = line.speaker,
                text = "",
                portraitKey = line.portraitKey
            });

            string displayed = "";
            foreach (char c in line.text)
            {
                displayed += c;
                OnTypewriterTick?.Invoke(displayed);
                yield return new WaitForSecondsRealtime(_typewriterSpeed);
            }

            _isTyping = false;
        }

        private void SkipTypewriter()
        {
            if (_typewriterCoroutine != null) StopCoroutine(_typewriterCoroutine);
            _isTyping = false;
            OnTypewriterTick?.Invoke(_currentFullLine);
        }
    }
}
