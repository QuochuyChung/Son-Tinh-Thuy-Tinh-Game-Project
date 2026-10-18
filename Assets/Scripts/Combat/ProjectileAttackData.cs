using UnityEngine;

namespace SonTinhThuyTinh.Combat
{
    [CreateAssetMenu(menuName = "Son Tinh Thuy Tinh/Projectile Attack", fileName = "ProjectileAttack")]
    public class ProjectileAttackData : AttackData
    {
        [Header("Projectile")]
        public GameObject projectilePrefab;
        [Tooltip("Seconds into the cast when the projectile leaves the hands.")]
        public float projectileTime = 0.45f;
        public float projectileSpeed = 16f;
        public float projectileRange = 14f;
        public float projectileRadius = 0.65f;
        [Tooltip("Local spawn point: x right, y up, z forward.")]
        public Vector3 projectileSpawnOffset = new(0f, 1.25f, 0.85f);
    }
}
