using UnityEngine;

namespace SonTinhThuyTinh.Combat
{
    public enum SpellKind { Wind, Rain, Wave }

    // A spell (U / I / O): an attack with a cooldown that spawns an effect prefab. The effect script on the prefab (WindBlast, RainZone,
    // WaveProjectile) reads the numbers below.
    [CreateAssetMenu(menuName = "Son Tinh Thuy Tinh/Spell", fileName = "Spell")]
    public class SpellData : AttackData
    {
        [Header("Spell")]
        public string displayName;
        public Color color = Color.white;
        public SpellKind kind;
        public float cooldown = 8f;
        public GameObject effectPrefab;
        [Tooltip("Seconds into the animation when the effect appears.")]
        public float effectTime = 0.3f;

        [Header("Effect numbers")]
        [Tooltip("Wind: pull radius. Rain: radius of the rain. Wave: half width.")]
        public float radius = 8f;
        [Tooltip("Rain: how far in front of the caster the rain falls. Wave: how far the wave travels.")]
        public float range = 10f;
        [Tooltip("Wave: travel speed. Wind: top pull speed.")]
        public float speed = 14f;
        [Tooltip("Wind: upward speed of the targets.")]
        public float knockUpSpeed = 7f;
        [Tooltip("Rain: how long it keeps raining. Wind: how long the effect object lives.")]
        public float effectDuration = 6f;
        [Tooltip("Rain: damage per tick and seconds between ticks.")]
        public float tickDamage = 2f;
        public float tickInterval = 0.5f;
        [Tooltip("Rain: speed of targets inside (0.5 = half).")]
        public float slowFactor = 0.5f;
        [Tooltip("Wind / Wave: how long what it hits is stunned (0 = no stun). Bosses are stunned for bossStunScale of it.")]
        public float stunSeconds = 0f;
        [Range(0f, 1f)] public float bossStunScale = 0.5f;
        [Tooltip("Wave: also slows what it hits for this many seconds, at slowFactor.")]
        public float slowAfterHit = 0f;
    }
}
