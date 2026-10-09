using System;
using System.Collections;
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

        [Tooltip("Boss bị choáng tới thời điểm này (Time.time); AI (C13) đọc IsStunned để đứng im.")]
        public bool IsStunned => Time.time < stunnedUntil;
        public float StunRemaining => Mathf.Max(0f, stunnedUntil - Time.time);

        public event Action<float, float> HpChanged;
        public event Action<int> PhaseStarted;
        public event Action Exhausted;

        float stunnedUntil;
        bool hopping;

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

        public bool ReceiveAttack(in AttackHit hit)
        {
            ApplyDamage(hit.damage);
            if (hit.stunSeconds > 0f) ApplyStun(hit.stunSeconds);
            if (hit.causesKnockdown && !hopping) StartCoroutine(Hop());
            return true;
        }

        public void ApplyStun(float seconds)
        {
            if (seconds <= 0f) return;
            stunnedUntil = Mathf.Max(stunnedUntil, Time.time + seconds);
        }

        IEnumerator Hop()
        {
            hopping = true;
            Vector3 basePos = transform.position;
            const float lift = 0.6f;
            const float riseSeconds = 0.15f;
            const float fallSeconds = 0.35f;
            float t = 0f;
            while (t < riseSeconds)
            {
                t += Time.deltaTime;
                transform.position = basePos + Vector3.up * (lift * Mathf.Clamp01(t / riseSeconds));
                yield return null;
            }
            t = 0f;
            while (t < fallSeconds)
            {
                t += Time.deltaTime;
                transform.position = basePos + Vector3.up * (lift * (1f - Mathf.Clamp01(t / fallSeconds)));
                yield return null;
            }
            transform.position = basePos;
            hopping = false;
        }
    }
}
