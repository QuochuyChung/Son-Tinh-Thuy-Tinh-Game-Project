using SonTinhThuyTinh.Player;
using UnityEngine;

namespace SonTinhThuyTinh.Environment
{
    // Safety net for a map built on a Terrain: a player who ends up below the ground (a missed collider, a teleport onto a wrong height,
    // a hot reload in the middle of Play) is put back on the last place they stood safely, or on the ground right above where they are.
    // Seen from under the terrain the world looks like a blue-grey void with dark grass blades hanging down, and the way out is gone.
    // Pits are part of the terrain (their floor is the ground), so falling into one is not "below the ground"; see FallRespawnZone.
    public class TerrainFloorGuard : MonoBehaviour
    {
        [Tooltip("Metres below the terrain surface that count as fallen through.")]
        [SerializeField] float tolerance = 1.5f;
        [SerializeField] float checkEvery = 0.2f;

        PlayerController player;
        Vector3 lastSafe;
        bool hasSafe;
        float nextCheck, nextSafeAt;

        void Update()
        {
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + checkEvery;

            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null) return;
            if (player == null || !player.isActiveAndEnabled)
            {
                player = null;
                foreach (PlayerController c in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
                    if (c.isActiveAndEnabled) { player = c; break; }
                if (player == null) return;
            }

            Vector3 p = player.transform.position;
            Vector3 origin = terrain.transform.position, size = terrain.terrainData.size;
            bool inside = p.x >= origin.x && p.x <= origin.x + size.x && p.z >= origin.z && p.z <= origin.z + size.z;
            float ground = inside ? terrain.SampleHeight(p) + origin.y : float.NegativeInfinity;

            if (inside ? p.y < ground - tolerance : p.y < origin.y - 5f)
            {
                Vector3 target = hasSafe ? lastSafe : inside ? new Vector3(p.x, ground + 0.5f, p.z) : p;
                if (target == p) return;
                player.SetBodyEnabled(false);
                player.transform.position = target;
                player.SetBodyEnabled(true);   // also clears the fall speed
                Debug.LogWarning($"TerrainFloorGuard: the player was under the ground at {p}, put back at {target}", this);
            }
            else if (inside && player.IsGrounded && p.y >= ground - 0.3f && Time.unscaledTime >= nextSafeAt)
            {
                lastSafe = p + Vector3.up * 0.1f;
                hasSafe = true;
                nextSafeAt = Time.unscaledTime + 1f;
            }
        }
    }
}
