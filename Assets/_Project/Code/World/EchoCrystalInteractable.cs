using UnityEngine;

// ============================================================
//  EchoCrystalInteractable — Solenne archaeological memory node
//  Implements IInteractable: rotates/bobs gently, plays memory
//  dialogue on interaction, grants XP, and progresses quests.
// ============================================================
namespace EchoesOfTheLost.World
{
    [RequireComponent(typeof(Collider))]
    public class EchoCrystalInteractable : MonoBehaviour, Characters.Player.IInteractable
    {
        [Header("Echo Identity")]
        [SerializeField] private string _echoId = "echo_crystal";
        [SerializeField] private string _displayName = "Solenne Echo Crystal";
        [SerializeField] private int _xpReward = 40;
        [SerializeField] private bool _oneTimeActivation = true;

        [Header("Memory Lore")]
        [SerializeField] private string _speakerTitle = "Solenne Memory Echo";
        [SerializeField] [TextArea(2, 4)] private string[] _memoryLines = new[]
        {
            "[RESONANCE RECORD #041]: The Core frequency is destabilizing across the Verda Wilds...",
            "[RESONANCE RECORD #041]: Seal the Citadel gates before the Unraveling consumes the surface!"
        };

        [Header("Visual Float")]
        [SerializeField] private float _rotateSpeed = 45f;
        [SerializeField] private float _bobAmplitude = 0.15f;
        [SerializeField] private float _bobFrequency = 2f;

        private Vector3 _basePosition;
        private bool _hasBeenExamined;

        private void Awake()
        {
            _basePosition = transform.position;
        }

        private void Update()
        {
            transform.Rotate(Vector3.up, _rotateSpeed * Time.deltaTime, Space.World);
            float yOffset = Mathf.Sin(Time.time * _bobFrequency) * _bobAmplitude;
            transform.position = _basePosition + new Vector3(0f, yOffset, 0f);
        }

        public void Interact(Characters.Player.PlayerController player)
        {
            if (_oneTimeActivation && _hasBeenExamined)
            {
                Systems.Dialogue.DialogueManager.Instance?.StartDialogue(
                    _echoId,
                    _speakerTitle,
                    new[] { "The Echo Crystal hums softly. Its memory has already been recorded." });
                return;
            }

            _hasBeenExamined = true;

            // Play holographic memory dialogue
            Systems.Dialogue.DialogueManager.Instance?.StartDialogue(_echoId, _speakerTitle, _memoryLines);

            // Grant XP
            if (player != null && player.TryGetComponent<Characters.Player.PlayerStats>(out var stats))
                stats.AddXP(_xpReward);

            // Publish ItemPickedUpEvent so CollectItem / Examine objectives progress
            Core.EventBus.Publish(new Core.ItemPickedUpEvent
            {
                ItemId = _echoId,
                Quantity = 1
            });

            // Dim crystal slightly after activation
            transform.localScale *= 0.85f;
        }

        public string GetInteractionPrompt() =>
            _hasBeenExamined ? $"[E] Re-examine {_displayName}" : $"[E] Examine {_displayName}";
    }
}
