# 📊 Tiến Độ Phát Triển — Echoes of the Lost

> **Cập nhật:** 2026-10-23 | **Unity Version:** `6000.6.5f1` (Unity 6 LTS) | **Trạng thái:** Phase 0–4 Vertical Slice "Verda Wilds" Hoàn Chỉnh ✅

---

## Phase 0 — Pre-Production & Setup ✅

| Task | Status | File |
|------|--------|------|
| Game Design Document (GDD) | ✅ Done | `Docs/GDD.md` |
| Technical Design Document (TDD) | ✅ Done | `Docs/TDD.md` |
| World & Lore Bible | ✅ Done | `Docs/WorldBible.md` |
| Story Bible (3 Acts, 12 Main Quests) | ✅ Done | `Docs/StoryBible.md` |
| Cấu trúc thư mục chuẩn (99 folders) | ✅ Done | `Assets/_Project/...` |
| `.gitignore` + `.gitattributes` (Git LFS) | ✅ Done | Root |
| Unity 6 Packages (`URP`, `InputSystem`, `AI Navigation`, `Addressables`, `Timeline`) | ✅ Done | `Packages/manifest.json` |
| `PlayerInputActions.inputactions` (14 actions, 32 bindings) | ✅ Done | `Assets/_Project/Settings/Input/PlayerInputActions.inputactions` |

---

## Phase 1 — Core Architecture ✅

| Task | Status | File |
|------|--------|------|
| `Singleton<T>` base class | ✅ Done | `Assets/_Project/Code/Utils/Singleton.cs` |
| `ObjectPool<T>` | ✅ Done | `Assets/_Project/Code/Utils/ObjectPool.cs` |
| `Extensions` | ✅ Done | `Assets/_Project/Code/Utils/Extensions.cs` |
| `StateMachine` & `IState` | ✅ Done | `Assets/_Project/Code/Core/StateMachine.cs` |
| `EventBus` + `GameEvents` | ✅ Done | `Assets/_Project/Code/Core/EventBus.cs`, `GameEvents.cs` |
| `ServiceLocator` | ✅ Done | `Assets/_Project/Code/Core/ServiceLocator.cs` |
| `GameManager` | ✅ Done | `Assets/_Project/Code/Core/GameManager.cs` |
| `SceneLoader` | ✅ Done | `Assets/_Project/Code/Core/SceneLoader.cs` |
| `InputManager` (Direct `InputActionAsset` binding) | ✅ Done | `Assets/_Project/Code/Core/InputManager.cs` |
| `BootLoader` | ✅ Done | `Assets/_Project/Code/Core/BootLoader.cs` |
| `PlayerController` (Rigidbody 3P + StateMachine) | ✅ Done | `Assets/_Project/Code/Characters/Player/PlayerController.cs` |
| `PlayerStats` (`StatValue`, HP, Stamina, XP, Level) | ✅ Done | `Assets/_Project/Code/Characters/Player/PlayerStats.cs` |
| `InteractionSystem` (`IInteractable` detection) | ✅ Done | `Assets/_Project/Code/Characters/Player/InteractionSystem.cs` |
| `ThirdPersonCamera` (Mouse orbit, collision, smooth follow) | ✅ Done | `Assets/_Project/Code/Characters/Player/ThirdPersonCamera.cs` |
| `SaveSystem` (JSON multi-slot + `F5`/`F9` Quick Save/Load) | ✅ Done | `Assets/_Project/Code/Systems/SaveSystem/SaveSystem.cs` |
| `AudioManager` (Pooled 3D SFX + Music crossfade) | ✅ Done | `Assets/_Project/Code/Systems/AudioSystem/AudioManager.cs` |

---

## Phase 2 — World Building & Environment Systems ✅

| Task | Status | File |
|------|--------|------|
| `TimeSystem` (Chu kỳ Ngày/Đêm 24h, xoay Sun/Moon, đổi màu Fog/Ambient) | ✅ Done | `Assets/_Project/Code/World/TimeSystem.cs` |
| `WeatherSystem` (`Clear`, `Cloudy`, `Rain`, `Thunderstorm`, `Foggy`) | ✅ Done | `Assets/_Project/Code/World/WeatherSystem.cs` |
| `BiomeData` ScriptableObject + `Biome_VerdaWilds.asset` | ✅ Done | `Assets/_Project/Code/World/BiomeData.cs` |
| `ChunkSystem` (Streaming theo bán kính quanh Player) | ✅ Done | `Assets/_Project/Code/World/ChunkSystem.cs` |
| `TerrainManager` | ✅ Done | `Assets/_Project/Code/World/TerrainManager.cs` |
| `FoliageSystem` (GPU Instancing) | ✅ Done | `Assets/_Project/Code/World/FoliageSystem.cs` |
| `WaterSystem` & `WaterZone` (Buoyancy + Swim trigger) | ✅ Done | `Assets/_Project/Code/World/WaterSystem.cs` |
| `POIManager` (Khám phá địa danh + thưởng XP) | ✅ Done | `Assets/_Project/Code/World/POIManager.cs` |
| `EnvironmentManager` | ✅ Done | `Assets/_Project/Code/World/EnvironmentManager.cs` |
| `InteriorLoader` & `AmbientSoundZone` | ✅ Done | `Assets/_Project/Code/World/InteriorLoader.cs`, `AmbientSoundZone.cs` |
| `EchoCrystalInteractable` (Tinh thể ký ức Solenne phát sáng) | ✅ Done | `Assets/_Project/Code/World/EchoCrystalInteractable.cs` |

