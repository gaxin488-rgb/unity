using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using EchoesOfTheLost.Characters.Player;
using EchoesOfTheLost.Core;
using EchoesOfTheLost.Systems;
using EchoesOfTheLost.Systems.Economy;
using EchoesOfTheLost.Systems.Inventory;
using EchoesOfTheLost.Systems.Progression;
using EchoesOfTheLost.Systems.Quest;
using EchoesOfTheLost.World;

// ============================================================
//  UIManager — Runtime modal windows for Inventory [Tab/I],
//  Quest Journal [J], Character & Factions [K], and Pause /
//  World Sandbox Controls [Esc].
//  Attach to: HUD_Canvas (or auto-created by SandboxSceneBuilder)
// ============================================================
namespace EchoesOfTheLost.UI
{
    public class UIManager : MonoBehaviour
    {
        public enum ActiveTab
        {
            None,
            Inventory,
            QuestJournal,
            Character,
            PauseSandbox
        }

        [Header("Modal Window References (Auto-Built if Null)")]
        [SerializeField] private GameObject _modalRoot;
        [SerializeField] private TextMeshProUGUI _modalTitleText;
        [SerializeField] private TextMeshProUGUI _modalBodyText;
        [SerializeField] private TextMeshProUGUI _modalFooterText;

        private ActiveTab _currentTab = ActiveTab.None;
        private PlayerStats _playerStats;
        private InventoryManager _inventory;
        private ProgressionSystem _progression;
        private ConsumableData _starterPotion;
        private WeaponData _starterSword;
        private ArmorData _starterCoat;
        private ItemData _starterShard;

        private static readonly WeatherType[] _weatherCycle =
        {
            WeatherType.Clear,
            WeatherType.Cloudy,
            WeatherType.Raining,
            WeatherType.Thunderstorm,
            WeatherType.Foggy
        };
        private int _weatherIndex;

        public bool IsWindowOpen => _currentTab != ActiveTab.None;

        private void Awake()
        {
            ServiceLocator.Register<UIManager>(this);
        }

        private void Start()
        {
            EnsureModalUI();
            BindPlayerComponents();
            SeedStarterInventoryIfEmpty();
            CloseWindow();
        }

        private void BindPlayerComponents()
        {
            var playerGO = GameObject.FindGameObjectWithTag("Player");
            if (playerGO != null)
            {
                _playerStats = playerGO.GetComponent<PlayerStats>();
                _inventory = playerGO.GetComponent<InventoryManager>();
                _progression = playerGO.GetComponent<ProgressionSystem>();
            }

            if (_inventory == null)
                ServiceLocator.TryGet(out _inventory);
            if (_progression == null)
                ServiceLocator.TryGet(out _progression);
        }

