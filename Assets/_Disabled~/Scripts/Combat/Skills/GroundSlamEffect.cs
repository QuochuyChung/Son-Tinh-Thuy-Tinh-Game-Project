using System.Collections;
using UnityEngine;

namespace SonTinhThuyTinh.Combat.Skills
{
    public sealed class GroundSlamEffect : SkillEffect
    {
        const float BaseDamage = 30f;
        const float StopDistance = 2f;
        const float TargetSearchRange = 12f;

        [SerializeField] float dashSpeed = 16f;

        Transform ring;

        void Awake()
        {
            ring = transform.Find("Ring");
        }

        public override void Play(in SkillContext context)
        {
            SkillContext copy = context;
            StartCoroutine(Run(copy));
        }

        IEnumerator Run(SkillContext context)
        {
            Transform caster = context.Caster;
            SkillDefinition skill = context.Skill;
            CharacterController cc = caster.GetComponent<CharacterController>();

            bool hasTarget = TryFindTarget(caster, out Vector3 targetPos);
            Vector3 dir;
            float maxDash;
            if (hasTarget)
            {
                Vector3 flat = targetPos - caster.position;
                flat.y = 0f;
                float distance = flat.magnitude;
                dir = distance > 0.01f ? flat / distance : FlatForward(caster);
                maxDash = Mathf.Min(distance - StopDistance, skill.dashDistance);
            }
            else
            {
                dir = FlatForward(caster);
                maxDash = skill.dashDistance;
            }

            caster.rotation = Quaternion.LookRotation(dir);

            float traveled = 0f;
            while (traveled < maxDash)
            {
                float step = dashSpeed * Time.deltaTime;
                Vector3 delta = dir * step + Vector3.up * (-2f * Time.deltaTime);
                if (cc != null) cc.Move(delta);
                else caster.position += delta;
                traveled += step;
                yield return null;
            }

            transform.position = caster.position;
            AttackHit hit = new AttackHit
            {
                damage = BaseDamage * skill.damageMultiplier,
                isHeavy = false,
                causesKnockdown = true,
                origin = caster.position + Vector3.up * 0.5f,
                direction = dir,
                source = caster.gameObject
            };
            AttackResolver.Area(in hit, skill.radius);

            if (ring != null)
            {
                ring.gameObject.SetActive(true);
                Vector3 full = new Vector3(skill.radius * 2f, ring.localScale.y, skill.radius * 2f);
                float elapsed = 0f;
                const float growSeconds = 0.35f;
                while (elapsed < growSeconds)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / growSeconds);
                    t = 1f - (1f - t) * (1f - t);
                    ring.localScale = Vector3.Lerp(full * 0.25f, full, t);
                    yield return null;
                }
                ring.localScale = full;
                yield return new WaitForSeconds(0.5f);
            }

            Destroy(gameObject);
        }

        static bool TryFindTarget(Transform caster, out Vector3 targetPos)
        {
            targetPos = Vector3.zero;
            float bestSqr = TargetSearchRange * TargetSearchRange;
            bool found = false;
            foreach (MonoBehaviour mb in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (mb is not ISkillTarget target) continue;
                Transform tt = target.TargetTransform;
                if (tt == null || tt == caster) continue;
                float sqr = (tt.position - caster.position).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                targetPos = tt.position;
                found = true;
            }
            return found;
        }

        static Vector3 FlatForward(Transform caster)
        {
            Vector3 forward = caster.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            return forward.normalized;
        }
    }
}
