using UnityEngine;

// ============================================================
//  ItemDatabase — Central SO registry for all items
//  Create one instance via [CreateAssetMenu] and assign to GameManager
// ============================================================
namespace EchoesOfTheLost.Systems.Inventory
{
    [CreateAssetMenu(fileName = "ItemDatabase", menuName = "EotL/Items/Item Database")]
    public class ItemDatabase : ScriptableObject
    {
        [SerializeField] private ItemData[] _allItems;

        private System.Collections.Generic.Dictionary<string, ItemData> _lookup;

        private void OnEnable() => BuildLookup();

        private void BuildLookup()
        {
            _lookup = new System.Collections.Generic.Dictionary<string, ItemData>();
            if (_allItems == null) return;
            foreach (var item in _allItems)
            {
                if (item != null && !string.IsNullOrEmpty(item.itemId))
                    _lookup[item.itemId] = item;
            }
        }

        public ItemData GetItem(string itemId)
        {
            if (_lookup == null) BuildLookup();
            return _lookup.TryGetValue(itemId, out var item) ? item : null;
        }

        public T GetItem<T>(string itemId) where T : ItemData
            => GetItem(itemId) as T;

        public ItemData[] GetAll() => _allItems;

        public ItemData[] GetByCategory(ItemCategory category)
        {
            var result = new System.Collections.Generic.List<ItemData>();
            foreach (var item in _allItems)
                if (item.category == category) result.Add(item);
            return result.ToArray();
        }

        public ItemData[] GetByRarity(ItemRarity rarity)
        {
            var result = new System.Collections.Generic.List<ItemData>();
            foreach (var item in _allItems)
                if (item.rarity == rarity) result.Add(item);
            return result.ToArray();
        }
    }
}
