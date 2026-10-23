using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// ============================================================
//  QuestManager — Manages all quest states
//  Attach to: World_Persistent scene
// ============================================================
namespace EchoesOfTheLost.Systems.Quest
{
    public class QuestManager : Utils.Singleton<QuestManager>
    {
        [Header("Quest Database")]
        [SerializeField] private QuestData[] _allQuests;

        private Dictionary<string, QuestState> _questStates = new Dictionary<string, QuestState>();
        private Dictionary<string, QuestObjective[]> _activeObjectives = new Dictionary<string, QuestObjective[]>();
        private List<QuestData> _activeQuests = new List<QuestData>();
        private List<QuestData> _completedQuests = new List<QuestData>();

        public IReadOnlyList<QuestData> ActiveQuests => _activeQuests;
        public IReadOnlyList<QuestData> CompletedQuests => _completedQuests;

        protected override void Awake()
        {
            base.Awake();
            Core.ServiceLocator.Register<QuestManager>(this);

            // Subscribe to game events that update objectives
            Core.EventBus.Subscribe<Core.EnemyDiedEvent>(OnEnemyDied);
            Core.EventBus.Subscribe<Core.ItemPickedUpEvent>(OnItemPickedUp);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            Core.EventBus.Unsubscribe<Core.EnemyDiedEvent>(OnEnemyDied);
            Core.EventBus.Unsubscribe<Core.ItemPickedUpEvent>(OnItemPickedUp);
        }

        // ── Public API ──────────────────────────────────────

        /// <summary>Start a quest by ID. Returns false if prerequisites not met.</summary>
        public bool StartQuest(string questId)
        {
            var quest = GetQuestData(questId);
            if (quest == null) return false;
            if (_questStates.TryGetValue(questId, out var state) && state != QuestState.NotStarted)
                return false;
            if (!ArePrerequisitesMet(quest)) return false;

            _questStates[questId] = QuestState.Active;
            _activeQuests.Add(quest);

            // Deep copy objectives
            _activeObjectives[questId] = System.Array.ConvertAll(quest.objectives, o =>
                new QuestObjective
                {
                    objectiveId = o.objectiveId,
                    description = o.description,
                    type = o.type,
                    targetId = o.targetId,
                    requiredCount = o.requiredCount,
                    currentCount = 0
                });

            Core.EventBus.Publish(new Core.QuestStartedEvent { QuestId = questId });
            Debug.Log($"[QuestManager] Started: {quest.title}");
            return true;
        }

        /// <summary>Progress an objective for an active quest.</summary>
        public void UpdateObjective(string questId, string targetId, int count = 1)
        {
            if (!_activeObjectives.TryGetValue(questId, out var objectives)) return;

            foreach (var obj in objectives)
            {
                if (obj.targetId == targetId && !obj.IsCompleted)
                {
                    obj.currentCount = Mathf.Min(obj.currentCount + count, obj.requiredCount);
                    CheckQuestCompletion(questId);
                    return;
                }
            }
        }

        public QuestState GetQuestState(string questId) =>
            _questStates.TryGetValue(questId, out var s) ? s : QuestState.NotStarted;

        public bool IsQuestCompleted(string questId) =>
            GetQuestState(questId) == QuestState.Completed;

        public QuestObjective[] GetActiveObjectives(string questId) =>
            _activeObjectives.TryGetValue(questId, out var o) ? o : null;

        // ── Private ─────────────────────────────────────────

        private void CheckQuestCompletion(string questId)
        {
            if (!_activeObjectives.TryGetValue(questId, out var objectives)) return;
            if (!objectives.All(o => o.IsCompleted)) return;

            var quest = GetQuestData(questId);
            _questStates[questId] = QuestState.Completed;
            _activeQuests.Remove(quest);
            _completedQuests.Add(quest);
            _activeObjectives.Remove(questId);

            // Grant rewards
            GrantRewards(quest.reward);

            // Unlock follow-up quests
            if (quest.reward?.unlockQuestIds != null)
                foreach (var id in quest.reward.unlockQuestIds)
                    StartQuest(id);

            Core.EventBus.Publish(new Core.QuestCompletedEvent { QuestId = questId });
            Debug.Log($"[QuestManager] Completed: {quest.title}");
        }

        private void GrantRewards(QuestReward reward)
        {
            if (reward == null) return;

            var playerGO = GameObject.FindGameObjectWithTag("Player");
            if (playerGO != null && reward.experiencePoints > 0)
                playerGO.GetComponent<Characters.Player.PlayerStats>()?.AddXP(reward.experiencePoints);

            if (reward.goldAmount > 0f && Core.ServiceLocator.TryGet<Systems.Economy.EconomySystem>(out var econ))
                econ.AddGold(reward.goldAmount);

            Debug.Log($"[QuestManager] Granted {reward.experiencePoints} XP, {reward.goldAmount} Gold");
        }

        private bool ArePrerequisitesMet(QuestData quest)
        {
            if (quest.prerequisiteQuestIds == null) return true;
            foreach (var prereq in quest.prerequisiteQuestIds)
                if (!IsQuestCompleted(prereq)) return false;
            return true;
        }

        private QuestData GetQuestData(string id) =>
            System.Array.Find(_allQuests, q => q.questId == id);

        // ── Event Handlers ───────────────────────────────────
        private void OnEnemyDied(Core.EnemyDiedEvent e)
        {
            foreach (var quest in _activeQuests)
                foreach (var obj in _activeObjectives[quest.questId])
                    if (obj.type == ObjectiveType.KillEnemy && obj.targetId == e.EnemyId)
                        UpdateObjective(quest.questId, e.EnemyId);
        }

        private void OnItemPickedUp(Core.ItemPickedUpEvent e)
        {
            foreach (var quest in _activeQuests)
                foreach (var obj in _activeObjectives[quest.questId])
                    if (obj.type == ObjectiveType.CollectItem && obj.targetId == e.ItemId)
                        UpdateObjective(quest.questId, e.ItemId, e.Quantity);
        }
    }
}
