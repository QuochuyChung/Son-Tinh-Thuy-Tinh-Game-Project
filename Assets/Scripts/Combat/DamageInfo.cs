using UnityEngine;

namespace SonTinhThuyTinh.Combat
{
    public readonly struct DamageInfo
    {
        public readonly float Amount;
        public readonly GameObject Source;
        public readonly Vector3 HitPoint;
        // Heavy hits break poise and cause a stagger; light hits only flinch.
        public readonly bool IsHeavy;
        // A plain weapon blow (the player's attack combo, heavy and jump attacks), as opposed to a spell (U / I / O): enemies that guard
        // (the Ninja) can block these, never spells.
        public readonly bool IsNormalAttack;

        public DamageInfo(float amount, GameObject source = null, Vector3 hitPoint = default, bool isHeavy = false, bool isNormalAttack = false)
        {
            IsNormalAttack = isNormalAttack;
            Amount = amount;
            Source = source;
            HitPoint = hitPoint;
            IsHeavy = isHeavy;
        }
    }
}
