using UnityEngine;

namespace EchoesOfTheLost.Systems.Combat
{
    public enum DamageType { Physical, Fire, Ice, Lightning, Arcane, True }

    [System.Serializable]
    public struct DamageInfo
    {
        public float amount;
        public DamageType type;
        public bool isCritical;
        public bool ignoreDefense;
        public Vector3 hitPoint;
        public Vector3 hitNormal;
        public GameObject source;
    }

    public interface IDamageable
    {
        void TakeDamage(DamageInfo info);
        bool IsDead { get; }
    }
}
