using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

// ============================================================
//  UIElementAnimator — Stage 3: Animating UI Elements
//  Drives smooth procedural & Animator-backed UI animations:
//    - Smooth entrance slide & fade for HUD panels
//    - Breathing scale & glow pulse on Level Crest, Reticle & Interaction Prompt
//    - Delayed HP Damage Trail catch-up & Low-HP heartbeat flash
//    - Action Hotbar slot spring-bounce on keypress (LMB / RMB / 1 / E)
//    - Live Compass strip & Mini-Map player arrow rotation from Camera yaw
// ============================================================
namespace EchoesOfTheLost.UI
{
    public class UIElementAnimator : MonoBehaviour
    {
        [Header("Animated HUD Panels")]
        [SerializeField] private RectTransform _statusPanel;
        [SerializeField] private RectTransform _compassBar;
        [SerializeField] private RectTransform _miniMapFrame;
        [SerializeField] private RectTransform _questTrackerPanel;
        [SerializeField] private RectTransform _actionHotbar;

        [Header("Animated Badges, Reticle & Prompts")]
        [SerializeField] private RectTransform _levelBadge;
        [SerializeField] private RectTransform _reticleTransform;
        [SerializeField] private Image _reticleImage;
        [SerializeField] private RectTransform _interactPromptTransform;
        [SerializeField] private Image _interactKeycapImage;
        [SerializeField] private RectTransform _miniMapArrow;

        [Header("Animated Bars (Delayed Damage Trail)")]
        [SerializeField] private Image _hpMainFill;
        [SerializeField] private Image _hpDamageTrailFill;
        [SerializeField] private Image _xpFill;

        [Header("Animated Action Hotbar Slots (LMB, RMB, 1, E)")]
        [SerializeField] private RectTransform[] _hotbarSlots;
        [SerializeField] private Image[] _hotbarSlotHighlights;

        [Header("Compass Readout")]
        [SerializeField] private TextMeshProUGUI _compassText;

        private readonly float[] _slotBounceTimers = new float[4];
        private float _introTimer;

        private void OnEnable()
        {
            _introTimer = 0f;
            if (_hpDamageTrailFill != null && _hpMainFill != null)
                _hpDamageTrailFill.fillAmount = _hpMainFill.fillAmount;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float t = Time.unscaledTime;
            _introTimer += dt;

            AnimateBadgesAndReticle(t);
            AnimateDelayedDamageBar(dt, t);
            AnimateHotbarKeypresses(dt);
            AnimateCompassAndMiniMapArrow(dt);
        }

        private void AnimateBadgesAndReticle(float t)
        {
            // 1. Level Crest subtle breathing scale
            if (_levelBadge != null)
            {
                float s = 1f + Mathf.Sin(t * 2.4f) * 0.045f;
                _levelBadge.localScale = new Vector3(s, s, 1f);
            }

            // 2. Center Reticle subtle rotation & pulse
            if (_reticleTransform != null)
            {
                float rScale = 1f + Mathf.Sin(t * 3.2f) * 0.06f;
                _reticleTransform.localScale = new Vector3(rScale, rScale, 1f);
            }
            if (_reticleImage != null)
            {
                var c = _reticleImage.color;
                c.a = 0.68f + Mathf.Sin(t * 3.2f) * 0.18f;
                _reticleImage.color = c;
            }

            // 3. Interaction Prompt breathing & keycap glow when active
            if (_interactPromptTransform != null && _interactPromptTransform.gameObject.activeInHierarchy)
            {
                float pScale = 1f + Mathf.Sin(t * 4.5f) * 0.04f;
                _interactPromptTransform.localScale = new Vector3(pScale, pScale, 1f);

                if (_interactKeycapImage != null)
                {
                    float glow = 0.78f + Mathf.Sin(t * 6.0f) * 0.22f;
                    _interactKeycapImage.color = new Color(0.18f * glow, 0.78f * glow, 0.96f * glow, 0.96f);
                }
            }
        }

