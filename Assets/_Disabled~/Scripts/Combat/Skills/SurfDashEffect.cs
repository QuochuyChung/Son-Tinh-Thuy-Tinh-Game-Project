using System.Collections;
using SonTinhThuyTinh.Player;
using UnityEngine;

namespace SonTinhThuyTinh.Combat.Skills
{
    public sealed class SurfDashEffect : SkillEffect
    {
        const float BaseDamage = 30f;

        [SerializeField] float dashSpeed = 16f;
        [SerializeField, Range(1f, 180f)] float slashHalfAngle = 90f;
        [SerializeField] float iFrameSeconds = 0.2f;
        [SerializeField] float slashHoldSeconds = 0.3f;

        Transform trail;
        Vector2 trailBaseScale;

        void Awake()
        {
            trail = transform.Find("Trail");
            if (trail != null) trailBaseScale = new Vector2(trail.localScale.x, trail.localScale.y);
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
            PlayerController player = caster.GetComponentInParent<PlayerController>();
            Vector3 dir = FlatForward(caster);
            caster.rotation = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.LookRotation(dir);

            if (player != null) player.IsInvulnerable = true;
            float iFrameEnd = Time.time + iFrameSeconds;

            float maxDash = skill.dashDistance;
            float traveled = 0f;
            while (traveled < maxDash)
            {
                float step = dashSpeed * Time.deltaTime;
                Vector3 delta = dir * step + Vector3.up * (-2f * Time.deltaTime);
                if (cc != null) cc.Move(delta);
                else caster.position += delta;
                traveled += step;

                if (player != null && Time.time >= iFrameEnd) player.IsInvulnerable = false;

                if (trail != null)
                {
                    transform.position = caster.position;
                    trail.localPosition = new Vector3(0f, trailBaseScale.x * 0.05f, -traveled * 0.5f);
                    trail.localScale = new Vector3(trailBaseScale.x, trailBaseScale.y, Mathf.Max(0.1f, traveled));
                }
                yield return null;
            }
            if (player != null) player.IsInvulnerable = false;

            transform.position = caster.position;
            AttackHit hit = new AttackHit
            {
                damage = BaseDamage * skill.damageMultiplier,
                isHeavy = false,
                causesKnockdown = false,
                origin = caster.position + Vector3.up * 0.5f,
                direction = dir,
                source = caster.gameObject
            };
            AttackResolver.Fan(in hit, skill.radius, slashHalfAngle);

            yield return new WaitForSeconds(slashHoldSeconds);
            Destroy(gameObject);
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
