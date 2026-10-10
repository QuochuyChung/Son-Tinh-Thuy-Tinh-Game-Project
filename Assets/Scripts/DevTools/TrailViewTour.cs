using System.Collections;
using System.IO;
using System.Linq;
using SonTinhThuyTinh.Player;
using UnityEngine;

namespace SonTinhThuyTinh.DevTools
{
    // Dev tool: puts the player at a list of places (object names or world positions), waits for the follow camera to settle and saves what
    // the Game camera sees to <project>/Temp/tour_NN_<name>.png, plus the player and camera positions to Temp/tour.txt. Not part of the game.
    public class TrailViewTour : MonoBehaviour
    {
        public string[] anchors = { "Anchor_LedgeStart", "Anchor_CanyonStart", "Anchor_Steps", "Anchor_BeforePit", "Anchor_AfterPit", "Anchor_BeastClearing", "Anchor_Summit", "ElephantPlayerEntry" };

        IEnumerator Start()
        {
            yield return new WaitForSeconds(1.5f);
            var player = FindObjectsByType<PlayerController>(FindObjectsSortMode.None).First(p => p.isActiveAndEnabled);
            var log = new System.Text.StringBuilder();
            int i = 0;
            foreach (string name in anchors)
            {
                var go = GameObject.Find(name);
                if (go == null) { log.AppendLine("missing " + name); continue; }
                Vector3 at = go.transform.position + Vector3.up * 0.3f;
                player.SetBodyEnabled(false);
                player.transform.SetPositionAndRotation(at, go.transform.rotation);
                player.SetBodyEnabled(true);
                yield return new WaitForSeconds(3f);
                var cam = Camera.main;
                log.AppendLine($"{name}: player {player.transform.position:F1} grounded {player.IsGrounded}; camera {cam.transform.position:F1}");
                Snap(cam, $"{i++:00}_{name}");
            }
            File.WriteAllText(Path.Combine(Application.dataPath, "../Temp/tour.txt"), log.ToString());
            Debug.Log("TrailViewTour done");
        }

        static void Snap(Camera cam, string tag)
        {
            var rt = new RenderTexture(1280, 720, 24);
            var old = cam.targetTexture;
            cam.targetTexture = rt; cam.Render(); cam.targetTexture = old;
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(Application.dataPath, $"../Temp/tour_{tag}.png"), tex.EncodeToPNG());
            Destroy(tex); rt.Release();
        }
    }
}
// recompile probe 1791599914
