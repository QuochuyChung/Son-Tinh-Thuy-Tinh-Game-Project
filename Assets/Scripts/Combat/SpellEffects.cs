using System.Collections.Generic;
using SonTinhThuyTinh.Player;
using UnityEngine;

namespace SonTinhThuyTinh.Combat
{
    public static class SpellEffects
    {
        // Spawns the spell's effect prefab at the caster and lets it do its work.
        public static void Cast(PlayerController caster, SpellData spell)
        {
            if (spell.effectPrefab == null) return;
            Transform t = caster.transform;
            GameObject effect = Object.Instantiate(spell.effectPrefab, t.position, t.rotation);
            foreach (ISpellEffect script in effect.GetComponents<ISpellEffect>()) script.Init(caster, spell);
        }

        // Everything with Health among the colliders (not `exclude`), each once.
        public static List<Health> Targets(Collider[] found, Health exclude)
        {
            var seen = new HashSet<Health>();
            var list = new List<Health>();
            foreach (Collider c in found)
            {
                Health h = c.GetComponentInParent<Health>();
                if (h == null || h == exclude || !seen.Add(h)) continue;
                list.Add(h);
            }
            return list;
        }
    }
}
