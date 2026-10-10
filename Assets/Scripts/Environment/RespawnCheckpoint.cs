using SonTinhThuyTinh.Player;
using UnityEngine;

namespace SonTinhThuyTinh.Environment
{
    // Trigger volume across a route: remembers where the player last stood safely, for FallRespawnZone.
    [RequireComponent(typeof(Collider))]
    public class RespawnCheckpoint : MonoBehaviour
    {
        [Tooltip("Where the player reappears. This object when empty.")]
        [SerializeField] Transform spawnPoint;

        void Reset() => GetComponent<Collider>().isTrigger = true;

        void OnTriggerEnter(Collider other)
        {
            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player == null || !player.isActiveAndEnabled) return;
            FallRespawn.Remember(gameObject.scene.handle, (spawnPoint != null ? spawnPoint : transform).position);
        }
    }
}
