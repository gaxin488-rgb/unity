using UnityEngine;

// ============================================================
//  GameManager — Central game state controller
//  Attach to: _Boot scene persistent GameObject
// ============================================================
namespace EchoesOfTheLost.Core
{
    public enum GameState
    {
        Initializing,
        MainMenu,
        Loading,
        Playing,
        Paused,
        Dialogue,
        Cinematic,
        GameOver
    }

    public class GameManager : Utils.Singleton<GameManager>
    {
        [Header("Game Info")]
        [SerializeField] private string _gameVersion = "0.1.0";

        // ── State Machine ────────────────────────────────────
        private StateMachine _stateMachine = new StateMachine();
        private GameState _currentGameState = GameState.Initializing;

        public GameState CurrentState => _currentGameState;
        public string GameVersion => _gameVersion;
        public float TotalPlaytime { get; private set; }

        protected override void Awake()
        {
            base.Awake();

            // Register self with ServiceLocator
            ServiceLocator.Register<GameManager>(this);

            Debug.Log($"[GameManager] Echoes of the Lost v{_gameVersion} — Initialized");
        }

        private void Start()
        {
            SetGameState(GameState.MainMenu);
        }

        private void Update()
        {
            _stateMachine.Update();

            // Track total playtime while playing
            if (_currentGameState == GameState.Playing)
                TotalPlaytime += Time.deltaTime;
        }

        // ── State Management ─────────────────────────────────

        /// <summary>Transition to a new game state.</summary>
        public void SetGameState(GameState newState)
        {
            if (_currentGameState == newState) return;

            var prev = _currentGameState;
            _currentGameState = newState;

            ApplyStateEffects(newState);

            EventBus.Publish(new GameStateChangedEvent
            {
                PreviousState = prev,
                NewState = newState
            });

            Debug.Log($"[GameManager] State: {prev} → {newState}");
        }

        private void ApplyStateEffects(GameState state)
        {
            switch (state)
            {
                case GameState.Playing:
                    Time.timeScale = 1f;
                    InputManager.Instance?.EnablePlayerInput();
                    break;

                case GameState.Paused:
                    Time.timeScale = 0f;
                    InputManager.Instance?.DisablePlayerInput();
                    break;

                case GameState.Dialogue:
                case GameState.Cinematic:
                    Time.timeScale = 1f;
                    InputManager.Instance?.DisablePlayerInput();
                    break;

                case GameState.GameOver:
                    Time.timeScale = 0f;
                    InputManager.Instance?.DisablePlayerInput();
                    break;

                case GameState.MainMenu:
                    Time.timeScale = 1f;
                    break;
            }
        }

        // ── Game Flow Helpers ────────────────────────────────

        public void StartNewGame()
        {
            SetGameState(GameState.Loading);
            SceneLoader.Instance?.LoadWorld("World_Persistent");
        }

        public void LoadGame(int slot)
        {
            var saveData = Systems.SaveSystem.Instance?.LoadGame(slot);
            if (saveData == null) return;

            SetGameState(GameState.Loading);
            SceneLoader.Instance?.LoadWorld(saveData.currentScene ?? "World_Persistent");
        }

        public void PauseGame()
        {
            if (_currentGameState == GameState.Playing)
                SetGameState(GameState.Paused);
        }

        public void ResumeGame()
        {
            if (_currentGameState == GameState.Paused)
                SetGameState(GameState.Playing);
        }

        public void ReturnToMainMenu()
        {
            SetGameState(GameState.Loading);
            SceneLoader.Instance?.LoadMainMenu();
            SetGameState(GameState.MainMenu);
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
