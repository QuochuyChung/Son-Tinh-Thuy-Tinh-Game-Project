using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace SonTinhThuyTinh.EditorTools
{
    // The Meshy tree (ArtSource/Meshy/Tree_Meshy, made by the user) planted all over the plateau of Map_HungVuong. Two meshes from the same
    // model: tree_meshy_full.fbx (273 000 faces, every root and branch: what the player sees near) and tree_meshy.fbx (80 000 faces, Blender
    // decimate, tools/prep_prop.py logic: what it falls back to at a distance), one LODGroup, one URP Lit material with the Meshy colour map.
    // Only the "MeshyTrees" object of the open scene is touched; running it again replaces the trees (same seed, same places).
    public static class HungVuongMeshyTrees
    {
        const string Dir = "Assets/Art/Props/";
        const string PrefabPath = "Assets/Prefabs/Props/MeshyTree.prefab";
        const string MaterialPath = "Assets/Art/Props/M_tree_meshy.mat";
        const float TreeHeight = 10.5f;

        [MenuItem("Tools/Son Tinh Thuy Tinh/Plant Meshy Trees In Map_HungVuong (open scene)")]
        public static void Plant() => Debug.Log(PlantTrees(46));

        public static string PlantTrees(int count)
        {
            GameObject prefab = EnsurePrefab();
            if (prefab == null) return "tree models are not in the project";
            Scene();
            var scene = EditorSceneManager.GetActiveScene();
            var terrain = Terrain.activeTerrain;
            if (terrain == null) return "no terrain in " + scene.name;

            GameObject old = GameObject.Find("MeshyTrees");
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject("MeshyTrees").transform;
            Physics.SyncTransforms();

            // places that must stay free: the roads and plazas (their objects), the court, the spawn road
            var keepOut = new List<(Vector2 centre, float radius)>
            {
                (new Vector2(0f, 0f), 25f),          // the plaza
                (new Vector2(0f, 38f), 30f),         // palace front and court
                (new Vector2(0f, 62f), 34f),         // the palace itself
            };
            foreach (string group in new[] { "Roadside", "Plaza", "PalaceFront", "Entries", "Spawns" })
            {
                Transform g = GameObject.Find("Generated")?.transform.Find(group);
                if (g == null) continue;
                foreach (Transform t in g) keepOut.Add((new Vector2(t.position.x, t.position.z), group == "Roadside" ? 5.5f : 8f));
            }
            var spawner = Object.FindFirstObjectByType<SonTinhThuyTinh.Player.PlayerSpawner>();
            Vector2 spawn = spawner != null ? new Vector2(spawner.transform.position.x, spawner.transform.position.z) : new Vector2(-57f, -1f);
            for (float k = 0f; k <= 1f; k += 0.04f) keepOut.Add((Vector2.Lerp(spawn, Vector2.zero, k), 7f));   // the road from the spawn to the plaza

            var existing = new List<Vector2>();
            Transform oldTrees = GameObject.Find("Generated")?.transform.Find("Trees");
            if (oldTrees != null) foreach (Transform t in oldTrees) existing.Add(new Vector2(t.position.x, t.position.z));

            var rng = new System.Random(20261017);
            var placed = new List<Vector2>();
            float size = terrain.terrainData.size.x;
            Vector3 origin = terrain.transform.position;
            int tries = 0, made = 0;
            while (made < count && tries++ < 60000)
            {
                float x = origin.x + 8f + (float)rng.NextDouble() * (size - 16f), z = origin.z + 8f + (float)rng.NextDouble() * (terrain.terrainData.size.z - 16f);
                var p = new Vector2(x, z);
                if (keepOut.Any(k => (k.centre - p).sqrMagnitude < k.radius * k.radius)) continue;
                if (existing.Any(e => (e - p).sqrMagnitude < 4.5f * 4.5f) || placed.Any(e => (e - p).sqrMagnitude < 9f * 9f)) continue;
                float y = terrain.SampleHeight(new Vector3(x, 0f, z)) + origin.y;
                if (y < 12.8f || y > 16.5f) continue;                                   // the plateau only (not the shore, not the cliffs)
                Vector3 n = terrain.terrainData.GetInterpolatedNormal((x - origin.x) / size, (z - origin.z) / terrain.terrainData.size.z);
                if (n.y < 0.96f) continue;                                              // flat ground
                bool blocked = false;
                foreach (Collider c in Physics.OverlapSphere(new Vector3(x, y + 1.5f, z), 3.2f, ~0, QueryTriggerInteraction.Ignore))
                    if (!(c is TerrainCollider)) { blocked = true; break; }
                if (blocked) continue;

                var tree = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
                tree.name = "MeshyTree";
                float scale = 0.85f + (float)rng.NextDouble() * 0.4f;
                tree.transform.SetPositionAndRotation(new Vector3(x, y - 0.12f * scale, z), Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));
                tree.transform.localScale = Vector3.one * scale;
                placed.Add(p); made++;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            return made + " Meshy trees planted in " + scene.name + " (" + tries + " tries)";
        }

        static void Scene()
        {
            if (!EditorSceneManager.GetActiveScene().name.Contains("HungVuong"))
                Debug.LogWarning("[MeshyTrees] the open scene is " + EditorSceneManager.GetActiveScene().name + ", not Map_HungVuong");
        }

        // ---------------------------------------------------------------- the prefab

        public static GameObject EnsurePrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null) return existing;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Dir + "tree_meshy_full.fbx") == null) return null;

            ConfigureModel(Dir + "tree_meshy_full.fbx");
            ConfigureModel(Dir + "tree_meshy.fbx");
            Material material = EnsureMaterial();

            var root = new GameObject("MeshyTree");
            Transform lod0 = Part(root.transform, "LOD0", Dir + "tree_meshy_full.fbx", material);
            Transform lod1 = Part(root.transform, "LOD1", Dir + "tree_meshy.fbx", material);

            // the tree on the ground: base at the origin, centred, TreeHeight tall
            Bounds b = Bounds(lod0);
            float scale = TreeHeight / b.size.y;
            foreach (Transform t in new[] { lod0, lod1 }) t.localScale *= scale;   // (the FBX root carries its own 100x unit scale: multiply, do not replace)
            b = Bounds(lod0);
            Vector3 shift = -new Vector3(b.center.x, b.min.y, b.center.z);
            foreach (Transform t in new[] { lod0, lod1 }) t.localPosition += shift;
            b = Bounds(lod0);

            var col = root.AddComponent<CapsuleCollider>();   // the trunk
            col.radius = b.size.x * 0.045f; col.height = b.size.y * 0.45f; col.center = new Vector3(0f, col.height * 0.5f, 0f);

            var lods = root.AddComponent<LODGroup>();
            lods.SetLODs(new[]
            {
                new LOD(0.10f, lod0.GetComponentsInChildren<Renderer>()),
                new LOD(0.015f, lod1.GetComponentsInChildren<Renderer>()),
            });
            lods.fadeMode = LODFadeMode.None;
            lods.RecalculateBounds();

            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Props")) AssetDatabase.CreateFolder("Assets/Prefabs", "Props");
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            Debug.Log("[MeshyTrees] MeshyTree.prefab made: " + b.size.y.ToString("F1") + " m tall, scale " + scale.ToString("F2"));
            return saved;
        }

        static Transform Part(Transform parent, string name, string fbxPath, Material material)
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath), parent);
            model.name = name;
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterial = material;
                r.shadowCastingMode = ShadowCastingMode.On;
                r.receiveShadows = true;
            }
            return model.transform;
        }

        static Bounds Bounds(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>();
            Bounds b = rs[0].bounds;
            foreach (Renderer r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        static void ConfigureModel(string path)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            if (importer == null) return;
            bool changed = false;
            if (importer.materialImportMode != ModelImporterMaterialImportMode.None) { importer.materialImportMode = ModelImporterMaterialImportMode.None; changed = true; }
            if (importer.meshCompression != ModelImporterMeshCompression.Off) { importer.meshCompression = ModelImporterMeshCompression.Off; changed = true; }
            if (importer.indexFormat != ModelImporterIndexFormat.UInt32) { importer.indexFormat = ModelImporterIndexFormat.UInt32; changed = true; }
            if (importer.isReadable) { importer.isReadable = false; changed = true; }
            if (changed) importer.SaveAndReimport();
        }

        static Material EnsureMaterial()
        {
            string texPath = Dir + "tree_meshy_basecolor.png";
            var ti = (TextureImporter)AssetImporter.GetAtPath(texPath);
            if (ti != null && (ti.maxTextureSize < 4096 || ti.anisoLevel < 8 || ti.textureCompression != TextureImporterCompression.CompressedHQ))
            {
                ti.maxTextureSize = 4096; ti.anisoLevel = 8; ti.mipmapEnabled = true; ti.sRGBTexture = true;
                ti.textureCompression = TextureImporterCompression.CompressedHQ; ti.compressionQuality = 100;
                ti.SaveAndReimport();
            }
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_tree_meshy" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0.1f);
            material.SetFloat("_Cull", 0f);                    // both sides: the leaf cards and thin branches are seen from behind too
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
