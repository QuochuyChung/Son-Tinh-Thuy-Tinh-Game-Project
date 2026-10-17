using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SonTinhThuyTinh.EditorTools
{
    // Gives the solid decor of the maps a collider so the player, the NPCs and the enemies cannot walk through it: torches, tents,
    // campfire logs and stones, crystals, boats, the lighthouse, the fish racks, harbour posts, barricade stakes and rails, the
    // bronze drums, statues... Trees, rocks, houses, banners and pillars already have theirs; grass, flowers, bushes and corals stay
    // walk-through on purpose, and so do the floors you walk on (pier boards, plank walks). Each prop gets a box collider fitted to
    // its own mesh. Safe to run again: a prop that already has a collider is left alone.
    public static class PropColliderFixer
    {
        static readonly string[] SolidNames =
        {
            "Torch", "Tent", "Log", "Crystal", "Mound", "Stone", "Post", "Pillar", "Stake", "Rail", "BronzeDrum", "Statue",
            "Cabin", "Side", "Gunwale", "Bow", "Stern", "Mast", "Keel", "Base", "Gallery", "Tower", "Bar", "Barrel", "Crate",
            "TroughBox", "TroughHay", "Anvil", "Brazier", "Lantern", "Pot", "Jar", "Totem", "Cliff", "Obelisk",
        };

        // never get a collider: you walk on them, or they are far backdrops / effects
        static readonly string[] WalkOn = { "Board", "Plank", "StubPlanks", "Floor", "Deck", "Waterfall", "Mountain", "Water", "Sea", "Sail", "Net", "Cloth", "Fish" };

        [MenuItem("Tools/Son Tinh Thuy Tinh/Fix Prop Colliders (open scene)")]
        public static void FixOpenScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            string report = Fix();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("PropColliderFixer " + scene.name + ": " + report);
        }

        // Opens the scene, fixes it, saves it and returns a short report (used from the command line).
        public static string FixScene(string path)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            string report = Fix();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return scene.name + ": " + report;
        }

        static string Fix()
        {
            var added = new SortedDictionary<string, int>();
            int total = 0;
            var done = new HashSet<Transform>();
            foreach (var mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (mr.GetComponentInParent<Canvas>() != null) continue;
                var filter = mr.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                var unit = UnitRoot(mr.transform);
                if (done.Contains(unit)) continue;
                string name = Norm(unit.name), container = unit.parent != null ? unit.parent.name : "";
                if (!IsSolid(name, container)) continue;
                done.Add(unit);
                if (unit.GetComponentsInChildren<Collider>(true).Any(c => !(c is TerrainCollider))) continue;
                if (unit.GetComponentInParent<LODGroup>() != null) continue;
                foreach (var part in unit.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var f = part.GetComponent<MeshFilter>();
                    if (f == null || f.sharedMesh == null || part.GetComponent<Collider>() != null) continue;
                    if (WalkOn.Any(w => Norm(part.name).Contains(w))) continue;
                    Undo.AddComponent<BoxCollider>(part.gameObject);
                    total++;
                    string key = container + "/" + name;
                    added[key] = added.TryGetValue(key, out int n) ? n + 1 : 1;
                }
            }
            return total + " colliders added" + (total > 0 ? " (" + string.Join(", ", added.Select(kv => kv.Key + " x" + kv.Value)) + ")" : "");
        }

        static bool IsSolid(string name, string container)
        {
            if (WalkOn.Any(w => name == w || name.StartsWith(w))) return false;
            bool inBoat = container.StartsWith("Boat");
            if (inBoat && name == "Floor") return true;
            return SolidNames.Any(s => name == s || name.StartsWith(s));
        }

        // the topmost transform of one prop: climb until the parent is a container (more than 6 children) or a scene root
        static Transform UnitRoot(Transform t)
        {
            var u = t;
            while (u.parent != null && u.parent.parent != null && u.parent.childCount <= 6 && u.parent.childCount != 0) u = u.parent;
            return u;
        }

        static string Norm(string n)
        {
            var s = new System.Text.StringBuilder();
            foreach (char c in n) if (!char.IsDigit(c)) s.Append(c);
            return s.ToString().Replace("(Clone)", "").Replace("_ ", "_").Trim('_', ' ');
        }
    }
}
