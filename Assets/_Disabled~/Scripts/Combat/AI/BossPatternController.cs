using System.Collections;
using SonTinhThuyTinh.Arena;
using SonTinhThuyTinh.Audio;
using SonTinhThuyTinh.Combat.Boss;
using SonTinhThuyTinh.Combat.Environment;
using SonTinhThuyTinh.Combat.Skills;
using SonTinhThuyTinh.Player;
using UnityEngine;

namespace SonTinhThuyTinh.Combat.AI
{
    [DisallowMultipleComponent]
    public sealed class BossPatternController : MonoBehaviour
    {
        enum Pattern
        {
            Combo,
            Heavy,
            Quake
        }

        [Tooltip("Bộ aggression dùng chung cho 2 boss — §8.3: giãn cách, % E1/E2, P3 tăng tốc.")]
        [SerializeField] BossAggressionConfig aggression;

        [Tooltip("SkillCaster trên boss: E1/E2 TryCast (cooldown), F Execute trực tiếp — không đụng Thần Lực người chơi.")]
        [SerializeField] SkillCaster caster;

        [Tooltip("Phe của boss trong bảng env (đọc speed multiplier).")]
        [SerializeField] EnvFaction bossFaction = EnvFaction.SonTinh;

        BossHealth boss;
        PlayerHealth player;
        SkillDefinition e1;
        SkillDefinition e2;
        SkillDefinition ult;
        Coroutine loop;
        Vector3 baseScale;
        bool scriptedFQueued;

        public bool IsRecovering { get; private set; }
        public int ScriptedFCastCount { get; private set; }

        void Start()
        {
            boss = BossHealth.Instance;
            player = FindFirstObjectByType<PlayerHealth>();
            if (caster == null) caster = GetComponent<SkillCaster>();
            baseScale = transform.localScale;

            if (aggression == null || caster == null || boss == null || player == null)
            {
                Debug.LogError("[BossPattern] thiếu tham chiếu (aggression/caster/boss/player) — tắt controller");
                enabled = false;
                return;
            }

            e1 = caster.Get(SkillSlot.E1);
            e2 = caster.Get(SkillSlot.E2);
            ult = caster.Get(SkillSlot.Ult);
            if (e1 == null || e2 == null || ult == null)
            {
                Debug.LogError("[BossPattern] SkillCaster chưa gán đủ definition E1/E2/Ult — tắt controller");
                enabled = false;
                return;
            }

            boss.PhaseStarted += OnPhaseStarted;
            boss.Exhausted += OnExhausted;
            loop = StartCoroutine(RunLoop());
            Debug.Log($"[BossPattern] khởi động — gap {aggression.attackGapSeconds:F1}s, E1 {aggression.e1Chance:P0}, E2 {aggression.e2Chance:P0}");
        }

        void OnDestroy()
        {
            if (boss != null)
            {
                boss.PhaseStarted -= OnPhaseStarted;
                boss.Exhausted -= OnExhausted;
            }
        }

        void OnPhaseStarted(int phase)
        {
            scriptedFQueued = true;
            Debug.Log($"[BossPattern] PhaseStarted({phase}) — xếp lịch Đại Pháp scripted");
        }

        void OnExhausted()
        {
            if (loop != null)
            {
                StopCoroutine(loop);
                loop = null;
            }
            scriptedFQueued = false;
            IsRecovering = false;
            transform.localScale = baseScale;
        }

        IEnumerator RunLoop()
        {
            while (true)
            {
                if (EntryCutscene.IntroActive)
                {
                    yield return null;
                    continue;
                }
                if (BattleFlow.Instance != null && BattleFlow.Instance.State != BattleState.Fighting) yield break;
                if (boss.IsExhausted || player.IsDead) yield break;
                if (boss.IsStunned)
                {
                    yield return null;
                    continue;
                }
                if (scriptedFQueued)
                {
                    scriptedFQueued = false;
                    yield return ScriptedF();
                    continue;
                }

                Vector3 toPlayer = player.transform.position - transform.position;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude > aggression.attackRange * aggression.attackRange)
                {
                    Chase(toPlayer);
                    yield return null;
                    continue;
                }

                Face(toPlayer);
                yield return RunPattern();
            }
        }

        IEnumerator RunPattern()
        {
            yield return CastOrStrike();
            float tempo = boss.CurrentSegment >= 3 ? aggression.p3TempoMultiplier : 1f;
            yield return new WaitForSeconds(aggression.attackGapSeconds * tempo);
        }

