using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.IO;
using System.Collections.Generic;

// ============================================================
//  SaveSystem — JSON-based save/load with multi-slot support
//  Uses JsonUtility-compatible serializable structs (no Dictionary).
//  Attach to: _Boot / Sandbox persistent GameObject
// ============================================================
namespace EchoesOfTheLost.Systems
{
    [System.Serializable]
    public struct StringIntPair
    {
        public string key;
        public int value;
    }

    [System.Serializable]
    public struct StringBoolPair
    {
        public string key;
        public bool value;
    }

    [System.Serializable]
    public class SaveData
    {
        public string gameVersion = "0.2.0";
        public string saveDateTime;
        public float totalPlaytimeSeconds;
        public string currentScene;
        public SerializableVector3 playerPosition;
        public float gameTimeHours = 8f;
        public PlayerSaveData player = new PlayerSaveData();
        public WorldSaveData world = new WorldSaveData();
        public QuestSaveData quests = new QuestSaveData();
    }

    [System.Serializable]
    public class PlayerSaveData
    {
        public string displayName = "Kael";
        public int level = 1;
        public int currentXP = 0;
        public int currentHealth = 100;
        public int currentStamina = 100;
        public float gold = 0f;
        public List<string> unlockedSkills = new List<string>();
        public List<SerializedItem> inventoryItems = new List<SerializedItem>();
        public List<StringIntPair> factionReputation = new List<StringIntPair>();
    }

    [System.Serializable]
    public class WorldSaveData
    {
        public List<string> discoveredPOIs = new List<string>();
        public List<string> clearedDungeons = new List<string>();
        public List<string> killedUniqueEnemies = new List<string>();
        public List<StringBoolPair> worldFlags = new List<StringBoolPair>();
    }

    [System.Serializable]
    public class QuestSaveData
    {
        public List<string> activeQuestIds = new List<string>();
        public List<string> completedQuestIds = new List<string>();
        public List<StringIntPair> questObjectiveProgress = new List<StringIntPair>();
    }

    [System.Serializable]
    public class SerializedItem
    {
        public string itemId;
        public int quantity;
        public int durability;
    }

    [System.Serializable]
    public struct SerializableVector3
    {
        public float x, y, z;
        public SerializableVector3(Vector3 v) { x = v.x; y = v.y; z = v.z; }
        public Vector3 ToVector3() => new Vector3(x, y, z);
    }

    [System.Serializable]
    public class SaveSlotInfo
    {
        public int slot;
        public string playerName;
        public int playerLevel;
        public string sceneName;
        public string saveDateTime;
        public float totalPlaytimeSeconds;
        public bool isEmpty = true;
    }

    public class SaveSystem : Utils.Singleton<SaveSystem>
    {
        [Header("Save Settings")]
        [SerializeField] private float _autoSaveInterval = 300f; // 5 minutes
        [SerializeField] private int _maxSaveSlots = 5;

        private string SaveDirectory => Path.Combine(Application.persistentDataPath, "saves");
        private float _autoSaveTimer;
        private SaveData _currentSave;

        public SaveData CurrentSave => _currentSave;
        public event Action<int> OnSaveCompleted;
        public event Action<int> OnLoadCompleted;

        protected override void Awake()
        {
            base.Awake();
            Directory.CreateDirectory(SaveDirectory);
            Core.ServiceLocator.Register<SaveSystem>(this);
        }

        private void Update()
        {
            _autoSaveTimer += Time.deltaTime;
            if (_autoSaveTimer >= _autoSaveInterval)
            {
                _autoSaveTimer = 0f;
                AutoSave();
            }
        }

        // ── Public API ──────────────────────────────────────

