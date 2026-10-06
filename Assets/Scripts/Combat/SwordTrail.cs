using UnityEngine;

namespace SonTinhThuyTinh.Combat
{
    // The glowing streak behind the sword while an attack is swinging. The trail renderers sit on the blade (built by CombatVfxBuilder).
    public class SwordTrail : MonoBehaviour
    {
        [SerializeField] TrailRenderer[] trails;

        public void SetEmitting(bool on)
        {
            if (trails == null) return;
            foreach (TrailRenderer t in trails)
            {
                if (t == null) continue;
                if (on && !t.emitting) t.Clear();
                t.emitting = on;
            }
        }
    }
}
