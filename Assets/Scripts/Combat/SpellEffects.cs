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
            if (spell.kind == SpellKind.WaterPillar) { ThuyTinhWaterSpells.CastGroundWave(caster, spell); return; }
            if (spell.kind == SpellKind.AquaShield) { ThuyTinhWaterSpells.CastShield(caster, spell); return; }
            if (spell.effectPrefab == null) return;
            Transform t = caster.transform;
            GameObject effect = Object.Instantiate(spell.effectPrefab, t.position, t.rotation);
            foreach (ISpellEffect script in effect.GetComponents<ISpellEffect>()) script.Init(caster, spell);
        }

        // Stuns what a spell just hit (and slows it a while after), with a floating "Choáng!"; bosses only get a part of it.
        public static void StunAndSlow(Health target, SpellData spell)
        {
            if (spell == null || target == null) return;
            var receiver = target.GetComponentInParent<IHitReceiver>();
            if (receiver == null) return;
            if (spell.stunSeconds > 0f)
            {
                float seconds = target.GetComponentInParent<IArenaBoss>() != null ? spell.stunSeconds * spell.bossStunScale : spell.stunSeconds;
                if (seconds > 0.05f)
                {
                    receiver.ApplyStun(seconds);
                    FloatingText.Spawn(target.transform.position + Vector3.up * 2.6f, "Choáng!", new Color(1f, 0.92f, 0.3f));
                }
            }
            if (spell.slowAfterHit > 0f) receiver.ApplySlow(spell.slowFactor, spell.slowAfterHit + spell.stunSeconds);
        }

        // A real stun, also on a character (the rival in the final duel): it staggers on the spot for spell.stunSeconds (bosses for
        // bossStunScale of it) with stars over its head; `push` (m/s) shoves a character back while it starts.
        public static void Stun(Health target, SpellData spell, Vector3 push)
        {
            if (spell == null || target == null || target.IsDead || spell.stunSeconds <= 0f) return;
            float seconds = target.GetComponentInParent<IArenaBoss>() != null ? spell.stunSeconds * spell.bossStunScale : spell.stunSeconds;
            if (seconds <= 0.05f) return;
            var character = target.GetComponentInParent<PlayerController>();
            if (character != null) character.Stun(seconds, push);
            else
            {
                var receiver = target.GetComponentInParent<IHitReceiver>();
                if (receiver == null) return;
                receiver.ApplyStun(seconds);
            }
            FloatingText.Spawn(target.transform.position + Vector3.up * 2.6f, "Choáng!", new Color(1f, 0.92f, 0.3f));
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
