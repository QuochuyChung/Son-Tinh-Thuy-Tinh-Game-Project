using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using SonTinhThuyTinh.Player;
using SonTinhThuyTinh.Quest;
using UnityEngine;

namespace SonTinhThuyTinh.DevTools
{
    // Checks the summit horse's guide arrow and its kick (docs/progress.md 9.23): puts the quest in Cleared, walks the player up to the
    // horse, takes a picture of the arrow, calls the kick the way running Shift too close would, and logs the health before / after
    // (the kick must hurt but never kill). Writes Temp/horse_kick_test.txt and Temp/horsekick_*.png. Not part of the game.
    public class HorseKickTest : MonoBehaviour
    {
        IEnumerator Start()
        {
            yield return new WaitForSeconds(1.5f);
            var log = new System.Text.StringBuilder();
            var player = FindObjectsByType<PlayerController>(FindObjectsSortMode.None).First(p => p.isActiveAndEnabled);
            HorseQuest.Set(HorseQuestState.Accepted);
            HorseQuest.Set(HorseQuestState.Cleared);
            yield return new WaitForSeconds(0.5f);
            var summit = FindFirstObjectByType<HorseSummit>(FindObjectsInactive.Include);
            var arrow = summit.transform.parent.Find("GuideArrow");
            log.AppendLine($"horse active {summit.gameObject.activeInHierarchy}, arrow {(arrow != null ? "present at " + arrow.position : "MISSING")}, old beam {(summit.transform.parent.Find("Beam") != null ? "still there" : "gone")}");

            Vector3 at = summit.transform.parent.position + summit.transform.parent.forward * 5f;
            Teleport(player, at);
            yield return new WaitForSeconds(2.5f);
            Snap("arrow");

            float before = player.Health.Current;
            var kick = typeof(HorseSummit).GetMethod("Kick", BindingFlags.NonPublic | BindingFlags.Instance);
            kick.Invoke(summit, new object[] { (player.transform.position - summit.transform.position) });
            yield return new WaitForSeconds(0.6f);
            log.AppendLine($"kick: health {before} -> {player.Health.Current}, dead {player.Health.IsDead}, moved {Vector3.Distance(at, player.transform.position):F1} m");

            player.Health.TakeDamage(new SonTinhThuyTinh.Combat.DamageInfo(player.Health.Current - 4f, gameObject));   // nearly dead
            yield return new WaitForSeconds(0.5f);
            float low = player.Health.Current;
            kick.Invoke(summit, new object[] { (player.transform.position - summit.transform.position) });
            yield return new WaitForSeconds(0.6f);
            log.AppendLine($"kick at {low} health: now {player.Health.Current}, dead {player.Health.IsDead} (must stay alive)");
            File.WriteAllText(Path.Combine(Application.dataPath, "../Temp/horse_kick_test.txt"), log.ToString());
            Debug.Log("HorseKickTest done");
        }

        static void Teleport(PlayerController p, Vector3 pos)
        {
            Terrain t = Terrain.activeTerrain;
            if (t != null) pos.y = t.SampleHeight(pos) + t.transform.position.y + 0.1f;
            p.SetBodyEnabled(false);
            p.transform.position = pos;
            p.SetBodyEnabled(true);
        }

        void Snap(string tag)
        {
            var cam = Camera.main;
            if (cam == null) return;
            var rt = new RenderTexture(1280, 720, 24);
            var old = cam.targetTexture;
            cam.targetTexture = rt; cam.Render(); cam.targetTexture = old;
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(Application.dataPath, $"../Temp/horsekick_{tag}.png"), tex.EncodeToPNG());
            Destroy(tex); rt.Release();
        }
    }
}
