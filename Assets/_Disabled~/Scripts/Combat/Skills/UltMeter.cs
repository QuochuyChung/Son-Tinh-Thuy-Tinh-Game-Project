using System;
using UnityEngine;

namespace SonTinhThuyTinh.Combat.Skills
{
    public sealed class UltMeter : MonoBehaviour
    {
        public static UltMeter Instance { get; private set; }

        [Tooltip("Mức Thần Lực tối đa; đạt đầy mới bấm được F.")]
        [SerializeField, Min(1f)] float max = 100f;
        [Tooltip("Thần Lực nhận thêm khi đòn đánh nhẹ trúng đích.")]
        [SerializeField, Min(0f)] float lightHitGain = 3f;
        [Tooltip("Thần Lực nhận thêm khi đòn đánh nặng trúng đích.")]
        [SerializeField, Min(0f)] float heavyHitGain = 8f;
        [Tooltip("Thần Lực nhận thêm mỗi lần né hoàn hảo.")]
        [SerializeField, Min(0f)] float perfectDodgeGain = 12f;

        public float Value { get; private set; }
        public float Max => max;
        public bool IsFull => Value >= max;

        public event Action<float, float> ValueChanged;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
        }

        public void Add(float amount)
        {
            if (amount <= 0f) return;
            Set(Value + amount);
        }

        public void RegisterLightHit() => Add(lightHitGain);
        public void RegisterHeavyHit() => Add(heavyHitGain);
        public void RegisterPerfectDodge() => Add(perfectDodgeGain);

        public void Fill() => Set(max);

        public bool TryConsumeFull()
        {
            if (!IsFull) return false;
            Set(0f);
            return true;
        }

        void Set(float value)
        {
            float clamped = Mathf.Clamp(value, 0f, max);
            if (Mathf.Approximately(clamped, Value)) return;
            Value = clamped;
            ValueChanged?.Invoke(Value, max);
        }
    }
}
