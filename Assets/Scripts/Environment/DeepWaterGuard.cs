using SonTinhThuyTinh.Player;
using SonTinhThuyTinh.Quest;
using UnityEngine;

namespace SonTinhThuyTinh.Environment
{
    // The open sea of Map_ThuyTinh is walkable only where it is shallow (the shelf under the piers, the pool of Shark Bay, the islands).
    // A player who wades out into the deep water (feet more than maxDepth under the surface) for more than graceTime seconds is
    // swept back to the last place they stood in shallow water, so the nets and barriers on the way cannot be waded around.
    public class DeepWaterGuard : MonoBehaviour
    {
        [SerializeField] float waterY = 2.4f;
        [Tooltip("Water this deep over the feet counts as too deep.")]
        [SerializeField] float maxDepth = 1.9f;
        [SerializeField] float graceTime = 1.2f;
        [SerializeField] string message = "Nước quá sâu, dòng chảy cuốn bạn về chỗ cũ";

        PlayerController player;
        Vector3 lastSafe;
        bool hasSafe;
        float deepSince = -1f, nextSafeAt, nextCheck;

        void Update()
        {
            if (Time.time < nextCheck) return;
            nextCheck = Time.time + 0.1f;
            if (player == null || !player.isActiveAndEnabled)
            {
                player = null;
                foreach (PlayerController c in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
                    if (c.isActiveAndEnabled) { player = c; break; }
                if (player == null) return;
            }

            Vector3 p = player.transform.position;
            bool deep = p.y < waterY - maxDepth;
            if (!deep)
            {
                deepSince = -1f;
                if (player.IsGrounded && p.y > waterY - 1.3f && Time.time >= nextSafeAt)
                {
                    lastSafe = p + Vector3.up * 0.1f;
                    hasSafe = true;
                    nextSafeAt = Time.time + 0.5f;
                }
                return;
            }

            if (deepSince < 0f) deepSince = Time.time;
            if (Time.time - deepSince < graceTime || !hasSafe) return;
            deepSince = -1f;
            player.SetBodyEnabled(false);
            player.transform.position = lastSafe;
            player.SetBodyEnabled(true);
            GiftTracker.Announce(message);
        }
    }
}
