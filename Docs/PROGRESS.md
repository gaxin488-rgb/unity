# 📊 Tiến Độ Phát Triển — Echoes of the Lost

> **Cập nhật:** 2026-10-24 | **Unity Version:** `6000.6.5f1` (Unity 6 LTS) | **Trạng thái:** Phase 0–4 Vertical Slice "Verda Wilds" Hoàn Chỉnh ✅

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
| `HUDController` (HP/Stamina, Level/XP/Gold, Đồng hồ/Thời tiết, Quest Tracker, Prompt `[E]`, Hộp thoại, Toast slide/fade) | ✅ Done | `Assets/_Project/Code/UI/HUDController.cs` |
| `UIElementAnimator` (Hoạt ảnh HUD: HP Damage Trail, Low-HP Pulse, Hotbar Slot Pop, Compass & Mini-Map Arrow, AnimatorClips) | ✅ Done | `Assets/_Project/Code/UI/UIElementAnimator.cs` |
| `UIManager` (Cửa sổ Túi đồ `[Tab/I]`, Nhật ký Nhiệm vụ `[J]`, Nhân vật & Phe phái `[K]`, Pause & Sandbox `[Esc]`) | ✅ Done | `Assets/_Project/Code/UI/UIManager.cs` |
| `MiniMapSystem` & `MiniMap_Camera` (`RT_MiniMap.renderTexture` live overhead radar) | ✅ Done | `Assets/_Project/Code/UI/MiniMapSystem.cs` |
| `UICanvasCameraBuilder` (4-Stage UI & Camera: Set Up Canvas -> On-Screen UI Elements & 9 Sprites -> UI Animator/Clips -> 3 Camera View Presets) | ✅ Done | `Assets/_Project/Code/Editor/UICanvasCameraBuilder.cs` |
| `EnvironmentTextureGenerator` (16 PBR Textures `Albedo + Normal`, 10 URP Materials, 5 `TerrainLayer`s & URP Pipeline Asset) | ✅ Done | `Assets/_Project/Code/Editor/EnvironmentTextureGenerator.cs` |
| `TerrainWorldBuilder` (4-Stage Unity Terrain: Build Terrain -> Paint 5-Layer Splatmap -> Add 155 Trees & 3D Grass Details -> Edit/Smooth Terrain) | ✅ Done | `Assets/_Project/Code/Editor/TerrainWorldBuilder.cs` |
| `CoreSystemsVerifier` (`EotL/QA/Run Core Systems Self-Test`) | ✅ Done | `Assets/_Project/Code/Editor/CoreSystemsVerifier.cs` |

---

## Phase 6 — Scripting, Prototyping, Audio, Combat & Day/Night Systems ✅

