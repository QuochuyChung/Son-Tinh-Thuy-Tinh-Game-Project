using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SonTinhThuyTinh.EditorTools
{
    // Map_ThuyTinh: the Meshy lotus (assets/sen) replaces every water lily flower of the Idyllic pack, and the Meshy bark log (assets/wood) replaces the
    // cylinders of the raft of floating logs. Safe to run again. Menu: Tools > Son Tinh Thuy Tinh > New Props > Thuy Tinh: lotus and logs.
    public static class LotusAndLogs
    {
        const string ScenePath = "Assets/Scenes/Map_ThuyTinh.unity";

        [MenuItem("Tools/Son Tinh Thuy Tinh/New Props/Thuy Tinh: lotus and logs (saves Map_ThuyTinh)")]
        public static void Menu() => Debug.Log("LotusAndLogs: " + Apply());

        static string Norm(string n)
        {
            var s = new System.Text.StringBuilder();
            foreach (char c in n) if (!char.IsDigit(c)) s.Append(c);
            return s.ToString().Replace("(Clone)", "").Replace("_ ", "_").Trim('_', ' ');
        }

        public static string Apply()
        {
            NewProps.RebuildPrefabs();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var lotus = NewProps.Prefab(NewProps.Kind.Lotus);
            int lilies = 0, logs = 0;
            var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).ToList();
            foreach (var t in all)
            {
                if (t == null || lotus == null) continue;
                if (!Norm(t.name).StartsWith("Waterlily") || t.GetComponentInChildren<Renderer>(true) == null) continue;
                if (t.parent != null && Norm(t.parent.name).StartsWith("Waterlily")) continue;
                float s = Mathf.Clamp(t.lossyScale.x, 0.8f, 1.4f);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(lotus, t.parent);
                go.name = "Lotus";
                go.transform.SetPositionAndRotation(t.position + Vector3.down * 0.1f, t.rotation);
                go.transform.localScale = Vector3.one * s;
                Object.DestroyImmediate(t.gameObject);
                lilies++;
            }
            foreach (var t in all)
            {
                if (t == null || t.name != "Log" || (t.Find("Trunk") == null && t.Find("LogModel") == null)) continue;
                var box = t.GetComponent<BoxCollider>(); if (box == null) continue;
                if (NewProps.DressLog(t, box.size.x, box.size.z)) logs++;
            }
            // the lotus is a dense model (6.6k faces) and there are hundreds: it is culled when it is small on the screen
            var lp = NewProps.Prefab(NewProps.Kind.Lotus);
            if (lp != null && lp.GetComponent<LODGroup>() == null)
            {
                string path = AssetDatabase.GetAssetPath(lp); var contents = PrefabUtility.LoadPrefabContents(path);
                var g = contents.AddComponent<LODGroup>(); var rs = contents.GetComponentsInChildren<Renderer>();
                g.SetLODs(new[] { new LOD(0.025f, rs) }); g.RecalculateBounds();
                PrefabUtility.SaveAsPrefabAsset(contents, path); PrefabUtility.UnloadPrefabContents(contents);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return lilies + " water lily flowers became lotus, " + logs + " logs became bark logs";
        }
    }
}
