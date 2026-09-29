using System;
using UnityEngine;

namespace SonTinhThuyTinh.Combat
{
    public class Health : MonoBehaviour
    {
        [SerializeField] float max = 100f;

        public float Current { get; private set; }
        public float Max => max;
        public float Normalized => Current / max;
        public bool IsDead => Current <= 0f;
        public bool IsInvulnerable { get; set; }

        public event Action<float, float> Changed;
        public event Action<DamageInfo> Damaged;
        public event Action<DamageInfo> Died;

        void Awake() => Current = max;

        // Returns false when the hit is ignored (already dead, or i-frames) so the attacker can skip hit effects.
        public bool TakeDamage(DamageInfo damage)
        {
            if (IsDead || IsInvulnerable || damage.Amount <= 0f) return false;

            Current = Mathf.Max(0f, Current - damage.Amount);
            Changed?.Invoke(Current, max);
            Damaged?.Invoke(damage);

            if (IsDead) Died?.Invoke(damage);
            return true;
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;

            Current = Mathf.Min(max, Current + amount);
            Changed?.Invoke(Current, max);
        }

        public void Revive()
        {
            Current = max;
            Changed?.Invoke(Current, max);
        }
    }
}
