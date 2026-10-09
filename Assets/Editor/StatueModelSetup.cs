using System.IO;
using UnityEditor;
using UnityEngine;

namespace SonTinhThuyTinh.EditorTools
{
    // Prepares the 2 giant statues (Sơn Tinh - Stone Sovereign & Thủy Tinh - Feathered Sovereign):
    // imports texture settings, creates URP Lit materials with stone-carved properties,
    // and saves prefabs ready to be placed on the cliff walls flanking the arena.
    public static class StatueModelSetup
    {
        const string BaseDir = "Assets/Art/Statues/";
        const string StoneDir = BaseDir + "StoneSovereign/";
        const string FeatheredDir = BaseDir + "FeatheredSovereign/";

        public const string StoneModelPath = StoneDir + "stone_sovereign_mesh.fbx";
        public const string StonePrefabPath = StoneDir + "Statue_StoneSovereign.prefab";
        public const string StoneMatPath = StoneDir + "M_Statue_StoneSovereign.mat";

        public const string FeatheredModelPath = FeatheredDir + "feathered_sovereign_mesh.fbx";
        public const string FeatheredPrefabPath = FeatheredDir + "Statue_FeatheredSovereign.prefab";
        public const string FeatheredMatPath = FeatheredDir + "M_Statue_FeatheredSovereign.mat";

        public static (GameObject stone, GameObject feathered) EnsurePrefabs()
        {
            var stone = EnsureStatuePrefab(
                StoneModelPath,
                StonePrefabPath,
                StoneMatPath,
                StoneDir + "stone_sovereign_basecolor.png",
                StoneDir + "stone_sovereign_normal.png",
                "Statue_StoneSovereign",
                new Color(0.85f, 0.85f, 0.82f) // slightly warm weathered stone tint
            );

            var feathered = EnsureStatuePrefab(
                FeatheredModelPath,
                FeatheredPrefabPath,
                FeatheredMatPath,
                FeatheredDir + "feathered_sovereign_basecolor.png",
                FeatheredDir + "feathered_sovereign_normal.png",
                "Statue_FeatheredSovereign",
                new Color(0.80f, 0.84f, 0.88f) // slightly cool weathered sea stone tint
            );

            return (stone, feathered);
        }

        static GameObject EnsureStatuePrefab(
            string modelPath,
            string prefabPath,
            string matPath,
            string baseColorTexPath,
            string normalTexPath,
            string prefabName,
            Color tint)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null)
            {
                Debug.LogWarning($"Statue model not found at {modelPath}");
                return null;
            }

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existing != null) return existing;

            SetTexture(baseColorTexPath, false);
            SetTexture(normalTexPath, true);

            var material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, matPath);
            }

            var baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(baseColorTexPath);
            material.SetTexture("_BaseMap", baseTex);
            material.SetColor("_BaseColor", tint);

            var normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(normalTexPath);
            if (normalTex != null)
            {
                material.SetTexture("_BumpMap", normalTex);
                material.SetFloat("_BumpScale", 1.2f);
                material.EnableKeyword("_NORMALMAP");
            }

            material.SetFloat("_Metallic", 0.04f);
            material.SetFloat("_Smoothness", 0.18f); // rugged weathered stone
            material.SetFloat("_Cull", 0f); // double-sided in case of thin meshes
            EditorUtility.SetDirty(material);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name = prefabName;

            foreach (Renderer r in instance.GetComponentsInChildren<Renderer>())
            {
                var shared = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < shared.Length; i++) shared[i] = material;
                r.sharedMaterials = shared;

                // Add mesh colliders so characters cannot clip inside the feet/statue if they climb up
                var mf = r.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null && r.GetComponent<Collider>() == null)
                {
                    var col = r.gameObject.AddComponent<MeshCollider>();
                    col.sharedMesh = mf.sharedMesh;
                }
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
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
