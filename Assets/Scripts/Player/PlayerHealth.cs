using System;
using SonTinhThuyTinh.Combat;
using SonTinhThuyTinh.Combat.Boss;
using SonTinhThuyTinh.Combat.Environment;
using UnityEngine;

namespace SonTinhThuyTinh.Player
{
    public sealed class PlayerHealth : MonoBehaviour, IAttackReceiver
    {
        [Tooltip("HP tối đa của người chơi; hết là thua (lưới an toàn #1).")]
        [SerializeField, Min(1f)] float maxHp = 100f;

        PlayerController controller;

        public float Hp { get; private set; }
        public float MaxHp => maxHp;
        public bool IsDead => Hp <= 0f;
        public float HpFraction => maxHp > 0f ? Mathf.Clamp01(Hp / maxHp) : 0f;

        public event Action<float, float> HpChanged;
        public event Action Died;

        void Awake()
        {
            Hp = maxHp;
            controller = GetComponent<PlayerController>();
        }

        public bool ReceiveAttack(in AttackHit hit) => ApplyDamage(hit.damage);

        public bool ApplyDamage(float damage)
        {
            if (damage <= 0f || IsDead) return true;
            if (controller != null && controller.IsInvulnerable) return true;
            if (BattleFlow.Instance != null && BattleFlow.Instance.State != BattleState.Fighting) return true;

            Hp = Mathf.Max(0f, Hp - damage);
            EnvironmentDirector.Instance?.RegisterPlayerHit();
            HpChanged?.Invoke(Hp, maxHp);
            if (IsDead) Died?.Invoke();
            return true;
        }
    }
}
