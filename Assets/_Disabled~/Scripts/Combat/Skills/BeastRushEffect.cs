using System.Collections;
using UnityEngine;

namespace SonTinhThuyTinh.Combat.Skills
{
    public sealed class BeastRushEffect : SkillEffect
    {
        const float BaseDamage = 30f;
        const float StunSeconds = 1.5f;
        const float WindupSeconds = 0.2f;
        const float LeapSeconds = 0.6f;
        const float StartScale = 0.15f;
        const float PeakScale = 0.6f;

        static readonly Color[] BeastColors =
        {
            new Color(1f, 0.5f, 0.08f),
            new Color(0.55f, 0.6f, 0.68f),
            new Color(0.92f, 0.8f, 0.2f)
        };

        public override void Play(in SkillContext context)
        {
            SkillContext copy = context;
            StartCoroutine(Run(copy));
        }

        IEnumerator Run(SkillContext context)
        {
            Transform caster = context.Caster;
            SkillDefinition skill = context.Skill;
            transform.position = caster.position;

            yield return new WaitForSeconds(WindupSeconds);

            Vector3 origin = caster.position + Vector3.up * 0.5f;
            AttackHit hit = new AttackHit
            {
                damage = BaseDamage * skill.damageMultiplier,
                isHeavy = true,
                causesKnockdown = true,
                stunSeconds = StunSeconds,
                origin = origin,
                direction = FlatForward(caster),
                source = caster.gameObject
            };
            int hits = AttackResolver.Area(in hit, skill.radius);
            Debug.Log($"[Ult] Hùm Voi Báo: hits={hits} dmg={hit.damage:F0} r={skill.radius:F1}");

            Shader shader = Shader.Find("HDRP/Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");

            float yaw = caster.eulerAngles.y;
            int count = BeastColors.Length;
            Material[] mats = new Material[count];
            Vector3[] dirs = new Vector3[count];
            GameObject[] beasts = new GameObject[count];
            for (int i = 0; i < count; i++)
            {
                dirs[i] = Quaternion.Euler(0f, yaw + i * (360f / count), 0f) * Vector3.forward;
                beasts[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                beasts[i].name = "Beast_" + i;
                Destroy(beasts[i].GetComponent<Collider>());
                beasts[i].transform.position = origin;
                beasts[i].transform.localScale = Vector3.one * StartScale;
                if (shader != null)
                {
                    mats[i] = new Material(shader);
                    mats[i].SetColor("_BaseColor", BeastColors[i]);
                    beasts[i].GetComponent<Renderer>().sharedMaterial = mats[i];
                }
            }

            float t = 0f;
            while (t < LeapSeconds)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / LeapSeconds);
                float arc = Mathf.Sin(p * Mathf.PI) * 1.2f;
                float scale = Mathf.Lerp(StartScale, PeakScale, Mathf.Sin(p * Mathf.PI));
                for (int i = 0; i < count; i++)
                {
                    if (beasts[i] == null) continue;
                    beasts[i].transform.position = origin + dirs[i] * (skill.radius * p) + Vector3.up * arc;
                    beasts[i].transform.localScale = Vector3.one * scale;
                }
                yield return null;
            }

            for (int i = 0; i < count; i++)
            {
                if (beasts[i] != null) Destroy(beasts[i]);
                if (mats[i] != null) Destroy(mats[i]);
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
