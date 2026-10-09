using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using SonTinhThuyTinh.Combat;
using SonTinhThuyTinh.Player;
using SonTinhThuyTinh.Quest;
using UnityEngine;

namespace SonTinhThuyTinh.DevTools
{
    // Smoke test for the Sấu Chín Đuôi fight on Map_ThuyTinh (docs/task-sau-chin-duoi.md): walks the player onto the island, lets the
    // crocodile fight for a while (the player is healed so it survives), hits the crocodile down to phase 2, lets the player lose (checks
    // the reset), then beats it (checks the gift). Writes <project>/Temp/sau_smoke_test.txt and Temp/sau_*.png. Not part of the game;
    // add it to an empty object in Play mode.
    public class SauChinDuoiSmokeTest : MonoBehaviour
    {
        readonly StringBuilder log = new();
        PlayerController player;
        SauChinDuoiBoss boss;
        Transform root;
        float playerDamageTaken;
        int warningsSeen, spitsSeen, shot, spot;
        float nextMove;
        readonly System.Collections.Generic.SortedSet<string> states = new();
        static readonly string[] Clips = { "Idle", "Walk", "Charge", "Bite", "TailSweep", "WaterSpit", "TailSlam", "Hit", "Roar", "Death" };

        void Log(string s) { log.AppendLine($"[{Time.time:F1}] {s}"); Debug.Log("SauSmoke " + s); }

