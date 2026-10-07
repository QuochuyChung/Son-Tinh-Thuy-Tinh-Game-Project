using System.Collections;
using System.IO;
using System.Text;
using SonTinhThuyTinh.Player;
using UnityEngine;

namespace SonTinhThuyTinh.DevTools
{
    // Editor smoke test: puts the player in front of a point, walks it straight ahead through the character controller (so stairs, steps and
    // walls count) and logs the position every half second to <project>/Temp/gate_walk.txt. Used to check that the way into the palace gate is
    // open. Not part of the game; add it to an empty object in Play mode.
    public class GateWalkTest : MonoBehaviour
    {
        public Vector3 start = new(0f, 14.5f, 22f);
        public float speed = 4f;
        public float seconds = 14f;

        readonly StringBuilder log = new();

        IEnumerator Start()
        {
            yield return new WaitForSeconds(1.5f);
            var player = FindFirstObjectByType<PlayerController>();
            if (player == null) { Write("no player"); yield break; }

            var body = player.GetComponent<CharacterController>();
            body.enabled = false;
            player.transform.SetPositionAndRotation(start, Quaternion.identity);
            body.enabled = true;
            yield return null;

            string scene = gameObject.scene.name;
            float next = 0f;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                player.Move(Vector3.forward * (speed * Time.deltaTime), Time.deltaTime);
                if (t >= next)
                {
                    Vector3 p = player.transform.position;
                    log.AppendLine($"t={t:F1}  pos=({p.x:F1}, {p.y:F1}, {p.z:F1})  grounded={player.IsGrounded}");
                    Write(log.ToString());
                    next += 0.5f;
                }
                yield return null;
            }
            log.AppendLine("end, still in scene " + scene);
            Write(log.ToString());
        }

        static void Write(string text) => File.WriteAllText(Path.Combine(Application.dataPath, "..", "Temp", "gate_walk.txt"), text);
    }
}
