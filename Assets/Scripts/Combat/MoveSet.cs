using TMPro;
using UnityEngine;

namespace SonTinhThuyTinh.Combat
{
    // Everything a character can do in a fight. A character without one (Son Tinh for now) cannot attack.
    [CreateAssetMenu(menuName = "Son Tinh Thuy Tinh/Move Set", fileName = "MoveSet")]
    public class MoveSet : ScriptableObject
    {
        [Tooltip("Tap the attack button: these in order.")]
        public AttackData[] lightCombo;
        [Tooltip("Hold the attack button (or right mouse button).")]
        public AttackData heavy;
        [Tooltip("Attack button while in the air.")]
        public AttackData jumpAttack;
        [Tooltip("U, I, O.")]
        public SpellData[] spells;

        [Header("Getting hit (clip speed and how long the character is stunned)")]
        public float hitLightSpeed = 1.4f;
        public float hitLightDuration = 0.7f;
        public float hitHeavySpeed = 0.9f;
        public float hitHeavyDuration = 1.15f;
        [Tooltip("Damage from this up staggers (the heavy reaction).")]
        public float staggerDamage = 25f;

        [Header("Shared effects and UI")]
        public GameObject hitSplash;
        public TMP_FontAsset hudFont;
    }
}
