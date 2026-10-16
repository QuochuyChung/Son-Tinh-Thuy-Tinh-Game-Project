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
    // Smoke test for the story chain of Map_ThuyTinh (docs/ke-hoach-thuy-tinh-su-kien.md): opening narration -> fisherman's errand ->
    // pirates beaten (chain gone) -> village elder -> water ghosts beaten -> lamp lit (mist wall gone) -> narration at the crocodile's
    // island -> the deep water guard. Writes <project>/Temp/sea_story_test.txt and Temp/sea_story_*.png. Not part of the game.
    public class SeaStorySmokeTest : MonoBehaviour
    {
        readonly StringBuilder log = new();
        PlayerController player;
        DialogueRunner runner;
        int shot;

        void Log(string s) { log.AppendLine($"[{Time.time:F1}] {s}"); Debug.Log("SeaStory " + s); }

        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(1.5f);   // (the opening narration freezes the game's time from the first frame)
            player = FindObjectsByType<PlayerController>(FindObjectsSortMode.None).First(p => p.isActiveAndEnabled);
            runner = FindFirstObjectByType<DialogueRunner>(FindObjectsInactive.Include);
            Log($"start: state {SeaQuest.State}, ghosts {SeaQuest.GhostsCleared}, lamp {SeaQuest.LampLit}, fog {RenderSettings.fogDensity:F4}");

            // 0. opening narration (the player spawns inside its radius)
            yield return new WaitForSecondsRealtime(0.6f);
            Log($"opening narration playing: {runner.IsPlaying}");
            yield return Dialogue();

            // 1. the fisherman
            var fisher = FindObjectsByType<SeaNpc>(FindObjectsSortMode.None).First(n => n.name == "Fisherman");
            Teleport(fisher.transform.position + new Vector3(0f, 0f, -2f));
            yield return new WaitForSeconds(0.5f);
            fisher.GetComponent<Interactable>().Interact();
            yield return new WaitForSecondsRealtime(0.4f);
            Log($"fisherman talking: {runner.IsPlaying}");
            yield return Dialogue();
            Log($"after the offer: state {SeaQuest.State} (want Accepted)");

            // 2. the pirates on the deck
            var pirates = Director("Pirates");
            var chain = GameObject.Find("HarbourChain");
            Log($"pirates present: {Foes(pirates).Count(f => f.gameObject.activeSelf)}/{Foes(pirates).Length}, chain {(chain != null && chain.activeSelf)}");
            Vector3 deck = ((Transform)Field(pirates, "centre")).position;
            Teleport(deck + Vector3.left * 11f);
            yield return new WaitForSecondsRealtime(0.8f);
            Log($"ambush speaking: {runner.IsPlaying}");
            yield return Dialogue();
            yield return new WaitForSeconds(6f);
            Snap("pirates");
            yield return KillAll(pirates, 25f);
            yield return new WaitForSeconds(4f);
            Log($"after the pirates: state {SeaQuest.State} (want Cleared), chain active {(chain != null && chain.activeSelf)} (want false)");

            // 3. the elder
            var elder = FindObjectsByType<SeaNpc>(FindObjectsSortMode.None).First(n => n.name == "VillageElder");
            Teleport(elder.transform.position + new Vector3(-1.5f, 0f, 0f));
            yield return new WaitForSeconds(0.5f);
            elder.GetComponent<Interactable>().Interact();
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Dialogue();
            Log($"elder met: {SeaQuest.ElderMet}");

            // 4. the water ghosts and the lamp
            var ghosts = Director("Ghosts");
            Log($"ghosts present: {Foes(ghosts).Count(f => f.gameObject.activeSelf)}/{Foes(ghosts).Length}");
            Vector3 tower = ((Transform)Field(ghosts, "centre")).position;
            var mark = (Transform)Field(ghosts, "playerMark");
            Teleport(mark.position + Flat(tower - mark.position).normalized * 8f);   // a few steps up the hill from where the pier lands
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Dialogue();
            yield return new WaitForSeconds(6f);
            Snap("ghosts");
            yield return KillAll(ghosts, 25f);
            yield return new WaitForSeconds(4f);
            Log($"after the ghosts: ghosts cleared {SeaQuest.GhostsCleared}");
            var mist = GameObject.Find("SharkMist");
            Log($"mist before the lamp: {(mist != null && mist.activeSelf)}, fog {RenderSettings.fogDensity:F4}");
            Teleport(tower + new Vector3(2.5f, 0f, 0f));
            yield return new WaitForSeconds(0.5f);
            var lamp = FindObjectsByType<Interactable>(FindObjectsSortMode.None).First(i => i.name == "LampSpot");
            Log($"lamp interactable enabled: {lamp.enabled}");
            lamp.Interact();
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Dialogue();
            yield return new WaitForSeconds(5f);
            Log($"lamp lit: {SeaQuest.LampLit}, mist active {(mist != null && mist.activeSelf)} (want false), fog {RenderSettings.fogDensity:F4}");
            Snap("lamp");

            // 5. narration at the crocodile's island
            var intro = FindObjectsByType<SeaIntroTrigger>(FindObjectsSortMode.None).First(t => t.name == "CrocodileIsland");
            Teleport(intro.transform.position + new Vector3(20f, 0f, 0f));
            yield return new WaitForSecondsRealtime(0.8f);
            Log($"crocodile narration playing: {runner.IsPlaying}");
            yield return Dialogue();

            // 6. the deep water guard: walk into the open sea, expect to be put back on the shelf
            Vector3 safe = deck + Vector3.left * 9f;
            Teleport(safe);
            yield return new WaitForSeconds(1.2f);
            Vector3 seaPoint = new Vector3(deck.x + 22f, 0f, deck.z - 40f);
            float floor = Terrain.activeTerrain.SampleHeight(seaPoint) + Terrain.activeTerrain.transform.position.y;
            Log($"sea point floor {floor:F2} (water 2.4)");
            player.SetBodyEnabled(false); player.transform.position = new Vector3(seaPoint.x, floor + 0.1f, seaPoint.z); player.SetBodyEnabled(true);
            yield return new WaitForSeconds(3.5f);
            Log($"after 3.5 s in deep water: player at {player.transform.position} (was {seaPoint}), back near {safe}: {Vector3.Distance(Flat(player.transform.position), Flat(safe)) < 12f}");

            File.WriteAllText(Path.Combine(Application.dataPath, "../Temp/sea_story_test.txt"), log.ToString());
            Log("done");
        }

        static object Field(object o, string name) => o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o);

        SeaFoeDirector Director(string parentName) => GameObject.Find(parentName).GetComponent<SeaFoeDirector>();
        static NinjaEnemy[] Foes(SeaFoeDirector d) => (NinjaEnemy[])Field(d, "foes");

        IEnumerator KillAll(SeaFoeDirector d, float timeout)
        {
            for (float t = 0f; t < timeout; t += 0.3f)
            {
                if (!player.Health.IsDead && player.Health.Current < 60f) player.Health.Heal(100f);
                bool any = false;
                foreach (var f in Foes(d))
                    if (f != null && f.gameObject.activeInHierarchy && !f.IsDead) { any = true; f.Health.TakeDamage(new DamageInfo(60f, player.gameObject, f.transform.position + Vector3.up)); break; }
                if (!any) yield break;
                yield return new WaitForSeconds(0.3f);
            }
        }

        IEnumerator Dialogue()
        {
            int lines = 0;
            while (runner.IsPlaying && lines < 24)
            {
                yield return new WaitForSecondsRealtime(0.3f);
                Advance(); Advance();
                lines++;
            }
            yield return new WaitForSecondsRealtime(0.4f);
        }

        void Advance() { if (runner.IsPlaying) typeof(DialogueRunner).GetMethod(Typing() ? "CompleteLine" : "ShowNextLine", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(runner, null); }
        bool Typing() => (bool)typeof(DialogueRunner).GetField("typing", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(runner);

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        void Teleport(Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 40f, Vector3.down, out var hit, 80f, ~0, QueryTriggerInteraction.Ignore)) p.y = hit.point.y + 0.1f;
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
            File.WriteAllBytes(Path.Combine(Application.dataPath, $"../Temp/sea_story_{shot++:00}_{tag}.png"), tex.EncodeToPNG());
            Destroy(tex); rt.Release();
        }
    }
}
