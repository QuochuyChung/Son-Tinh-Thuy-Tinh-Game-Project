using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SonTinhThuyTinh.Flow;

namespace SonTinhThuyTinh.EditorTools
{
    // Map_HungVuong's court: the throne with Hùng Vương on the avenue in front of the palace and Mị Nương beside it, exactly where the three
    // cutscenes (Opening, Judgement, Ending) put them (CineSetBuilder's marks, same coordinates). The player only has to walk up to them:
    // the exit trigger to the judgement cutscene stands in front of the dais instead of inside the palace gate.
    // The cutscene builders remove the "Court" group when they copy the map, so the cutscenes build their own cast.
    public static class PalaceCourt
    {
        public const string GroupName = "Court";

        [MenuItem("Tools/Son Tinh Thuy Tinh/New Props/Place Court In Palace Map")]
        public static void Menu() => Debug.Log("PalaceCourt: " + Apply());

        // Opens Map_HungVuong, builds the court, moves the trigger, puts the palace on the ground, saves.
        public static string Apply()
        {
            var scene = EditorSceneManager.OpenScene(CineSetBuilder.MapPath, OpenSceneMode.Single);
            var sb = new System.Text.StringBuilder();
            foreach (var old in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(t => t.name == GroupName).ToList())
                Object.DestroyImmediate(old.gameObject);

            var group = new GameObject(GroupName).transform;
            CineSetBuilder.BuildThrone(group);
            Vector3 avenue = new(0f, 14f, 10f);
            var king = CineSetBuilder.PlacePrefab(group, CineSetBuilder.KingPrefab, null, new Vector3(0f, CineSetBuilder.StepTop(0f, 32.6f), 32.6f),
                Quaternion.LookRotation(Vector3.back), "HungVuong");
            var mi = CineSetBuilder.PlacePrefab(group, CineSetBuilder.MiNuongPrefab, null, new Vector3(-2.9f, 14f, 32.2f),
                Quaternion.LookRotation(new Vector3(0f, 0f, 22f) - new Vector3(-2.9f, 0f, 32.2f)), "MiNuong");
            foreach (var actor in new[] { king, mi })
            {
                var capsule = actor.gameObject.AddComponent<CapsuleCollider>();
                capsule.center = new Vector3(0f, 0.95f, 0f); capsule.height = 1.9f; capsule.radius = 0.35f;
            }

            // the exit: a few steps in front of the dais, so walking up to the court is enough
            var exit = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.name == "Exit_To_Next");
            if (exit != null)
            {
                exit.position = new Vector3(0f, 15.5f, 28.6f);
                var box = exit.GetComponent<BoxCollider>();
                if (box != null) box.size = new Vector3(14f, 7f, 4.5f);
                sb.AppendLine("exit trigger moved in front of the dais");
            }
            else sb.AppendLine("no Exit_To_Next in the map!");

            sb.AppendLine(LowerPalace());
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return sb.ToString();
        }

        // The palace model sat on the plateau (PalaceMapBuilder.Top) while the avenue in front of it was levelled lower: its base is brought to the
        // ground, and the ground under its footprint is levelled to that base so no corner floats or sinks.
        public static string LowerPalace()
        {
            var palace = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.name == "Palace_HungVuong");
            var terrain = Terrain.activeTerrain;
            if (palace == null || terrain == null) return "no palace";
            var rs = palace.GetComponentsInChildren<Renderer>().Where(r => !(r is ParticleSystemRenderer)).ToArray();
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            float baseY = b.min.y;
            // ground under the front part of the footprint (the stairs and the first rooms)
            float sum = 0f; int n = 0, above = 0;
            for (float x = b.min.x + 2f; x <= b.max.x - 2f; x += 3f)
                for (float z = b.min.z + 1f; z <= b.min.z + 14f; z += 3f)
                {
                    float gy = terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;
                    sum += gy; n++;
                    if (gy > baseY) above++;
                }
            float ground = sum / Mathf.Max(n, 1);
            string info = $"palace base {baseY:F2}, ground under its front {ground:F2} ({above}/{n} samples above the base), size {b.size.x:F1} x {b.size.z:F1}";
            float drop = baseY - ground + 0.05f;
            if (drop > 0.02f)
            {
                Undo.RecordObject(palace, "palace");
                palace.position -= new Vector3(0f, drop, 0f);
                info += $"; lowered by {drop:F2}";
            }
            return info;
        }
    }
}
