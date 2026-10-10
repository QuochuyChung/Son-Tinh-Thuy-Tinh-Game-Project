using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace SonTinhThuyTinh.EditorTools
{
    // The Meshy village models in Assets/Art/Village (the textured Meshy FBX with its embedded textures stripped by
    // tools/strip_material.py; the Meshy downloads are kept out of git): two villagers' houses, the horse stable and the heirloom bell.
    //  - Import: no FBX materials, one URP Lit material per model (base colour + normal + metallic/smoothness packed by
    //    tools/pack_metallic_smoothness.py), double-sided for the thin thatch and the bell's tassel.
    //  - Prefabs in Assets/Prefabs/Village: an empty root whose origin is the bottom centre of the model, +Z = the model's front (the stairs
    //    of the houses, the long fenced side of the stable), scaled to metres. Houses and stable carry a MeshCollider (static).
    //  - MapDecor.StiltHouse builds the houses from these prefabs (alternating the two kinds) and HorseQuestBuilder the stable and the bell,
    //    so rebuilding the maps keeps them. Without the models both fall back to the old block shapes.
    // Menu Tools > Son Tinh Thuy Tinh > Village > Setup Village Models: swaps the houses already in Map_SonTinh and Map_ThuyTinh in place
    // (no map rebuild), reruns Build Horse Quest (stable + bell), and clears the plants that the bigger footprints now cover.
    public static class VillageModelSetup
    {
        const string Dir = "Assets/Art/Village/";
        const string PrefabDir = "Assets/Prefabs/Village";

        public enum Kind { HouseA, HouseB, Stable, Bell }

        // file stem, prefab name, yaw that turns the model's front to +Z, the size it is scaled to (metres) and what that size measures
        struct Spec
        {
            public string file, prefab; public float yaw, size; public bool byHeight, collider;
        }

        // Bamboo Haven: long and narrow, stairs on a gable end; Hearth Lodge: almost square, a ladder on a gable end; the stable: long, the
        // side with the gate to the front; the bell: a hand bell with a wooden handle and a red tassel (~0.6 m so it reads as a quest item).
        static readonly Dictionary<Kind, Spec> Specs = new()
        {
            [Kind.HouseA] = new Spec { file = "nha_dan_a", prefab = "NhaDan_BambooHaven", yaw = 0f, size = 18f, collider = true },
            [Kind.HouseB] = new Spec { file = "nha_dan_b", prefab = "NhaDan_HearthLodge", yaw = 0f, size = 16f, collider = true },
            [Kind.Stable] = new Spec { file = "chuong_ngua", prefab = "ChuongNgua", yaw = 0f, size = 4.6f, byHeight = true, collider = true },
            [Kind.Bell] = new Spec { file = "chuong_co", prefab = "ChuongCoGiaTruyen", yaw = 0f, size = 0.6f, byHeight = true },
        };

        // where the old block house had its ladder foot (MapDecor.StiltHouse, local -Z): the model's stairs end there, the house grows back
        public const float HouseFrontZ = -3.3f;
        const float IsletBridgeZ = 8.1f;   // just short of where the islet's bridge lands

        static readonly string[] MapScenes = { "Assets/Scenes/Map_SonTinh.unity", "Assets/Scenes/Map_ThuyTinh.unity" };

        // Which house stands where (x, z of the StiltHouse root as the map builders place it), chosen for the room around each spot: the
        // narrow Bamboo Haven where the ground slopes or the stable is close, the wide Hearth Lodge on open flat ground. The village's
        // houses are too long for their old spots (east of the yard is the boardwalk, west of it the cliff and the waterfall pool): both
        // stand north of the yard, long side north, stairs to the south (SonTinhMapBuilder.BuildBays builds them there too). Houses not
        // listed alternate.
        public static readonly Vector2 VillageNorthHouse = new(-22f, 52.5f), VillageWestHouse = new(-36f, 50f);
        static readonly (Vector2 at, Kind kind)[] Plan =
        {
            (new Vector2(-2.9f, 30f), Kind.HouseB),     // Sơn Tinh, by the path before the village
            (VillageNorthHouse, Kind.HouseB),           // the village, north of the yard
            (VillageWestHouse, Kind.HouseA),            // the village, north-west of the yard, beside the stable
            (new Vector2(26.4f, 105f), Kind.HouseA),    // by the path after the horse stop (sloping)
            (new Vector2(-2.1f, 200f), Kind.HouseA),    // by the path near the end (sloping)
            (new Vector2(-24.3f, 40f), Kind.HouseB),    // Thủy Tinh, first islet
            (new Vector2(-36.9f, 215f), Kind.HouseA),   // Thủy Tinh, last islet
        };
        static readonly (Vector2 from, Vector2 to)[] VillageMoves = { (new(-21.4f, 42.5f), VillageNorthHouse), (new(-35.0f, 47.5f), VillageWestHouse) };

        static int PlannedVariant(Transform house, int fallback)
        {
            var at = new Vector2(house.position.x, house.position.z);
            foreach (var (spot, kind) in Plan)
                if (Vector2.Distance(spot, at) < 2f) return kind == Kind.HouseA ? 0 : 1;
            return fallback;
        }

        // ---------------------------------------------------------------- prefabs

        // The prefab (built on first use; delete it to rebuild after changing a spec); null when the model is not in the project.
        public static GameObject Prefab(Kind kind) => Prefab(kind, false);

        // Rebuilds every prefab in place (same asset, same GUID, so the scenes keep their instances), e.g. after replacing a model file.
        public static void RebuildPrefabs()
        {
            foreach (Kind k in System.Enum.GetValues(typeof(Kind))) Prefab(k, true);
            AssetDatabase.SaveAssets();
        }

        static GameObject Prefab(Kind kind, bool rebuild)
        {
            Spec spec = Specs[kind];
            string path = $"{PrefabDir}/{spec.prefab}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null && !rebuild) return existing;
            string modelPath = Dir + spec.file + ".fbx";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(modelPath) == null) return null;

            ConfigureModel(modelPath);
            Material material = EnsureMaterial(spec.file);
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder(PrefabDir)) AssetDatabase.CreateFolder("Assets/Prefabs", "Village");

            var root = new GameObject(spec.prefab);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath), root.transform);
            model.name = "Model";
            model.transform.localRotation = Quaternion.Euler(0f, spec.yaw, 0f) * model.transform.localRotation;   // keep the FBX root's own upright rotation
            model.transform.localScale = Vector3.one;
            Bounds b = LocalBounds(model, root.transform);
            float measured = spec.byHeight ? b.size.y : Mathf.Max(b.size.x, b.size.z);
            model.transform.localScale = Vector3.one * (spec.size / measured);
            b = LocalBounds(model, root.transform);
            model.transform.localPosition = -new Vector3(b.center.x, b.min.y, b.center.z);

            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterials = Enumerable.Repeat(material, r.sharedMaterials.Length).ToArray();
                if (spec.collider)
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf != null) r.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                }
            }
            if (kind != Kind.Bell)
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            b = LocalBounds(prefab, prefab.transform);
            Debug.Log($"VillageModelSetup: {path} size {b.size.x:F2} x {b.size.y:F2} x {b.size.z:F2} m");
            return prefab;
        }

        static void ConfigureModel(string path)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            if (importer.materialImportMode == ModelImporterMaterialImportMode.None && !importer.importAnimation) return;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;   // the material below is used, not the FBX's
            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.SaveAndReimport();
        }

        static void SetTexture(string path, TextureImporterType type, bool sRGB)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            if (importer.textureType == type && importer.sRGBTexture == sRGB && importer.maxTextureSize <= 2048) return;
            importer.textureType = type;
            importer.sRGBTexture = sRGB;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        static Material EnsureMaterial(string file)
        {
            string baseMap = Dir + file + "_basecolor.png", normalMap = Dir + file + "_normal.png", metalMap = Dir + file + "_metallic_smoothness.png";
            SetTexture(baseMap, TextureImporterType.Default, true);
            SetTexture(normalMap, TextureImporterType.NormalMap, false);
            SetTexture(metalMap, TextureImporterType.Default, false);

            string path = Dir + "M_" + file + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.shader = Shader.Find("Universal Render Pipeline/Lit");
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(baseMap));
            m.SetColor("_BaseColor", Color.white);
            m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalMap));
            m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP");
            m.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(metalMap));
            m.EnableKeyword("_METALLICSPECGLOSSMAP");
            m.SetFloat("_WorkflowMode", 1f); m.SetFloat("_Smoothness", 1f); m.SetFloat("_SmoothnessTextureChannel", 0f);
            m.SetFloat("_Cull", (float)CullMode.Off); m.doubleSidedGI = true;   // thatch fringes and the tassel are single sheets
            EditorUtility.SetDirty(m);
            return m;
        }

        // Bounds of every mesh under `go`, in `frame`'s space (from the meshes' own bounds, so a rotated model is not inflated twice).
        public static Bounds LocalBounds(GameObject go, Transform frame)
        {
            bool any = false; var b = new Bounds();
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                Bounds mb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var c = new Vector3((i & 1) == 0 ? mb.min.x : mb.max.x, (i & 2) == 0 ? mb.min.y : mb.max.y, (i & 4) == 0 ? mb.min.z : mb.max.z);
                    Vector3 p = frame.InverseTransformPoint(mf.transform.TransformPoint(c));
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
                }
            }
            return b;
        }

        // ---------------------------------------------------------------- placing

        // Lowest terrain under the middle 70 % of a footprint (the roof overhang left out), so stilts and walls never float.
        public static float LowestGround(Terrain terrain, Transform frame, Bounds local)
        {
            float min = float.MaxValue;
            for (int i = 0; i <= 4; i++)
                for (int j = 0; j <= 4; j++)
                {
                    float x = Mathf.Lerp(local.center.x - local.extents.x * 0.7f, local.center.x + local.extents.x * 0.7f, i / 4f);
                    float z = Mathf.Lerp(local.center.z - local.extents.z * 0.7f, local.center.z + local.extents.z * 0.7f, j / 4f);
                    Vector3 p = frame.TransformPoint(new Vector3(x, 0f, z));
                    min = Mathf.Min(min, terrain.SampleHeight(p) + terrain.transform.position.y);
                }
            return min;
        }

        public static GameObject HousePrefab(int variant) => Prefab(variant % 2 == 0 ? Kind.HouseA : Kind.HouseB);

        // The planned kind for a house the map builders are placing (MapDecor.StiltHouse), else alternate.
        public static int VariantFor(Vector3 position, int fallback)
        {
            foreach (var (spot, kind) in Plan)
                if (Vector2.Distance(spot, new Vector2(position.x, position.z)) < 2f) return kind == Kind.HouseA ? 0 : 1;
            return fallback;
        }

        // Puts house model `variant` under a StiltHouse root (its +Z away from the door, as the block house): the stairs end where the old
        // ladder did. The terrain under it is levelled to the height at its stairs (cut and fill, blended over 2.5 m round it), then the
        // model is dropped onto the lowest ground under it (feet sunk 15 cm). Returns null without the models.
        public static GameObject DressHouse(Transform house, int variant)
        {
            GameObject prefab = HousePrefab(variant);
            if (prefab == null) return null;
            // on the marsh islets the root's +Z points at the plank bridge from the causeway (ThuyTinhMapBuilder.BuildBridges lands it 8.4 m
            // out): the stairs face it and end at it, the house reaching back over the islet; elsewhere the stairs go where the old ladder
            // was (the root's -Z) and the house grows back from there
            bool islet = house.parent != null && house.parent.name == "Islets";
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, house);
            go.transform.localRotation = Quaternion.Euler(0f, islet ? 0f : 180f, 0f);
            go.transform.localPosition = Vector3.zero;
            Bounds b = LocalBounds(go, house);
            go.transform.localPosition = new Vector3(-b.center.x, 0f, islet ? IsletBridgeZ - b.max.z : HouseFrontZ - b.min.z);
            var terrain = Object.FindFirstObjectByType<Terrain>();
            if (terrain != null)
            {
                b = LocalBounds(go, house);
                if (!islet) LevelGround(terrain, house, b, 2.5f);
                float y = LowestGround(terrain, house, b) - house.position.y - 0.15f;
                go.transform.localPosition += Vector3.up * y;
            }
            return go;
        }

        // Levels the terrain under a footprint (the roof overhang left out) to the mean height along its front (local -Z) edge, blending
        // back to the old ground within `margin` metres outside it.
        static void LevelGround(Terrain terrain, Transform frame, Bounds local, float margin)
        {
            var td = terrain.terrainData; Vector3 tp = terrain.transform.position, size = td.size; int res = td.heightmapResolution;
            float half = 0.85f;
            Vector3 c = local.center, e = new Vector3(local.extents.x * half, 0f, local.extents.z * half);
            float target = 0f;
            for (int i = 0; i <= 4; i++)
            {
                Vector3 p = frame.TransformPoint(new Vector3(c.x - e.x + 2f * e.x * i / 4f, 0f, c.z - e.z));
                target += (terrain.SampleHeight(p) + tp.y) / 5f;
            }
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var (sx, sz) in new[] { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f) })
            {
                Vector3 p = frame.TransformPoint(new Vector3(c.x + sx * (e.x + margin), 0f, c.z + sz * (e.z + margin)));
                minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z);
            }
            int x0 = Mathf.Clamp(Mathf.FloorToInt((minX - tp.x) / size.x * (res - 1)), 0, res - 1), x1 = Mathf.Clamp(Mathf.CeilToInt((maxX - tp.x) / size.x * (res - 1)), 0, res - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((minZ - tp.z) / size.z * (res - 1)), 0, res - 1), z1 = Mathf.Clamp(Mathf.CeilToInt((maxZ - tp.z) / size.z * (res - 1)), 0, res - 1);
            float[,] h = td.GetHeights(x0, z0, x1 - x0 + 1, z1 - z0 + 1);
            for (int j = 0; j <= z1 - z0; j++)
                for (int i = 0; i <= x1 - x0; i++)
                {
                    var world = new Vector3(tp.x + (x0 + i) / (float)(res - 1) * size.x, 0f, tp.z + (z0 + j) / (float)(res - 1) * size.z);
                    Vector3 q = frame.InverseTransformPoint(world);
                    float dx = Mathf.Max(0f, Mathf.Abs(q.x - c.x) - e.x), dz = Mathf.Max(0f, Mathf.Abs(q.z - c.z) - e.z);
                    float d = Mathf.Sqrt(dx * dx + dz * dz);
                    if (d >= margin) continue;
                    float w = 1f - Mathf.SmoothStep(0f, 1f, d / margin);
                    h[j, i] = Mathf.Lerp(h[j, i], (target - tp.y) / size.y, w);
                }
            td.SetHeights(x0, z0, h);
            EditorUtility.SetDirty(td);
        }

        // ---------------------------------------------------------------- menu

        [MenuItem("Tools/Son Tinh Thuy Tinh/Village/Setup Village Models (houses, stable, bell)")]
        public static void SetupAll()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("Stop Play mode first."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            foreach (Kind k in System.Enum.GetValues(typeof(Kind)))
                if (Prefab(k) == null) { Debug.LogError($"VillageModelSetup: missing {Dir}{Specs[k].file}.fbx"); return; }
            AssetDatabase.SaveAssets();

            foreach (string path in MapScenes)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var houses = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                    .Where(t => t.name == "StiltHouse").OrderBy(t => t.position.z).ThenBy(t => t.position.x).ToList();
                var terrain = Object.FindFirstObjectByType<Terrain>();
                for (int i = 0; i < houses.Count; i++)
                {
                    for (int c = houses[i].childCount - 1; c >= 0; c--) Object.DestroyImmediate(houses[i].GetChild(c).gameObject);
                    foreach (var (from, to) in VillageMoves)
                        if (path == MapScenes[0] && terrain != null && Vector2.Distance(new Vector2(houses[i].position.x, houses[i].position.z), from) < 2f)
                        {
                            var p = new Vector3(to.x, 0f, to.y);
                            p.y = terrain.SampleHeight(p) + terrain.transform.position.y;
                            houses[i].SetPositionAndRotation(p, Quaternion.identity);   // +Z = north, the stairs face the yard
                        }
                    DressHouse(houses[i], PlannedVariant(houses[i], i));
                }
                int cleared = ClearPlantsUnder(houses.Select(h => h.gameObject));
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();   // the levelled terrain data
                Debug.Log($"VillageModelSetup: {path}: {houses.Count} house(s) swapped, {cleared} plant(s) under them removed.");
            }

            // the stable and the bell come from the horse quest builder (it rebuilds its whole root in Map_SonTinh)
            HorseQuestBuilder.Build();
            var son = EditorSceneManager.OpenScene(MapScenes[0], OpenSceneMode.Single);
            var stable = GameObject.Find("HorseQuest/Stable");
            if (stable != null)
            {
                int cleared = ClearPlantsUnder(new[] { stable });
                EditorSceneManager.MarkSceneDirty(son);
                EditorSceneManager.SaveScene(son);
                Debug.Log($"VillageModelSetup: stable placed, {cleared} plant(s) under it removed.");
            }
        }

        // Trees, rocks and plants (the generated forest and the Idyllic dressing) standing inside the footprint of `blockers`.
        static int ClearPlantsUnder(IEnumerable<GameObject> blockers)
        {
            var boxes = new List<(Transform frame, Bounds local)>();
            foreach (var b in blockers)
            {
                var model = b.GetComponentsInChildren<MeshCollider>(true).FirstOrDefault();
                if (model == null) continue;
                Bounds local = LocalBounds(model.gameObject, b.transform);
                local.Expand(new Vector3(2f, 0f, 2f));   // the levelled ring round a house too
                boxes.Add((b.transform, local));
            }
            int count = 0;
            var groups = new List<Transform>();
            var generated = GameObject.Find("Generated");
            if (generated != null) foreach (string n in new[] { "Trees", "Rocks", "Undergrowth" }) { var g = generated.transform.Find(n); if (g != null) groups.Add(g); }
            // small decor of the map builders (torches, banners) that the house now covers
            var decor = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(t => (t.name == "Torch" || t.name == "Banner") && t.parent != null).ToList();
            foreach (var t in decor)
                foreach (var (frame, local) in boxes)
                {
                    Vector3 q = frame.InverseTransformPoint(t.position);
                    if (q.x > local.min.x && q.x < local.max.x && q.z > local.min.z && q.z < local.max.z) { Object.DestroyImmediate(t.gameObject); count++; break; }
                }
            var dressing = GameObject.Find("Dressing");
            if (dressing != null) foreach (Transform g in dressing.transform) if (g.name != "Water") groups.Add(g);
            foreach (var g in groups)
                for (int i = g.childCount - 1; i >= 0; i--)
                {
                    Vector3 p = g.GetChild(i).position;
                    foreach (var (frame, local) in boxes)
                    {
                        Vector3 q = frame.InverseTransformPoint(p);
                        if (q.x > local.min.x && q.x < local.max.x && q.z > local.min.z && q.z < local.max.z)
                        { Object.DestroyImmediate(g.GetChild(i).gameObject); count++; break; }
                    }
                }
            return count;
        }
    }
}
