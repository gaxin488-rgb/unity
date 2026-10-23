# 🔧 Technical Design Document (TDD)
## Project: Echoes of the Lost

> **Version:** 0.1 | **Date:** 2026-10-23 | **Engine:** Unity 6000.6.5f1

---

## 1. Engine & Platform Specs

| Field | Value |
|---|---|
| **Unity Version** | 6000.6.5f1 (Unity 6 LTS) |
| **Render Pipeline** | Universal Render Pipeline (URP) |
| **Scripting Backend** | IL2CPP |
| **API Compatibility** | .NET Standard 2.1 |
| **Target FPS** | 60fps (PC High) / 30fps (PC Low / Console) |
| **Minimum PC Spec** | GTX 1060 / 8GB RAM / SSD recommended |
| **Recommended PC Spec** | RTX 3070 / 16GB RAM / NVMe SSD |

---

## 2. Architecture Pattern

```
Pattern: Service Locator + ScriptableObject Architecture + Event-Driven

Reasons:
- ScriptableObject: Decouples data from logic, designer-friendly, hot-reloadable
- Service Locator: Lightweight DI without full framework overhead (solo dev)
- Event Bus: Prevents tight coupling between systems (Combat ↔ UI ↔ Audio)
- State Machines: Predictable, debuggable character & game state management
```

### 2.1 System Dependency Graph

```
[GameManager]
    ├── [SceneLoader]       ← Manages additive scene loading
    ├── [ServiceLocator]    ← Registry for all services
    │   ├── [SaveService]
    │   ├── [AudioService]
    │   ├── [UIService]
    │   ├── [InputService]
    │   └── [TimeService]
    └── [EventBus]          ← Global pub/sub (no direct references)
            ↑ subscribed by all systems
```

---

## 3. Scene Architecture

```
Scene Structure (Additive Loading):
┌─────────────────────────────────────────┐
│  _Boot (PERSISTENT — never unloads)     │
│  ├── GameManager                        │
│  ├── ServiceLocator                     │
│  ├── EventBus                           │
│  ├── InputManager                       │
│  └── AudioManager                       │
├─────────────────────────────────────────┤
│  _UI (Persistent UI layer)              │
│  ├── HUD Canvas                         │
│  ├── Pause Menu Canvas                  │
│  └── Loading Screen Canvas             │
├─────────────────────────────────────────┤
│  MainMenu  OR  World_Persistent         │
│  World_Chunk_XX_YY (loaded dynamically) │
└─────────────────────────────────────────┘
```

---

## 4. Packages (Package Manager)

### 4.1 Unity Registry Packages
```json
{
  "dependencies": {
    "com.unity.render-pipelines.universal": "17.x",
    "com.unity.inputsystem": "1.x",
    "com.unity.cinemachine": "3.x",
    "com.unity.textmeshpro": "3.x",
    "com.unity.ai.navigation": "2.x",
    "com.unity.addressables": "2.x",
    "com.unity.timeline": "1.x",
    "com.unity.visualeffectgraph": "17.x",
    "com.unity.shadergraph": "17.x",
    "com.unity.terrain-tools": "5.x",
    "com.unity.burst": "1.x",
    "com.unity.collections": "2.x",
    "com.unity.probuilder": "6.x",
    "com.unity.test-framework": "1.x",
    "com.unity.analytics": "6.x",
    "com.unity.behavior": "1.x"
  }
}
```

### 4.2 Third-Party (Manual Import)
| Package | Purpose | Source |
|---|---|---|
| DOTween Pro | Smooth UI & world animations | Asset Store |
| Odin Inspector | Powerful editor tooling | Asset Store |
| Ink Unity Integration | Narrative branching dialogue | GitHub (inkle) |
| Naughty Attributes | Quick editor attributes | GitHub (dbrizov) |

---

## 5. Code Standards & Conventions

### 5.1 Naming Conventions
```csharp
// Classes: PascalCase
public class PlayerController { }
public class QuestData : ScriptableObject { }

// Interfaces: IPascalCase
public interface IDamageable { }
public interface ISaveable { }

// Private fields: _camelCase
private float _moveSpeed;
private PlayerStats _stats;

// Public properties: PascalCase
public float MoveSpeed => _moveSpeed;

// Constants: ALL_CAPS
private const float MAX_SPEED = 10f;

// Events: On + PascalCase
public static event Action<int> OnHealthChanged;
public static event Action<QuestData> OnQuestCompleted;
```

