using UnityEngine;

// ============================================================
//  ItemData — Base ScriptableObject for all items
// ============================================================
namespace EchoesOfTheLost.Systems.Inventory
{
    public enum ItemRarity { Common, Uncommon, Rare, Epic, Legendary, Solenne }
    public enum ItemCategory { Weapon, Armor, Consumable, Material, Quest, Artifact, Currency }

    [CreateAssetMenu(fileName = "Item_New", menuName = "EotL/Items/Item Data")]
    public class ItemData : ScriptableObject
    {
        [Header("Identity")]
        public string itemId;
        public string displayName;
        [TextArea(2, 4)] public string description;
        public Sprite icon;

        [Header("Classification")]
        public ItemCategory category;
        public ItemRarity rarity = ItemRarity.Common;

        [Header("Stack & Weight")]
        public int maxStackSize = 1;
        public float weight = 0.5f;

        [Header("Economy")]
        public float baseValue = 10f; // in gold
        public bool isSellable = true;
        public bool isDroppable = true;
        public bool isQuestItem = false;

        [Header("Prefab")]
        public GameObject worldPrefab; // spawned when dropped in world

        // ── Helpers ──────────────────────────────────────────
        public Color RarityColor => rarity switch
        {
            ItemRarity.Common => Color.white,
            ItemRarity.Uncommon => Color.green,
            ItemRarity.Rare => new Color(0.3f, 0.5f, 1f),
            ItemRarity.Epic => new Color(0.6f, 0.2f, 0.8f),
            ItemRarity.Legendary => new Color(1f, 0.65f, 0f),
            ItemRarity.Solenne => new Color(0.2f, 0.9f, 0.9f),
            _ => Color.white
        };

        public string RarityName => rarity.ToString();
    }

    [CreateAssetMenu(fileName = "Weapon_New", menuName = "EotL/Items/Weapon Data")]
    public class WeaponData : ItemData
    {
        [Header("Weapon Stats")]
        public float attackDamage = 10f;
        public float attackSpeed = 1f;
        public float attackRange = 1.5f;
        public int comboSteps = 3;

        public enum DamageType { Physical, Fire, Ice, Lightning, Arcane }
        public DamageType damageType = DamageType.Physical;

        [Header("Special")]
        public float critChance = 0.05f;
        public float critMultiplier = 1.5f;
        public string[] specialAbilityIds; // unlock on equip
    }

    [CreateAssetMenu(fileName = "Armor_New", menuName = "EotL/Items/Armor Data")]
    public class ArmorData : ItemData
    {
        public enum ArmorSlot { Head, Chest, Legs, Feet, Hands }

        [Header("Armor Stats")]
        public ArmorSlot slot;
        public float physicalDefense = 5f;
        public float fireResistance = 0f;
        public float iceResistance = 0f;
        public float lightningResistance = 0f;
        public float weightPenalty = 0f; // reduces movement speed
    }

    [CreateAssetMenu(fileName = "Consumable_New", menuName = "EotL/Items/Consumable Data")]
    public class ConsumableData : ItemData
    {
        public enum ConsumableEffect { RestoreHealth, RestoreStamina, BuffStrength, BuffSpeed, Antidote, RevealMap }

        [Header("Consumable Effect")]
        public ConsumableEffect effect;
        public float effectValue = 50f;
        public float effectDuration = 0f; // 0 = instant
        public float cooldown = 3f;
        public bool consumeOnUse = true;
    }
}
