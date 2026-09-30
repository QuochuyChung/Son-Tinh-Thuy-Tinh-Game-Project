using System;
using SonTinhThuyTinh.Combat.Environment;
using UnityEngine;

namespace SonTinhThuyTinh.Combat.Boss
{
    public sealed class BossHealth : MonoBehaviour, IAttackReceiver
    {
        [Tooltip("Số đoạn HP của boss (mỗi đoạn = 1 phase).")]
        [SerializeField, Min(1)] int segmentCount = 3;

        [Tooltip("HP tối đa của MỖI đoạn; hết đoạn là hồi đầy (trừ đoạn cuối).")]
        [SerializeField, Min(1f)] float maxHpPerSegment = 100f;

        public static BossHealth Instance { get; private set; }

        public float Hp { get; private set; }
        public float MaxHp => maxHpPerSegment;
        public int Segments => segmentCount;
        public int CurrentSegment { get; private set; } = 1;
        public bool IsExhausted { get; private set; }
        public float HpFraction => maxHpPerSegment > 0f ? Mathf.Clamp01(Hp / maxHpPerSegment) : 0f;

        public event Action<float, float> HpChanged;
        public event Action<int> PhaseStarted;
        public event Action Exhausted;

        void Awake()
        {
            Instance = this;
            Hp = maxHpPerSegment;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void ApplyDamage(float damage)
        {
            if (damage <= 0f || IsExhausted) return;
            if (BattleFlow.Instance != null && BattleFlow.Instance.State != BattleState.Fighting) return;

            Hp = Mathf.Max(0f, Hp - damage);
            HpChanged?.Invoke(Hp, maxHpPerSegment);
            if (Hp > 0f) return;

            if (CurrentSegment >= segmentCount)
            {
                IsExhausted = true;
                Exhausted?.Invoke();
                return;
            }

            CurrentSegment++;
            Hp = maxHpPerSegment;
            HpChanged?.Invoke(Hp, maxHpPerSegment);
            EnvironmentDirector.Instance?.FlipTowardBoss();
            PhaseStarted?.Invoke(CurrentSegment);
        }

        public bool ReceiveAttack(in AttackHit hit) => ReceiveAttack(hit.damage);

        bool ReceiveAttack(float damage)
        {
            ApplyDamage(damage);
            return true;
        }
    }
}
