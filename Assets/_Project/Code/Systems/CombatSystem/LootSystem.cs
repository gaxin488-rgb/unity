using UnityEngine;

// ============================================================
//  LootSystem — Drop tables and item spawning on enemy death
//  Attach to: Each enemy or as a shared manager
// ============================================================
namespace EchoesOfTheLost.Systems.Combat
{
    public enum LootRarity { Common, Uncommon, Rare, Epic }

    [System.Serializable]
    public class LootEntry
    {
        public Inventory.ItemData item;
        [Range(0f, 1f)] public float dropChance = 0.5f;
        public int minQuantity = 1;
        public int maxQuantity = 1;
    }

    [CreateAssetMenu(fileName = "LootTable_New", menuName = "EotL/Combat/Loot Table")]
    public class LootTable : ScriptableObject
    {
        public string tableId;
        [Range(0f, 1f)] public float goldDropChance = 0.8f;
        public float minGold = 1f;
        public float maxGold = 10f;
        public LootEntry[] entries;
        public int maxItemDrops = 3;  // maximum items from one kill
    }

    public class LootSystem : MonoBehaviour
    {
        [Header("Loot Table")]
        [SerializeField] private LootTable _lootTable;

        [Header("Drop Settings")]
        [SerializeField] private GameObject _lootPickupPrefab;
        [SerializeField] private float _dropRadius = 0.8f;
        [SerializeField] private float _dropUpwardForce = 2f;

        public void DropLoot(Vector3 position, float lootMultiplier = 1f)
        {
            if (_lootTable == null) return;

            // Gold drop
            if (Random.value <= _lootTable.goldDropChance * lootMultiplier)
            {
                float gold = Random.Range(_lootTable.minGold, _lootTable.maxGold) * lootMultiplier;

                if (Core.ServiceLocator.TryGet<Economy.EconomySystem>(out var economy))
                    economy.AddGold(gold);
            }

            // Item drops
            int dropsThisKill = 0;
            foreach (var entry in _lootTable.entries)
            {
                if (dropsThisKill >= _lootTable.maxItemDrops) break;

                float roll = Random.value;
                if (roll > entry.dropChance * lootMultiplier) continue;

                int qty = Random.Range(entry.minQuantity, entry.maxQuantity + 1);
                SpawnPickup(entry.item, qty, position);
                dropsThisKill++;
            }
        }

        private void SpawnPickup(Inventory.ItemData item, int quantity, Vector3 position)
        {
            if (_lootPickupPrefab == null)
            {
                // No prefab — add directly to inventory if player is nearby
                if (Core.ServiceLocator.TryGet<Inventory.InventoryManager>(out var inv))
                {
                    int leftover = inv.AddItem(item, quantity);
                    if (leftover > 0)
                        Debug.LogWarning($"[LootSystem] Inventory full, lost {leftover}x {item.displayName}");
                }
                return;
            }

            // Spawn world pickup object
            Vector3 spawnPos = position + Random.insideUnitSphere * _dropRadius;
            spawnPos.y = position.y + 0.5f;

            var pickup = Instantiate(_lootPickupPrefab, spawnPos, Random.rotation);
            pickup.AddComponent<LootPickup>().Initialize(item, quantity);

            // Add physics pop
            if (pickup.TryGetComponent<Rigidbody>(out var rb))
                rb.AddForce((Vector3.up * _dropUpwardForce + Random.insideUnitSphere * 1.5f), ForceMode.VelocityChange);
        }
    }

    // ── Pickup Object ─────────────────────────────────────────
    public class LootPickup : MonoBehaviour, Characters.Player.IInteractable
    {
        private Inventory.ItemData _item;
        private int _quantity;
        private float _lifetime = 120f;  // despawn after 2 minutes

        public void Initialize(Inventory.ItemData item, int qty)
        {
            _item = item;
            _quantity = qty;

            // Visual: Rarity glow color
            var renderer = GetComponentInChildren<Renderer>();
            if (renderer != null)
                renderer.material.color = item.RarityColor;

            Destroy(gameObject, _lifetime);
        }

        public void Interact(Characters.Player.PlayerController player)
        {
            if (Core.ServiceLocator.TryGet<Inventory.InventoryManager>(out var inv))
            {
                int leftover = inv.AddItem(_item, _quantity);
                if (leftover < _quantity)
                {
                    if (leftover > 0) _quantity = leftover;
                    else Destroy(gameObject);
                }
            }
        }

        public string GetInteractionPrompt() => $"Pick up {_item?.displayName} x{_quantity}";
    }
}
