using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SonTinhThuyTinh.EditorTools
{
    // The "Cong-cau-thuy-tinh" models: the Seashell Tiki Gateway replaces the pier gate of Map_ThuyTinh, the Wooden Fence replaces the nets / chains that
    // block the way there (HarbourChain, SharkNet; their invisible collider stays, so they still open when the fight is won), and the Emerald Grass
    // Tuft replaces every grass prop of every map. Safe to run again.
    // Menu: Tools > Son Tinh Thuy Tinh > New Props > Gate, Fence And Grass (all maps).
    public static class GateFenceGrass
    {
        static readonly string[] Scenes =
        {
            "Assets/Scenes/Map_SonTinh.unity", "Assets/Scenes/Map_ThuyTinh.unity", "Assets/Scenes/Map_HungVuong.unity", "Assets/Scenes/Map_FinalBattle.unity",
        };

        [MenuItem("Tools/Son Tinh Thuy Tinh/New Props/Gate, Fence And Grass (all maps)")]
        public static void Menu() => Debug.Log("GateFenceGrass:\n" + Run());

        public static string Run()
        {
            NewProps.RebuildPrefabs();
            return string.Join("\n", Scenes.Select(Apply));
        }

        public static string Apply(string path)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var sb = new System.Text.StringBuilder(scene.name + ": ");
            if (scene.name == "Map_ThuyTinh") { sb.Append(DressGate() + "; "); sb.Append(DressBarriers() + "; "); sb.Append(TrimHarbourOverlap() + "; "); }
            if (scene.name == "Map_HungVuong") sb.Append(DressEntryGates() + "; ");
            sb.Append(SwapGrass());
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return sb.ToString();
        }

        static string Norm(string n)
        {
            var s = new System.Text.StringBuilder();
            foreach (char c in n) if (!char.IsDigit(c)) s.Append(c);
            return s.ToString().Replace("(Clone)", "").Replace("_ ", "_").Trim('_', ' ');
        }

        // ---------------------------------------------------------------- the pier gate
        // the lowest beam of the model is at 52% of its height: stretched so a player on the pier (deck 1.45 m above the foot) walks under it with 3+ m to spare
        const float GateStretch = 1.45f;

        public static string DressGate()
        {
            var prefab = NewProps.Prefab(NewProps.Kind.TikiGate);
            var groups = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(t => t.name == "Gate" && t.parent != null && t.parent.name == "Gate").ToList();
            int n = 0;
            foreach (var done in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(t => t.name == "TikiGate")) done.localScale = new Vector3(1f, GateStretch, 1f);
            foreach (var old in groups)
            {
                if (old.Find("Pillar") == null) continue;
                var parent = old.parent; Vector3 pos = old.position; Quaternion rot = old.rotation;
                Object.DestroyImmediate(old.gameObject);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                go.name = "TikiGate"; go.transform.SetPositionAndRotation(pos, rot); go.transform.localScale = new Vector3(1f, GateStretch, 1f);
                n++;
            }
            return n + " pier gate(s) replaced by the tiki gateway";
        }

        // Map_HungVuong: the two red/teal entry gates (MapDecor.Gate under "Entries") become tiki gateways of the same footprint.
        public static string DressEntryGates()
        {
            var prefab = NewProps.Prefab(NewProps.Kind.TikiGate);
            int n = 0;
            foreach (var old in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(t => t.name == "Gate" && t.parent != null && t.parent.name == "Entries" && t.Find("Pillar") != null).ToList())
            {
                var parent = old.parent; Vector3 pos = old.position; Quaternion rot = old.rotation;
                Object.DestroyImmediate(old.gameObject);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                go.name = "TikiGate"; go.transform.SetPositionAndRotation(pos, rot); go.transform.localScale = new Vector3(1f, GateStretch, 1f);
                n++;
            }
            return n + " entry gate(s) replaced by the tiki gateway";
        }

        // The harbour deck is a 13 m square; the piers that start or end at its centre laid bridge copies over it (flickering boards): the
        // copies whose middle is on the deck go, so each bridge stops at the deck's edge.
        public static string TrimHarbourOverlap()
        {
            var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var deck = all.FirstOrDefault(t => t.name == "Deck" && t.parent != null && t.parent.name == "Harbour");
            if (deck == null) return "no harbour deck";
            Vector3 c = deck.position; var box = deck.GetComponent<BoxCollider>(); float half = (box != null ? box.size.x : 13f) * 0.5f;
            int n = 0;
            foreach (var seg in all.Where(t => t.name == "BridgeSegment" && t.parent != null && t.parent.name == "BridgeModel").ToList())
            {
                var rs = seg.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                if (Mathf.Abs(b.center.x - c.x) < half - 0.3f && Mathf.Abs(b.center.z - c.z) < half - 0.3f) { Object.DestroyImmediate(seg.gameObject); n++; }
            }
            return n + " bridge copies on the harbour deck removed";
        }

        // ---------------------------------------------------------------- barriers
        public static string DressBarriers()
        {
            var prefab = NewProps.Prefab(NewProps.Kind.Fence);
            int n = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).ToList())
            {
                if (t == null || (t.name != "HarbourChain" && t.name != "SharkNet")) continue;
                var box = t.GetComponent<BoxCollider>();
                if (box == null) continue;
                foreach (var c in t.Cast<Transform>().ToList()) Object.DestroyImmediate(c.gameObject);
                float width = box.size.x, height = box.size.y - 1.5f;
                int pieces = Mathf.Max(1, Mathf.CeilToInt(width / 4f)); float seg = width / pieces;
                float h = Mathf.Min(height * 1.05f, 5f), sy = h / 1.94f;
                for (int i = 0; i < pieces; i++)
                {
                    var f = (GameObject)PrefabUtility.InstantiatePrefab(prefab, t);
                    f.name = "Fence_" + i;
                    f.transform.localPosition = new Vector3(-width * 0.5f + seg * (i + 0.5f), 0f, 0f);
                    f.transform.localRotation = Quaternion.identity;
                    f.transform.localScale = new Vector3(seg / 4f, sy, Mathf.Min(sy, 1.3f));
                }
                n++;
            }
            return n + " barrier(s) now wooden fences";
        }

        // ---------------------------------------------------------------- grass
        public static string SwapGrass()
        {
            var prefab = NewProps.Prefab(NewProps.Kind.GrassTuft);
            var rng = new System.Random(77);
            int n = 0, details = 0;
            var victims = new List<Transform>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t == null || t.name.StartsWith("GrassTuft")) continue;
                string name = Norm(t.name);
                bool isGrass = name.Contains("Grass");
                if (!isGrass)
                {
                    var src = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                    isGrass = PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject) && src != null && src.name.Contains("Grass");
                }
                if (!isGrass) continue;
                if (t.GetComponentInChildren<Renderer>(true) == null) continue;
                if (t.parent != null && Norm(t.parent.name).Contains("Grass")) continue;   // a part of a grass prop
                victims.Add(t);
            }
            foreach (var t in victims)
            {
                if (t == null) continue;
                var rs = t.GetComponentsInChildren<Renderer>(true); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                float width = Mathf.Max(b.size.x, b.size.z);
                float s = Mathf.Clamp(width / 1.75f, 0.55f, 1.5f) * (0.9f + 0.2f * (float)rng.NextDouble());
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, t.parent);
                go.name = "GrassTuft";
                go.transform.SetPositionAndRotation(t.position, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));
                go.transform.localScale = Vector3.one * s;
                Object.DestroyImmediate(t.gameObject);
                n++;
            }
            foreach (var terrain in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                details += terrain.terrainData != null ? terrain.terrainData.detailPrototypes.Length : 0;
            return n + " grass props swapped (" + details + " terrain detail layers found)";
        }
    }
}
