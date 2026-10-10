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
    // Smoke test for Gà Chín Cựa on Map_SonTinh (docs/ke-hoach-ga-chin-cua.md): walks Sơn Tinh into the stone ring, steps through the cutscene
    // lines (a press every 2.2 s, pictures of each shot), lets the two Ninja fight for a while (the player is healed so it survives), checks
    // that normal attacks get blocked now and then and spells never, loses once (reset), wins, steps through the leader's lines, chases the
    // rooster and catches it. Writes <project>/Temp/rooster_smoke_test.txt and Temp/rooster_*.png. Not part of the game.
    public class RoosterQuestSmokeTest : MonoBehaviour
    {
        readonly StringBuilder log = new();
        PlayerController player;
        RoosterQuestDirector director;
        NinjaEnemy[] fighters;
        RoosterRunner rooster;
        DialogueRunner runner;
        Transform centre;
        float damageTaken, maxDrift;
        int shot;

        void Log(string s) { log.AppendLine($"[{Time.time:F1}] {s}"); Debug.Log("RoosterSmoke " + s); }

        IEnumerator Start()
        {
            yield return new WaitForSeconds(1.5f);
            player = FindObjectsByType<PlayerController>(FindObjectsSortMode.None).First(p => p.isActiveAndEnabled);
            director = FindFirstObjectByType<RoosterQuestDirector>();
            var root = director.transform;
            fighters = new[] { root.Find("Ninja_1").GetComponent<NinjaEnemy>(), root.Find("Ninja_2").GetComponent<NinjaEnemy>() };
            rooster = root.GetComponentInChildren<RoosterRunner>(true);
            runner = FindFirstObjectByType<DialogueRunner>(FindObjectsInactive.Include);
            centre = root.Find("Marks/RingCentre");
            var mark = root.Find("Marks/SonTinhMark");
            player.Health.Damaged += d => damageTaken += d.Amount;
            Log($"start: state {RoosterQuest.State}, ninja active {fighters[0].gameObject.activeSelf}, rooster at {rooster.transform.position}, centre {centre.position}");

            // into the ring: the cutscene
            Teleport(centre.position - (centre.position - mark.position).normalized * 17f);
            yield return new WaitForSeconds(0.5f);
            Log($"near the ring: dialogue playing {runner.IsPlaying}");
            // pressed through quickly, like an impatient player (this used to leave the Ninja without their swords)
            int lines = 0;
            while (runner.IsPlaying && lines < 12)
            {
                yield return new WaitForSeconds(0.35f);
                if (lines == 4) Snap("cut" + lines);
                Advance(); Advance();   // finish the typing, then go on
                lines++;
            }
            yield return new WaitForSeconds(1f);
            Log($"after the cutscene: state {RoosterQuest.State}, rooster {rooster.Current} (parent {rooster.transform.parent.name}), player at {player.transform.position}");

            // the fight: are the swords in hand, does a body ever drift off its root (the clips used to snap back)
            yield return new WaitForSeconds(1f);
            Log($"swords in hand: {fighters[0].IsArmed}/{fighters[1].IsArmed}");
            maxDrift = 0f;
            yield return Watch(18f, true);
            Log($"fight 18 s: player took {damageTaken:F0}, ninja health {fighters[0].Health.Current}/{fighters[1].Health.Current}, largest body drift from the root {maxDrift:F2} m, swords in hand {fighters[0].IsArmed}/{fighters[1].IsArmed}");
            Snap("fight");

            // guard: 20 normal blows and 10 spells on Ninja_1
            int blocked = 0, spellBlocked = 0;
            for (int i = 0; i < 20; i++)
            {
                Teleport(fighters[0].transform.position - fighters[0].transform.forward * -1.6f);
                if (!Hit(fighters[0], 1f, true)) blocked++;
                yield return Watch(0.7f, true);
            }
            for (int i = 0; i < 10; i++) { if (!Hit(fighters[0], 1f, false)) spellBlocked++; yield return Watch(0.7f, true); }
            Log($"guard: normal blows blocked {blocked}/20, spells blocked {spellBlocked}/10");

            // lose
            player.IsInvulnerable = false;   // (not in the middle of a hit's i-frames)
            player.Health.TakeDamage(new DamageInfo(1000f, fighters[0].gameObject));
            yield return new WaitForSeconds(5f);
            Log($"after losing: player dead {player.Health.IsDead} at {player.transform.position} (mark {mark.position}), ninja health {fighters[0].Health.Current}/{fighters[1].Health.Current}");
            Teleport(centre.position);
            yield return Watch(3f, true);

            // win
            foreach (var n in fighters) while (!n.IsDead) { Hit(n, 30f, false); yield return Watch(0.3f, true); }
            Log("both Ninja down");
            yield return new WaitForSecondsRealtime(2f);   // the leader's lines freeze the game (QuestDialogue)
            Snap("leader");
            lines = 0;
            while (runner.IsPlaying && lines < 6) { yield return new WaitForSecondsRealtime(1.5f); Advance(); Advance(); lines++; }
            yield return new WaitForSeconds(1.5f);
            Snap("leaderruns");
            Log($"after the leader's lines: state {RoosterQuest.State}, rooster {rooster.Current}");

            // the rooster runs about: sample where it goes, then chase it
            var path = new StringBuilder();
            for (int i = 0; i < 10; i++) { yield return new WaitForSeconds(0.6f); path.Append(Flat(rooster.transform.position - centre.position).ToString("F1")).Append(' '); }
            Log("rooster positions (from the centre): " + path);
            Teleport(rooster.transform.position + (centre.position - rooster.transform.position).normalized * 8f);   // start 8 m away
            float chaseStart = Time.time, nextNote = 0f;
            Log($"chase starts {Flat(rooster.transform.position - player.transform.position).magnitude:F1} m from the rooster");
            var interact = rooster.GetComponent<Interactable>();
            int tries = 0;
            while (rooster.Current == RoosterRunner.Mode.Free && Time.time - chaseStart < 90f)
            {
                // run at it like a player would (Sơn Tinh runs 5.4 m/s), press E when close enough
                Vector3 to = Flat(rooster.transform.position - player.transform.position);
                if (Time.time >= nextNote)
                {
                    nextNote = Time.time + 3f;
                    var bf = BindingFlags.NonPublic | BindingFlags.Instance;
                    Log($"  chase: {to.magnitude:F1} m, rooster {typeof(RoosterRunner).GetField("state", bf).GetValue(rooster)}, flaps left {typeof(RoosterRunner).GetField("left", bf).GetValue(rooster)}");
                }
                if (to.magnitude <= interact.Radius * 0.9f) { interact.Interact(); tries++; }
                Teleport(player.transform.position + to.normalized * Mathf.Min(to.magnitude, 5.4f * Time.deltaTime));
                yield return null;
            }
            Log($"rooster caught: {rooster.Current} after {Time.time - chaseStart:F1} s ({tries} presses); state {RoosterQuest.State}; gifts {GiftTracker.Count}");
            yield return new WaitForSeconds(0.5f);
            var hud = FindFirstObjectByType<GiftTrackerHUD>();
            if (hud != null) Log("gift HUD: " + string.Join(" || ", hud.GetComponentsInChildren<TMPro.TMP_Text>(true).Select(t => t.text.Replace("\n", " | "))));
            File.WriteAllText(Path.Combine(Application.dataPath, "../Temp/rooster_smoke_test.txt"), log.ToString());
            Log("done");
        }

        void Advance() { if (runner.IsPlaying) typeof(DialogueRunner).GetMethod(runnerTyping() ? "CompleteLine" : "ShowNextLine", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(runner, null); }
        bool runnerTyping() => (bool)typeof(DialogueRunner).GetField("typing", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(runner);

        bool Hit(NinjaEnemy n, float amount, bool normal)
        {
            var info = new DamageInfo(amount, player.gameObject, n.transform.position + Vector3.up, false, normal);
            if (!n.Health.TakeDamage(info)) return false;
            n.OnHit(info, Vector3.zero);
            return true;
        }

        void Teleport(Vector3 p)
        {
            Terrain t = Terrain.activeTerrain;
            if (t != null) p.y = t.SampleHeight(p) + t.transform.position.y + 0.1f;
            player.SetBodyEnabled(false);
            player.transform.position = p;
            player.SetBodyEnabled(true);
        }

        IEnumerator Watch(float seconds, bool heal)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                if (heal && !player.Health.IsDead && player.Health.Current < 50f) player.Health.Heal(100f);
                foreach (var n in fighters)
                {
                    if (n == null || n.IsDead || !n.gameObject.activeInHierarchy) continue;
                    var hips = n.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Hips);
                    if (hips != null) maxDrift = Mathf.Max(maxDrift, Flat(hips.position - n.transform.position).magnitude);
                }
                yield return null;
            }
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

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
            File.WriteAllBytes(Path.Combine(Application.dataPath, $"../Temp/rooster_{shot++:00}_{tag}.png"), tex.EncodeToPNG());
            Destroy(tex); rt.Release();
        }
    }
}
