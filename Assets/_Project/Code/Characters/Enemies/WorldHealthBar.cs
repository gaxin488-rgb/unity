using UnityEngine;

// ============================================================
//  WorldHealthBar — Simple 3D floating health bar above EnemyBase
//  Uses two child Quads/Cubes so it requires zero external UI prefabs.
// ============================================================
namespace EchoesOfTheLost.Characters.Enemies
{
    [RequireComponent(typeof(EnemyBase))]
    public class WorldHealthBar : MonoBehaviour
    {
        [SerializeField] private Vector3 _offset = new Vector3(0f, 2.4f, 0f);
        [SerializeField] private float _barWidth = 1.2f;
        [SerializeField] private float _barHeight = 0.14f;

        private EnemyBase _enemy;
        private Transform _barRoot;
        private Transform _fillTransform;
        private Camera _mainCam;

        private void Awake()
        {
            _enemy = GetComponent<EnemyBase>();
            CreateBarVisuals();
        }

        private void Start()
        {
            _mainCam = Camera.main;
            _enemy.OnHealthChanged += OnHealthChanged;
            UpdateFill(_enemy.HealthPercent);
        }

        private void OnDestroy()
        {
            if (_enemy != null)
                _enemy.OnHealthChanged -= OnHealthChanged;
        }

        private void LateUpdate()
        {
            if (_barRoot == null) return;
            if (_enemy.IsDead)
            {
                _barRoot.gameObject.SetActive(false);
                return;
            }

            _barRoot.position = transform.position + _offset;
            if (_mainCam == null) _mainCam = Camera.main;
            if (_mainCam != null)
                _barRoot.rotation = _mainCam.transform.rotation;
        }

        private void OnHealthChanged(float current, float max)
        {
            UpdateFill(max > 0f ? Mathf.Clamp01(current / max) : 0f);
        }

        private void UpdateFill(float pct)
        {
            if (_fillTransform == null) return;
            _fillTransform.localScale = new Vector3(_barWidth * pct, _barHeight, 0.02f);
            _fillTransform.localPosition = new Vector3(-(_barWidth * (1f - pct)) * 0.5f, 0f, -0.01f);
        }

        private void CreateBarVisuals()
        {
            _barRoot = new GameObject("HealthBarRoot").transform;
            _barRoot.SetParent(transform, false);
            _barRoot.localPosition = _offset;

            var bg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bg.name = "BG";
            bg.transform.SetParent(_barRoot, false);
            bg.transform.localScale = new Vector3(_barWidth + 0.06f, _barHeight + 0.06f, 0.01f);
            Destroy(bg.GetComponent<Collider>());
            SetUnlitColor(bg.GetComponent<Renderer>(), new Color(0.12f, 0.12f, 0.14f));

            var fill = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fill.name = "Fill";
            fill.transform.SetParent(_barRoot, false);
            fill.transform.localScale = new Vector3(_barWidth, _barHeight, 0.02f);
            fill.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            Destroy(fill.GetComponent<Collider>());
            SetUnlitColor(fill.GetComponent<Renderer>(), new Color(0.9f, 0.22f, 0.22f));
            _fillTransform = fill.transform;
        }

        private static void SetUnlitColor(Renderer r, Color c)
        {
            if (r == null) return;
            var mat = r.material;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            if (mat.HasProperty("_Color")) mat.color = c;
        }
    }
}
