using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SonTinhThuyTinh.EditorTools
{
    // Map_ThuyTinh: the big karst stacks of the lagoon (raised in the terrain by ThuyTinhMapBuilder.KarstTop) are levelled out of the terrain
    // and a tall rock model stands in each one's place (same spot, same height, width of the stack); the lighthouse is replaced by the
    // Sunspire watchtower model (Assets/Art/Props/watchtower.fbx), the beacon light moved to its top. The reef rocks (small stacks) stay.
    // Menu: Tools > Son Tinh Thuy Tinh > New Props > Thuy Tinh: karst rocks + watchtower (open scene). The scene is saved by Apply(path).
    public static class KarstRocks
    {
        const string ScenePath = "Assets/Scenes/Map_ThuyTinh.unity";
        static readonly string[] Prefabs = { "RockKaya", "RockMonolith", "RockTundra", "RockFormation" };
        static readonly float[] Aspect = { 1.55f, 1.45f, 0.93f, 0.62f };   // natural height / width of each model

        [MenuItem("Tools/Son Tinh Thuy Tinh/New Props/Thuy Tinh: karst rocks + watchtower (saves Map_ThuyTinh)")]
        public static void Menu() => Debug.Log("KarstRocks: " + Apply());

        static System.Type Builder => System.AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => { try { return a.GetTypes(); } catch { return new System.Type[0]; } }).First(t => t.Name == "ThuyTinhMapBuilder");
        static T Const<T>(string name) => (T)Builder.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static).GetRawConstantValue();

        public static string Apply()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(ReplaceKarsts());
            sb.AppendLine(ReplaceLighthouse());
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return sb.ToString();
        }

        static string ReplaceKarsts()
        {
            var type = Builder;
            var karstProp = type.GetProperty("Karsts", BindingFlags.NonPublic | BindingFlags.Static);
            var karstField = type.GetField("karsts", BindingFlags.NonPublic | BindingFlags.Static);
            var all = (System.Array)karstProp.GetValue(null);
            var elemType = all.GetType().GetElementType();
            FieldInfo fC = elemType.GetField("C"), fR = elemType.GetField("Radius"), fH = elemType.GetField("Height");
            var big = new List<(Vector2 c, float r, float h)>(); var keep = new List<object>();
            foreach (var k in all)
            {
                var c = (Vector2)fC.GetValue(k); float r = (float)fR.GetValue(k), h = (float)fH.GetValue(k);
                if (h >= 10f && r >= 4f) big.Add((c, r, h)); else keep.Add(k);
            }
            var terrain = Terrain.activeTerrain; var data = terrain.terrainData;
            float xMin = Const<float>("XMin"), zMin = Const<float>("ZMin"), width = data.size.x, length = data.size.z;
            int res = data.heightmapResolution;
            var groundHeight = type.GetMethod("GroundHeight", BindingFlags.NonPublic | BindingFlags.Static);
            // the ground without the big stacks
            var reduced = System.Array.CreateInstance(elemType, keep.Count);
            for (int i = 0; i < keep.Count; i++) reduced.SetValue(keep[i], i);
            karstField.SetValue(null, reduced);
            int changed = 0;
            try
            {
                foreach (var (c, r, h) in big)
                {
                    float reach = r * 1.6f + 2f;
                    int i0 = Mathf.Max(0, Mathf.FloorToInt((c.x - reach - xMin) / width * (res - 1))), i1 = Mathf.Min(res - 1, Mathf.CeilToInt((c.x + reach - xMin) / width * (res - 1)));
                    int j0 = Mathf.Max(0, Mathf.FloorToInt((c.y - reach - zMin) / length * (res - 1))), j1 = Mathf.Min(res - 1, Mathf.CeilToInt((c.y + reach - zMin) / length * (res - 1)));
                    var hs = data.GetHeights(i0, j0, i1 - i0 + 1, j1 - j0 + 1);
                    for (int j = j0; j <= j1; j++)
                        for (int i = i0; i <= i1; i++)
                        {
                            float x = xMin + width * i / (res - 1), z = zMin + length * j / (res - 1);
                            float d = Vector2.Distance(new Vector2(x, z), c);
                            if (d > reach) continue;
                            float flat = Mathf.Clamp01((float)groundHeight.Invoke(null, new object[] { x, z }) / data.size.y);
                            float cur = hs[j - j0, i - i0];
                            if (cur <= flat) continue;
                            hs[j - j0, i - i0] = flat; changed++;
                        }
                    data.SetHeights(i0, j0, hs);
                }
            }
            finally { karstField.SetValue(null, all); }
            EditorUtility.SetDirty(data);
            // the rocks
            var prefabs = Prefabs.Select(n => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Props/" + n + ".prefab")).ToArray();
            var sea = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).FirstOrDefault(t => t.name == "Sea");
            var group = new GameObject("KarstRocks").transform;
            if (sea != null) group.SetParent(sea.parent != null ? sea.parent : sea, false);
            int made = 0; var rng = new System.Random(4242);
            foreach (var (c, r, h) in big)
            {
                float want = h / (2f * r), best = 1e9f; int pick = 0;
                for (int i = 0; i < Aspect.Length; i++) { float d = Mathf.Abs(Aspect[i] - want); if (d < best) { best = d; pick = i; } }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[pick], group); go.name = "KarstRock_" + made;
                var rr = go.GetComponentsInChildren<Renderer>(); var b = rr[0].bounds; foreach (var q in rr) b.Encapsulate(q.bounds);
                float sH = (h + 1.2f) / b.size.y, sW = 2f * r * 1.05f / Mathf.Max(b.size.x, b.size.z);
                float sx = Mathf.Clamp(sW, sH * 0.7f, sH * 1.4f);
                go.transform.localScale = new Vector3(sx, sH, sx);
                go.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                float floor = terrain.SampleHeight(new Vector3(c.x, 0f, c.y)) + terrain.transform.position.y;
                go.transform.position = new Vector3(c.x, floor - 0.3f, c.y);
                made++;
            }
            return $"{big.Count} karst stacks levelled ({changed} height samples), {made} rocks placed";
        }

        static string ReplaceLighthouse()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Props/Watchtower.prefab");
            if (prefab == null) return "no watchtower prefab";
            var tower = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.name == "LighthouseTower");
            if (tower == null) return "no LighthouseTower";
            if (tower.Find("Watchtower") != null) return "lighthouse already replaced";
            var beacon = tower.Find("Beacon");
            foreach (var c in tower.Cast<Transform>().ToList()) if (c != beacon) Object.DestroyImmediate(c.gameObject);
            foreach (var col in tower.GetComponents<Collider>()) Object.DestroyImmediate(col);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, tower);
            model.name = "Watchtower"; model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.identity;
            var rr = model.GetComponentsInChildren<Renderer>(); var b = rr[0].bounds; foreach (var q in rr) b.Encapsulate(q.bounds);
            if (beacon != null) beacon.localPosition = new Vector3(0f, b.size.y * 0.88f, 0f);
            return $"lighthouse replaced by the watchtower ({b.size.x:F1} x {b.size.y:F1} x {b.size.z:F1} m)";
        }
    }
}
