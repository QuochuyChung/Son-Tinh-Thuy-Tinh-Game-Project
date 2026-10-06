using System.Collections;
using System.IO;
using System.Text;
using SonTinhThuyTinh.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace SonTinhThuyTinh.DevTools
{
    // Editor smoke test for climbing (docs/progress.md 9.13): in Sandbox_Combat it walks the character up to each ClimbBlock (made by the
    // climb builder), presses Space and records the states, the position and how the hands and feet sit against the block, with side-view
    // pictures Temp/climb_*.png; also Space in the open (must be a plain jump) and Space with the back to the block. Writes
    // <project>/Temp/climb_smoke_test.txt. Add it to an empty object in Play mode.
    public class ClimbSmokeTest : MonoBehaviour
    {
        static readonly string[] StateNames = { "Locomotion", "Climb", "JumpUp", "RunJump", "Dodge" };

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
            previousBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#endif
            foreach (InputDevice stale in InputSystem.devices.ToArray())
                if (stale.name == "ClimbSmokeKeyboard") InputSystem.RemoveDevice(stale);
            keyboard = InputSystem.AddDevice<Keyboard>("ClimbSmokeKeyboard");
            yield return new WaitForSeconds(1.5f);
            var pauseMenu = FindFirstObjectByType<SonTinhThuyTinh.UI.PauseMenu>();
            if (pauseMenu != null) pauseMenu.enabled = false;
            // and a real mouse moved meanwhile would turn the camera, so W / S would no longer mean the same direction
            var cameraInput = FindFirstObjectByType<SonTinhThuyTinh.CameraSystem.ThirdPersonCameraInput>();
            if (cameraInput != null) cameraInput.enabled = false;
            player = FindFirstObjectByType<PlayerController>();
            if (player == null) { Finish("no PlayerController in the scene"); yield break; }
            log.AppendLine($"player: {player.name}  climb enabled={player.Climb.enabled}  scene={gameObject.scene.name}");

            var blocks = GameObject.Find("ClimbBlocks");
            if (blocks == null) { Finish("no ClimbBlocks in the scene (run Tools > Son Tinh Thuy Tinh > Set Up Climb)"); yield break; }

            // each block, walking up to it from the south (the character faces +z)
            foreach (Transform block in blocks.transform)
            {
                float top = block.position.y + block.localScale.y * 0.5f;
                float face = block.position.z - block.localScale.z * 0.5f;
                log.AppendLine($"== {block.name}: top {top:F2} m, south face z={face:F2}");
                Teleport(new Vector3(block.position.x, 0f, face - 1.8f));
                yield return new WaitForSeconds(0.4f);
                Keys(Key.W);
                yield return new WaitForSeconds(0.45f);   // walks up and stands against the face
                Keys(Key.W, Key.Space);
                yield return null;
                yield return null;
                Keys(Key.W);
                yield return Sample("climb", 3.6f, 0.2f, block.name, top, new[] { 0.3f, 0.9f, 1.6f, 2.2f });
                Keys();
                log.AppendLine($"  after: state={player.CurrentStateName} pos=({player.transform.position.x:F2},{player.transform.position.y:F2},{player.transform.position.z:F2}) grounded={player.IsGrounded} (top {top:F2})");

                // and walk on across the top to prove the controller works there
                Keys(Key.W);
                yield return new WaitForSeconds(0.6f);
                Keys();
                log.AppendLine($"  walked on: pos=({player.transform.position.x:F2},{player.transform.position.y:F2},{player.transform.position.z:F2}) grounded={player.IsGrounded}");
            }

            // Space in the open: a plain jump, not a climb
            log.AppendLine("== Space in the open");
            Teleport(new Vector3(0f, 0f, -6f));
            yield return new WaitForSeconds(0.5f);
            Keys(Key.Space);
            yield return null;
            yield return null;
            Keys();
            yield return Sample("open jump", 1.2f, 0.2f);

            // Space with the back to the block: not a climb either
            Transform first = blocks.transform.GetChild(1);
            log.AppendLine("== Space with the back to the block");
            Teleport(new Vector3(first.position.x, 0f, first.position.z - first.localScale.z * 0.5f - 0.45f));
            player.transform.rotation = Quaternion.LookRotation(Vector3.back);
            yield return new WaitForSeconds(0.4f);
            Keys(Key.S);
            yield return new WaitForSeconds(0.2f);
            Keys(Key.S, Key.Space);
            yield return null;
            yield return null;
            Keys();
            yield return Sample("back to block", 1.2f, 0.2f);

            Finish("done");
        }

        void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));

        void Teleport(Vector3 position)
        {
            var body = player.GetComponent<CharacterController>();
            body.enabled = false;
            player.transform.SetPositionAndRotation(position, Quaternion.identity);
            body.enabled = true;
            player.SetSpeed(0f);
        }

        IEnumerator Sample(string label, float seconds, float every, string snapName = null, float ledgeTop = 0f, float[] snapAt = null)
        {
            log.AppendLine("-- " + label);
            int snapIndex = 0;
            for (float t = 0f; t < seconds; t += every)
            {
                Line($"t={t:F1}", ledgeTop);
                float wait = every;
                while (wait > 0f) { wait -= Time.deltaTime; yield return null; }
                if (snapName != null && snapAt != null && snapIndex < snapAt.Length && t + every >= snapAt[snapIndex])
                    Snap($"{snapName}_{snapIndex++}", ledgeTop);
            }
        }

        void Line(string prefix, float ledgeTop)
        {
            Animator a = player.Animator;
            string current = "?", next = "";
            foreach (string s in StateNames)
            {
                if (a.GetCurrentAnimatorStateInfo(0).IsName(s)) current = s;
                if (a.IsInTransition(0) && a.GetNextAnimatorStateInfo(0).IsName(s)) next = "->" + s;
            }
            Vector3 p = player.transform.position;
            string bones = "";
            if (ledgeTop > 0f)
            {
                float foot = Mathf.Min(a.GetBoneTransform(HumanBodyBones.LeftFoot).position.y, a.GetBoneTransform(HumanBodyBones.RightFoot).position.y);
                float hand = Mathf.Max(a.GetBoneTransform(HumanBodyBones.LeftHand).position.y, a.GetBoneTransform(HumanBodyBones.RightHand).position.y);
                bones = $" foot y={foot:F2} hand y={hand:F2} hips y={a.GetBoneTransform(HumanBodyBones.Hips).position.y:F2}";
            }
            log.AppendLine($"  {prefix}  code={player.CurrentStateName,-20} anim={current}{next}  pos=({p.x:F2},{p.y:F2},{p.z:F2}){bones}");
        }

        // a picture from the side (+x, the character faces +z): Temp/climb_<name>.png
        void Snap(string name, float ledgeTop)
        {
            var go = new GameObject("SnapCamera");
            var cam = go.AddComponent<Camera>();
            Vector3 focus = new Vector3(player.transform.position.x, ledgeTop * 0.5f + 0.9f, player.transform.position.z + 0.2f);
            go.transform.position = focus + Vector3.right * 6.5f;
            go.transform.LookAt(focus);
            cam.fieldOfView = 38f;
            var rt = new RenderTexture(800, 600, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(800, 600, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 800, 600), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(Application.dataPath, "..", "Temp", "climb_" + name + ".png"), tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;
            Destroy(rt);
            Destroy(tex);
            Destroy(go);
        }

        void Finish(string message)
        {
            log.AppendLine(message);
            File.WriteAllText(Path.Combine(Application.dataPath, "..", "Temp", "climb_smoke_test.txt"), log.ToString());
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
#if UNITY_EDITOR
            InputSystem.settings.editorInputBehaviorInPlayMode = previousBehavior;
            InputSystem.settings.backgroundBehavior = previousBackground;
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
