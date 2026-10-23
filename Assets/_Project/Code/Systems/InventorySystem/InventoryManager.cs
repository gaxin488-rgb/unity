using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// ============================================================
//  InventoryManager — Grid-based inventory with stacking
//  Attach to: Player GameObject
// ============================================================
namespace EchoesOfTheLost.Systems.Inventory
{
    [System.Serializable]
    public class InventorySlot
    {
        public ItemData item;
        public int quantity;
        public bool IsEmpty => item == null || quantity <= 0;

        public InventorySlot() { }
        public InventorySlot(ItemData item, int qty) { this.item = item; quantity = qty; }

        public void Clear() { item = null; quantity = 0; }
    }

    public class InventoryManager : MonoBehaviour
    {
        [Header("Inventory Settings")]
        [SerializeField] private int _maxSlots = 40;
        [SerializeField] private float _maxCarryWeight = 100f;

        [Header("Equipment Slots")]
        [SerializeField] private WeaponData _equippedWeapon;
        [SerializeField] private ArmorData _equippedHead;
        [SerializeField] private ArmorData _equippedChest;
        [SerializeField] private ArmorData _equippedLegs;
        [SerializeField] private ArmorData _equippedFeet;
        [SerializeField] private ArmorData _equippedHands;

        // ── State ─────────────────────────────────────────────
        private List<InventorySlot> _slots;
        private float _currentWeight;

        // ── Events ────────────────────────────────────────────
        public event System.Action OnInventoryChanged;
        public event System.Action<WeaponData> OnWeaponEquipped;
        public event System.Action<ArmorData> OnArmorEquipped;

        public IReadOnlyList<InventorySlot> Slots => _slots;
        public float CurrentWeight => _currentWeight;
        public float MaxWeight => _maxCarryWeight;
        public float WeightPercent => _currentWeight / _maxCarryWeight;
        public bool IsOverEncumbered => _currentWeight > _maxCarryWeight;

        private void Awake()
        {
            _slots = new List<InventorySlot>(_maxSlots);
            for (int i = 0; i < _maxSlots; i++)
                _slots.Add(new InventorySlot());

            Core.ServiceLocator.Register<InventoryManager>(this);
        }

        // ── Add / Remove ─────────────────────────────────────

        /// <summary>Add items. Returns leftover quantity that couldn't fit.</summary>
        public int AddItem(ItemData item, int quantity = 1)
        {
            if (item == null || quantity <= 0) return quantity;

            int remaining = quantity;

            // Try to stack onto existing slots first
            if (item.maxStackSize > 1)
            {
                foreach (var slot in _slots)
                {
                    if (remaining <= 0) break;
                    if (slot.item == item && slot.quantity < item.maxStackSize)
                    {
                        int canAdd = Mathf.Min(remaining, item.maxStackSize - slot.quantity);
                        slot.quantity += canAdd;
                        remaining -= canAdd;
                        _currentWeight += item.weight * canAdd;
                    }
                }
            }

            // Fill empty slots
            while (remaining > 0)
            {
                var emptySlot = _slots.FirstOrDefault(s => s.IsEmpty);
                if (emptySlot == null) break; // Inventory full

                int toPlace = Mathf.Min(remaining, item.maxStackSize);
                emptySlot.item = item;
                emptySlot.quantity = toPlace;
                remaining -= toPlace;
                _currentWeight += item.weight * toPlace;
            }

            if (remaining < quantity)
            {
                Core.EventBus.Publish(new Core.ItemPickedUpEvent
                {
                    ItemId = item.itemId,
                    Quantity = quantity - remaining
                });
                OnInventoryChanged?.Invoke();
            }

            return remaining; // 0 = success, >0 = partial (inventory full)
        }

        /// <summary>Remove items by ID. Returns true if fully removed.</summary>
        public bool RemoveItem(string itemId, int quantity = 1)
        {
            int remaining = quantity;

            for (int i = _slots.Count - 1; i >= 0 && remaining > 0; i--)
            {
                var slot = _slots[i];
                if (slot.IsEmpty || slot.item.itemId != itemId) continue;

                int toRemove = Mathf.Min(remaining, slot.quantity);
                slot.quantity -= toRemove;
                _currentWeight -= slot.item.weight * toRemove;
                remaining -= toRemove;

                if (slot.quantity <= 0) slot.Clear();
            }

            if (remaining < quantity) OnInventoryChanged?.Invoke();
            return remaining == 0;
        }

        /// <summary>Count total of an item by ID.</summary>
        public int CountItem(string itemId)
        {
            return _slots
                .Where(s => !s.IsEmpty && s.item.itemId == itemId)
                .Sum(s => s.quantity);
        }

        public bool HasItem(string itemId, int quantity = 1) => CountItem(itemId) >= quantity;

        // ── Equipment ────────────────────────────────────────

        public void EquipWeapon(WeaponData weapon)
        {
            if (_equippedWeapon != null)
                AddItem(_equippedWeapon); // return old weapon to inventory

            _equippedWeapon = weapon;
            RemoveItem(weapon.itemId);
            OnWeaponEquipped?.Invoke(weapon);
            OnInventoryChanged?.Invoke();
        }

        public void EquipArmor(ArmorData armor)
        {
            // Unequip slot first
            var current = GetEquippedArmor(armor.slot);
            if (current != null) AddItem(current);

            SetEquippedArmor(armor.slot, armor);
            RemoveItem(armor.itemId);
            OnArmorEquipped?.Invoke(armor);
            OnInventoryChanged?.Invoke();
        }

        private ArmorData GetEquippedArmor(ArmorData.ArmorSlot slot) => slot switch
        {
            ArmorData.ArmorSlot.Head => _equippedHead,
            ArmorData.ArmorSlot.Chest => _equippedChest,
            ArmorData.ArmorSlot.Legs => _equippedLegs,
            ArmorData.ArmorSlot.Feet => _equippedFeet,
            ArmorData.ArmorSlot.Hands => _equippedHands,
            _ => null
        };

        private void SetEquippedArmor(ArmorData.ArmorSlot slot, ArmorData armor)
        {
            switch (slot)
            {
                case ArmorData.ArmorSlot.Head: _equippedHead = armor; break;
                case ArmorData.ArmorSlot.Chest: _equippedChest = armor; break;
                case ArmorData.ArmorSlot.Legs: _equippedLegs = armor; break;
                case ArmorData.ArmorSlot.Feet: _equippedFeet = armor; break;
                case ArmorData.ArmorSlot.Hands: _equippedHands = armor; break;
            }
        }

        // ── Stat Bonuses from Equipment ──────────────────────

        /// <summary>Total defense from all equipped armor.</summary>
        public float GetTotalArmorDefense()
        {
            float total = 0f;
            if (_equippedHead != null) total += _equippedHead.physicalDefense;
            if (_equippedChest != null) total += _equippedChest.physicalDefense;
            if (_equippedLegs != null) total += _equippedLegs.physicalDefense;
            if (_equippedFeet != null) total += _equippedFeet.physicalDefense;
            if (_equippedHands != null) total += _equippedHands.physicalDefense;
            return total;
        }

        // ── Save/Load ─────────────────────────────────────────

        public List<EchoesOfTheLost.Systems.SerializedItem> Serialize()
        {
            var result = new List<EchoesOfTheLost.Systems.SerializedItem>();
            foreach (var slot in _slots)
            {
                if (!slot.IsEmpty)
                    result.Add(new EchoesOfTheLost.Systems.SerializedItem { itemId = slot.item.itemId, quantity = slot.quantity });
            }
            return result;
        }
    }
}