        IEnumerator CastOrStrike()
        {
            float roll = Random.value;
            if (roll < aggression.e1Chance && caster.TryCast(SkillSlot.E1))
            {
                Debug.Log("[BossPattern] pattern=E1 Thành Lũy");
                caster.Execute(e1, transform);
                yield break;
            }
            if (roll < aggression.e1Chance + aggression.e2Chance && caster.TryCast(SkillSlot.E2))
            {
                Debug.Log("[BossPattern] pattern=E2 Đạp Núi");
                caster.Execute(e2, transform);
                yield break;
            }
            yield return MeleePattern();
        }

        IEnumerator MeleePattern()
        {
            Pattern pattern = PickPattern();
            switch (pattern)
            {
                case Pattern.Combo:
                    Debug.Log($"[BossPattern] pattern=combo 2 đòn (P{boss.CurrentSegment})");
                    yield return Windup(0.5f);
                    Strike(7f, 1.6f, 2.4f, false);
                    yield return new WaitForSeconds(0.35f);
                    Strike(7f, 1.6f, 2.4f, false);
                    break;
                case Pattern.Heavy:
                    Debug.Log($"[BossPattern] pattern=heavy (P{boss.CurrentSegment})");
                    yield return Windup(0.7f);
                    Strike(16f, 1.9f, 2.7f, true);
                    break;
                case Pattern.Quake:
                    Debug.Log($"[BossPattern] pattern=quake AoE (P{boss.CurrentSegment})");
                    yield return Windup(0.8f);
                    Quake();
                    break;
            }
        }

        Pattern PickPattern()
        {
            if (boss.CurrentSegment >= 2 && Random.value < 0.4f) return Pattern.Quake;
            return Random.value < 0.55f ? Pattern.Combo : Pattern.Heavy;
        }

        IEnumerator ScriptedF()
        {
            int phase = boss.CurrentSegment;
            Debug.Log($"[BossPattern] Đại Pháp scripted P{phase} — telegraph {aggression.fTelegraphSeconds:F1}s");
            yield return Windup(aggression.fTelegraphSeconds);
            caster.Execute(ult, transform);
            BattleAudio.Instance?.PlayUltStinger(bossFaction);
            ScriptedFCastCount++;
            Debug.Log($"[BossPattern] Đại Pháp P{phase} đã cast (lần {ScriptedFCastCount}) — kiệt sức {aggression.exhaustionSeconds:F1}s");

            IsRecovering = true;
            transform.localScale = new Vector3(baseScale.x, baseScale.y * 0.65f, baseScale.z);
            float until = Time.time + aggression.exhaustionSeconds;
            while (Time.time < until)
            {
                if (BattleFlow.Instance != null && BattleFlow.Instance.State != BattleState.Fighting) break;
                yield return null;
            }
            transform.localScale = baseScale;
            IsRecovering = false;
            Debug.Log("[BossPattern] hết kiệt sức — tiếp tục pattern");
        }

        IEnumerator Windup(float seconds)
        {
            transform.localScale = new Vector3(baseScale.x * 1.12f, baseScale.y, baseScale.z * 1.12f);
            if (seconds > 0f) yield return new WaitForSeconds(seconds);
            transform.localScale = baseScale;
        }

        void Strike(float damage, float radius, float distance, bool heavy)
        {
            AttackHit hit = MakeHit(damage, heavy);
            Vector3 forward = transform.forward;
            forward.y = 0f;
            hit.origin = transform.position + forward * 0.4f + Vector3.up * 1f;
            hit.direction = forward;
            IAttackReceiver receiver = AttackResolver.Melee(hit, radius, distance);
            Debug.Log($"[BossPattern] strike trúng={receiver != null} dmg={damage:F0}");
        }

        void Quake()
        {
            AttackHit hit = MakeHit(14f, true);
            hit.origin = transform.position + Vector3.up * 1f;
            int count = AttackResolver.Area(hit, 4f);
            Debug.Log($"[BossPattern] quake trúng={count} r=4m");
        }

        AttackHit MakeHit(float damage, bool heavy) => new AttackHit
        {
            damage = damage,
            isHeavy = heavy,
            causesKnockdown = false,
            stunSeconds = 0f,
            origin = transform.position,
            direction = transform.forward,
            source = gameObject
        };

        void Chase(Vector3 toPlayer)
        {
            Face(toPlayer);
            float speed = aggression.moveSpeed;
            EnvironmentDirector env = EnvironmentDirector.Instance;
            if (env != null) speed *= env.GetSpeedMultiplier(bossFaction);
            Vector3 next = transform.position + toPlayer.normalized * (speed * Time.deltaTime);
            next.y = transform.position.y;
            ArenaBoundary boundary = ArenaBoundary.Instance;
            if (boundary != null) next = boundary.ClampPosition(next);
            transform.position = next;
        }

        void Face(Vector3 toPlayer)
        {
            if (toPlayer.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.LookRotation(toPlayer, Vector3.up);
        }
    }
}