        private void SeedStarterInventoryIfEmpty()
        {
            if (_inventory == null) return;

            bool hasAny = false;
            foreach (var s in _inventory.Slots)
            {
                if (!s.IsEmpty) { hasAny = true; break; }
            }
            if (hasAny) return;

            // Runtime starter items so Inventory [Tab] has rich content immediately
            _starterSword = ScriptableObject.CreateInstance<WeaponData>();
            _starterSword.itemId = "wpn_solenne_blade";
            _starterSword.displayName = "Solenne Relic Blade";
            _starterSword.description = "A resonant bronze-and-aether shortsword forged before the Great Silence.";
            _starterSword.category = ItemCategory.Weapon;
            _starterSword.rarity = ItemRarity.Rare;
            _starterSword.maxStackSize = 1;
            _starterSword.weight = 2.5f;
            _starterSword.baseValue = 180f;
            _starterSword.attackDamage = 24f;
            _starterSword.attackSpeed = 1.15f;

            _starterCoat = ScriptableObject.CreateInstance<ArmorData>();
            _starterCoat.itemId = "arm_drifter_coat";
            _starterCoat.displayName = "Drifter's Field Coat";
            _starterCoat.description = "Weatherproof expedition coat worn by Verda Wilds relic hunters.";
            _starterCoat.category = ItemCategory.Armor;
            _starterCoat.rarity = ItemRarity.Uncommon;
            _starterCoat.slot = ArmorData.ArmorSlot.Chest;
            _starterCoat.maxStackSize = 1;
            _starterCoat.weight = 3.0f;
            _starterCoat.baseValue = 95f;
            _starterCoat.physicalDefense = 8f;

            _starterPotion = ScriptableObject.CreateInstance<ConsumableData>();
            _starterPotion.itemId = "pot_aether_tonic";
            _starterPotion.displayName = "Aether Mending Tonic";
            _starterPotion.description = "Restores +45 HP instantly using distilled Verda luminescence.";
            _starterPotion.category = ItemCategory.Consumable;
            _starterPotion.rarity = ItemRarity.Common;
            _starterPotion.effect = ConsumableData.ConsumableEffect.RestoreHealth;
            _starterPotion.effectValue = 45f;
            _starterPotion.maxStackSize = 10;
            _starterPotion.weight = 0.3f;
            _starterPotion.baseValue = 25f;

            _starterShard = ScriptableObject.CreateInstance<ItemData>();
            _starterShard.itemId = "mat_echo_shard";
            _starterShard.displayName = "Solenne Echo Shard";
            _starterShard.description = "Crystalline fragment humming with ancient memory frequencies.";
            _starterShard.category = ItemCategory.Artifact;
            _starterShard.rarity = ItemRarity.Solenne;
            _starterShard.maxStackSize = 20;
            _starterShard.weight = 0.2f;
            _starterShard.baseValue = 60f;

            _inventory.AddItem(_starterSword, 1);
            _inventory.AddItem(_starterCoat, 1);
            _inventory.AddItem(_starterPotion, 3);
            _inventory.AddItem(_starterShard, 2);
            _inventory.EquipWeapon(_starterSword);
            _inventory.EquipArmor(_starterCoat);
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.tabKey.wasPressedThisFrame || kb.iKey.wasPressedThisFrame)
                ToggleTab(ActiveTab.Inventory);
            else if (kb.jKey.wasPressedThisFrame)
                ToggleTab(ActiveTab.QuestJournal);
            else if (kb.kKey.wasPressedThisFrame)
                ToggleTab(ActiveTab.Character);
            else if (kb.escapeKey.wasPressedThisFrame)
            {
                if (_currentTab != ActiveTab.None && _currentTab != ActiveTab.PauseSandbox)
                    CloseWindow();
                else
                    ToggleTab(ActiveTab.PauseSandbox);
            }

            if (_currentTab != ActiveTab.None)
            {
                HandleModalHotkeys(kb);
                RefreshModalContent();
            }
        }

        private void ToggleTab(ActiveTab tab)
        {
            if (_currentTab == tab)
            {
                CloseWindow();
                return;
            }

            _currentTab = tab;
            if (_modalRoot != null) _modalRoot.SetActive(true);
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            RefreshModalContent();
        }

