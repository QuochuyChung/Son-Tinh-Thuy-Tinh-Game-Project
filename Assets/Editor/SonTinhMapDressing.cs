using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SonTinhThuyTinh.EditorTools
{
    // Plants from "Idyllic Fantasy Nature" on top of the TriForge forest of Assets/Scenes/Map_SonTinh.unity (SonTinhMapBuilder), and a
    // lotus pond of "Simple stylized water" beside the path at z 150 (both packs: docs/progress.md 9.6, not in git):
    //  - flowers and flower patches on the verges of the path, bushes a bit further out, flowering (peach) and broadleaf trees in the gaps
    //    of the forest, a few rocks at the foot of the walls;
    //  - reeds, cattails and water lilies round the two waterfall pools and the grove pond; the lotus pond with willows beside it.
    // Everything goes under one root "Dressing", deleted and rebuilt on every run (fixed seed). Build Son Tinh Map calls this at the end.
    // Nothing goes on the path (only flowers, which have no collider, come closer than 4.5 m to its centre line), in the gift stops, in
    // the bays' yards or on the decor (houses, torches, plank walks...); the horse village is dressed by HorseQuestBuilder.
    public static class SonTinhMapDressing
    {
        const string ScenePath = "Assets/Scenes/Map_SonTinh.unity";
        const string Pack = "Assets/Idyllic Fantasy Nature/Prefabs/";
        const string RootName = "Dressing";
        const float PathEnd = 295f;

        static System.Random rng;
        static Terrain terrain;
        static readonly List<Bounds> blocked = new();      // footprints of the decor, the stops' landmarks and the quest objects
        static readonly List<Vector2> treeSpots = new();   // trunks already standing (TriForge forest + the ones placed here)

        [MenuItem("Tools/Son Tinh Thuy Tinh/Dress Son Tinh Map (Idyllic plants, lotus pond)")]
        public static void DressMenu()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("Stop Play mode first."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Dress(Object.FindFirstObjectByType<Terrain>());
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        // Builds the "Dressing" root in the open Map_SonTinh scene (does not save).
        public static void Dress(Terrain mapTerrain)
        {
            if (!AssetDatabase.IsValidFolder(Pack.TrimEnd('/'))) { Debug.LogError("Idyllic Fantasy Nature is not imported (docs/progress.md 9.6)."); return; }
            terrain = mapTerrain;
            rng = new System.Random(20261008);
            SonTinhMapBuilder.DestroyByName(RootName);
            SonTinhMapBuilder.PlanPools();

            DigLotusPond();
            CollectObstacles();
            var root = new GameObject(RootName).transform;

            Transform water = SonTinhMapBuilder.Group(root, "Water"), trees = SonTinhMapBuilder.Group(root, "Trees"),
                bushes = SonTinhMapBuilder.Group(root, "Bushes"), flowers = SonTinhMapBuilder.Group(root, "Flowers"), rocks = SonTinhMapBuilder.Group(root, "Rocks");
            BuildWater(water, trees);
            ScatterTrees(trees);
            ScatterBushes(bushes);
            ScatterFlowers(flowers);
            ScatterRocks(rocks);
            AddButterflies(root);
            Debug.Log($"Map_SonTinh dressed: {trees.childCount} trees, {bushes.childCount} bushes, {flowers.childCount} flowers, {rocks.childCount} rocks, {water.childCount} water pieces.");
        }

        // ---------------------------------------------------------------- helpers

        static float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        static T Pick<T>(T[] items) => items[rng.Next(items.Length)];
        static float Height(float x, float z) => terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;
        static Vector3 Ground(float x, float z) => new(x, Height(x, z), z);
        static float PathDistance(float x, float z) => Mathf.Abs(x - SonTinhMapBuilder.PathX(z));

        static GameObject[] Prefabs(params string[] names)
        {
            var list = new List<GameObject>();
            foreach (string name in names)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Pack + name + ".prefab");
                if (prefab != null) list.Add(prefab); else Debug.LogWarning("Prefab missing (is Idyllic Fantasy Nature imported?): " + name);
            }
            return list.ToArray();
        }

        static GameObject Place(GameObject prefab, Vector3 position, float scale, Transform parent, bool isStatic = true)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, R(0f, 360f), 0f));
            go.transform.localScale = Vector3.one * scale;
            if (isStatic) GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            Retint(go);
            return go;
        }

        // The pack's "green" foliage is tinted lime yellow (_Top_Color / _Bottom_Color of its foliage shader), which glows against the deep
        // green TriForge forest. Trees and bushes placed here use copies tinted forest green, kept in MaterialDir (they need the pack too).
        const string MaterialDir = "Assets/Art/Maps/SonTinh/Dressing";
        static readonly Dictionary<string, (Color bottom, Color top)> ForestTints = new()
        {
            { "Willow_Branch_Green", (new Color(0.20f, 0.36f, 0.11f), new Color(0.40f, 0.58f, 0.20f)) },
            { "Broadleaf_Green", (new Color(0.22f, 0.40f, 0.12f), new Color(0.42f, 0.60f, 0.20f)) },
        };

        internal static void Retint(GameObject go)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null || !ForestTints.TryGetValue(materials[i].name, out var tint)) continue;
                    materials[i] = ForestCopy(materials[i], tint.bottom, tint.top);
                    changed = true;
                }
                if (changed) r.sharedMaterials = materials;
            }
        }

        static Material ForestCopy(Material source, Color bottom, Color top)
        {
            string path = $"{MaterialDir}/{source.name}_Forest.mat";
            var copy = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (copy == null)
            {
                System.IO.Directory.CreateDirectory(MaterialDir);
                copy = new Material(source);
                AssetDatabase.CreateAsset(copy, path);
            }
            copy.SetColor("_Bottom_Color", bottom);
            copy.SetColor("_Top_Color", top);
            EditorUtility.SetDirty(copy);
            return copy;
        }

        // All water in the map: the two waterfall pools, the grove pond and the lotus pond (centre, radius, surface height).
        static IEnumerable<(Vector2 centre, float radius, float level)> Waters()
        {
            foreach (int i in SonTinhMapBuilder.FallBays)
                yield return (SonTinhMapBuilder.poolCenters[i], 3.6f, 3f + SonTinhMapBuilder.Bays[i].x * 0.018f - 0.2f);
            yield return (SonTinhMapBuilder.PondCentre, SonTinhMapBuilder.PondRadius, 3f + SonTinhMapBuilder.Bays[1].x * 0.018f - 0.2f);
            yield return (SonTinhMapBuilder.LotusPondCentre, SonTinhMapBuilder.LotusPondRadius, SonTinhMapBuilder.LotusPondLevel);
        }

        static bool InWater(float x, float z, float margin)
        {
            var p = new Vector2(x, z);
            foreach (var w in Waters()) if (Vector2.Distance(p, w.centre) < w.radius + margin) return true;
            return false;
        }

        static bool OnDecor(float x, float z, float margin)
        {
            foreach (var b in blocked)
                if (x > b.min.x - margin && x < b.max.x + margin && z > b.min.z - margin && z < b.max.z + margin) return true;
            return false;
        }

        // Free ground for a land plant: in the valley, off the path, out of the stops/bays/water/decor, not on a steep wall.
        static bool Free(float x, float z, float pathMargin, float margin)
        {
            if (z < -6f || z > PathEnd + 4f || Mathf.Abs(x) > 104f) return false;
            if (PathDistance(x, z) < pathMargin) return false;
            if (SonTinhMapBuilder.InClearing(x, z, margin) || InWater(x, z, 0.8f + margin) || OnDecor(x, z, margin)) return false;
            if (z > SonTinhMapBuilder.ClimbStart - 4f && PathDistance(x, z) < 6f) return false;   // the plank walk up to the gate
            Vector3 n = terrain.terrainData.GetInterpolatedNormal((x - terrain.transform.position.x) / terrain.terrainData.size.x,
                (z - terrain.transform.position.z) / terrain.terrainData.size.z);
            return n.y > 0.82f;   // < ~35°
        }

        // Footprints (x, z) of what the builders put in the map, so nothing grows through a house, a torch or a plank walk.
        static void CollectObstacles()
        {
            blocked.Clear();
            treeSpots.Clear();
            var generated = GameObject.Find("Generated");
            if (generated != null)
            {
                foreach (Transform t in generated.transform)
                {
                    if (t.name == "Trees" || t.name == "Rocks" || t.name == "Undergrowth") continue;   // the forest itself
                    foreach (var r in t.GetComponentsInChildren<Renderer>(true))
                        if (r is not ParticleSystemRenderer && r.name != "Pool" && r.bounds.size.x < 40f && r.bounds.size.z < 40f) blocked.Add(r.bounds);
                }
                Transform trees = generated.transform.Find("Trees");
                if (trees != null) foreach (Transform t in trees) treeSpots.Add(new Vector2(t.position.x, t.position.z));
            }
            foreach (string name in new[] { "HorseQuest", "GiftPickups" })
            {
                var go = GameObject.Find(name);
                if (go == null) continue;
                foreach (var r in go.GetComponentsInChildren<Renderer>(true)) if (r.bounds.size.x < 40f) blocked.Add(r.bounds);
                foreach (var c in go.GetComponentsInChildren<Collider>(true)) if (c.bounds.size.x < 40f) blocked.Add(c.bounds);
            }
        }

        // ---------------------------------------------------------------- lotus pond

        // The pond is part of the height field (SonTinhMapBuilder.GroundHeight). A map built before it existed only needs the patch of terrain
        // under it re-sampled, and the forest that stood there cleared, which is what this does (harmless on a map built with it).
        static void DigLotusPond()
        {
            TerrainData data = terrain.terrainData;
            Vector2 c = SonTinhMapBuilder.LotusPondCentre;
            float reach = SonTinhMapBuilder.LotusPondRadius + 6f;
            Vector3 origin = terrain.transform.position, size = data.size;
            int res = data.heightmapResolution;
            int x0 = Mathf.Clamp(Mathf.FloorToInt((c.x - reach - origin.x) / size.x * (res - 1)), 0, res - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt((c.x + reach - origin.x) / size.x * (res - 1)), 0, res - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((c.y - reach - origin.z) / size.z * (res - 1)), 0, res - 1);
            int z1 = Mathf.Clamp(Mathf.CeilToInt((c.y + reach - origin.z) / size.z * (res - 1)), 0, res - 1);
            var heights = new float[z1 - z0 + 1, x1 - x0 + 1];
            for (int j = z0; j <= z1; j++)
                for (int i = x0; i <= x1; i++)
                {
                    float x = origin.x + size.x * i / (res - 1), z = origin.z + size.z * j / (res - 1);
                    heights[j - z0, i - x0] = Mathf.Clamp01(SonTinhMapBuilder.GroundHeight(x, z) / size.y);
                }
            data.SetHeights(x0, z0, heights);

            // soil (layer 1) on the bottom and the muddy bank
            if (data.alphamapLayers == 4)
            {
                int ares = data.alphamapResolution;
                int ax0 = Mathf.Clamp(Mathf.FloorToInt((c.x - reach - origin.x) / size.x * ares), 0, ares - 1), ax1 = Mathf.Clamp(Mathf.CeilToInt((c.x + reach - origin.x) / size.x * ares), 0, ares - 1);
                int az0 = Mathf.Clamp(Mathf.FloorToInt((c.y - reach - origin.z) / size.z * ares), 0, ares - 1), az1 = Mathf.Clamp(Mathf.CeilToInt((c.y + reach - origin.z) / size.z * ares), 0, ares - 1);
                float[,,] map = data.GetAlphamaps(ax0, az0, ax1 - ax0 + 1, az1 - az0 + 1);
                for (int j = 0; j <= az1 - az0; j++)
                    for (int i = 0; i <= ax1 - ax0; i++)
                    {
                        float x = origin.x + (ax0 + i + 0.5f) / ares * size.x, z = origin.z + (az0 + j + 0.5f) / ares * size.z;
                        float mud = 1f - SonTinhMapBuilder.Smooth(SonTinhMapBuilder.LotusPondRadius + 0.3f, SonTinhMapBuilder.LotusPondRadius + 1.6f, Vector2.Distance(new Vector2(x, z), c));
                        if (mud <= 0f) continue;
                        for (int l = 0; l < 4; l++) map[j, i, l] *= 1f - mud;
                        map[j, i, 1] += mud;
                    }
                data.SetAlphamaps(ax0, az0, map);
            }
            EditorUtility.SetDirty(data);

            // the forest that stood there
            var generated = GameObject.Find("Generated");
            if (generated == null) return;
            foreach (var (group, clear) in new[] { ("Trees", 9f), ("Rocks", 4f), ("Undergrowth", 1.2f) })
            {
                Transform parent = generated.transform.Find(group);
                if (parent == null) continue;
                for (int i = parent.childCount - 1; i >= 0; i--)
                {
                    Vector3 p = parent.GetChild(i).position;
                    if (Vector2.Distance(new Vector2(p.x, p.z), c) < SonTinhMapBuilder.LotusPondRadius + clear) Object.DestroyImmediate(parent.GetChild(i).gameObject);
                }
            }
        }

        // ---------------------------------------------------------------- water plants

        static void BuildWater(Transform water, Transform trees)
        {
            GameObject[] reeds = Prefabs("Reeds_01", "Reeds_02", "Reeds_03"), cattails = Prefabs("Cattail_01", "Cattail_02", "Cattail_03");
            GameObject[] lilies = Prefabs("Waterlily_01", "Waterlily_02"), pads = Prefabs("LilyPads_01", "LilyPads_02", "LilyPads_03");
            GameObject[] willows = Prefabs("WillowTree_01_Green", "WillowTree_02_Green", "WillowTree_03_Green", "WillowTree_04_Green", "WillowTree_05_Green");

            Vector2 lotus = SonTinhMapBuilder.LotusPondCentre;
            float level = SonTinhMapBuilder.LotusPondLevel;
            var pond = MapDecor.Pool(water, new Vector3(lotus.x, level, lotus.y), SonTinhMapBuilder.LotusPondRadius, SonTinhMapBuilder.EnsurePoolMaterial());
            pond.name = "LotusPond";

            foreach (var w in Waters())
            {
                bool isLotus = w.centre == lotus;
                // reeds and cattails on the bank, leaving the side that faces the path open so the water stays visible
                int bank = isLotus ? 16 : 9;
                for (int i = 0; i < bank; i++)
                {
                    float a = R(0f, Mathf.PI * 2f), r = w.radius + R(-0.4f, 0.6f);
                    float x = w.centre.x + Mathf.Sin(a) * r, z = w.centre.y + Mathf.Cos(a) * r;
                    if (PathDistance(x, z) < PathDistance(w.centre.x, w.centre.y) - w.radius * 0.5f) continue;   // towards the path
                    if (OnDecor(x, z, 0.3f)) continue;
                    var prefabs = rng.Next(3) == 0 ? cattails : reeds;
                    if (prefabs.Length > 0) Place(Pick(prefabs), new Vector3(x, Mathf.Max(Height(x, z), w.level - 0.25f), z), R(0.9f, 1.4f), water);
                }
                // lilies floating on the surface
                int floating = isLotus ? 9 : 3;
                for (int i = 0; i < floating; i++)
                {
                    float a = R(0f, Mathf.PI * 2f), r = R(0.6f, w.radius - 0.9f);
                    var at = new Vector3(w.centre.x + Mathf.Sin(a) * r, w.level + 0.01f, w.centre.y + Mathf.Cos(a) * r);
                    var prefabs = i % 3 == 0 && pads.Length > 0 ? pads : lilies;
                    if (prefabs.Length > 0) Place(Pick(prefabs), at, R(0.8f, 1.2f), water);
                }
            }

            // two willows leaning over the lotus pond, on the far side from the path
            float away = Mathf.Sign(lotus.x - SonTinhMapBuilder.PathX(lotus.y));
            foreach (float dz in new[] { -2.5f, 3f })
            {
                float x = lotus.x + away * (SonTinhMapBuilder.LotusPondRadius + 2.6f), z = lotus.y + dz;
                if (willows.Length > 0) Place(Pick(willows), Ground(x, z) - Vector3.up * 0.1f, R(1.2f, 1.5f), trees);
                treeSpots.Add(new Vector2(x, z));
            }
        }

        // ---------------------------------------------------------------- land plants

        // Flowering (peach-blossom) and broadleaf trees in the gaps of the TriForge forest, never closer than 7 m to the path.
        static void ScatterTrees(Transform trees)
        {
            GameObject[] blossom = Prefabs("BlossomTree_01", "BlossomTree_02", "BlossomTree_03", "BlossomTree_04", "BlossomTree_05");
            GameObject[] broadleaf = Prefabs("BroadleafTree_01_Green", "BroadleafTree_02_Green", "BroadleafTree_03_Green", "BroadleafTree_04_Green", "BroadleafTree_05_Green");
            int placedBlossom = 0, placedBroadleaf = 0;
            for (int attempt = 0; attempt < 8000 && (placedBlossom < 24 || placedBroadleaf < 26); attempt++)
            {
                float z = R(-4f, PathEnd), x = SonTinhMapBuilder.PathX(z) + R(-34f, 34f) ;
                if (!Free(x, z, 7f, 2f)) continue;
                var p = new Vector2(x, z);
                if (treeSpots.Exists(o => Vector2.Distance(o, p) < 7f)) continue;
                bool isBlossom = placedBlossom < 24 && (placedBroadleaf >= 26 || rng.Next(2) == 0);
                GameObject[] set = isBlossom ? blossom : broadleaf;
                if (set.Length == 0) continue;
                Place(Pick(set), Ground(x, z) - Vector3.up * 0.15f, R(1.25f, 1.7f), trees);
                treeSpots.Add(p);
                if (isBlossom) placedBlossom++; else placedBroadleaf++;
            }
        }

        // Round bushes 5-20 m from the path (their collider is a trigger that shakes leaves off, so they never block).
        static void ScatterBushes(Transform bushes)
        {
            GameObject[] set = Prefabs("Bush_02_01", "Bush_02_02");   // the plain green ones (Bush_01 and Bush_03 are tinted lime yellow)
            if (set.Length == 0) return;
            int placed = 0;
            for (int attempt = 0; attempt < 6000 && placed < 110; attempt++)
            {
                float z = R(-4f, PathEnd + 2f), side = rng.Next(2) == 0 ? -1f : 1f, x = SonTinhMapBuilder.PathX(z) + side * R(5f, 20f);
                if (!Free(x, z, 5f, 0.5f)) continue;
                Place(Pick(set), Ground(x, z) - Vector3.up * 0.05f, R(0.9f, 1.4f), bushes);
                placed++;
            }
        }

        // Single flowers and flower patches on both verges of the path, thinning out away from it.
        static void ScatterFlowers(Transform flowers)
        {
            GameObject[] single = Prefabs("Flower_Blue_01", "Flower_Blue_02", "Flower_Orange", "Flower_Pink", "Flower_Purple", "Flower_Red", "Flower_White", "Flower_Yellow", "Flower_YellowRed");
            GameObject[] meadows = Prefabs("FlowerMeadow_Blue", "FlowerMeadow_Orange", "FlowerMeadow_Pink", "FlowerMeadow_Red", "FlowerMeadow_White", "FlowerMeadow_RedOrange",
                "FlowerMeadow_OrangePinkRedPurpleBlue", "FlowerMeadow_Purple", "FlowerMeadow_PurpleRedPink", "FlowerMeadow_RedPink", "FlowerMeadow_BluePurple");
            int placed = 0;
            for (int attempt = 0; attempt < 12000 && placed < 650; attempt++)
            {
                float z = R(-2f, PathEnd), side = rng.Next(2) == 0 ? -1f : 1f;
                float lateral = 3.1f + Mathf.Pow((float)rng.NextDouble(), 1.8f) * 11f;   // most of them close to the path
                float x = SonTinhMapBuilder.PathX(z) + side * lateral;
                if (!Free(x, z, 3.1f, 0f)) continue;
                bool patch = rng.Next(5) < 2;
                GameObject[] set = patch ? meadows : single;
                if (set.Length == 0) continue;
                Place(Pick(set), Ground(x, z) - Vector3.up * 0.03f, patch ? R(2f, 3.4f) : R(1f, 1.6f), flowers);
                placed++;
            }
        }

        // Mossy boulders at the foot of the walls, where the valley floor starts to rise.
        static void ScatterRocks(Transform rocks)
        {
            GameObject[] set = Prefabs("Rock_Medium_01", "Rock_Medium_02", "Rock_Medium_03", "Stone_Medium_01", "Stone_Medium_02", "Stone_Medium_03");
            if (set.Length == 0) return;
            int placed = 0;
            for (int attempt = 0; attempt < 3000 && placed < 36; attempt++)
            {
                float z = R(0f, PathEnd), side = rng.Next(2) == 0 ? -1f : 1f, x = SonTinhMapBuilder.PathX(z) + side * R(22f, 30f);
                if (!Free(x, z, 12f, 1f)) continue;
                Place(Pick(set), Ground(x, z) - Vector3.up * 0.4f, R(0.9f, 1.6f), rocks);
                placed++;
            }
        }

        // Butterflies over the lotus pond and the grove camp (the pack's spawner; it moves, so not static).
        static void AddButterflies(Transform root)
        {
            var spawner = AssetDatabase.LoadAssetAtPath<GameObject>(Pack + "ButterflySpawnArea.prefab");
            if (spawner == null) return;
            Vector2 lotus = SonTinhMapBuilder.LotusPondCentre, grove = SonTinhMapBuilder.Bay(2);
            foreach (Vector2 at in new[] { lotus, grove + new Vector2(6f, 6f) })
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(spawner, root);
                go.transform.position = Ground(at.x, at.y) + Vector3.up * 1.2f;
                foreach (var c in go.GetComponentsInChildren<Collider>(true)) c.isTrigger = true;   // never blocks the player
            }
        }
    }
}
