using System;
using UnityEngine;

namespace SonTinhThuyTinh.Combat
{
    public class Stamina : MonoBehaviour
    {
        [SerializeField] float max = 100f;
        [SerializeField] float regenPerSecond = 30f;
        [Tooltip("Seconds after spending stamina before it starts regenerating.")]
        [SerializeField] float regenDelay = 0.8f;
        [Tooltip("Seconds the character stays exhausted (vulnerable, no stamina actions) after hitting zero.")]
        [SerializeField] float exhaustedDuration = 1.5f;

        float regenBlockedUntil;
        float exhaustedUntil;

        public float Current { get; private set; }
        public float Max => max;
        public bool IsExhausted => Time.time < exhaustedUntil;

        public event Action<float, float> Changed;
        public event Action Exhausted;

        void Awake() => Current = max;

        // Souls-style: any remaining stamina allows the action; overspending drops to zero and exhausts.
        public bool TryConsume(float amount)
        {
            if (IsExhausted || Current <= 0f) return false;

            Current = Mathf.Max(0f, Current - amount);
            regenBlockedUntil = Time.time + regenDelay;

            if (Current <= 0f)
            {
                exhaustedUntil = Time.time + exhaustedDuration;
                regenBlockedUntil = exhaustedUntil;
                Exhausted?.Invoke();
            }

            Changed?.Invoke(Current, max);
            return true;
        }

        void Update()
        {
            if (Current >= max || Time.time < regenBlockedUntil) return;

            Current = Mathf.Min(max, Current + regenPerSecond * Time.deltaTime);
            Changed?.Invoke(Current, max);
        }
    }
}
