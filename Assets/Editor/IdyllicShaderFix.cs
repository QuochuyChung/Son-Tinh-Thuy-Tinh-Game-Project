using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace SonTinhThuyTinh.EditorTools
{
    // Makes the shader graphs of the Asset Store pack "Idyllic Fantasy Nature" (written for Unity 2021 / URP 12) compile on Unity 6 / URP 17.
    // The graphs declare URP's own lighting keywords (_MAIN_LIGHT_SHADOWS, _MAIN_LIGHT_SHADOWS_CASCADE) as ShaderFeature keywords while
    // URP declares them itself, which Shader Graph now rejects ("Keyword ... is duplicated in several directives"), so every tree and
    // bush turns pink. Switching those keywords to "Predefined" keeps the graphs working and stops them emitting a second declaration.
    // The pack is not in git, so everybody runs this once after importing it (docs/progress.md 9.6).
    public static class IdyllicShaderFix
    {
        const string Folder = "Assets/Idyllic Fantasy Nature/Shader";
        static readonly string[] UrpKeywords =
        {
            "_MAIN_LIGHT_SHADOWS", "_MAIN_LIGHT_SHADOWS_CASCADE", "_MAIN_LIGHT_SHADOWS_SCREEN",
            "_ADDITIONAL_LIGHTS", "_ADDITIONAL_LIGHT_SHADOWS", "_SHADOWS_SOFT",
        };

        [MenuItem("Tools/Son Tinh Thuy Tinh/Fix Idyllic Fantasy Nature shaders for Unity 6")]
        public static void Fix()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) { Debug.LogWarning("Idyllic Fantasy Nature is not imported (no " + Folder + ")."); return; }

            int changedFiles = 0, changedKeywords = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Shader", new[] { Folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".shadergraph") && !path.EndsWith(".shadersubgraph")) continue;

                string text = File.ReadAllText(path);
                // A graph file is a list of JSON objects separated by blank lines; keywords are one object each.
                string[] blocks = Regex.Split(text, @"(\r?\n\r?\n)");
                int count = 0;
                for (int i = 0; i < blocks.Length; i++)
                {
                    string block = blocks[i];
                    if (!block.Contains("UnityEditor.ShaderGraph.ShaderKeyword")) continue;
                    foreach (string keyword in UrpKeywords)
                    {
                        if (!block.Contains("\"m_Name\": \"" + keyword + "\"")) continue;
                        string patched = Regex.Replace(block, "\"m_KeywordDefinition\": [01]", "\"m_KeywordDefinition\": 2");
                        if (patched != block) { blocks[i] = patched; count++; }
                        break;
                    }
                }
                if (count == 0) continue;

                File.WriteAllText(path, string.Concat(blocks), new UTF8Encoding(false));
                AssetDatabase.ImportAsset(path);
                changedFiles++;
                changedKeywords += count;
            }
            Debug.Log($"Idyllic Fantasy Nature: {changedKeywords} keyword(s) switched to Predefined in {changedFiles} shader graph(s).");
        }
    }
}
