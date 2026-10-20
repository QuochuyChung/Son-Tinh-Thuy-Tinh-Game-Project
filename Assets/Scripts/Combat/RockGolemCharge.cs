using System.Collections.Generic;
using SonTinhThuyTinh.Player;
using UnityEngine;

namespace SonTinhThuyTinh.Combat
{
    // Sơn Tinh's Z, Người đá: two rock golems burst out of the ground beside him and charge straight ahead as far as Thủy Tinh's Sóng đẩy
    // goes (spell.range at spell.speed). Whoever they run into is hurt, thrown away from Sơn Tinh and stunned (spell.stunSeconds); each
    // enemy is hit once even when both golems catch it. At the end of the run they sink back into the ground. The golem is rigged
    // (tools/rig_golem.py): it plays Emerge while it comes up and Run (Legacy clips) while it charges, out of step with the other one.
    // A golem model without the clips falls back to a lean and a side-to-side stomp.
    public class RockGolemCharge : MonoBehaviour, ISpellEffect
    {
        [SerializeField] GameObject golemModel;
        [SerializeField] GameObject dustPrefab;
        [Tooltip("Metres from Sơn Tinh's centre line to each golem.")]
        [SerializeField] float sideOffset = 0.95f;
        [Tooltip("Metres in front of Sơn Tinh where they come out.")]
        [SerializeField] float startAhead = 1.2f;
        [SerializeField] float riseTime = 0.25f;
        [SerializeField] float sinkTime = 0.4f;
        [Tooltip("Half size of the box each golem hits with (x across, y up, z ahead).")]
        [SerializeField] Vector3 hitHalfSize = new(1.05f, 1.2f, 0.85f);
        [Tooltip("Metres of ground covered by one Run cycle (both feet): sets the clip speed from the charge speed.")]
        [SerializeField] float strideLength = 4.2f;

        class Golem
        {
            public Transform transform;
            public Vector3 position;    // on the ground
            public float height;
            public float phase;
            public Animation clips;     // null: the model is not rigged
        }

        PlayerController caster;
        SpellData spell;
        Vector3 forward;
        readonly List<Golem> golems = new();
        readonly HashSet<Health> hit = new();
        float elapsed;
        float travelled;
        float sinkStartedAt = -1f;

        public void Init(PlayerController owner, SpellData data)
        {
            caster = owner;
            spell = data;
            forward = Vector3.ProjectOnPlane(owner.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            transform.SetPositionAndRotation(owner.transform.position, Quaternion.LookRotation(forward));

            for (int side = -1; side <= 1; side += 2)
            {
                if (golemModel == null) break;
                Vector3 at = owner.transform.position + forward * startAhead + right * (side * sideOffset);
                at.y = RockSkillUtil.GroundY(at, owner.transform.position.y);
                Transform model = Instantiate(golemModel, at, Quaternion.LookRotation(forward), transform).transform;
                float height = Mathf.Max(0.5f, RockSkillUtil.LocalBounds(model).size.y);
                var golem = new Golem { transform = model, position = at, height = height, phase = side > 0 ? 0f : Mathf.PI };
                golem.clips = model.GetComponentInChildren<Animation>();
                if (golem.clips != null && (golem.clips["Run"] == null || golem.clips["Emerge"] == null)) golem.clips = null;
                if (golem.clips != null)
                {
                    golem.clips.playAutomatically = false;
                    golem.clips["Emerge"].speed = golem.clips["Emerge"].length / Mathf.Max(riseTime * 1.4f, 0.05f);
                    golem.clips.Play("Emerge");
                }
                golems.Add(golem);
                Place(golem, -height, 0f, 0f);
                RockSkillUtil.Dust(dustPrefab, at, 0.8f);
            }
        }

        void Update()
        {
            if (caster == null || spell == null) { Destroy(gameObject); return; }
            elapsed += Time.deltaTime;

            if (elapsed < riseTime)
            {
                float t = elapsed / riseTime;
                float ease = 1f - (1f - t) * (1f - t) * (1f - t);
                foreach (Golem g in golems) Place(g, Mathf.Lerp(-g.height, 0f, ease), Mathf.Lerp(-25f, 0f, ease), 0f);
                HitTargets(0f);
                return;
            }

            if (sinkStartedAt < 0f)
            {
                foreach (Golem g in golems)
                {
                    if (g.clips == null || g.clips.IsPlaying("Run")) continue;
                    AnimationState run = g.clips["Run"];
                    run.speed = spell.speed / Mathf.Max(strideLength, 0.1f) * run.length;
                    run.normalizedTime = g.phase > 0f ? 0.5f : 0f;
                    g.clips.CrossFade("Run", 0.08f);
                }
                float step = spell.speed * Time.deltaTime;
                travelled += step;
                foreach (Golem g in golems)
                {
                    g.position += forward * step;
                    g.position.y = Mathf.Lerp(g.position.y, RockSkillUtil.GroundY(g.position, g.position.y), 0.5f);
                    float stride = travelled * 1.6f + g.phase;
                    if (g.clips != null) Place(g, 0f, 4f, 0f);   // the clip does the stomping
                    else Place(g, Mathf.Abs(Mathf.Sin(stride)) * 0.18f, 12f, Mathf.Sin(stride) * 6f);
                }
                HitTargets(step);
                if (travelled >= spell.range)
                {
                    sinkStartedAt = elapsed;
                    foreach (Golem g in golems) RockSkillUtil.Dust(dustPrefab, g.position, 0.9f);
                    caster.Shake(spell.shake * 0.5f);
                }
                return;
            }

            float sink = Mathf.Clamp01((elapsed - sinkStartedAt) / sinkTime);
            foreach (Golem g in golems) Place(g, Mathf.Lerp(0f, -g.height * 1.05f, sink * sink), Mathf.Lerp(12f, -8f, sink), 0f);
            if (sink >= 1f) Destroy(gameObject);
        }

        void Place(Golem g, float rise, float lean, float roll)
        {
            g.transform.SetPositionAndRotation(g.position + Vector3.up * rise, Quaternion.LookRotation(forward) * Quaternion.Euler(lean, 0f, roll));
        }

        // `step`: metres moved this frame; the box reaches back over them, so a slow frame cannot jump a golem past someone
        void HitTargets(float step)
        {
            foreach (Golem g in golems)
            {
                Vector3 centre = g.position + Vector3.up * hitHalfSize.y + forward * (0.2f - step * 0.5f);
                Vector3 half = hitHalfSize + new Vector3(0f, 0f, step * 0.5f);
                Collider[] found = Physics.OverlapBox(centre, half, Quaternion.LookRotation(forward), ~0, QueryTriggerInteraction.Ignore);
                foreach (Health target in SpellEffects.Targets(found, caster.Health))
                {
                    if (!hit.Add(target)) continue;
                    Vector3 push = forward * spell.knockback + Vector3.up * spell.knockUp;
                    if (RockSkillUtil.Strike(caster, spell, target, target.transform.position + Vector3.up, push, true))
                        RockSkillUtil.Dust(dustPrefab, target.transform.position, 0.6f);
                }
            }
        }
    }
}
