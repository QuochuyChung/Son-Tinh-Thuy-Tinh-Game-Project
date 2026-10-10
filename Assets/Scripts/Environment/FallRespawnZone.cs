using SonTinhThuyTinh.Combat;
using SonTinhThuyTinh.Player;
using UnityEngine;

namespace SonTinhThuyTinh.Environment
{
    // Trigger volume at the bottom of a pit: a player who falls in is put back at the last RespawnCheckpoint passed
    // (or at `fallback` when none was passed in this scene) and takes a little damage. Never kills the player.
    [RequireComponent(typeof(Collider))]
    public class FallRespawnZone : MonoBehaviour
    {
        [Tooltip("Where the player goes when no checkpoint was passed yet.")]
        [SerializeField] Transform fallback;
        [SerializeField] float damage = 8f;

        void Reset() => GetComponent<Collider>().isTrigger = true;

        void OnTriggerEnter(Collider other)
        {
            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player == null || !player.isActiveAndEnabled) return;

            Vector3 target = FallRespawn.TryGetLast(gameObject.scene.handle, out Vector3 last) ? last
                : fallback != null ? fallback.position : transform.position;

            player.SetBodyEnabled(false);
            player.transform.position = target;
            player.SetBodyEnabled(true);   // also clears the fall speed

            Health health = player.Health;
            if (health != null && !health.IsDead && damage > 0f && health.Current > damage + 1f)
                health.TakeDamage(new DamageInfo(damage, gameObject));
        }
    }
}
