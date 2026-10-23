using UnityEngine;

// ============================================================
//  NPCController — Base class for all NPCs (schedule, dialogue, quest giver)
//  Attach to: NPC root GameObject
// ============================================================
namespace EchoesOfTheLost.Characters.NPCs
{
    public enum NPCScheduleSlot { EarlyMorning, Morning, Afternoon, Evening, Night, LateNight }

    [System.Serializable]
    public class NPCScheduleEntry
    {
        public NPCScheduleSlot timeSlot;
        public Transform destination;
        public string animationState;  // "Idle", "Sitting", "Working", "Walking"
        [TextArea(1, 2)] public string activityDescription;
    }

    [RequireComponent(typeof(UnityEngine.AI.NavMeshAgent))]
    [RequireComponent(typeof(Animator))]
    public class NPCController : MonoBehaviour, Characters.Player.IInteractable
    {
        [Header("NPC Info")]
        [SerializeField] protected string _npcId = "npc_elder_maren";
        [SerializeField] protected string _displayName = "Elder Maren";
        [SerializeField] protected Sprite _portrait;
        [SerializeField] protected TextAsset _inkDialogueFile;
        [SerializeField] [TextArea(2, 4)] protected string[] _dialogueLines = new[]
        {
            "Welcome to the Verda Wilds, traveler. These Solenne ruins have slumbered for a thousand years.",
            "Examine the glowing Echo Crystals nearby and clear out the Overgrown Guardians to uncover what happened here."
        };

        [Header("Quest Giver")]
        [SerializeField] protected string _questToOfferId = "quest_first_echo";
        [SerializeField] [TextArea(2, 3)] protected string[] _questActiveLines = new[]
        {
            "The Echo Crystals still hum with ancient memories. Examine both crystals and defeat a Guardian."
        };
        [SerializeField] [TextArea(2, 3)] protected string[] _questCompletedLines = new[]
        {
            "Incredible... So the Unraveling was triggered from the Citadel itself. Thank you, Kael."
        };

        [Header("Schedule")]
        [SerializeField] protected NPCScheduleEntry[] _schedule;
        [SerializeField] protected bool _hasSchedule = true;

        [Header("Interaction")]
        [SerializeField] protected float _interactRadius = 2.5f;
        [SerializeField] protected bool _facesPlayerOnInteract = true;

        [Header("Relationship")]
        [SerializeField] protected int _relationshipLevel = 0; // -100 to 100

        // ── Components ────────────────────────────────────────
        protected UnityEngine.AI.NavMeshAgent _agent;
        protected Animator _animator;

        // ── State ─────────────────────────────────────────────
        protected Transform _playerTransform;
        protected bool _isInteracting;
        protected NPCScheduleEntry _currentActivity;

        // ── Animator Hashes ──────────────────────────────────
        private static readonly int _animSpeed = Animator.StringToHash("Speed");

        public string NpcId => _npcId;
        public string DisplayName => _displayName;
        public Sprite Portrait => _portrait;
        public int RelationshipLevel => _relationshipLevel;

        protected virtual void Awake()
        {
            _agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            _animator = GetComponent<Animator>();

            var playerGO = GameObject.FindGameObjectWithTag("Player");
            if (playerGO != null) _playerTransform = playerGO.transform;

            Core.EventBus.Subscribe<Core.DaytimeChangedEvent>(OnTimeChanged);
        }

        protected virtual void OnDestroy()
        {
            Core.EventBus.Unsubscribe<Core.DaytimeChangedEvent>(OnTimeChanged);
        }

        protected virtual void Start()
        {
            if (_hasSchedule && Core.ServiceLocator.TryGet<World.TimeSystem>(out var time))
                OnTimeChanged(new Core.DaytimeChangedEvent { Hour = time.CurrentHour });
        }

        protected virtual void Update()
        {
            if (_isInteracting) return;
            UpdateAnimator();
        }

        // ── Schedule ─────────────────────────────────────────

        private void OnTimeChanged(Core.DaytimeChangedEvent e)
        {
            if (!_hasSchedule || _schedule == null) return;

            var slot = GetCurrentSlot(e.Hour);
            var entry = GetScheduleEntry(slot);

            if (entry != null && entry != _currentActivity)
            {
                _currentActivity = entry;
                ExecuteActivity(entry);
            }
        }

