using UnityEngine;
using System.Collections.Generic;

// ============================================================
//  QuestData — ScriptableObject for quest definition
// ============================================================
namespace EchoesOfTheLost.Systems.Quest
{
    public enum QuestType { Main, Side, World, Repeatable, Hidden }
    public enum QuestState { NotStarted, Active, Completed, Failed }
    public enum ObjectiveType { KillEnemy, CollectItem, ReachLocation, TalkToNPC, SurviveTime, Escort }

    [System.Serializable]
    public class QuestObjective
    {
        public string objectiveId;
        public string description;
        public ObjectiveType type;
        public string targetId; // enemy ID, item ID, NPC ID, etc.
        public int requiredCount = 1;
        [System.NonSerialized] public int currentCount = 0;
        public bool IsCompleted => currentCount >= requiredCount;
        public Vector3 markerPosition; // optional world marker
    }

    [System.Serializable]
    public class QuestReward
    {
        public int experiencePoints;
        public float goldAmount;
        public string[] itemIds;
        public string[] unlockQuestIds; // quests to unlock on completion
        [System.NonSerialized] public Dictionary<string, int> reputationChanges; // faction → delta
    }

    [CreateAssetMenu(fileName = "Quest_New", menuName = "EotL/Quest/Quest Data")]
    public class QuestData : ScriptableObject
    {
        [Header("Identity")]
        public string questId;
        public string title;
        [TextArea(3, 6)] public string description;
        public Sprite questIcon;

        [Header("Type & Requirements")]
        public QuestType questType = QuestType.Side;
        public int recommendedLevel = 1;
        public string[] prerequisiteQuestIds;

        [Header("Objectives")]
        public QuestObjective[] objectives;

        [Header("Dialogue")]
        public string questGiverNpcId;
        public string questCompleteDialogueId;
        [TextArea(2, 4)] public string activeHint;

        [Header("Reward")]
        public QuestReward reward;

        [Header("Flags")]
        public bool isMainQuest;
        public bool showOnMap = true;
        public bool trackByDefault = true;
    }
}
