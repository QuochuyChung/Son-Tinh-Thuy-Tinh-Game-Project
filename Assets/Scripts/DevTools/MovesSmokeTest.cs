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
    // Editor smoke test for the slide, the two jumps and the knock-down: drives a virtual keyboard through
    // run -> sprint -> running jump -> slide -> standing jump -> knocked down -> get up, and writes what the player did to
    // <project>/Temp/moves_smoke_test.txt. Not part of the game; add it to an empty object in Play mode.
    public class MovesSmokeTest : MonoBehaviour
    {
        static readonly string[] StateNames = { "Locomotion", "Dodge", "Slide", "JumpUp", "RunJump", "Death" };

        readonly StringBuilder log = new();
        Keyboard keyboard;
        PlayerController player;

#if UNITY_EDITOR
        InputSettings.EditorInputBehaviorInPlayMode previousBehavior;
#endif

        IEnumerator Start()
        {
#if UNITY_EDITOR
            // the Game view is not focused while this runs from outside the editor window: let the virtual keyboard through anyway
            previousBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            keyboard = InputSystem.AddDevice<Keyboard>("SmokeTestKeyboard");
            yield return new WaitForSeconds(1.5f);
            player = FindFirstObjectByType<PlayerController>();
            if (player == null) { Finish("no PlayerController in the scene"); yield break; }
            log.AppendLine("player: " + player.name + " in " + gameObject.scene.name);
            Line("start", player);

            Keys(Key.W);
            yield return Sample("walk/run", 1.2f, 0.4f);

            Keys(Key.W, Key.LeftShift);
            yield return Sample("sprint", 1.4f, 0.7f);

            Keys(Key.W, Key.LeftShift, Key.Space);
            yield return null;
            Keys(Key.W, Key.LeftShift);
            yield return Sample("running jump", 1.3f, 0.1f, "runjump", 0.3f);

            yield return Sample("after the jump", 0.6f, 0.3f);

            Keys(Key.W, Key.LeftShift, Key.C);
            yield return null;
            Keys(Key.W, Key.LeftShift);
            Vector3 slideStart = player.transform.position;
            yield return Sample("slide", 1.9f, 0.15f, "slide", 0.6f);
            log.AppendLine($"  slide travelled {Vector3.Distance(slideStart, player.transform.position):F2} m in 1.9 s");

            Keys();
            yield return Sample("stand still", 0.8f, 0.4f);

            Keys(Key.Space);
            yield return null;
            Keys();
            yield return Sample("standing jump", 1.4f, 0.1f, "jumpup", 0.6f);

            player.Health.TakeDamage(new DamageInfo(1000f));
            yield return Sample("knocked down", 4.8f, 0.8f, "death", 3.9f);

            player.Revive();
            yield return Sample("revived", 0.8f, 0.4f);

            Finish("done");
        }

        // press exactly these keys (all others released)
        void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));

        IEnumerator Sample(string label, float seconds, float every, string snapName = null, float snapAt = -1f)
        {
            log.AppendLine("== " + label);
            bool snapped = false;
            for (float t = 0f; t < seconds; t += every)
            {
                Line($"t={t:F2}", player);
                float wait = every;
                while (wait > 0f) { wait -= Time.deltaTime; yield return null; }
                if (!snapped && snapName != null && t + every >= snapAt) { snapped = true; Snap(snapName); }
            }
        }

        // a picture of the player from the side, to check the poses (Temp/smoke_<name>.png)
        void Snap(string name)
        {
            var go = new GameObject("SnapCamera");
            var cam = go.AddComponent<Camera>();
            Vector3 target = player.transform.position + Vector3.up * 0.8f;
            go.transform.position = target + player.transform.right * 4.2f + Vector3.up * 0.4f;
            go.transform.LookAt(target);
            cam.fieldOfView = 38f;
            var rt = new RenderTexture(640, 480, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(640, 480, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 640, 480), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(Application.dataPath, "..", "Temp", "smoke_" + name + ".png"), tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;
            Destroy(rt);
            Destroy(tex);
            Destroy(go);
        }

        void Line(string prefix, PlayerController p)
        {
            Animator a = p.Animator;
            string current = "?", next = "";
            foreach (string s in StateNames)
            {
                if (a.GetCurrentAnimatorStateInfo(0).IsName(s)) current = s;
                if (a.IsInTransition(0) && a.GetNextAnimatorStateInfo(0).IsName(s)) next = "->" + s;
            }
            Vector3 pos = p.transform.position;
            log.AppendLine($"  {prefix}  code={p.CurrentStateName,-22} anim={current}{next}  y={pos.y:F2} pos=({pos.x:F1},{pos.z:F1}) speed={p.CurrentSpeed:F2} grounded={p.IsGrounded} hp={p.Health.Current:F0}");
        }

        void Finish(string message)
        {
            log.AppendLine(message);
            File.WriteAllText(Path.Combine(Application.dataPath, "..", "Temp", "moves_smoke_test.txt"), log.ToString());
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
#if UNITY_EDITOR
            InputSystem.settings.editorInputBehaviorInPlayMode = previousBehavior;   // the setting lives in an asset, so put it back
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
