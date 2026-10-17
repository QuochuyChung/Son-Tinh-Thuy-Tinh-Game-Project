using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using SonTinhThuyTinh.Dialogue;
using SonTinhThuyTinh.Player;
using SonTinhThuyTinh.Quest;
using UnityEngine;

namespace SonTinhThuyTinh.DevTools
{
    // Smoke test for the map films (MapIntroCutscene, docs/progress.md 9.36 / 9.37): waits for a film to start, takes a picture of its camera
    // at every line (Temp/intro_<scene>_NN.png), advances the dialogue like the player would and checks the game is released afterwards.
    // With simulateGifts it then collects the character's three gifts one by one (GiftTracker.Collect), so the "next keeper" films and the
    // "all gifts" film play, and lists the quest keepers it finds. Writes Temp/intro_test.txt. Not part of the game.
    public class MapIntroSmokeTest : MonoBehaviour
    {
        public bool simulateGifts;
        [Tooltip("Also saves a Game view screenshot (HUD included) 4 s after the film: Temp/intro_gameview.png.")]
        public bool screenshotAfter;

        readonly StringBuilder log = new();
        DialogueRunner runner;
        int shot;

        void Log(string s) { log.AppendLine($"[{Time.realtimeSinceStartup:F1}] {s}"); Debug.Log("MapIntroSmoke " + s); }

        IEnumerator Start()
        {
            runner = FindFirstObjectByType<DialogueRunner>(FindObjectsInactive.Include);
            runner.LineStarted += l => Log("line: " + (string.IsNullOrEmpty(l.speaker) ? "(narration)" : l.speaker) + " | " + l.text);
            yield return Film("film 1");

            if (simulateGifts)
            {
                yield return new WaitForSecondsRealtime(1f);
                foreach (QuestKeeperNpc keeper in FindObjectsByType<QuestKeeperNpc>(FindObjectsSortMode.None).OrderBy(k => k.name))
                    Log($"keeper {keeper.name} at {keeper.transform.position}");
                var spawner = FindFirstObjectByType<PlayerSpawner>();
                var quest = spawner.Roster.Get(spawner.Character).GiftQuest;
                for (int i = 0; i < quest.RequiredGifts.Count; i++)
                {
                    GiftTracker.Collect(quest.RequiredGifts[i]);
                    Log($"collected gift {i + 1}: {quest.RequiredGifts[i].DisplayName} (count {GiftTracker.Count})");
                    yield return Film("gift " + (i + 1));
                }
            }

            if (screenshotAfter)
            {
                yield return new WaitForSecondsRealtime(4f);
                ScreenCapture.CaptureScreenshot(Path.Combine(Application.dataPath, "../Temp/intro_gameview.png"));
                yield return new WaitForSecondsRealtime(1.5f);
            }
            yield return new WaitForSecondsRealtime(1f);
            File.WriteAllText(Path.Combine(Application.dataPath, "../Temp/intro_test.txt"), log.ToString());
            Log("done");
        }

        IEnumerator Film(string label)
        {
            for (float t = 0f; t < 9f && !runner.IsPlaying; t += 0.1f) yield return new WaitForSecondsRealtime(0.1f);
            Log($"{label}: playing {runner.IsPlaying}, camera {(GameObject.Find("MapIntroCamera") != null)}, timeScale {Time.timeScale}");
            int lines = 0;
            while (runner.IsPlaying && lines < 20)
            {
                yield return new WaitForSecondsRealtime(3.2f);   // let the shot move
                Snap(label);
                Advance(); Advance();
                lines++;
            }
            yield return new WaitForSecondsRealtime(2.5f);
            Log($"{label} after: dialogue playing {runner.IsPlaying}, intro camera {(GameObject.Find("MapIntroCamera") != null)}, screen {(GameObject.Find("MapIntroScreen") != null)}, timeScale {Time.timeScale}, main camera enabled {Camera.main != null && Camera.main.enabled}");
        }

        void Advance() { if (runner.IsPlaying) typeof(DialogueRunner).GetMethod(Typing() ? "CompleteLine" : "ShowNextLine", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(runner, null); }
        bool Typing() => (bool)typeof(DialogueRunner).GetField("typing", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(runner);

        void Snap(string label)
        {
            var go = GameObject.Find("MapIntroCamera");
            var cam = go != null ? go.GetComponent<Camera>() : null;
            if (cam == null) return;
            var rt = new RenderTexture(1280, 720, 24);
            var old = cam.targetTexture;
            cam.targetTexture = rt; cam.Render(); cam.targetTexture = old;
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(Application.dataPath, $"../Temp/intro_{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}_{shot++:00}_{label.Replace(' ', '_')}.png"), tex.EncodeToPNG());
            Destroy(tex); rt.Release();
        }
    }
}
