using SonTinhThuyTinh.Player;
using UnityEngine;

namespace SonTinhThuyTinh.Combat
{
    // Shared bits of Sơn Tinh's rock skills (RockGolemCharge, RockWallBarrier, RockSpikeLine).
    public static class RockSkillUtil
    {
        static readonly RaycastHit[] hits = new RaycastHit[16];

        // Height of the ground under `point` (characters, rock skill pieces and triggers do not count); `fallback` when nothing is found.
        public static float GroundY(Vector3 point, float fallback)
        {
            int count = Physics.RaycastNonAlloc(point + Vector3.up * 3f, Vector3.down, hits, 9f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.NegativeInfinity;
            for (int i = 0; i < count; i++)
            {
                Collider c = hits[i].collider;
                if (c is CharacterController || c.GetComponentInParent<Health>() != null || c.GetComponentInParent<RockWallBarrier>() != null) continue;
                if (hits[i].point.y > best) best = hits[i].point.y;
            }
            return float.IsNegativeInfinity(best) ? fallback : best;
        }

        // A cloud of dust and stones bursting out of the ground.
        public static void Dust(GameObject prefab, Vector3 at, float scale = 1f)
        {
            if (prefab == null) return;
            GameObject fx = Object.Instantiate(prefab, at, Quaternion.identity);
            fx.transform.localScale *= scale;
            Object.Destroy(fx, 3f);
        }

        // A hit by a rock skill: damage, then the push (enemies) or the stagger (characters) and, when the spell has one, the stun.
        public static bool Strike(PlayerController caster, SpellData spell, Health target, Vector3 point, Vector3 push, bool stun)
        {
            var info = new DamageInfo(spell.damage, caster.gameObject, point, spell.heavy);
            if (!target.TakeDamage(info)) return false;
            target.GetComponentInParent<IHitReceiver>()?.OnHit(info, push);
            if (stun) SpellEffects.Stun(target, spell, push);
            caster.HitStop(spell.hitStop > 0f ? spell.hitStop : 0.05f);
            caster.Shake(spell.shake);
            return true;
        }

        // Scales a model so its renderers span `size` on each axis set above zero (others keep the proportion of the first one set).
        public static void FitTo(Transform model, Vector3 size)
        {
            Bounds b = LocalBounds(model);
            if (b.size == Vector3.zero) return;
            float fx = size.x > 0f ? size.x / b.size.x : 0f, fy = size.y > 0f ? size.y / b.size.y : 0f, fz = size.z > 0f ? size.z / b.size.z : 0f;
            float any = fx > 0f ? fx : fy > 0f ? fy : fz;
            model.localScale = Vector3.Scale(model.localScale, new Vector3(fx > 0f ? fx : any, fy > 0f ? fy : any, fz > 0f ? fz : any));
        }

        // Bounds of a model's renderers in its parent's space.
        public static Bounds LocalBounds(Transform model)
        {
            Transform frame = model.parent;
            bool any = false;
            Bounds b = default;
            foreach (MeshFilter mf in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                Bounds m = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = m.center + Vector3.Scale(m.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 world = mf.transform.TransformPoint(corner);
                    Vector3 local = frame != null ? frame.InverseTransformPoint(world) : world;
                    if (!any) { b = new Bounds(local, Vector3.zero); any = true; }
                    else b.Encapsulate(local);
                }
            }
            return b;
        }
    }
}
