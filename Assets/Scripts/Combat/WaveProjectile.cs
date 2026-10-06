using System.Collections.Generic;
using SonTinhThuyTinh.Player;
using UnityEngine;

namespace SonTinhThuyTinh.Combat
{
    // Sóng nước nghe lệnh: a wall of water that runs straight ahead and throws back whatever it meets.
    public class WaveProjectile : MonoBehaviour, ISpellEffect
    {
        SpellData spell;
        PlayerController caster;
        Vector3 start;
        readonly HashSet<Health> hit = new();

        public void Init(PlayerController owner, SpellData data)
        {
            caster = owner;
            spell = data;
            transform.SetPositionAndRotation(owner.transform.position + owner.transform.forward * 1.2f, owner.transform.rotation);
            start = transform.position;
        }

        void Update()
        {
            if (spell == null) return;
            transform.position += transform.forward * (spell.speed * Time.deltaTime);
            Collider[] found = Physics.OverlapBox(transform.position + Vector3.up * 1.1f, new Vector3(spell.radius, 1.2f, 0.9f), transform.rotation, ~0, QueryTriggerInteraction.Ignore);
            foreach (Health h in SpellEffects.Targets(found, caster.Health))
            {
                if (!hit.Add(h)) continue;
                var info = new DamageInfo(spell.damage, caster.gameObject, h.transform.position, true);
                if (h.TakeDamage(info)) h.GetComponentInParent<IHitReceiver>()?.OnHit(info, transform.forward * spell.knockback + Vector3.up * spell.knockUp);
            }
            if (Vector3.Distance(start, transform.position) >= spell.range)
            {
                spell = null;
                Destroy(gameObject, 0.6f);
            }
        }
    }
}
