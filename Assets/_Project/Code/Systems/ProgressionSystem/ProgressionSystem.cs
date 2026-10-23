using UnityEngine;

// ============================================================
//  ProgressionSystem — Level, Skill Tree, Perks, Reputation
//  Attach to: Player GameObject
// ============================================================
namespace EchoesOfTheLost.Systems.Progression
{
    public enum SkillTreeBranch { Combat, Traversal, Scholar }

    [System.Serializable]
    public class SkillNode
    {
        public string skillId;
        public string displayName;
        public string description;
        public SkillTreeBranch branch;
        public int tier;                      // 1=basic, 2=mid, 3=advanced
        public string[] prerequisiteSkillIds;
        public int skillPointCost = 1;
        public bool isUnlocked;
        public Sprite icon;
    }

    [System.Serializable]
    public class ReputationEntry
    {
        public string factionId;
        public string factionName;
        [Range(-100, 100)] public int value;

        public string Tier => value switch
        {
            >= 75 => "Champion",
            >= 40 => "Ally",
            >= 10 => "Friendly",
            >= -10 => "Neutral",
            >= -40 => "Unfriendly",
            >= -75 => "Hostile",
            _ => "Enemy"
        };
    }

    [CreateAssetMenu(fileName = "SkillTree_New", menuName = "EotL/Progression/Skill Tree")]
    public class SkillTreeData : ScriptableObject
    {
        public SkillNode[] combatNodes;
        public SkillNode[] traversalNodes;
        public SkillNode[] scholarNodes;

        public SkillNode GetNode(string skillId)
        {
            foreach (var n in combatNodes) if (n.skillId == skillId) return n;
            foreach (var n in traversalNodes) if (n.skillId == skillId) return n;
            foreach (var n in scholarNodes) if (n.skillId == skillId) return n;
            return null;
        }
    }

    public class ProgressionSystem : MonoBehaviour
    {
        [Header("Skill Tree")]
        [SerializeField] private SkillTreeData _skillTree;

        [Header("Skill Points")]
        [SerializeField] private int _skillPointsPerLevel = 1;
        private int _availableSkillPoints = 0;

        [Header("Factions")]
        [SerializeField] private ReputationEntry[] _factions = new[]
        {
            new ReputationEntry { factionId = "accord", factionName = "The Accord", value = 0 },
            new ReputationEntry { factionId = "void_seekers", factionName = "Void Seekers", value = -20 },
            new ReputationEntry { factionId = "drifters", factionName = "The Drifters", value = 0 }
        };

        // ── Events ────────────────────────────────────────────
        public event System.Action<SkillNode> OnSkillUnlocked;
        public event System.Action<string, int> OnReputationChanged; // factionId, newValue
        public event System.Action<int> OnSkillPointsChanged;

        public int AvailableSkillPoints => _availableSkillPoints;

        private void Awake()
        {
            // Subscribe to level up event
            Core.EventBus.Subscribe<Core.PlayerLevelUpEvent>(OnPlayerLevelUp);
            Core.ServiceLocator.Register<ProgressionSystem>(this);
        }

        private void OnDestroy()
        {
            Core.EventBus.Unsubscribe<Core.PlayerLevelUpEvent>(OnPlayerLevelUp);
        }

        // ── Skill Tree ────────────────────────────────────────

        /// <summary>Attempt to unlock a skill. Returns false if prerequisites not met.</summary>
        public bool UnlockSkill(string skillId)
        {
            var node = _skillTree?.GetNode(skillId);
            if (node == null || node.isUnlocked) return false;
            if (_availableSkillPoints < node.skillPointCost) return false;
            if (!ArePrerequisitesMet(node)) return false;

            node.isUnlocked = true;
            _availableSkillPoints -= node.skillPointCost;

            OnSkillUnlocked?.Invoke(node);
            OnSkillPointsChanged?.Invoke(_availableSkillPoints);
            ApplySkillEffect(node);

            Debug.Log($"[Progression] Skill unlocked: {node.displayName}");
            return true;
        }

        public bool IsSkillUnlocked(string skillId) =>
            _skillTree?.GetNode(skillId)?.isUnlocked ?? false;

        private bool ArePrerequisitesMet(SkillNode node)
        {
            if (node.prerequisiteSkillIds == null) return true;
            foreach (var prereqId in node.prerequisiteSkillIds)
                if (!IsSkillUnlocked(prereqId)) return false;
            return true;
        }

        private void ApplySkillEffect(SkillNode node)
        {
            // Apply stat bonuses based on skill ID
            var stats = GetComponent<Characters.Player.PlayerStats>();
            if (stats == null) return;

            // Example skill effects — expand per skill
            switch (node.skillId)
            {
                case "combat_heavy_strikes":
                    stats.AttackDamage.AddModifier(5f);
                    break;
                case "scholar_lore_sense":
                    // Reveal nearby echo crystals on minimap
                    break;
                case "traversal_fleet_foot":
                    stats.MoveSpeed.AddModifier(0.5f);
                    break;
            }
        }

        // ── Reputation ────────────────────────────────────────

        /// <summary>Change reputation with a faction (+/-).</summary>
        public void ChangeReputation(string factionId, int delta)
        {
            var entry = GetFactionEntry(factionId);
            if (entry == null) return;

            entry.value = Mathf.Clamp(entry.value + delta, -100, 100);
            OnReputationChanged?.Invoke(factionId, entry.value);

            Debug.Log($"[Progression] {entry.factionName}: {entry.value:+#;-#;0} ({entry.Tier})");
        }

        public int GetReputation(string factionId) => GetFactionEntry(factionId)?.value ?? 0;
        public string GetReputationTier(string factionId) => GetFactionEntry(factionId)?.Tier ?? "Unknown";

        public ReputationEntry[] GetAllFactions() => _factions;

        private ReputationEntry GetFactionEntry(string id)
        {
            foreach (var f in _factions)
                if (f.factionId == id) return f;
            return null;
        }

        // ── Event Handlers ────────────────────────────────────

        private void OnPlayerLevelUp(Core.PlayerLevelUpEvent e)
        {
            _availableSkillPoints += _skillPointsPerLevel;
            OnSkillPointsChanged?.Invoke(_availableSkillPoints);
        }
    }
}
