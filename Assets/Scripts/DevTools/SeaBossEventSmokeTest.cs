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
    // Smoke test for a boss fight inside Map_ThuyTinh (SeaBossArenaEvent, docs/ke-hoach-thuy-tinh-su-kien.md): steps into the arena,
    // reads the intro, fights for a while (the player is healed so it survives; counts the damage taken), loses once (back at the
    // entrance, boss healed and asleep), walks in again and beats the boss (gift collected, the net gone). Writes
    // <project>/Temp/sea_<name>_test.txt and Temp/sea_<name>_*.png. Not part of the game.
    public class SeaBossEventSmokeTest : MonoBehaviour
    {
        public string eventName = "SharkEvent";

        readonly StringBuilder log = new();
        PlayerController player;
        DialogueRunner runner;
        float damageTaken;
        int shot;

        void Log(string s) { log.AppendLine($"[{Time.time:F1}] {s}"); Debug.Log("SeaBossSmoke " + s); }

        IEnumerator Start()
        {
            yield return new WaitForSeconds(1.5f);
            player = FindObjectsByType<PlayerController>(FindObjectsSortMode.None).First(p => p.isActiveAndEnabled);
            runner = FindFirstObjectByType<DialogueRunner>(FindObjectsInactive.Include);
            UnlockAllGifts();   // the keeper's quiz (QuestQuizProgress) gates the bosses: skip it here
            var evObject = GameObject.Find(eventName);
            var ev = evObject.GetComponent<SeaBossArenaEvent>();
            var bossBehaviour = (MonoBehaviour)Field(ev, "boss");
            var ai = (IArenaBoss)bossBehaviour;
            var centre = (Transform)Field(ev, "arenaCentre");
            var entry = (Transform)Field(ev, "playerMark");
            var gift = (GiftItem)Field(ev, "gift");
            var open = (GameObject[])Field(ev, "openOnWin");
            float trigger = (float)Field(ev, "triggerRadius"), arena = (float)Field(ev, "arenaRadius");
            player.Health.Damaged += d => damageTaken += d.Amount;

            Log($"start: boss active {bossBehaviour.gameObject.activeInHierarchy}, AI enabled {bossBehaviour.enabled}, health {ai.Health.Current}/{ai.Health.Max}, at {bossBehaviour.transform.position}, gift owned {GiftTracker.Has(gift)}, blockers on: {string.Join(",", open.Select(o => o.name + "=" + o.activeSelf))}");

            Teleport(entry.position);
            yield return new WaitForSecondsRealtime(1.5f);
            Log($"at the entrance ({Flat(entry.position - centre.position).magnitude:F1} m from the centre, trigger {trigger}): dialogue playing {runner.IsPlaying}, AI enabled {bossBehaviour.enabled}");

            Vector3 inside = centre.position + Flat(entry.position - centre.position).normalized * (trigger - 6f);
            Teleport(inside);
            yield return new WaitForSecondsRealtime(0.6f);
            Log($"inside: dialogue playing {runner.IsPlaying}");
            yield return Dialogue();
            yield return new WaitForSeconds(1f);
            Log($"after the intro: AI enabled {bossBehaviour.enabled}");
            Snap("start");

            float maxOff = 0f;
            for (float t = 0f; t < 24f; t += Time.deltaTime)
            {
                Heal();
                maxOff = Mathf.Max(maxOff, Flat(bossBehaviour.transform.position - centre.position).magnitude);
                if (Mathf.Abs(t - 12f) < Time.deltaTime) Snap("fight");
                yield return null;
            }
            Log($"fight 24 s: player took {damageTaken:F0}, boss farthest from the centre {maxOff:F1} m (arena {arena}), boss health {ai.Health.Current}/{ai.Health.Max}");

            player.IsInvulnerable = false;
            player.Health.TakeDamage(new DamageInfo(5000f, bossBehaviour.gameObject));
            yield return new WaitForSeconds(6f);
            Log($"after losing: player dead {player.Health.IsDead} at {player.transform.position} (entrance {entry.position}), boss AI enabled {bossBehaviour.enabled}, boss health {ai.Health.Current}/{ai.Health.Max}, boss at {bossBehaviour.transform.position}");

            yield return new WaitForSeconds(1.5f);
            Teleport(inside);
            yield return new WaitForSeconds(1.5f);
            Log($"back in: AI enabled {bossBehaviour.enabled}, dialogue playing {runner.IsPlaying} (should be on, no second intro)");
            for (int i = 0; i < 80 && !ai.Health.IsDead; i++)
            {
                Heal();
                ai.Health.TakeDamage(new DamageInfo(40f, player.gameObject, bossBehaviour.transform.position + Vector3.up * 2f));
                yield return new WaitForSeconds(0.25f);
            }
            Log($"boss dead {ai.Health.IsDead}");
            yield return new WaitForSeconds(3f);
            Snap("won");
            Log($"gift owned {GiftTracker.Has(gift)}, gifts {GiftTracker.Count}, blockers: {string.Join(",", open.Select(o => o.name + "=" + o.activeSelf))}");
            File.WriteAllText(Path.Combine(Application.dataPath, $"../Temp/sea_{eventName}_test.txt"), log.ToString());
            Log("done");
        }

        static void UnlockAllGifts()
        {
            var spawner = FindFirstObjectByType<PlayerSpawner>();
            var quest = spawner != null ? spawner.Roster.Get(spawner.Character).GiftQuest : null;
            if (quest != null) foreach (GiftItem gift in quest.RequiredGifts) QuestQuizProgress.Unlock(gift);
        }

        static object Field(object o, string name) => o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o);

        IEnumerator Dialogue()
        {
            int lines = 0;
            while (runner.IsPlaying && lines < 20)
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

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        // the player is put 0.1 m over whatever is under the point: a deck (box collider) or the terrain
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
            File.WriteAllBytes(Path.Combine(Application.dataPath, $"../Temp/sea_{eventName}_{shot++:00}_{tag}.png"), tex.EncodeToPNG());
            Destroy(tex); rt.Release();
        }
    }
}