        private void ExecuteActivity(NPCScheduleEntry entry)
        {
            if (entry.destination != null && _agent != null && _agent.isOnNavMesh)
                _agent.SetDestination(entry.destination.position);
        }

        private NPCScheduleSlot GetCurrentSlot(float hour) => hour switch
        {
            < 6f => NPCScheduleSlot.LateNight,
            < 8f => NPCScheduleSlot.EarlyMorning,
            < 12f => NPCScheduleSlot.Morning,
            < 17f => NPCScheduleSlot.Afternoon,
            < 20f => NPCScheduleSlot.Evening,
            _ => NPCScheduleSlot.Night
        };

        private NPCScheduleEntry GetScheduleEntry(NPCScheduleSlot slot)
        {
            if (_schedule == null) return null;
            foreach (var e in _schedule)
                if (e.timeSlot == slot) return e;
            return null;
        }

        // ── Interaction (IInteractable) ───────────────────────

        public virtual void Interact(Characters.Player.PlayerController player)
        {
            if (_isInteracting) return;

            if (_facesPlayerOnInteract && player != null)
            {
                Vector3 lookTarget = new Vector3(player.transform.position.x, transform.position.y, player.transform.position.z);
                transform.LookAt(lookTarget);
            }

            _isInteracting = true;
            if (_agent != null && _agent.isOnNavMesh)
                _agent.ResetPath();

            // Select dialogue lines based on quest state
            string[] linesToPlay = _dialogueLines;
            var qm = Core.ServiceLocator.TryGet<Systems.Quest.QuestManager>(out var questMgr) ? questMgr : null;

            if (qm != null && !string.IsNullOrEmpty(_questToOfferId))
            {
                var qState = qm.GetQuestState(_questToOfferId);
                if (qState == Systems.Quest.QuestState.Completed && _questCompletedLines != null && _questCompletedLines.Length > 0)
                    linesToPlay = _questCompletedLines;
                else if (qState == Systems.Quest.QuestState.Active && _questActiveLines != null && _questActiveLines.Length > 0)
                    linesToPlay = _questActiveLines;
            }

            var dm = Systems.Dialogue.DialogueManager.Instance;
            if (dm != null)
            {
                dm.OnDialogueEnded += OnDialogueEnded;
                if (_inkDialogueFile != null)
                    dm.StartDialogue(_npcId, _inkDialogueFile);
                else
                    dm.StartDialogue(_npcId, _displayName, linesToPlay);
            }
            else
            {
                OnDialogueEnded();
            }

            // Progress any "TalkToNPC" objective targeting this NPC
            if (qm != null)
            {
                foreach (var activeQuest in qm.ActiveQuests)
                    qm.UpdateObjective(activeQuest.questId, _npcId, 1);
            }
        }

        private void OnDialogueEnded()
        {
            _isInteracting = false;
            if (Systems.Dialogue.DialogueManager.Instance != null)
                Systems.Dialogue.DialogueManager.Instance.OnDialogueEnded -= OnDialogueEnded;

            // Offer quest if not started yet
            if (!string.IsNullOrEmpty(_questToOfferId) &&
                Core.ServiceLocator.TryGet<Systems.Quest.QuestManager>(out var qm) &&
                qm.GetQuestState(_questToOfferId) == Systems.Quest.QuestState.NotStarted)
            {
                qm.StartQuest(_questToOfferId);
            }
        }

        public virtual string GetInteractionPrompt() => $"[E] Talk to {_displayName}";

        // ── Relationship ─────────────────────────────────────

        public void ChangeRelationship(int delta)
        {
            _relationshipLevel = Mathf.Clamp(_relationshipLevel + delta, -100, 100);
        }

        // ── Animator ─────────────────────────────────────────
        private void UpdateAnimator()
        {
            float speed = (_agent != null && _agent.isOnNavMesh) ? _agent.velocity.magnitude : 0f;
            _animator.SetFloat(_animSpeed, speed);
        }

        // ── Gizmos ────────────────────────────────────────────
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 1f, 1f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, _interactRadius);
        }
    }
}
