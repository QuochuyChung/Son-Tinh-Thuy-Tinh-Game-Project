using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SonTinhThuyTinh.Combat.Skills
{
    public sealed class WaveFanEffect : SkillEffect
    {
        const float BaseDamage = 30f;

        [SerializeField, Range(1f, 180f)] float halfAngle = 60f;
        [SerializeField] float pushDistance = 0.75f;
        [SerializeField] float growSeconds = 0.35f;
        [SerializeField] float lifeSeconds = 3f;

        Transform sheet;

        void Awake()
        {
            sheet = transform.Find("Sheet");
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
            Vector3 dir = FlatForward(caster);

            transform.position = caster.position;
            transform.rotation = Quaternion.LookRotation(dir);

            AttackHit hit = new AttackHit
            {
                damage = BaseDamage * skill.damageMultiplier,
                isHeavy = false,
                causesKnockdown = true,
                origin = caster.position + Vector3.up * 0.5f,
                direction = dir,
                source = caster.gameObject
            };
            List<IAttackReceiver> receivers = new List<IAttackReceiver>();
            AttackResolver.Fan(in hit, skill.radius, halfAngle, receivers);
            foreach (IAttackReceiver receiver in receivers)
            {
                if (receiver is not Component comp) continue;
                CharacterController cc = comp.GetComponent<CharacterController>();
                Vector3 push = dir * pushDistance;
                if (cc != null) cc.Move(push);
                else comp.transform.position += push;
            }

            if (sheet != null)
            {
                float width = 2f * skill.radius * Mathf.Sin(halfAngle * Mathf.Deg2Rad);
                Vector3 full = new Vector3(width, sheet.localScale.y, skill.radius);
                sheet.localPosition = new Vector3(0f, 0f, skill.radius * 0.5f);
                float elapsed = 0f;
                while (elapsed < growSeconds)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / growSeconds);
                    t = 1f - (1f - t) * (1f - t);
                    sheet.localScale = Vector3.Lerp(full * 0.3f, full, t);
                    yield return null;
                }
                sheet.localScale = full;
                yield return new WaitForSeconds(lifeSeconds);
            }

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