| Task | Status | File |
|------|--------|------|
| `SolenneAetherShrine` (Pipeline A: C# Scripting, Coroutines, Multi-GameObject binding, Attunement sequence) | ✅ Done | `Assets/_Project/Code/World/SolenneAetherShrine.cs` |
| `AetherCompanionDrone` (Pipeline A: Escort orbit, Scanning coroutines, Stabilizer gyros) | ✅ Done | `Assets/_Project/Code/World/AetherCompanionDrone.cs` |
| `FirstPersonController` (Pipeline B: First-person camera mount, head-bob, FOV kick, viewmodel blade `[C]`) | ✅ Done | `Assets/_Project/Code/Characters/Player/FirstPersonController.cs` |
| `PlaceholderObjectMarker` (Pipeline B: Greybox blockouts, traversal ramps, stepping pillars, cover walls) | ✅ Done | `Assets/_Project/Code/World/PlaceholderObjectMarker.cs` |
| `GameplayTriggerVolume` (Pipeline B: SpeedBoostGate, HealingSanctuary, RuinsAmbushAlarm) | ✅ Done | `Assets/_Project/Code/World/GameplayTriggerVolume.cs` |
| `PlayerFootstepAudio` (Pipeline B: Terrain Splatmap & Surface tag detection, walk/sprint cadence audio) | ✅ Done | `Assets/_Project/Code/Characters/Player/PlayerFootstepAudio.cs` |
| `GameAudioGenerator` (14 procedural 16-bit PCM WAV SFX: footsteps, slashes, impacts, parry sparks) | ✅ Done | `Assets/_Project/Code/Editor/GameAudioGenerator.cs` |
| `CombatTargetDummy` (Pipeline C: Sparring dummy, IDamageable, squash/stretch wobble reaction) | ✅ Done | `Assets/_Project/Code/World/CombatTargetDummy.cs` |
| `CombatManager` (Pipeline C: Synchronized slash/impact/parry audio, combo timing, hitstop) | ✅ Done | `Assets/_Project/Code/Systems/CombatSystem/CombatManager.cs` |
| `TimeSystem` & `WeatherSystem` (Pipeline D: 24h Sun & Moon rotation, soft shadows, atmospheric fog, `[F1]`-`[F4]`) | ✅ Done | `Assets/_Project/Code/World/TimeSystem.cs` |
| `GameplaySystemsBuilder` (Automated builder linking all gameplay, audio, triggers, and lighting) | ✅ Done | `Assets/_Project/Code/Editor/GameplaySystemsBuilder.cs` |
| `SandboxSceneBuilder` (`v6_scripting_gameplay_systems` — scene build orchestrator) | ✅ Done | `Assets/_Project/Code/Editor/SandboxSceneBuilder.cs` |

---

## Phase 7 — Scenic Nature: Branching Trees, Undulating Hills & 4-Color Wildflowers ✅

| Task | Status | File |
|------|--------|------|
| `BuildCombinedTreeMesh` (Cây cối phân nhánh: Thân chính, 4 nhánh bough, 10 cành phụ & 22 tán lá nhỏ thoáng đãng) | ✅ Done | `Assets/_Project/Code/Editor/TerrainWorldBuilder.cs` |
| `SculptVerdaWildsHeightmap` (Thảm cỏ trải dài cao thấp: Thuật toán sóng giao thoa đa tần số nhấp nhô $2\text{m} - 6\text{m}$) | ✅ Done | `Assets/_Project/Code/Editor/TerrainWorldBuilder.cs` |
| `BuildFlowerClusterMesh` (Khóm hoa đa tầng: Submesh 0 cánh hoa sắc màu, Submesh 1 cành lá xanh mướt) | ✅ Done | `Assets/_Project/Code/Editor/TerrainWorldBuilder.cs` |
| `EnvironmentTextureGenerator` (4 vật liệu hoa PBR URP Lit bão hòa: Vàng, Đỏ, Xanh, Tím + Cuống xanh) | ✅ Done | `Assets/_Project/Code/Editor/EnvironmentTextureGenerator.cs` |
| `TerrainWorldBuilder` (5 DetailPrototypes & phân bố mật độ tế bào Perlin trên TerrainData) | ✅ Done | `Assets/_Project/Code/Editor/TerrainWorldBuilder.cs` |
| `CreateRoadsideWallAndFlowers` (Bờ tường đá cuội mộc mạc và dải hoa dại ôm chân tường phỏng theo *Slow Roads*) | ✅ Done | `Assets/_Project/Code/Editor/SandboxSceneBuilder.cs` |
| 4 Prefabs Hoa Dại 3D (`PF_FlowerCluster_Yellow`, `_Red`, `_Blue`, `_Purple`) | ✅ Done | `Assets/_Project/Prefabs/Environment/` |
| Bụi cỏ Alpha Cutout & Crossed-Quads (Tạo ngọn cỏ nhọn tự nhiên, nền trong suốt `Color.clear`, URP Alpha Clipping `_AlphaClip = 1f`, double-sided) | ✅ Done | `EnvironmentTextureGenerator.cs`, `TerrainWorldBuilder.cs`, `SandboxSceneBuilder.cs` |
| Cây cao nhiều nhánh đệ quy & Tán lá Crossed Quads cắt Alpha (Hết ô tròn, texture lá viền răng cưa trong suốt, hàm `BuildBranch` đệ quy 4 cấp, URP Alpha Cutout `_Cull = Off`) | ✅ Done | `EnvironmentTextureGenerator.cs`, `SandboxSceneBuilder.cs`, `TerrainWorldBuilder.cs` |
| 95 Lá Mũi Mác Xếp Lớp & Cây Uốn Cong Tự Nhiên (`AppendCurvedTaperedBranch`, 5 Biến thể Sồi/Thông, Arched Dome Leaf Cards & Spherical Outward Normals) | ✅ Done | `TerrainWorldBuilder.cs`, `EnvironmentTextureGenerator.cs` |
| Khảo Sát Cảnh Quan Map Thuần Khiết (Tạm ẩn capsule nhân vật, viewmodel kiếm FP, Guardian enemies, NPC Elder Maren, Dummy bia tập, tường combat, tắt CombatManager; cho phép di chuyển WASD tự do với tầm nhìn quang đãng 100%) | ✅ Done | `SandboxSceneBuilder.cs`, `GameplaySystemsBuilder.cs`, `Sandbox.unity` |
| Cây Ven Đường & Tán Lá 380 Lá Nhỏ Với Cành Dăm & Khe Nắng (`UpgradeStandaloneSceneTrees` tự động quét thay thế cây rời cũ sang Mesh Sồi uốn lượn; Texture 7 chùm cành gỗ dăm & 380 lá sồi nhỏ tơi xốp chừa khe thoáng bầu trời, chống viền đen alpha bleeding) | ✅ Done | `EnvironmentTextureGenerator.cs`, `TerrainWorldBuilder.cs`, `SandboxSceneBuilder.cs`, `Sandbox.unity` |
| Sửa Lỗi Toàn Diện Điều Khiển, Camera & Bật Lại Nhân Vật/Combat (`InputManager` lia chuột không trễ, `ThirdPersonCamera` xoay mượt `SmoothDampAngle` & auto-lock cursor, `FirstPersonController` đồng bộ góc nhìn mượt khi bấm [C]/[V], `PlayerController` chống tự xoay 180 độ khi đi lùi FP, bật lại Player/Combat/NPC/Quái, chuyển Cây & Hoa ven đường sang Mesh 3D tối ưu) | ✅ Done | `InputManager.cs`, `ThirdPersonCamera.cs`, `FirstPersonController.cs`, `PlayerController.cs`, `UIManager.cs`, `SandboxSceneBuilder.cs`, `Sandbox.unity` |
| Xóa Bỏ Player Model, CombatManager, NPC Elder Maren & Combat Target Dummy (Dọn dẹp triệt để mesh capsule nhân vật, gỡ bỏ CombatManager, xóa NPC Elder Maren & Mat_ElderMaren, gỡ dummy bia tập khỏi GameplaySystemsBuilder; tách độc lập IDamageable interface đảm bảo compile sạch 0 lỗi và scene Sandbox tinh gọn) | ✅ Done | `CombatManager.cs` (deleted), `CombatTargetDummy.cs` (deleted), `Mat_ElderMaren.mat` (deleted), `IDamageable.cs`, `SandboxSceneBuilder.cs`, `GameplaySystemsBuilder.cs`, `Sandbox.unity` |
| Tinh Gọn HUD Map & Sửa Lỗi Mini-Map (`MiniMapSystem.cs` sửa culling mask chống đen màn hình & tắt FogOfWar; `FirstPersonController.cs` luôn ẩn body model + kiếm viewmodel; `GameplaySystemsBuilder.cs` tắt sinh Wayshrine/Drone/Placeholders/Triggers; `UICanvasCameraBuilder.cs` loại bỏ HP/Stamina/XP/Hotbar/Reticle, chỉ giữ La bàn, Mini-Map sắc nét và Toast thông báo) | ✅ Done | `MiniMapSystem.cs`, `FirstPersonController.cs`, `GameplaySystemsBuilder.cs`, `UICanvasCameraBuilder.cs`, `Sandbox.unity` |
| Sửa 4 Lỗi Đỏ Console Input & Xóa Quái Guardian / Đá Echo Crystal (`InputManager.cs` đọc trực tiếp `Keyboard.current` và `Mouse.current` loại bỏ hoàn toàn lỗi "Map index on InputActionMap is out of range"; `SandboxSceneBuilder.cs` xóa bỏ việc tạo 2 quái OvergrownGuardian và 2 trụ đá EchoCrystal, cảnh quan thuần khiết 100%) | ✅ Done | `InputManager.cs`, `SandboxSceneBuilder.cs`, `Sandbox.unity` |

---

## 🎮 Hướng Dẫn Điều Khiển Trong Chế Độ Khám Phá Bản Đồ (`Sandbox.unity`)

- **Di chuyển & Hành động:** `WASD` Di chuyển | `Mouse` Xoay Camera | `Shift` Chạy nhanh (Footstep cadence tăng tốc) | `Space` Nhảy | `Ctrl` Lộn né (Roll)
- **Góc nhìn Camera & Góc nhìn Thứ Nhất:**
  - `C`: **Bật/Tắt Góc nhìn Thứ Nhất (First-Person View)** — góc nhìn trực diện quang đãng không bị che khuất bởi model hay kiếm, có chuyển động nhấp nhô đầu (Head-bob).
  - `V`: **Đổi 3 góc nhìn Camera Thứ Ba** (`Exploration` / `Close Action` / `Panoramic Tactical`)
  - `Con lăn chuột (Scroll)`: Phóng to / Thu nhỏ khoảng cách Camera Orbit
- **Giao diện Khám Phá (HUD):**
  - **La bàn đỉnh màn hình (Top-Center):** Hiển thị hướng đi (N, S, E, W) và độ góc thời gian thực.
  - **Mini-Map góc phải (Top-Right):** Radar nhìn từ trên cao 65m, hiển thị rõ ràng địa hình, đường đi và tán cây cối, mũi tên theo hướng nhìn.
  - **Bảng hướng dẫn góc trái (Bottom-Left):** Gợi ý phím tắt nhanh.
- **Chu kỳ Thời gian & Ánh sáng Nhanh:**
  - `F1`: Bình minh (06:30 AM)
  - `F2`: Giữa trưa (12:00 PM)
  - `F3`: Hoàng hôn (06:15 PM — xem bóng đổ mềm dài tuyệt đẹp)
  - `F4`: Nửa đêm (00:00 AM — ánh trăng bạc soi thung lũng)
- **Giao diện Tạm dừng & Menu:**
  - `Esc`: Mở **Menu Điều Khiển Thế Giới (World Sandbox Controls)** để tùy chỉnh thời gian và thời tiết.
- **Lưu / Tải nhanh:** `F5` Quick Save | `F9` Quick Load
