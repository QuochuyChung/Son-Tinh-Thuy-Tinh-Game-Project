using UnityEditor;
using UnityEngine;

namespace SonTinhThuyTinh.EditorTools
{
    // Prepares the Meshy palace model (ArtSource/Meshy/Palace_HungVuong, copied to Assets/Art/Palace): texture import settings, a URP Lit
    // material from its base colour + normal maps, and a prefab with that material. PalaceMapBuilder puts the prefab on the plateau
    // in place of the placeholder; without the model it falls back to the placeholder.
    public static class PalaceModelSetup
    {
        const string Dir = "Assets/Art/Palace/";
        public const string ModelPath = Dir + "hungvuong_palace.fbx";
        public const string PrefabPath = Dir + "Palace_HungVuong.prefab";
        const string MaterialPath = Dir + "M_HungVuongPalace.mat";

        // The prefab (built on first use); null when the model file is not in the project.
        public static GameObject EnsurePrefab()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null) return null;
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null) return existing;   // delete the prefab to rebuild it (after changing the material settings below)

            SetTexture(Dir + "hungvuong_palace_basecolor.png", false);
            SetTexture(Dir + "hungvuong_palace_normal.png", true);

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "hungvuong_palace_basecolor.png"));
            material.SetColor("_BaseColor", Color.white);
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "hungvuong_palace_normal.png");
            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.SetFloat("_BumpScale", 1f);
                material.EnableKeyword("_NORMALMAP");
            }
            material.SetFloat("_Metallic", 0.05f);
            material.SetFloat("_Smoothness", 0.28f);
            material.SetFloat("_Cull", 0f);   // thin roofs and banners are single sheets
            EditorUtility.SetDirty(material);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            foreach (Renderer r in instance.GetComponentsInChildren<Renderer>())
            {
                var shared = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < shared.Length; i++) shared[i] = material;
                r.sharedMaterials = shared;
            }
            instance.name = "Palace_HungVuong";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
            Object.DestroyImmediate(instance);
            AssetDatabase.SaveAssets();
            return prefab;
        }

        static void SetTexture(string path, bool normalMap)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            bool wanted = normalMap ? importer.textureType == TextureImporterType.NormalMap : importer.textureType == TextureImporterType.Default;
            if (wanted && importer.maxTextureSize <= 2048) return;
            importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.maxTextureSize = 2048;
            importer.sRGBTexture = !normalMap;
            importer.SaveAndReimport();
        }
    }
}
