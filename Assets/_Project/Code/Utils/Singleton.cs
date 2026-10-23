using UnityEngine;

// ============================================================
//  Singleton<T> — Generic MonoBehaviour Singleton base class
//  Usage: public class GameManager : Singleton<GameManager>
// ============================================================
namespace EchoesOfTheLost.Utils
{
    public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T _instance;
        private static readonly object _lock = new object();
        private static bool _isQuitting = false;

        [Header("Singleton Settings")]
        [SerializeField] private bool _dontDestroyOnLoad = true;

        public static T Instance
        {
            get
            {
                if (_isQuitting) return null;

                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = FindAnyObjectByType<T>();

                        if (_instance == null)
                        {
                            var go = new GameObject($"[{typeof(T).Name}]");
                            _instance = go.AddComponent<T>();
                        }
                    }
                    return _instance;
                }
            }
        }

        protected virtual void Awake()
        {
            if (_instance != null && _instance != this as T)
            {
                Debug.LogWarning($"[Singleton] Duplicate {typeof(T).Name} destroyed.");
                Destroy(gameObject);
                return;
            }

            _instance = this as T;
            _isQuitting = false;

            if (_dontDestroyOnLoad)
                DontDestroyOnLoad(gameObject);
        }

        private void OnApplicationQuit()
        {
            _isQuitting = true;
        }

        protected virtual void OnDestroy()
        {
            if (_instance == this as T)
                _instance = null;
        }
    }
}