        IEnumerator Start()
        {
            yield return new WaitForSeconds(1.5f);
            player = FindObjectsByType<PlayerController>(FindObjectsSortMode.None).First(p => p.isActiveAndEnabled);
            boss = FindFirstObjectByType<SauChinDuoiBoss>(FindObjectsInactive.Include);
            root = GameObject.Find("SauChinDuoiArena").transform;
            Vector3 centre = root.Find("ArenaCenter").position, respawn = root.Find("PlayerRespawn").position;
            player.Health.Damaged += d => playerDamageTaken += d.Amount;
            Log($"start: boss active {boss.gameObject.activeInHierarchy}, awake {boss.IsAwake}, boss at {boss.transform.position}, player {player.transform.position}");

            Teleport(respawn);
            yield return new WaitForSeconds(1f);
            Log($"at the respawn point ({respawn}): awake {boss.IsAwake} (expected False)");

            Teleport(Vector3.Lerp(respawn, centre, 0.75f));
            yield return Watch(4f, false);
            Log($"after stepping on the island: awake {boss.IsAwake}, boss at {boss.transform.position}, bar alpha {BarAlpha():F2}");
            Snap("wake");
            yield return Watch(14f, true);
            Log($"phase 1 fight: player took {playerDamageTaken:F0} damage, warnings seen {warningsSeen}, spit balls seen {spitsSeen}, clips played {string.Join(",", states)}, boss health {boss.Health.Current}/{boss.Health.Max}");

            // hit it down to phase 2
            while (boss.Health.Current > boss.Health.Max * 0.48f) { Hit(25f); yield return Watch(0.35f, true); }
            yield return Watch(3f, true);
            Log($"phase 2: {boss.IsPhase2} (health {boss.Health.Current}), bar alpha {BarAlpha():F2}");
            playerDamageTaken = 0f; warningsSeen = 0; spitsSeen = 0; states.Clear();
            yield return Watch(16f, true, true);
            Log($"phase 2 fight: player took {playerDamageTaken:F0} damage, warnings seen {warningsSeen}, spit balls seen {spitsSeen}, clips played {string.Join(",", states)}");

            // lose
            player.Health.TakeDamage(new DamageInfo(1000f, boss.gameObject));
            Log($"player knocked out: dead {player.Health.IsDead}");
            yield return new WaitForSeconds(6f);
            Log($"after the reset: player dead {player.Health.IsDead}, player at {player.transform.position} (respawn {respawn}), boss awake {boss.IsAwake}, phase2 {boss.IsPhase2}, health {boss.Health.Current}/{boss.Health.Max}, boss at {boss.transform.position}");

            // win
            Teleport(Vector3.Lerp(respawn, centre, 0.75f));
            yield return Watch(7f, true);
            Log($"woken again: {boss.IsAwake}");
            while (!boss.Health.IsDead) { Hit(40f); yield return Watch(0.3f, true); }
            yield return new WaitForSeconds(1f);
            Snap("death");
            yield return new WaitForSeconds(3f);
            var gift = boss.GetType().GetField("reward", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(boss) as GiftItem;
            Log($"boss dead {boss.Health.IsDead}; gift {gift?.DisplayName} collected {GiftTracker.Has(gift)}; gifts collected {GiftTracker.Count}; bar alpha {BarAlpha():F2}");
            var hud = FindFirstObjectByType<GiftTrackerHUD>();
            if (hud != null)
            {
                var text = hud.GetComponentsInChildren<TMPro.TMP_Text>(true).Select(t => t.text.Replace("\n", " | "));
                Log("gift HUD: " + string.Join(" || ", text));
            }
            Snap("after");
            File.WriteAllText(Path.Combine(Application.dataPath, "../Temp/sau_smoke_test.txt"), log.ToString());
            Log("done");
        }

        void Teleport(Vector3 p)
        {
            player.SetBodyEnabled(false);
            player.transform.position = p + Vector3.up * 0.2f;
            player.SetBodyEnabled(true);
        }

        void Hit(float amount)
        {
            var info = new DamageInfo(amount, player.gameObject, boss.transform.position + Vector3.up * 1.5f, false);
            if (boss.Health.TakeDamage(info)) boss.OnHit(info, Vector3.zero);
        }

        float BarAlpha()
        {
            var bar = FindFirstObjectByType<SonTinhThuyTinh.UI.BossHealthBar>();
            return bar != null ? bar.GetComponent<CanvasGroup>().alpha : -1f;
        }

        // lets time pass, counting the crocodile's warnings / spit balls, keeping the player alive and near the crocodile
        IEnumerator Watch(float seconds, bool heal, bool snaps = false)
        {
            var seen = new System.Collections.Generic.HashSet<int>();
            float nextSnap = Time.time + 2f;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                foreach (var go in FindObjectsByType<Transform>(FindObjectsSortMode.None))
                {
                    if (go.name == "Warning" && seen.Add(go.GetInstanceID())) { warningsSeen++; if (snaps && Time.time > nextSnap) { Snap("warning"); nextSnap = Time.time + 5f; } }
                    if (go.name == "WaterSpit" && seen.Add(go.GetInstanceID())) spitsSeen++;
                }
                var info = boss.GetComponentInChildren<Animator>().GetCurrentAnimatorStateInfo(0);
                foreach (string c in Clips) if (info.IsName(c)) states.Add(c);
                if (heal && !player.Health.IsDead && player.Health.Current < 60f) player.Health.Heal(100f);
                // every 3 s the player stands somewhere else round the crocodile: in front close, beside it close, in front far away,
                // so the bite, the tail sweep, the spit and the charge all get their turn
                if (heal && boss.IsAwake && Time.time >= nextMove)
                {
                    nextMove = Time.time + 3f;
                    Transform b = boss.transform;
                    Vector3 c = root.Find("ArenaCenter").position;
                    Vector3 p = (spot++ % 3) switch { 0 => b.position + b.forward * 4.5f, 1 => b.position + b.right * 3.5f - b.forward * 1f, _ => b.position + b.forward * 11f };
                    Vector3 off = p - c; off.y = 0f;
                    if (off.magnitude > 13f) p = c + off.normalized * 13f;
                    p.y = c.y;
                    Teleport(p);
                }
                yield return null;
            }
        }

        void Snap(string tag)
        {
            var camGo = new GameObject("SmokeCam");
            var cam = camGo.AddComponent<Camera>();
            Vector3 c = root.Find("ArenaCenter").position, r = root.Find("PlayerRespawn").position;
            camGo.transform.position = c + (r - c).normalized * 22f + Vector3.up * 9f;
            camGo.transform.LookAt(boss.transform.position + Vector3.up * 1.5f);
            var rt = new RenderTexture(1280, 720, 24);
            var old = cam.targetTexture;
            cam.targetTexture = rt; cam.Render(); cam.targetTexture = old;
            cam.enabled = false;
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(Application.dataPath, $"../Temp/sau_{shot++:00}_{tag}.png"), tex.EncodeToPNG());
            Destroy(tex); rt.Release(); Destroy(camGo);
        }
    }
}