        /// <summary>Save current game to slot (0 = autosave, 1..N = manual slots).</summary>
        public bool SaveGame(int slot)
        {
            if (slot < 0 || slot > _maxSaveSlots) return false;

            try
            {
                _currentSave ??= new SaveData();
                _currentSave.saveDateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                _currentSave.currentScene = SceneManager.GetActiveScene().name;
                _currentSave.totalPlaytimeSeconds = Core.GameManager.Instance != null ? Core.GameManager.Instance.TotalPlaytime : 0f;

                if (Core.ServiceLocator.TryGet<World.TimeSystem>(out var timeSys))
                    _currentSave.gameTimeHours = timeSys.CurrentHour;

                // Collect Player state
                var playerGO = GameObject.FindGameObjectWithTag("Player");
                if (playerGO != null)
                {
                    _currentSave.playerPosition = new SerializableVector3(playerGO.transform.position);
                    if (playerGO.TryGetComponent<Characters.Player.PlayerStats>(out var stats))
                    {
                        _currentSave.player.level = stats.Level;
                        _currentSave.player.currentXP = stats.CurrentXP;
                        _currentSave.player.currentHealth = Mathf.RoundToInt(stats.CurrentHealth);
                        _currentSave.player.currentStamina = Mathf.RoundToInt(stats.CurrentStamina);
                    }
                    if (playerGO.TryGetComponent<Inventory.InventoryManager>(out var inv))
                        _currentSave.player.inventoryItems = inv.Serialize();
                }

                if (Core.ServiceLocator.TryGet<Economy.EconomySystem>(out var econ))
                    _currentSave.player.gold = econ.PlayerGold;

                if (Core.ServiceLocator.TryGet<World.POIManager>(out var poiMgr))
                    _currentSave.world.discoveredPOIs = poiMgr.GetDiscoveredIds();

                if (Core.ServiceLocator.TryGet<Quest.QuestManager>(out var qm))
                {
                    _currentSave.quests.activeQuestIds.Clear();
                    foreach (var q in qm.ActiveQuests) _currentSave.quests.activeQuestIds.Add(q.questId);

                    _currentSave.quests.completedQuestIds.Clear();
                    foreach (var q in qm.CompletedQuests) _currentSave.quests.completedQuestIds.Add(q.questId);
                }

                string json = JsonUtility.ToJson(_currentSave, prettyPrint: true);
                File.WriteAllText(GetSavePath(slot), json);

                Debug.Log($"[SaveSystem] Game saved to slot {slot} ({GetSavePath(slot)})");
                OnSaveCompleted?.Invoke(slot);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Save failed: {e.Message}");
                return false;
            }
        }

        /// <summary>Load game from slot and apply state to live scene objects.</summary>
        public SaveData LoadGame(int slot)
        {
            string path = GetSavePath(slot);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[SaveSystem] No save in slot {slot}");
                return null;
            }

            try
            {
                string json = File.ReadAllText(path);
                _currentSave = JsonUtility.FromJson<SaveData>(json);
                ApplyLoadedState(_currentSave);
                OnLoadCompleted?.Invoke(slot);
                Debug.Log($"[SaveSystem] Loaded game from slot {slot}");
                return _currentSave;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Load failed: {e.Message}");
                return null;
            }
        }

        private void ApplyLoadedState(SaveData data)
        {
            if (data == null) return;

            if (Core.ServiceLocator.TryGet<World.TimeSystem>(out var timeSys))
                timeSys.SetTime(data.gameTimeHours);

            var playerGO = GameObject.FindGameObjectWithTag("Player");
            if (playerGO != null)
            {
                if (playerGO.TryGetComponent<Rigidbody>(out var rb))
                    rb.linearVelocity = Vector3.zero;
                playerGO.transform.position = data.playerPosition.ToVector3();
            }

            if (Core.ServiceLocator.TryGet<World.POIManager>(out var poiMgr))
                poiMgr.RestoreFromSave(data.world.discoveredPOIs, data.world.clearedDungeons);
        }

        public bool DeleteSave(int slot)
        {
            string path = GetSavePath(slot);
            if (!File.Exists(path)) return false;
            File.Delete(path);
            return true;
        }

        public SaveSlotInfo GetSaveInfo(int slot)
        {
            string path = GetSavePath(slot);
            if (!File.Exists(path)) return new SaveSlotInfo { slot = slot, isEmpty = true };

            try
            {
                string json = File.ReadAllText(path);
                var data = JsonUtility.FromJson<SaveData>(json);
                return new SaveSlotInfo
                {
                    slot = slot,
                    playerName = data.player.displayName,
                    playerLevel = data.player.level,
                    sceneName = data.currentScene,
                    saveDateTime = data.saveDateTime,
                    totalPlaytimeSeconds = data.totalPlaytimeSeconds,
                    isEmpty = false
                };
            }
            catch { return new SaveSlotInfo { slot = slot, isEmpty = true }; }
        }

        public bool HasAnySave()
        {
            for (int i = 0; i <= _maxSaveSlots; i++)
                if (File.Exists(GetSavePath(i))) return true;
            return false;
        }

        private void AutoSave() => SaveGame(0);

        private string GetSavePath(int slot) =>
            Path.Combine(SaveDirectory, slot == 0 ? "autosave.sav" : $"slot_{slot}.sav");
    }
}
