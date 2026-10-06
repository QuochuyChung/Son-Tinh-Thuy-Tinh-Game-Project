using System.Collections;
using System.IO;
using System.Text;
using SonTinhThuyTinh.Combat;
using SonTinhThuyTinh.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace SonTinhThuyTinh.DevTools
{
    // Editor smoke test for the fight (docs/progress.md 9.12): drives a virtual keyboard through the light combo, the heavy attack, the jump
    // attack, the three spells, a roll and two hits taken, in front of the training dummies of Sandbox_Combat, and writes what happened to
    // <project>/Temp/combat_smoke_test.txt plus side-view pictures Temp/combat_*.png. Not part of the game; add it to an empty object in
    // Play mode (the editor needs Application.runInBackground when it is not the active window).
    public class CombatSmokeTest : MonoBehaviour
    {
        static readonly string[] StateNames = { "Locomotion", "Dodge", "Attack1", "Attack2", "Attack3", "HeavyAttack", "JumpAttack", "SpellWind", "SpellRain", "SpellWave", "Hit", "JumpUp", "RunJump", "Death" };

        readonly StringBuilder log = new();
        Keyboard keyboard;
        PlayerController player;

#if UNITY_EDITOR
        InputSettings.EditorInputBehaviorInPlayMode previousBehavior;
        InputSettings.BackgroundBehavior previousBackground;
#endif

        IEnumerator Start()
        {
#if UNITY_EDITOR
            previousBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            // a virtual device is reset and disabled as soon as the editor is not the active window, unless focus is ignored
            previousBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#endif
            foreach (InputDevice stale in InputSystem.devices.ToArray())   // a leftover from a run that was stopped before Finish
                if (stale.name == "CombatSmokeKeyboard") InputSystem.RemoveDevice(stale);
            keyboard = InputSystem.AddDevice<Keyboard>("CombatSmokeKeyboard");
            // (disabling the real devices also silences the virtual keyboard in the editor, so the test just has to be run hands off)
            yield return new WaitForSeconds(1.5f);
            // an Esc from the real keyboard would pause the game (Time.timeScale 0) and freeze the test for good
            var pauseMenu = FindFirstObjectByType<SonTinhThuyTinh.UI.PauseMenu>();
            if (pauseMenu != null) pauseMenu.enabled = false;
            // and a real mouse moved meanwhile would turn the camera, so W / S would no longer mean the same direction
            var cameraInput = FindFirstObjectByType<SonTinhThuyTinh.CameraSystem.ThirdPersonCameraInput>();
            if (cameraInput != null) cameraInput.enabled = false;
            player = FindFirstObjectByType<PlayerController>();
            if (player == null) { Finish("no PlayerController in the scene"); yield break; }
            log.AppendLine($"player: {player.name}  hasCombat={player.HasCombat}  scene={gameObject.scene.name}  focused={Application.isFocused}  keyboard enabled={keyboard.enabled}");
            var dummies = FindObjectsByType<TrainingDummy>(FindObjectsSortMode.None);
            log.AppendLine("dummies: " + dummies.Length);
            Line("start");

            // walk up to the first dummy (6 m ahead), then stop
            Keys(Key.W);
            yield return new WaitForSeconds(0.85f);
            Keys();
            yield return Sample("stopped in front of the dummy", 0.5f, 0.25f);
            LogDummies();

            // light combo: three taps of J, each once the combo window is open
            log.AppendLine("== light combo (3 taps)");
            Tap(Key.J);
            yield return Sample("tap 1", 0.7f, 0.1f, "slash", 0.45f);
            Tap(Key.J);
            yield return Sample("tap 2", 0.55f, 0.1f, "kick", 0.4f);
            Tap(Key.J);
            yield return Sample("tap 3 (spin finisher)", 1.2f, 0.1f, "spin", 0.55f);
            LogDummies();
            yield return Sample("recover", 0.6f, 0.3f);

            // heavy: hold J
            log.AppendLine("== heavy attack (hold J)");
            Keys(Key.J);
            yield return new WaitForSeconds(0.45f);
            Keys();
            yield return Sample("heavy", 1.7f, 0.15f, "heavy", 0.55f);
            LogDummies();

            // jump attack: Space then J while in the air
            log.AppendLine("== jump attack");
            Tap(Key.Space);
            yield return new WaitForSeconds(0.35f);
            Tap(Key.J);
            yield return Sample("jump attack", 1.6f, 0.12f, "jumpattack", 0.8f);
            LogDummies();

            // spells
            log.AppendLine("== spells U I O");
            Tap(Key.U);
            yield return Sample("U wind", 1.4f, 0.15f, "wind", 0.7f);
            LogDummies();
            Tap(Key.I);
            yield return Sample("I rain", 1.4f, 0.2f, "rain", 1.1f);
            LogDummies();
            Tap(Key.O);
            yield return Sample("O wave", 1.8f, 0.15f, "wave", 0.75f);
            LogDummies();
            log.AppendLine($"  cooldowns left: U {player.SpellCooldownLeft(0):F1}  I {player.SpellCooldownLeft(1):F1}  O {player.SpellCooldownLeft(2):F1}");
            yield return FullScreen("hud");
            Tap(Key.U);
            yield return Sample("U again while cooling down (must not cast)", 0.5f, 0.25f);

            // roll
            log.AppendLine("== roll (Ctrl)");
            // sideways: a dummy that stands back in front of the player would block the way ahead
            Keys(Key.D);
            yield return new WaitForSeconds(0.3f);
            Keys(Key.D, Key.LeftCtrl);
            yield return null;
            Keys(Key.D);
            yield return Sample("roll", 1.1f, 0.12f, "roll", 0.35f);
            Keys();

            // getting hit: a light hit and a heavy one
            log.AppendLine("== hit taken");
            player.Health.TakeDamage(new DamageInfo(10f, gameObject, default, false));
            yield return Sample("light hit", 1.0f, 0.12f, "hit", 0.2f);
            yield return new WaitForSeconds(0.2f);
            player.Health.TakeDamage(new DamageInfo(30f, gameObject, default, true));
            yield return Sample("heavy hit", 1.4f, 0.15f, "stagger", 0.3f);
            log.AppendLine($"  player health {player.Health.Current:F0}/{player.Health.Max:F0}, stamina {player.Stamina.Current:F0}");

            Finish("done");
        }

        // press exactly these keys (all others released)
        void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));

        // a quick press and release (a Tap for the Attack action): the keys stay down for two frames
        void Tap(Key key)
        {
            Keys(key);
            StartCoroutine(Release());
        }

        IEnumerator Release()
        {
            yield return null;
            yield return null;
            Keys();
        }

        IEnumerator Sample(string label, float seconds, float every, string snapName = null, float snapAt = -1f)
        {
            log.AppendLine("-- " + label);
            bool snapped = false;
            for (float t = 0f; t < seconds; t += every)
            {
                Line($"t={t:F2}");
                float wait = every;
                while (wait > 0f) { wait -= Time.deltaTime; yield return null; }
                if (!snapped && snapName != null && t + every >= snapAt) { snapped = true; Snap(snapName); }
            }
        }

        void Line(string prefix)
        {
            Animator a = player.Animator;
            string current = "?", next = "";
            foreach (string s in StateNames)
            {
                if (a.GetCurrentAnimatorStateInfo(0).IsName(s)) current = s;
                if (a.IsInTransition(0) && a.GetNextAnimatorStateInfo(0).IsName(s)) next = "->" + s;
            }
            Vector3 p = player.transform.position;
            log.AppendLine($"  {prefix}  code={player.CurrentStateName,-22} anim={current}{next}  y={p.y:F2} pos=({p.x:F1},{p.z:F1}) stamina={player.Stamina.Current:F0}");
        }

        void LogDummies()
        {
            var sb = new StringBuilder("  dummies hp:");
            foreach (var d in FindObjectsByType<TrainingDummy>(FindObjectsSortMode.None))
            {
                var h = d.GetComponent<Health>();
                sb.Append($" {d.name.Replace("TrainingDummy_", "#")}={h.Current:F0}");
            }
            log.AppendLine(sb.ToString());
        }

        // the whole screen as the player sees it, HUD included (Temp/combat_<name>.png)
        IEnumerator FullScreen(string name)
        {
            yield return new WaitForEndOfFrame();
            Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(Application.dataPath, "..", "Temp", "combat_" + name + ".png"), tex.EncodeToPNG());
            Destroy(tex);
        }

        // a picture from behind and to the right of the player, looking at the spot in front of it (Temp/combat_<name>.png)
        void Snap(string name)
        {
            var go = new GameObject("SnapCamera");
            var cam = go.AddComponent<Camera>();
            Transform t = player.transform;
            Vector3 focus = t.position + t.forward * 3.2f + Vector3.up * 1.1f;
            go.transform.position = t.position + t.right * 5.5f - t.forward * 2.2f + Vector3.up * 2.6f;
            go.transform.LookAt(focus);
            cam.fieldOfView = 55f;
            var rt = new RenderTexture(960, 640, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(960, 640, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 960, 640), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(Application.dataPath, "..", "Temp", "combat_" + name + ".png"), tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;
            Destroy(rt);
            Destroy(tex);
            Destroy(go);
        }

        void Finish(string message)
        {
            log.AppendLine(message);
            File.WriteAllText(Path.Combine(Application.dataPath, "..", "Temp", "combat_smoke_test.txt"), log.ToString());
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
#if UNITY_EDITOR
            InputSystem.settings.editorInputBehaviorInPlayMode = previousBehavior;
            InputSystem.settings.backgroundBehavior = previousBackground;
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