        public void CloseWindow()
        {
            _currentTab = ActiveTab.None;
            if (_modalRoot != null) _modalRoot.SetActive(false);
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void HandleModalHotkeys(Keyboard kb)
        {
            switch (_currentTab)
            {
                case ActiveTab.Inventory:
                    if (kb.digit1Key.wasPressedThisFrame) UseAetherTonic();
                    if (kb.digit2Key.wasPressedThisFrame && _starterPotion != null)
                        _inventory?.AddItem(_starterPotion, 1);
                    break;

                case ActiveTab.Character:
                    if (kb.digit1Key.wasPressedThisFrame) _progression?.ChangeReputation("accord", +10);
                    if (kb.digit2Key.wasPressedThisFrame) _progression?.ChangeReputation("drifters", +10);
                    if (kb.digit3Key.wasPressedThisFrame) _progression?.ChangeReputation("void_seekers", +10);
                    break;

                case ActiveTab.PauseSandbox:
                    if (kb.digit1Key.wasPressedThisFrame) TimeSystem.Instance?.SetTime(6.5f);
                    if (kb.digit2Key.wasPressedThisFrame) TimeSystem.Instance?.SetTime(12.0f);
                    if (kb.digit3Key.wasPressedThisFrame) TimeSystem.Instance?.SetTime(18.0f);
                    if (kb.digit4Key.wasPressedThisFrame) TimeSystem.Instance?.SetTime(23.0f);
                    if (kb.digit5Key.wasPressedThisFrame)
                    {
                        _weatherIndex = (_weatherIndex + 1) % _weatherCycle.Length;
                        WeatherSystem.Instance?.SetWeather(_weatherCycle[_weatherIndex], instant: true);
                    }
                    break;
            }
        }

        private void UseAetherTonic()
        {
            if (_inventory == null || _playerStats == null) return;
            if (_inventory.RemoveItem("pot_aether_tonic", 1))
            {
                _playerStats.Heal(45f);
            }
        }

        private void RefreshModalContent()
        {
            if (_modalTitleText == null || _modalBodyText == null) return;

            var sb = new StringBuilder(512);

            switch (_currentTab)
            {
                case ActiveTab.Inventory:
                    _modalTitleText.text = "INVENTORY & EQUIPMENT  [Tab / I]";
                    BuildInventoryText(sb);
                    _modalBodyText.text = sb.ToString();
                    _modalFooterText.text = "[1] Drink Aether Mending Tonic (+45 HP)   |   [2] Craft +1 Tonic   |   [Tab / Esc] Close";
                    break;

                case ActiveTab.QuestJournal:
                    _modalTitleText.text = "QUEST JOURNAL — CHRONICLES OF AETHARA  [J]";
                    BuildQuestJournalText(sb);
                    _modalBodyText.text = sb.ToString();
                    _modalFooterText.text = "[Tab] Inventory   |   [K] Character & Factions   |   [J / Esc] Close";
                    break;

                case ActiveTab.Character:
                    _modalTitleText.text = "CHARACTER, RESONANCE & FACTIONS  [K]";
                    BuildCharacterText(sb);
                    _modalBodyText.text = sb.ToString();
                    _modalFooterText.text = "[1] +10 Accord Rep   |   [2] +10 Drifters Rep   |   [3] +10 Void Seekers Rep   |   [K / Esc] Close";
                    break;

                case ActiveTab.PauseSandbox:
                    _modalTitleText.text = "PAUSED — WORLD SANDBOX CONTROLS  [Esc]";
                    BuildPauseSandboxText(sb);
                    _modalBodyText.text = sb.ToString();
                    _modalFooterText.text = "[1-4] Set Time of Day   |   [5] Cycle Weather   |   [F5] Save   |   [F9] Load   |   [Esc] Resume";
                    break;
            }
        }

        private void BuildInventoryText(StringBuilder sb)
        {
            if (_inventory == null) BindPlayerComponents();
            float gold = ServiceLocator.TryGet<EconomySystem>(out var econ) ? econ.PlayerGold : 0f;

            sb.AppendLine($"<color=#FFD966><b>Gold:</b> {gold:0} G</color>     |     <b>Carry Weight:</b> {(_inventory != null ? $"{_inventory.CurrentWeight:0.0} / {_inventory.MaxWeight:0.0} kg" : "0 / 100 kg")}     |     <b>Armor Defense:</b> +{(_inventory != null ? _inventory.GetTotalArmorDefense() : 0f):0}");
            sb.AppendLine("<color=#6688AA>────────────────────────────────────────────────────────────</color>");
            sb.AppendLine("<b>Equipped Gear:</b>  <color=#80E5FF>Solenne Relic Blade</color> (+24 ATK)   |   <color=#99EE99>Drifter's Field Coat</color> (+8 DEF)");
            sb.AppendLine();
            sb.AppendLine("<b>Backpack Contents:</b>");

            if (_inventory == null)
            {
                sb.AppendLine("  <i>No InventoryManager found on Player.</i>");
                return;
            }

            int index = 1;
            foreach (var slot in _inventory.Slots)
            {
                if (slot.IsEmpty) continue;
                string rarityHex = ColorUtility.ToHtmlStringRGB(slot.item.RarityColor);
                sb.AppendLine($"  {index}. <color=#{rarityHex}><b>{slot.item.displayName}</b></color>  x{slot.quantity}   <i>({slot.item.category}, {slot.item.RarityName})</i>");
                sb.AppendLine($"      <color=#B8C4D0>{slot.item.description}</color>");
                index++;
            }

            if (index == 1)
                sb.AppendLine("  <i>Your backpack is currently empty.</i>");
        }

        private void BuildQuestJournalText(StringBuilder sb)
        {
            var qm = QuestManager.Instance;
            if (qm == null)
            {
                sb.AppendLine("<i>QuestManager not initialized.</i>");
                return;
            }

            sb.AppendLine("<color=#FFD966><b>ACTIVE QUESTS</b></color>");
            sb.AppendLine("<color=#6688AA>────────────────────────────────────────────────────────────</color>");

            if (qm.ActiveQuests.Count == 0)
            {
                sb.AppendLine("  <i>No active quests. Speak with <b>Elder Maren</b> near the Verda Ruins ([E]) to begin your expedition!</i>");
            }
            else
            {
                foreach (var q in qm.ActiveQuests)
                {
                    sb.AppendLine($"• <color=#80E5FF><b>{q.title}</b></color>  [{q.questType}]");
                    sb.AppendLine($"  <i>{q.description}</i>");
                    var objs = qm.GetActiveObjectives(q.questId);
                    if (objs != null)
                    {
                        foreach (var o in objs)
                        {
                            string mark = o.IsCompleted ? "<color=#66FF88>[✓]</color>" : "<color=#FFD966>[ ]</color>";
                            sb.AppendLine($"    {mark} {o.description}: <b>{o.currentCount}/{o.requiredCount}</b>");
                        }
                    }
                    if (q.reward != null)
                        sb.AppendLine($"  <color=#FFD966>Rewards: {q.reward.experiencePoints} XP  |  {q.reward.goldAmount:0} Gold</color>");
                    sb.AppendLine();
                }
            }

            sb.AppendLine();
            sb.AppendLine("<color=#66FF88><b>COMPLETED QUESTS</b></color>");
            sb.AppendLine("<color=#6688AA>────────────────────────────────────────────────────────────</color>");
            if (qm.CompletedQuests.Count == 0)
            {
                sb.AppendLine("  <i>None completed yet.</i>");
            }
            else
            {
                foreach (var q in qm.CompletedQuests)
                    sb.AppendLine($"  <color=#66FF88>✓ {q.title}</color>");
            }
        }

        private void BuildCharacterText(StringBuilder sb)
        {
            if (_playerStats == null || _progression == null) BindPlayerComponents();

            sb.AppendLine("<color=#FFD966><b>EXPLORER ATTRIBUTES — KAELEN VEY</b></color>");
            sb.AppendLine("<color=#6688AA>────────────────────────────────────────────────────────────</color>");
            if (_playerStats != null)
            {
                int xpNext = 100 * _playerStats.Level;
                sb.AppendLine($"  <b>Level:</b> {_playerStats.Level}  ({_playerStats.CurrentXP} / {xpNext} XP)     |     <b>Available Skill Points:</b> {(_progression != null ? _progression.AvailableSkillPoints : 0)}");
                sb.AppendLine($"  <b>Health:</b> {_playerStats.CurrentHealth:0}/{_playerStats.MaxHealth.FinalValue:0}     |     <b>Stamina:</b> {_playerStats.CurrentStamina:0}/{_playerStats.MaxStamina.FinalValue:0}");
                sb.AppendLine($"  <b>Attack Power:</b> {_playerStats.AttackDamage.FinalValue:0}     |     <b>Physical Defense:</b> {_playerStats.Defense.FinalValue:0}     |     <b>Move Speed:</b> {_playerStats.MoveSpeed.FinalValue:0.0} m/s");
            }

            sb.AppendLine();
            sb.AppendLine("<color=#80E5FF><b>FACTION REPUTATION (CONTINENT OF AETHARA)</b></color>");
            sb.AppendLine("<color=#6688AA>────────────────────────────────────────────────────────────</color>");
            if (_progression != null)
            {
                foreach (var f in _progression.GetAllFactions())
                {
                    string color = f.value >= 10 ? "#66FF88" : (f.value <= -10 ? "#FF7777" : "#E0E6ED");
                    sb.AppendLine($"  • <b>{f.factionName}:</b> <color={color}>{f.value:+#;-#;0} ({f.Tier})</color>");
                }
            }

            sb.AppendLine();
            sb.AppendLine("<color=#FFD966><b>RESONANCE SKILL BRANCHES</b></color>");
            sb.AppendLine("  • <b>Combat Branch:</b> Aether Blade Combo (Active)  |  Heavy Strikes (+5 ATK)");
            sb.AppendLine("  • <b>Traversal Branch:</b> Fleet Foot (+0.5 Speed)   |  Evasive Roll (Active)");
            sb.AppendLine("  • <b>Scholar Branch:</b> Solenne Echo Sight (Active — Reads Echo Crystals)");
        }

        private void BuildPauseSandboxText(StringBuilder sb)
        {
            string timeStr = TimeSystem.Instance != null ? TimeSystem.Instance.FormattedTime : "08:00 AM";
            string weatherStr = WeatherSystem.Instance != null ? WeatherSystem.Instance.CurrentWeather.ToString() : "Clear";

            sb.AppendLine($"<b>Current World Time:</b> <color=#FFD966>{timeStr}</color>     |     <b>Current Weather:</b> <color=#80E5FF>{weatherStr}</color>");
            sb.AppendLine("<color=#6688AA>────────────────────────────────────────────────────────────</color>");
            sb.AppendLine("<b>LIVE WORLD SANDBOX TELEPORT & ENVIRONMENT CONTROLS:</b>");
            sb.AppendLine("  • Press <b>[1]</b> — Set Time to <b>Dawn (06:30 AM)</b>");
            sb.AppendLine("  • Press <b>[2]</b> — Set Time to <b>High Noon (12:00 PM)</b>");
            sb.AppendLine("  • Press <b>[3]</b> — Set Time to <b>Golden Dusk (06:00 PM)</b>");
            sb.AppendLine("  • Press <b>[4]</b> — Set Time to <b>Starlit Night (11:00 PM)</b>");
            sb.AppendLine("  • Press <b>[5]</b> — Cycle Dynamic Weather (Clear → Cloudy → Rain → Thunderstorm → Foggy)");
            sb.AppendLine();
            sb.AppendLine("<b>CONTROLS REFERENCE:</b>");
            sb.AppendLine("  • <b>WASD</b>: Move   |   <b>Shift</b>: Sprint   |   <b>Space</b>: Jump   |   <b>Ctrl</b>: Dodge Roll");
            sb.AppendLine("  • <b>Left / Right Click</b>: Combo Attack   |   <b>E</b>: Talk to NPC / Examine Echo Crystal");
            sb.AppendLine("  • <b>Tab / I</b>: Inventory   |   <b>J</b>: Quest Journal   |   <b>K</b>: Character & Factions");
            sb.AppendLine("  • <b>F5</b>: Quick Save (Slot 0)   |   <b>F9</b>: Quick Load (Slot 0)");
        }

        private void EnsureModalUI()
        {
            if (_modalRoot != null) return;

            var rootObj = new GameObject("Modal_Window_Overlay", typeof(RectTransform), typeof(Image));
            rootObj.transform.SetParent(transform, false);
            var rootRT = rootObj.GetComponent<RectTransform>();
            rootRT.anchorMin = new Vector2(0.5f, 0.5f);
            rootRT.anchorMax = new Vector2(0.5f, 0.5f);
            rootRT.pivot = new Vector2(0.5f, 0.5f);
            rootRT.sizeDelta = new Vector2(880f, 540f);

            var bgImg = rootObj.GetComponent<Image>();
            bgImg.color = new Color(0.05f, 0.08f, 0.13f, 0.94f);

            _modalTitleText = CreateTMP(rootObj.transform, "ModalTitle",
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -18f), new Vector2(-48f, 42f),
                24f, new Color(1f, 0.86f, 0.42f), TextAlignmentOptions.Left);

            _modalBodyText = CreateTMP(rootObj.transform, "ModalBody",
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -10f), new Vector2(-56f, -130f),
                17f, new Color(0.92f, 0.95f, 0.98f), TextAlignmentOptions.TopLeft);

            _modalFooterText = CreateTMP(rootObj.transform, "ModalFooter",
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 16f), new Vector2(-48f, 36f),
                15f, new Color(0.55f, 0.88f, 1f), TextAlignmentOptions.Center);

            _modalRoot = rootObj;
        }

        private static TextMeshProUGUI CreateTMP(
            Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPos, Vector2 sizeDelta,
            float fontSize, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;

            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = align;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            return tmp;
        }
    }
}
