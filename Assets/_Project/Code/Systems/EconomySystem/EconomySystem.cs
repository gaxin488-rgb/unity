using UnityEngine;
using System.Collections.Generic;

// ============================================================
//  EconomySystem — Shop, currency, pricing, crafting
//  Attach to: Vendor NPC or World_Persistent GameObject
// ============================================================
namespace EchoesOfTheLost.Systems.Economy
{
    [System.Serializable]
    public class ShopEntry
    {
        public Inventory.ItemData item;
        public int stockCount = -1;    // -1 = unlimited
        public float priceMultiplier = 1f;
        public int minReputation = -100; // minimum faction reputation to buy
        public string requiredFactionId;
    }

    [CreateAssetMenu(fileName = "ShopInventory_New", menuName = "EotL/Economy/Shop Inventory")]
    public class ShopInventoryData : ScriptableObject
    {
        public string shopId;
        public string shopName;
        public string ownerFactionId;
        public ShopEntry[] items;
        [Range(0.1f, 1f)] public float sellPriceRatio = 0.4f;  // player gets 40% of item value
        public bool restocksDaily = true;
    }

    public class EconomySystem : Utils.Singleton<EconomySystem>
    {
        [Header("Economy Settings")]
        [SerializeField] private float _globalPriceModifier = 1f;  // inflate/deflate globally
        [SerializeField] private float _reputationPriceBonus = 0.005f;  // per rep point
        [SerializeField] private float _playerGold;

        public float PlayerGold => _playerGold;

        public event System.Action<float> OnGoldChanged;

        protected override void Awake()
        {
            base.Awake();
            Core.ServiceLocator.Register<EconomySystem>(this);
        }

        // ── Currency ─────────────────────────────────────────

        public void AddGold(float amount)
        {
            _playerGold += amount;
            OnGoldChanged?.Invoke(_playerGold);
        }

        public bool SpendGold(float amount)
        {
            if (_playerGold < amount) return false;
            _playerGold -= amount;
            OnGoldChanged?.Invoke(_playerGold);
            return true;
        }

        // ── Price Calculation ─────────────────────────────────

        /// <summary>Get buy price for an item from a shop.</summary>
        public float GetBuyPrice(Inventory.ItemData item, ShopInventoryData shop, string factionId = null)
        {
            float basePrice = item.baseValue * _globalPriceModifier;

            // Faction reputation discount
            if (!string.IsNullOrEmpty(factionId))
            {
                int rep = 0;
                if (Core.ServiceLocator.TryGet<Progression.ProgressionSystem>(out var prog))
                    rep = prog.GetReputation(factionId);

                float discount = rep * _reputationPriceBonus;  // +1% per rep point
                basePrice *= (1f - Mathf.Clamp(discount, -0.3f, 0.3f));
            }

            return Mathf.RoundToInt(basePrice * 10f) / 10f;
        }

        /// <summary>Get sell price (player selling to shop).</summary>
        public float GetSellPrice(Inventory.ItemData item, ShopInventoryData shop)
        {
            return item.baseValue * shop.sellPriceRatio * _globalPriceModifier;
        }

        // ── Transactions ─────────────────────────────────────

        /// <summary>Player buys item from shop. Returns true on success.</summary>
        public bool BuyItem(Inventory.ItemData item, int quantity, ShopInventoryData shop)
        {
            var inv = Core.ServiceLocator.TryGet<Inventory.InventoryManager>(out var i) ? i : null;
            if (inv == null) return false;

            float totalCost = GetBuyPrice(item, shop, shop.ownerFactionId) * quantity;
            if (!SpendGold(totalCost)) return false;

            int leftover = inv.AddItem(item, quantity);
            if (leftover > 0)
            {
                // Refund items that didn't fit
                AddGold(GetBuyPrice(item, shop, shop.ownerFactionId) * leftover);
            }

            return true;
        }

        /// <summary>Player sells item to shop. Returns gold received.</summary>
        public float SellItem(string itemId, int quantity, ShopInventoryData shop)
        {
            var inv = Core.ServiceLocator.TryGet<Inventory.InventoryManager>(out var i) ? i : null;
            if (inv == null) return 0f;

            // Find item data
            if (!inv.HasItem(itemId, quantity)) return 0f;

            // Get the item to calculate price (needs ItemDatabase lookup — simplified here)
            // float sellPrice = GetSellPrice(item, shop) * quantity;
            // inv.RemoveItem(itemId, quantity);
            // AddGold(sellPrice);

            return 0f;
        }
    }
}
