using SonTinhThuyTinh.Player;
using UnityEngine;

namespace SonTinhThuyTinh.Combat
{
    // Gọi gió: a gust that pulls everything around the caster in and throws it up. Instant; the prefab only lives on for the swirl.
    public class WindBlast : MonoBehaviour, ISpellEffect
    {
        public void Init(PlayerController caster, SpellData spell)
        {
            Vector3 centre = caster.transform.position + Vector3.up;
            Vector3 gather = caster.transform.position + caster.transform.forward * 2.5f;
            foreach (Health h in SpellEffects.Targets(Physics.OverlapSphere(centre, spell.radius, ~0, QueryTriggerInteraction.Ignore), caster.Health))
            {
                Vector3 pull = gather - h.transform.position;
                pull.y = 0f;
                float distance = pull.magnitude;
                pull = distance > 0.1f ? pull / distance : Vector3.zero;
                var info = new DamageInfo(spell.damage, caster.gameObject, h.transform.position, false);
                if (!h.TakeDamage(info)) continue;
                h.GetComponentInParent<IHitReceiver>()?.OnHit(info, pull * Mathf.Min(spell.speed, distance * 3f) + Vector3.up * spell.knockUpSpeed);
            }
            transform.SetParent(caster.transform, true);
            Destroy(gameObject, spell.effectDuration);
        }
    }
}
