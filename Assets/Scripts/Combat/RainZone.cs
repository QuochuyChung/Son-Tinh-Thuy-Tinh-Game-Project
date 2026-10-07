using SonTinhThuyTinh.Player;
using UnityEngine;

namespace SonTinhThuyTinh.Combat
{
    // Hô mưa: a patch of rain in front of the caster that slows and slowly hurts what stands in it.
    public class RainZone : MonoBehaviour, ISpellEffect
    {
        SpellData spell;
        PlayerController caster;
        float endsAt;
        float nextTick;

        public void Init(PlayerController owner, SpellData data)
        {
            caster = owner;
            spell = data;
            Vector3 p = owner.transform.position + owner.transform.forward * data.range * 0.5f;
            transform.SetPositionAndRotation(p, Quaternion.identity);
            endsAt = Time.time + data.effectDuration;
            transform.localScale = new Vector3(data.radius, 1f, data.radius) / 6f;   // the prefab is built for a radius of 6
            Destroy(gameObject, data.effectDuration + 1.5f);
        }

        void Update()
        {
            if (spell == null || Time.time > endsAt || Time.time < nextTick) return;
            nextTick = Time.time + spell.tickInterval;
            foreach (Health h in SpellEffects.Targets(Physics.OverlapSphere(transform.position + Vector3.up, spell.radius, ~0, QueryTriggerInteraction.Ignore), caster.Health))
            {
                var receiver = h.GetComponentInParent<IHitReceiver>();
                receiver?.ApplySlow(spell.slowFactor, spell.tickInterval + 0.3f);
                if (spell.tickDamage > 0f)
                {
                    var info = new DamageInfo(spell.tickDamage, caster.gameObject, h.transform.position, false);
                    if (h.TakeDamage(info)) receiver?.OnHit(info, Vector3.zero);
                }
            }
        }
    }
}
