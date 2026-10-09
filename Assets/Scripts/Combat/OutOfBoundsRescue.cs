using SonTinhThuyTinh.Player;
using UnityEngine;

namespace SonTinhThuyTinh.Combat
{
    // Safety net for an arena: a player that ends up in the water below the platform, or outside it, is put back at the respawn point after
    // a moment (so a fall never goes on for ever). Put it on any object of the arena; the pool bed collider catches the fall first.
    public class OutOfBoundsRescue : MonoBehaviour
    {
        [Tooltip("Where the player is put back. The arena centre (this object) when empty.")]
        [SerializeField] Transform respawn;
        [Tooltip("Lower than this (m) counts as fallen into the water.")]
        [SerializeField] float minY = -1f;
        [Tooltip("Further than this (m) from the arena centre counts as outside the arena.")]
        [SerializeField] float maxDistance = 18f;
        [Tooltip("Seconds the player may stay out of bounds before being put back.")]
        [SerializeField] float delay = 1.5f;

        PlayerController player;
        float outSince = -1f;

        void Update()
        {
            if (player == null || !player.isActiveAndEnabled)
            {
                player = null;
                foreach (PlayerController candidate in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
                    if (candidate.isActiveAndEnabled) { player = candidate; break; }   // the boss copy of a player prefab has its controller switched off
                if (player == null) return;
            }

            Vector3 centre = transform.position;
            Vector3 p = player.transform.position;
            bool outside = p.y < minY || Vector2.Distance(new Vector2(p.x, p.z), new Vector2(centre.x, centre.z)) > maxDistance;
            if (!outside) { outSince = -1f; return; }

            if (outSince < 0f) outSince = Time.time;
            if (Time.time - outSince < delay) return;

            Transform target = respawn != null ? respawn : transform;
            player.SetBodyEnabled(false);
            player.transform.position = target.position;
            player.SetBodyEnabled(true);   // also clears the fall speed
            outSince = -1f;
        }
    }
}
