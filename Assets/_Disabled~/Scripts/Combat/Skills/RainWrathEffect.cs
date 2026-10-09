using System.Collections;
using UnityEngine;

namespace SonTinhThuyTinh.Combat.Skills
{
    public sealed class RainWrathEffect : SkillEffect
    {
        const float BaseDamage = 30f;
        const float StunSeconds = 1.5f;
        const float WindupSeconds = 0.2f;
        const float WaveSeconds = 0.45f;
        const float WaveStart = 0.8f;
        const float RainSeconds = 8f;
        const float DropInterval = 0.12f;
        const float DropSpeed = 9f;
        const float DimFactor = 0.75f;

        static readonly Color WaveColor = new Color(0.05f, 0.35f, 0.95f);
        static readonly Color DropColor = new Color(0.15f, 0.5f, 0.95f);

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
            Debug.Log($"[Ult] Hà Bá Giận: hits={hits} dmg={hit.damage:F0} r={skill.radius:F1}");

            Shader shader = Shader.Find("HDRP/Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            Material waveMat = null;
            Material dropMat = null;
            if (shader != null)
            {
                waveMat = new Material(shader);
                waveMat.SetColor("_BaseColor", WaveColor);
                dropMat = new Material(shader);
                dropMat.SetColor("_BaseColor", DropColor);
            }

            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Wave_360";
            Destroy(ring.GetComponent<Collider>());
            ring.transform.position = new Vector3(origin.x, caster.position.y + 0.05f, origin.z);
            if (waveMat != null) ring.GetComponent<Renderer>().sharedMaterial = waveMat;
            float w = 0f;
            while (w < WaveSeconds)
            {
                w += Time.deltaTime;
                float p = Mathf.Clamp01(w / WaveSeconds);
                float r = Mathf.Lerp(WaveStart, skill.radius, p);
                ring.transform.localScale = new Vector3(r * 2f, 0.02f, r * 2f);
                yield return null;
            }
            Destroy(ring);

            System.Collections.Generic.List<UnityEngine.Light> dimmed = new System.Collections.Generic.List<UnityEngine.Light>();
            System.Collections.Generic.List<float> savedIntensities = new System.Collections.Generic.List<float>();
            foreach (UnityEngine.Light sun in UnityEngine.Object.FindObjectsByType<UnityEngine.Light>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None))
            {
                if (sun.type == UnityEngine.LightType.Directional && sun.enabled)
                {
                    dimmed.Add(sun);
                    savedIntensities.Add(sun.intensity);
                    sun.intensity = sun.intensity * DimFactor;
                }
            }

            float elapsed = 0f;
            float nextDrop = 0f;
            Vector3 ground = new Vector3(origin.x, caster.position.y, origin.z);
            while (elapsed < RainSeconds)
            {
                elapsed += Time.deltaTime;
                if (elapsed >= nextDrop)
                {
                    nextDrop += DropInterval;
                    SpawnDrop(ground, skill.radius, dropMat);
                }
                yield return null;
            }

            for (int i = 0; i < dimmed.Count; i++)
                if (dimmed[i] != null) dimmed[i].intensity = savedIntensities[i];
            if (waveMat != null) Destroy(waveMat);
            if (dropMat != null) Destroy(dropMat);
            Destroy(gameObject);
        }

        void SpawnDrop(Vector3 ground, float radius, Material mat)
        {
            Vector2 circle = Random.insideUnitCircle * radius;
            GameObject drop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            drop.name = "RainDrop";
            Destroy(drop.GetComponent<Collider>());
            drop.transform.position = ground + new Vector3(circle.x, 5f, circle.y);
            drop.transform.localScale = new Vector3(0.05f, 0.45f, 0.05f);
            drop.transform.SetParent(transform);
            if (mat != null) drop.GetComponent<Renderer>().sharedMaterial = mat;
            StartCoroutine(Fall(drop, ground.y));
        }

        IEnumerator Fall(GameObject drop, float floorY)
        {
            while (drop != null && drop.transform.position.y > floorY)
            {
                drop.transform.position += Vector3.down * (DropSpeed * Time.deltaTime);
                yield return null;
            }
            if (drop != null) Destroy(drop);
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
