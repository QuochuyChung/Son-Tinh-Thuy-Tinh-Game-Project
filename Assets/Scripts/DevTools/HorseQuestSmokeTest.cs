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
    // Smoke test for the horse errand on Map_SonTinh (docs/progress.md 9.23): talks to the keeper (Accepted), walks to the clearing, steps
    // through the thieves' lines, lets them fight, loses once (reset at the checkpoint), wins (barricade falls, Cleared), rings the bell on the
    // summit (Calmed, the stable horse appears), and takes the horse from the keeper (Completed). Writes <project>/Temp/horse_smoke_test.txt
    // and Temp/horse_*.png. Not part of the game.
    public class HorseQuestSmokeTest : MonoBehaviour
    {
        readonly StringBuilder log = new();
        PlayerController player;
        DialogueRunner runner;
        Transform ev;
        NinjaEnemy[] thieves;
        float damageTaken;
        int shot;

        void Log(string s) { log.AppendLine($"[{Time.time:F1}] {s}"); Debug.Log("HorseSmoke " + s); }

        IEnumerator Start()
        {
            yield return new WaitForSeconds(1.5f);
            player = FindObjectsByType<PlayerController>(FindObjectsSortMode.None).First(p => p.isActiveAndEnabled);
            runner = FindFirstObjectByType<DialogueRunner>(FindObjectsInactive.Include);
            ev = GameObject.Find("MountainEvent").transform;
            thieves = new[] { ev.Find("HorseThief_1"), ev.Find("HorseThief_2"), ev.Find("HorseThief_3") }.Select(t => t.GetComponent<NinjaEnemy>()).ToArray();
            var barricade = ev.Find("Barricade").gameObject;
            var summitHorse = ev.Find("HorseSummit").gameObject;
            var stableHorse = FindFirstObjectByType<HorseStall>(FindObjectsInactive.Include).gameObject;
            foreach (var h in FindObjectsByType<HorseStall>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (h.gameObject != summitHorse) stableHorse = h.gameObject;
            var keeper = GameObject.Find("HorseKeeper").GetComponent<Interactable>();
            var centre = ev.Find("Marks/ClearingCentre");
            var mark = ev.Find("Marks/PlayerMark");
            player.Health.Damaged += d => damageTaken += d.Amount;

            void State(string when) => Log($"{when}: state {HorseQuest.State}; thieves {thieves.Count(t => t.gameObject.activeInHierarchy)}/3 active, barricade {barricade.activeInHierarchy}, summit horse {summitHorse.activeInHierarchy}, stable horse {stableHorse.activeInHierarchy}");
            State("start");

            // the keeper: the offer
            Teleport(keeper.transform.position + keeper.transform.forward * 1.5f);
            keeper.Interact();
            yield return Dialogue();
            State("after the keeper's offer");

            // the clearing: the thieves speak and fight
            Teleport(centre.position - centre.forward * 16f);
            yield return new WaitForSecondsRealtime(0.5f);
            Log($"near the clearing: dialogue playing {runner.IsPlaying}");
            yield return Dialogue();
            yield return new WaitForSeconds(1.2f);
            Log($"swords in hand {thieves.Count(t => t.IsArmed)}/3");
            Teleport(centre.position - centre.forward * 5f);
            for (float t = 0f; t < 14f; t += Time.deltaTime) { Heal(); yield return null; }
            Log($"fight 14 s: player took {damageTaken:F0}, thief health {string.Join("/", thieves.Select(x => x.Health.Current))}");
            Snap("fight");

            // lose once
            player.IsInvulnerable = false;
            player.Health.TakeDamage(new DamageInfo(1000f, thieves[0].gameObject));
            yield return new WaitForSeconds(6f);
            Log($"after losing: player dead {player.Health.IsDead}, at {player.transform.position} (mark {mark.position}), thief health {string.Join("/", thieves.Select(x => x.Health.Current))}");

            // win
            Teleport(centre.position - centre.forward * 5f);
            yield return new WaitForSeconds(2f);
            foreach (var n in thieves) while (!n.IsDead) { Hit(n, 30f); Heal(); yield return new WaitForSeconds(0.3f); }
            Log("all three thieves down");
            yield return new WaitForSeconds(4f);
            State("after the fight");
            Snap("cleared");

            // the summit: the bell
            var bell = summitHorse.GetComponentInChildren<HorseSummit>(true);
            Teleport(bell.transform.position + (summitHorse.transform.position - bell.transform.position).normalized * 0.5f);
            yield return new WaitForSeconds(0.6f);
            var interact = bell.GetComponent<Interactable>();
            Log($"at the horse: bell prompt enabled {interact.enabled} ('{interact.Prompt}')");
            Snap("summit");
            interact.Interact();
            yield return Dialogue();
            State("after ringing the bell");

            // the keeper: the horse is given
            Teleport(keeper.transform.position + keeper.transform.forward * 1.5f);
            keeper.Interact();
            yield return Dialogue();
            yield return new WaitForSeconds(0.5f);
            State("after the keeper's thanks");
            Log($"gifts collected: {GiftTracker.Count}");

            File.WriteAllText(Path.Combine(Application.dataPath, "../Temp/horse_smoke_test.txt"), log.ToString());
            Log("done");
        }

        // steps through whatever dialogue is playing (the game is frozen during it, so real time)
        IEnumerator Dialogue()
        {
            int lines = 0;
            yield return new WaitForSecondsRealtime(0.3f);
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

        void Heal() { if (!player.Health.IsDead && player.Health.Current < 50f) player.Health.Heal(100f); }

        void Hit(NinjaEnemy n, float amount)
        {
            var info = new DamageInfo(amount, player.gameObject, n.transform.position + Vector3.up, false, false);
            if (n.Health.TakeDamage(info)) n.OnHit(info, Vector3.zero);
        }

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
            File.WriteAllBytes(Path.Combine(Application.dataPath, $"../Temp/horse_{shot++:00}_{tag}.png"), tex.EncodeToPNG());
            Destroy(tex); rt.Release();
        }
    }
}
