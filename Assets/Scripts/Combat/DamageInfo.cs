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

        public DamageInfo(float amount, GameObject source = null, Vector3 hitPoint = default, bool isHeavy = false)
        {
            Amount = amount;
            Source = source;
            HitPoint = hitPoint;
            IsHeavy = isHeavy;
        }
    }
}
