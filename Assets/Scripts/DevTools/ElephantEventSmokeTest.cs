using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using SonTinhThuyTinh.Combat;
using SonTinhThuyTinh.Dialogue;
using SonTinhThuyTinh.Player;
using SonTinhThuyTinh.Quest;
using UnityEngine;

namespace SonTinhThuyTinh.DevTools
{
    // Smoke test for the elephant fight in Map_SonTinh (docs/progress.md 9.24): walks into the arena, steps through the intro, lets the
    // elephant fight for a while (the player is healed so it survives; counts the damage taken), loses once (back at the entrance, elephant
    // healed and asleep), walks in again and beats it (gift collected). Writes <project>/Temp/elephant_smoke_test.txt and Temp/elephant_*.png.
    public class ElephantEventSmokeTest : MonoBehaviour
    {
        readonly StringBuilder log = new();
        PlayerController player;
        DialogueRunner runner;
        float damageTaken;
        int shot;
        readonly System.Collections.Generic.HashSet<string> seenRings = new();

        void Log(string s) { log.AppendLine($"[{Time.time:F1}] {s}"); Debug.Log("ElephantSmoke " + s); }

        IEnumerator Start()
        {
            yield return new WaitForSeconds(1.5f);
            player = FindObjectsByType<PlayerController>(FindObjectsSortMode.None).First(p => p.isActiveAndEnabled);
            runner = FindFirstObjectByType<DialogueRunner>(FindObjectsInactive.Include);
            var ev = GameObject.Find("ElephantEvent").transform;
            var boss = ev.Find("VoiChinNga_Boss").GetComponent<VoiChinNgaBoss>();
            var entry = ev.Find("PlayerMark");
            var centre = ev.Find("ArenaCentre");
            var gift = AssetGift(ev);
            player.Health.Damaged += d => damageTaken += d.Amount;

            Log($"start: boss active {boss.gameObject.activeInHierarchy}, AI enabled {boss.enabled}, health {boss.Health.Current}/{boss.Health.Max}, at {boss.transform.position}, gift owned {GiftTracker.Has(gift)}");

            // the entrance is outside the wake-up radius (a defeated player must be able to breathe): nothing happens there
            Teleport(entry.position);
            yield return new WaitForSecondsRealtime(1.5f);
            Log($"at the entrance: dialogue playing {runner.IsPlaying}, AI enabled {boss.enabled}");
            // two steps in
            Vector3 inside = Vector3.Lerp(entry.position, centre.position, 0.4f);
            Teleport(inside);
            yield return new WaitForSecondsRealtime(0.6f);
            Log($"inside: dialogue playing {runner.IsPlaying}");
            yield return Dialogue();
            yield return new WaitForSeconds(1f);
            Log($"after the intro: AI enabled {boss.enabled}, hud {(GameObject.Find("ElephantBossHUD") != null)}");
            Snap("start");

            // the fight: stand in front of it, healed, for a while
            float hpBefore = boss.Health.Current;
            for (float t = 0f; t < 28f; t += Time.deltaTime)
            {
                Heal();
                // the red warning rings: log the first one of each kind and take a picture of it
                foreach (var lr in FindObjectsByType<LineRenderer>(FindObjectsSortMode.None))
                {
                    if (!lr.name.StartsWith("VoiChinNga_") || !seenRings.Add(lr.name)) continue;
                    var m = lr.sharedMaterial;
                    Log($"ring {lr.name}: shader {(m != null ? m.shader.name : "none")}, material colour {(m != null && m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor").ToString() : "n/a")}, line colour {lr.startColor}");
                    Snap("ring");
                }
                yield return null;
            }
            Log($"fight 28 s: player took {damageTaken:F0}, boss at {boss.transform.position} ({Vector3.Distance(boss.transform.position, centre.position):F1} m from the centre), boss health {boss.Health.Current}/{boss.Health.Max}");

            // lose
            player.IsInvulnerable = false;
            player.Health.TakeDamage(new DamageInfo(5000f, boss.gameObject));
            yield return new WaitForSeconds(6f);
            Log($"after losing: player dead {player.Health.IsDead} at {player.transform.position} (entrance {entry.position}), boss AI enabled {boss.enabled}, boss health {boss.Health.Current}/{boss.Health.Max}, boss at {boss.transform.position}");

            // again, then win
            yield return new WaitForSeconds(1.5f);
            Log($"standing at the entrance after the defeat: AI enabled {boss.enabled} (should be off)");
            Teleport(inside);
            yield return new WaitForSeconds(1.5f);
            Log($"back in: AI enabled {boss.enabled}, dialogue playing {runner.IsPlaying} (should be on, no second intro)");
            for (int i = 0; i < 60 && !boss.Health.IsDead; i++)
            {
                Heal();
                boss.Health.TakeDamage(new DamageInfo(40f, player.gameObject, boss.transform.position + Vector3.up * 3f));
                yield return new WaitForSeconds(0.25f);
            }
            Log($"boss dead {boss.Health.IsDead}");
            yield return new WaitForSeconds(3f);
            Snap("won");
            Log($"gift owned {GiftTracker.Has(gift)}, gifts {GiftTracker.Count}, hud {(GameObject.Find("ElephantBossHUD") != null && GameObject.Find("ElephantBossHUD").activeSelf)}");
            File.WriteAllText(Path.Combine(Application.dataPath, "../Temp/elephant_smoke_test.txt"), log.ToString());
            Log("done");
        }

        static GiftItem AssetGift(Transform ev)
        {
            var so = new UnityEngine.Object[] { ev.GetComponent<VoiChinNgaArenaEvent>() };
            var f = typeof(VoiChinNgaArenaEvent).GetField("gift", BindingFlags.NonPublic | BindingFlags.Instance);
            return (GiftItem)f.GetValue(so[0]);
        }

        IEnumerator Dialogue()
        {
            int lines = 0;
            while (runner.IsPlaying && lines < 14)
            {
                yield return new WaitForSecondsRealtime(0.3f);
                Advance(); Advance();
                lines++;
            }
            yield return new WaitForSecondsRealtime(0.4f);
        }

        void Advance() { if (runner.IsPlaying) typeof(DialogueRunner).GetMethod(Typing() ? "CompleteLine" : "ShowNextLine", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(runner, null); }
        bool Typing() => (bool)typeof(DialogueRunner).GetField("typing", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(runner);

        void Heal() { if (!player.Health.IsDead && player.Health.Current < 60f) player.Health.Heal(100f); }

        void Teleport(Vector3 p)
        {
            Terrain t = Terrain.activeTerrain;
            if (t != null) p.y = t.SampleHeight(p) + t.transform.position.y + 0.1f;
            player.SetBodyEnabled(false);
            player.transform.position = p;
            player.SetBodyEnabled(true);
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
            File.WriteAllBytes(Path.Combine(Application.dataPath, $"../Temp/elephant_{shot++:00}_{tag}.png"), tex.EncodeToPNG());
            Destroy(tex); rt.Release();
        }
    }
}
