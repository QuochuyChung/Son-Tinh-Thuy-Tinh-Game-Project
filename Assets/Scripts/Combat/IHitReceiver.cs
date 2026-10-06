using UnityEngine;

namespace SonTinhThuyTinh.Combat
{
    // Something that reacts to being hit besides losing health (a training dummy now, enemies later). Put it on the same object as, or
    // above, the Health component. The attacker calls Health.TakeDamage first and this only when that hit was accepted.
    public interface IHitReceiver
    {
        // impulse: velocity kick in world space (horizontal push away from, or pull towards, the attacker plus an upward part).
        void OnHit(DamageInfo info, Vector3 impulse);

        // The target moves and acts at `factor` of its speed (0.5 = half) for `seconds`; calling again refreshes it.
        void ApplySlow(float factor, float seconds);
    }
}
