using System.IO;
using SonTinhThuyTinh.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SonTinhThuyTinh.EditorTools
{
    // Thủy Tinh walks on water (WaterWalker + WaterSurface):
    //  - textures Tex_WaterRipple (a soft ring) and Tex_WaterDrop (a soft dot), drawn here, and their URP Particles/Unlit materials;
    //  - WaterWalker on the Thủy Tinh player prefab (Player_ThuyTinh_v2), with those materials;
    //  - WaterSurface on Map_ThuyTinh's water plane ("Generated/Water"; ThuyTinhMapBuilder adds it too when it rebuilds the map).
    // Menu: Tools > Son Tinh Thuy Tinh > Build Water Walking (Thuy Tinh). Safe to run again.
    public static class WaterWalkBuilder
    {
        const string Dir = "Assets/Art/Combat/";
        const string RippleTex = Dir + "Tex_WaterRipple.png", DropTex = Dir + "Tex_WaterDrop.png";
        const string RippleMat = Dir + "Mat_WaterRipple.mat", DropMat = Dir + "Mat_WaterDrop.mat";
        static readonly string[] Prefabs = { "Assets/Prefabs/Player/Player_ThuyTinh_v2.prefab" };   // the one the game spawns (v1 is unused)
        const string ScenePath = "Assets/Scenes/Map_ThuyTinh.unity";

        [MenuItem("Tools/Son Tinh Thuy Tinh/Build Water Walking (Thuy Tinh)")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("Stop Play mode first."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!AssetDatabase.IsValidFolder("Assets/Art/Combat")) AssetDatabase.CreateFolder("Assets/Art", "Combat");

            WriteTexture(RippleTex, (x, y) =>
            {
                float r = Mathf.Sqrt(x * x + y * y);
                float ring = Mathf.Exp(-Mathf.Pow((r - 0.82f) / 0.07f, 2f));             // the crest
                float inner = 0.35f * Mathf.Exp(-Mathf.Pow((r - 0.62f) / 0.06f, 2f));    // a fainter ring inside it
                return Mathf.Clamp01(ring + inner) * (r < 1f ? 1f : 0f);
            });
            WriteTexture(DropTex, (x, y) => { float d = Mathf.Clamp01(1f - Mathf.Sqrt(x * x + y * y)); return d * d; });
            var ripple = ParticleMaterial(RippleMat, RippleTex);
            var drop = ParticleMaterial(DropMat, DropTex);

            foreach (string path in Prefabs)
            {
                if (!File.Exists(path)) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                var walker = root.GetComponent<WaterWalker>() ?? root.AddComponent<WaterWalker>();
                var so = new SerializedObject(walker);
                so.FindProperty("rippleMaterial").objectReferenceValue = ripple;
                so.FindProperty("sprayMaterial").objectReferenceValue = drop;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
                Debug.Log("WaterWalkBuilder: WaterWalker on " + path);
            }

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var water = GameObject.Find("Generated/Water");
            if (water != null)
            {
                if (water.GetComponent<WaterSurface>() == null) water.AddComponent<WaterSurface>();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("WaterWalkBuilder: WaterSurface on " + ScenePath + " Generated/Water");
            }
            else Debug.LogError("WaterWalkBuilder: no Generated/Water in " + ScenePath);
            AssetDatabase.SaveAssets();
        }

        // A white texture whose alpha is f(x, y), x and y from -1 to 1 across it.
        static void WriteTexture(string path, System.Func<float, float, float> alpha)
        {
            const int n = 256;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                    tex.SetPixel(i, j, new Color(1f, 1f, 1f, alpha(i / (n - 1f) * 2f - 1f, j / (n - 1f) * 2f - 1f)));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }

        // URP Particles/Unlit, transparent alpha blend, no depth write, both sides
        static Material ParticleMaterial(string path, string texture)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); AssetDatabase.CreateAsset(m, path); }
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texture));
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f); m.SetFloat("_Cull", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
