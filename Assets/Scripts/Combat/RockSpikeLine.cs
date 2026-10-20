using System.Collections.Generic;
using SonTinhThuyTinh.Player;
using UnityEngine;

namespace SonTinhThuyTinh.Combat
{
    // Sơn Tinh's C, Đá nhọn: `count` sharp rock spires stab up out of the ground one after another in a line ahead of him, out to about
    // spell.range (the reach of Thủy Tinh's spells). Whoever stands where one comes up is hurt and thrown up (each enemy once per cast).
    // They stand a moment and draw back into the ground.
    public class RockSpikeLine : MonoBehaviour, ISpellEffect
    {
        [SerializeField] GameObject spireModel;
        [SerializeField] GameObject dustPrefab;
        [SerializeField] int count = 4;
        [Tooltip("Metres from Sơn Tinh to the first spire.")]
        [SerializeField] float firstDistance = 3f;
        [Tooltip("Seconds between two spires.")]
        [SerializeField] float interval = 0.12f;
        [SerializeField] float riseTime = 0.12f;
        [Tooltip("Seconds a spire stands once up.")]
        [SerializeField] float holdTime = 1f;
        [SerializeField] float sinkTime = 0.3f;
        [Tooltip("Radius around a spire that it hits as it comes up.")]
        [SerializeField] float hitRadius = 1.4f;

        class Spire
        {
            public Transform transform;
            public Vector3 position;
            public Quaternion rotation;
            public float height;
            public float at;
            public bool erupted;
        }

        PlayerController caster;
        SpellData spell;
        Vector3 forward;
        readonly List<Spire> spires = new();
        readonly HashSet<Health> hit = new();
        float elapsed;

        public void Init(PlayerController owner, SpellData data)
        {
            caster = owner;
            spell = data;
            forward = Vector3.ProjectOnPlane(owner.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            transform.SetPositionAndRotation(owner.transform.position, Quaternion.LookRotation(forward));
            float spacing = count > 1 ? (Mathf.Max(spell.range, firstDistance + 1f) - firstDistance) / (count - 1) : 0f;
            Vector3 right = Vector3.Cross(Vector3.up, forward);

            for (int i = 0; i < count && spireModel != null; i++)
            {
                Vector3 at = owner.transform.position + forward * (firstDistance + spacing * i) + right * (i % 2 == 0 ? 0.25f : -0.25f);
                at.y = RockSkillUtil.GroundY(at, owner.transform.position.y);
                // each one a little different: turned, leaning forward and a bit bigger further out
                Quaternion rotation = Quaternion.LookRotation(forward) * Quaternion.Euler(10f + 4f * i, Random.Range(-40f, 40f), Random.Range(-8f, 8f));
                Transform model = Instantiate(spireModel, at, rotation, transform).transform;
                model.localScale *= 1f + 0.1f * i;
                foreach (Collider c in model.GetComponentsInChildren<Collider>()) Destroy(c);
                var spire = new Spire { transform = model, position = at, rotation = rotation, at = i * interval };
                spire.height = Mathf.Max(0.5f, RockSkillUtil.LocalBounds(model).size.y);
                spires.Add(spire);
                Place(spire, 0f);
            }
        }

        void Update()
        {
            if (caster == null || spell == null) { Destroy(gameObject); return; }
            elapsed += Time.deltaTime;
            bool allGone = true;
            foreach (Spire s in spires)
            {
                float t = elapsed - s.at;
                float up;
                if (t < 0f) up = 0f;
                else if (t < riseTime) up = 1f - (1f - t / riseTime) * (1f - t / riseTime);
                else if (t < riseTime + holdTime) up = 1f;
                else up = 1f - Mathf.Clamp01((t - riseTime - holdTime) / sinkTime);
                if (t >= 0f && !s.erupted)
                {
                    s.erupted = true;
                    RockSkillUtil.Dust(dustPrefab, s.position, 0.9f);
                    caster.Shake(spell.shake * 0.5f);
                }
                if (s.erupted && t < riseTime + 0.05f) HitAround(s);
                Place(s, up);
                if (t < riseTime + holdTime + sinkTime) allGone = false;
            }
            if (allGone) Destroy(gameObject);
        }

        void Place(Spire s, float up)
        {
            s.transform.SetPositionAndRotation(s.position + Vector3.down * (s.height * (1.05f - up)), s.rotation);
            s.transform.gameObject.SetActive(up > 0.001f);
        }

        void HitAround(Spire s)
        {
            Vector3 bottom = s.position + Vector3.up * 0.3f, top = s.position + Vector3.up * Mathf.Max(1.2f, s.height * 0.8f);
            foreach (Health target in SpellEffects.Targets(Physics.OverlapCapsule(bottom, top, hitRadius, ~0, QueryTriggerInteraction.Ignore), caster.Health))
            {
                if (!hit.Add(target)) continue;
                Vector3 push = forward * spell.knockback + Vector3.up * spell.knockUpSpeed;
                RockSkillUtil.Strike(caster, spell, target, target.transform.position + Vector3.up, push, spell.stunSeconds > 0f);
            }
        }
    }
}