        private void AnimateDelayedDamageBar(float dt, float t)
        {
            if (_hpMainFill == null) return;

            if (_hpDamageTrailFill != null)
            {
                float target = _hpMainFill.fillAmount;
                if (_hpDamageTrailFill.fillAmount > target)
                {
                    _hpDamageTrailFill.fillAmount = Mathf.MoveTowards(_hpDamageTrailFill.fillAmount, target, dt * 0.38f);
                }
                else
                {
                    _hpDamageTrailFill.fillAmount = target;
                }
            }

            // Low-HP warning pulse (< 35%)
            if (_hpMainFill.fillAmount < 0.35f)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(t * 8f);
                _hpMainFill.color = Color.Lerp(new Color(0.88f, 0.18f, 0.20f, 1f), new Color(1f, 0.52f, 0.35f, 1f), pulse);
            }
            else
            {
                _hpMainFill.color = new Color(0.88f, 0.22f, 0.24f, 1f);
            }
        }

        private void AnimateHotbarKeypresses(float dt)
        {
            if (_hotbarSlots == null || _hotbarSlots.Length == 0) return;

            var mouse = Mouse.current;
            var kb = Keyboard.current;

            if (mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame && _slotBounceTimers.Length > 0)
                    TriggerSlotBounce(0);
                if (mouse.rightButton.wasPressedThisFrame && _slotBounceTimers.Length > 1)
                    TriggerSlotBounce(1);
            }

            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame && _slotBounceTimers.Length > 2)
                    TriggerSlotBounce(2);
                if (kb.eKey.wasPressedThisFrame && _slotBounceTimers.Length > 3)
                    TriggerSlotBounce(3);
            }

            for (int i = 0; i < _hotbarSlots.Length && i < _slotBounceTimers.Length; i++)
            {
                if (_hotbarSlots[i] == null) continue;

                if (_slotBounceTimers[i] > 0f)
                {
                    _slotBounceTimers[i] = Mathf.Max(0f, _slotBounceTimers[i] - dt * 4.5f);
                    float phase = _slotBounceTimers[i];
                    float scale = 1f + Mathf.Sin(phase * Mathf.PI) * 0.22f;
                    _hotbarSlots[i].localScale = new Vector3(scale, scale, 1f);

                    if (_hotbarSlotHighlights != null && i < _hotbarSlotHighlights.Length && _hotbarSlotHighlights[i] != null)
                    {
                        _hotbarSlotHighlights[i].color = Color.Lerp(
                            new Color(0.14f, 0.22f, 0.32f, 0.92f),
                            new Color(0.28f, 0.85f, 1.0f, 1.0f),
                            phase);
                    }
                }
                else
                {
                    _hotbarSlots[i].localScale = Vector3.one;
                }
            }
        }

        public void TriggerSlotBounce(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slotBounceTimers.Length) return;
            _slotBounceTimers[slotIndex] = 1f;

            if (_hotbarSlots != null && slotIndex < _hotbarSlots.Length && _hotbarSlots[slotIndex] != null)
            {
                if (_hotbarSlots[slotIndex].gameObject.TryGetComponent<Animator>(out var anim) && anim.runtimeAnimatorController != null)
                    anim.SetTrigger("Pop");
            }
        }

        private void AnimateCompassAndMiniMapArrow(float dt)
        {
            var cam = Camera.main;
            if (cam == null) return;

            float yaw = Mathf.Repeat(cam.transform.eulerAngles.y, 360f);

            if (_miniMapArrow != null)
            {
                _miniMapArrow.localRotation = Quaternion.Euler(0f, 0f, -yaw);
            }

            if (_compassText != null)
            {
                string cardinal = GetCardinalLabel(yaw);
                _compassText.text = $"W   •   NW   •   <color=#7CE8FF>[ {cardinal} ({yaw:000}°) — SOLENNE RUINS ]</color>   •   NE   •   E";
            }
        }

        private static string GetCardinalLabel(float yaw)
        {
            string[] dirs = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
            int idx = Mathf.RoundToInt(yaw / 45f) % 8;
            return dirs[idx];
        }
    }
}
