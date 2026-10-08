using UnityEditor;
using UnityEngine;

namespace SonTinhThuyTinh.EditorTools
{
    // The Asset Store pack "Fantasy Forest Environment - Free Demo" (TriForge, v2.0, 2019) only ships Built-in materials (Standard,
    // Legacy Diffuse and its own no-culling Standard copy), which draw pink in URP. This switches them to URP/Lit with the same textures:
    // leaves and grass cards are alpha-clipped and double-sided. The pack is not in git (.gitignore), so run this once after importing it.
    // It also moves the terrain layers Unity's terrain upgrade wrote to Assets/_TerrainAutoUpgrade into the pack's folder.
    public static class FantasyForestUrpFix
    {
        const string Folder = "Assets/Fantasy Forest Environment Free Sample";

        [MenuItem("Tools/Son Tinh Thuy Tinh/Fix Fantasy Forest (v2 demo) materials for URP")]
        public static void Fix()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) { Debug.LogWarning("Fantasy Forest Environment Free Sample is not imported (no " + Folder + ")."); return; }
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            int changed = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { Folder }))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                string shader = m.shader.name;
                if (shader.StartsWith("Universal Render Pipeline") || shader.StartsWith("Skybox")) continue;
                bool cutout = shader.Contains("NoCulling");
                Texture tex = m.mainTexture;
                Color color = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
                Texture normal = m.HasProperty("_BumpMap") ? m.GetTexture("_BumpMap") : null;
                float cutoff = m.HasProperty("_Cutoff") ? m.GetFloat("_Cutoff") : 0.5f;

                m.shader = lit;
                m.SetTexture("_BaseMap", tex);
                m.SetColor("_BaseColor", color);
                m.SetFloat("_Smoothness", 0.1f);
                if (normal != null) { m.SetTexture("_BumpMap", normal); m.EnableKeyword("_NORMALMAP"); }
                m.SetFloat("_AlphaClip", cutout ? 1f : 0f);
                m.SetFloat("_Cutoff", cutoff);
                m.SetFloat("_Cull", cutout ? 0f : 2f);
                if (cutout) { m.EnableKeyword("_ALPHATEST_ON"); m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest; }
                else { m.DisableKeyword("_ALPHATEST_ON"); m.renderQueue = -1; }
                EditorUtility.SetDirty(m);
                changed++;
            }

            const string upgrade = "Assets/_TerrainAutoUpgrade";
            if (AssetDatabase.IsValidFolder(upgrade))
            {
                string target = Folder + "/TerrainLayers";
                if (!AssetDatabase.IsValidFolder(target)) AssetDatabase.CreateFolder(Folder, "TerrainLayers");
                foreach (string guid in AssetDatabase.FindAssets("", new[] { upgrade }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    AssetDatabase.MoveAsset(path, target + "/" + System.IO.Path.GetFileName(path));
                }
                AssetDatabase.DeleteAsset(upgrade);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"Fantasy Forest (v2 demo): {changed} material(s) switched to URP/Lit.");
        }
    }
}
