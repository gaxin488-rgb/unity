using UnityEngine;

namespace EchoesOfTheLost.Core
{
    // Game-wide event payloads published through EventBus.
    // Kept as structs (IGameEvent) so publishing does not allocate.

    public struct PlayerHealthChangedEvent : IGameEvent
    {
        public int CurrentHealth;
        public int MaxHealth;
        public int Delta; // negative = damage, positive = heal
    }

    public struct PlayerDiedEvent : IGameEvent { }

    public struct PlayerLevelUpEvent : IGameEvent
    {
        public int NewLevel;
    }

    public struct QuestStartedEvent : IGameEvent
    {
        public string QuestId;
    }

    public struct QuestCompletedEvent : IGameEvent
    {
        public string QuestId;
    }

    public struct EnemyDiedEvent : IGameEvent
    {
        public string EnemyId;
        public Vector3 Position;
    }

    public struct GameStateChangedEvent : IGameEvent
    {
        public GameState PreviousState;
        public GameState NewState;
    }

    public struct DaytimeChangedEvent : IGameEvent
    {
        public float Hour; // 0-24
    }

    public struct DialogueStartedEvent : IGameEvent
    {
        public string NpcId;
    }

    public struct DialogueEndedEvent : IGameEvent
    {
        public string NpcId;
    }

    public struct ItemPickedUpEvent : IGameEvent
    {
        public string ItemId;
        public int Quantity;
    }
}