---

## Phase 3 & 4 — Gameplay Loop, Content & Vertical Slice "Verda Wilds" ✅

| Task | Status | File |
|------|--------|------|
| `QuestData` & `QuestManager` + `Quest_FirstEcho.asset` | ✅ Done | `Assets/_Project/Code/Systems/QuestSystem/` |
| `DialogueManager` (Typewriter, multi-line NPC dialogue & Ink hooks) | ✅ Done | `Assets/_Project/Code/Systems/DialogueSystem/DialogueManager.cs` |
| `CombatManager` (Combo 3 đòn, Heavy Attack, Parry, HitStop, Timed Hitbox) | ✅ Done | `Assets/_Project/Code/Systems/CombatSystem/CombatManager.cs` |
| `EnemyBase` (Patrol/Chase/Attack/Flee/Dead, NavMesh + XZ fallback) | ✅ Done | `Assets/_Project/Code/Characters/Enemies/EnemyBase.cs` |
| `WorldHealthBar` (Thanh máu 3D nổi trên đầu quái) | ✅ Done | `Assets/_Project/Code/Characters/Enemies/WorldHealthBar.cs` |
| `EnemySpawner` & `LootSystem` | ✅ Done | `EnemySpawner.cs`, `LootSystem.cs` |
| `NPCController` (Elder Maren — thoại & giao nhiệm vụ) | ✅ Done | `Assets/_Project/Code/Characters/NPCs/NPCController.cs` |
| `ItemData`, `ItemDatabase`, `InventoryManager` | ✅ Done | `Assets/_Project/Code/Systems/InventorySystem/` |
| `EconomySystem` & `ProgressionSystem` (3 nhánh kỹ năng + 3 Phe phái) | ✅ Done | `EconomySystem.cs`, `ProgressionSystem.cs` |
| `WorldEventSystem` & `CinematicSystem` | ✅ Done | `WorldEventSystem.cs`, `CinematicSystem.cs` |

---

## Phase 5 — UI/UX, Scene Builder & QA Verification ✅

| Task | Status | File |
|------|--------|------|
| `HUDController` (HP/Stamina, Level/XP/Gold, Đồng hồ/Thời tiết, Quest Tracker, Prompt `[E]`, Hộp thoại, Toast) | ✅ Done | `Assets/_Project/Code/UI/HUDController.cs` |
| `UIManager` (Cửa sổ Túi đồ `[Tab/I]`, Nhật ký Nhiệm vụ `[J]`, Nhân vật & Phe phái `[K]`, Pause & Sandbox `[Esc]`) | ✅ Done | `Assets/_Project/Code/UI/UIManager.cs` |
| `MiniMapSystem` | ✅ Done | `Assets/_Project/Code/UI/MiniMapSystem.cs` |
| `EnvironmentTextureGenerator` (16 PBR Textures `Albedo + Normal`, 10 URP Materials, 5 `TerrainLayer`s & URP Pipeline Asset) | ✅ Done | `Assets/_Project/Code/Editor/EnvironmentTextureGenerator.cs` |
| `TerrainWorldBuilder` (4-Stage Unity Terrain: Build Terrain -> Paint 5-Layer Splatmap -> Add 155 Trees & 3D Grass Details -> Edit/Smooth Terrain) | ✅ Done | `Assets/_Project/Code/Editor/TerrainWorldBuilder.cs` |
| `SandboxSceneBuilder` (`v4_terrain_world` — tự động dựng toàn bộ Vertical Slice, PBR Materials & Unity Terrain) | ✅ Done | `Assets/_Project/Code/Editor/SandboxSceneBuilder.cs` |
| `CoreSystemsVerifier` (`EotL/QA/Run Core Systems Self-Test`) | ✅ Done | `Assets/_Project/Code/Editor/CoreSystemsVerifier.cs` |

---

## 🎮 Hướng Dẫn Điều Khiển Trong Play Mode (`Sandbox.unity`)

- **Di chuyển & Hành động:** `WASD` Di chuyển | `Mouse` Xoay Camera | `Shift` Chạy nhanh | `Space` Nhảy | `Ctrl` Lộn né (Roll)
- **Chiến đấu:** `Chuột Trái (LMB)` Đánh thường (Combo 3 đòn) | `Chuột Phải (RMB)` Đánh mạnh (Heavy Attack)
- **Tương tác:** `E` Trò chuyện với **Elder Maren**, giải mã **Solenne Echo Crystals**, hoặc tiếp tục hội thoại
- **Giao diện (UI Modals):**
  - `Tab` hoặc `I`: Mở **Túi Đồ & Trang Bị (Inventory)** — bấm `[1]` để uống bình máu *Aether Mending Tonic (+45 HP)*
  - `J`: Mở **Nhật Ký Nhiệm Vụ (Quest Journal)**
  - `K`: Mở **Bảng Nhân Vật, Kỹ Năng & Danh Vọng 3 Phe Phái (Character & Factions)**
  - `Esc`: Mở **Menu Tạm Dừng & Điều Khiển Thế Giới (World Sandbox Controls)** — chỉnh nhanh Bình minh / Giữa trưa / Hoàng hôn / Ban đêm và đổi Thời tiết (`Clear`, `Rain`, `Thunderstorm`, `Foggy`)
- **Lưu / Tải nhanh:** `F5` Quick Save | `F9` Quick Load