### 5.2 File Structure Convention
```
Every MonoBehaviour file follows:
1. [Header] regions
2. [SerializeField] private fields
3. Properties
4. Unity lifecycle (Awake → OnEnable → Start → Update → OnDisable → OnDestroy)
5. Public methods
6. Private methods
7. Event handlers (On___)
8. Gizmos
```

### 5.3 Performance Rules
```csharp
// ❌ NEVER in Update():
GetComponent<T>();
FindObjectOfType<T>();
new List<T>();

// ✅ ALWAYS:
// Cache in Awake/Start
// Use object pooling for frequent instantiation
// Prefer struct over class for value types in hot paths
// No LINQ in Update() — use loops
// Use [System.Serializable] structs for SaveData
```

---

## 6. World Streaming Architecture

```
World Chunk System:
- Chunk Size: 500 × 500 Unity units
- Load Radius: 3 chunks (1500 units from player)
- Unload Radius: 5 chunks (2500 units)
- Async Loading: Addressables (non-blocking)
- Priority: Distance-based priority queue

Memory Budget:
- Terrain textures: 512MB max
- Active meshes: 256MB max
- Audio: 128MB max
- Total target: < 4GB RAM usage
```

---

## 7. Save System Design

```csharp
// Save file location
// Windows: %APPDATA%\EchoesOfTheLost\saves\
// File: slot_X.sav (JSON + AES-256 encrypted)

[System.Serializable]
public class SaveData
{
    public string version;
    public System.DateTime saveTime;
    public PlayerSaveData player;
    public WorldSaveData world;
    public QuestSaveData quests;
    public string currentScene;
    public Vector3 playerPosition;
    public float gameTimeHours;
}
```

---

## 8. Input Map

```
Action                  | KB+Mouse          | Gamepad
------------------------|-------------------|------------------
Move                    | WASD              | L-Stick
Look                    | Mouse             | R-Stick
Jump                    | Space             | A / Cross
Sprint                  | Left Shift (hold) | L3 (hold)
Roll / Dodge            | Left Shift (tap)  | Circle / B
Crouch                  | C                 | R3
Interact                | E                 | Square / X
Attack (Light)          | Left Click        | X / Square
Attack (Heavy)          | Right Click       | Y / Triangle
Block / Parry           | Middle Click / Q  | L2 / LT
Inventory               | I / Tab           | D-pad Up
Map                     | M                 | Select / View
Quest Journal           | J                 | D-pad Down
Pause                   | Escape            | Start / Options
```

---

## 9. Audio Architecture

```
FMOD Studio Integration:
Banks:
- Master.bank          (always loaded)
- Music.bank           (streamed)
- SFX_Combat.bank      (loaded in combat zones)
- SFX_Environment.bank (always loaded)
- UI.bank              (always loaded)
- Ambient.bank         (loaded per biome)

Dynamic Music States:
- State: Exploration → calm, atmospheric
- State: Alert      → tense, faster tempo
- State: Combat     → intense, full arrangement
- State: Cinematic  → specific per scene
- Transition: Smooth crossfade via FMOD parameters
```

---

## 10. Rendering Settings (URP)

```yaml
# URP Asset Settings
mainLightCastShadows: true
shadowDistance: 150
shadowCascades: 4
additionalLightsPerObjectLimit: 8
softShadows: true
HDR: true
antiAliasing: TAA

# Post-Processing Stack (Global Volume):
- Bloom (Intensity: 0.3, Threshold: 0.9)
- Color Grading (LUT-based per biome)
- Ambient Occlusion (SSAO)
- Depth of Field (cinematic mode only)
- Vignette (subtle, strength: 0.25)
- Tonemapping: ACES
- Motion Blur: Off by default (accessibility)
- Film Grain: Subtle (intensity: 0.1)
```

---

## 11. Git & Version Control

```bash
# .gitignore includes:
/Library/
/Temp/
/Logs/
/obj/
/Build/
/Builds/
*.pidb.meta
*.pdb.meta
sysinfo.txt
*.apk
*.aab

# Git LFS tracks:
*.png *.jpg *.tga *.psd    # Textures
*.fbx *.obj *.blend        # 3D models
*.wav *.mp3 *.ogg          # Audio
*.unity *.prefab           # Unity binary files
*.controller               # Animator controllers
*.asset                    # Large assets
```

---

## 12. Build Pipeline

```
Branches:
  main       → Release build (manual trigger)
  develop    → QA build (nightly via Unity Cloud Build)
  feature/*  → Dev build (local only)

Build Configs:
  Debug:   Development build + Profiler + Cheats enabled
  QA:      Profiler enabled + Cheats disabled + Analytics
  Release: IL2CPP + stripped symbols + Analytics
```

---

*TDD sẽ được cập nhật sau mỗi phase.*
