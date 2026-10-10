using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static SonTinhThuyTinh.EditorTools.SonTinhMapBuilder;

namespace SonTinhThuyTinh.EditorTools
{
    // Marsh plants from "Idyllic Fantasy Nature" on top of the TriForge forest of Map_ThuyTinh (docs/progress.md 9.21), like
    // SonTinhMapDressing does for the mountain map:
    //  - reeds and cattails in the shallows and along the banks, water lilies and lily pads floating on the open water;
    //  - green willows leaning over the water on the dry hummocks and the foot of the walls, a few blossom trees by the stilt houses;
    //  - flowers on the verges of the causeway, bushes a bit further out, mossy boulders at the foot of the walls, butterflies;
    //  - and the water itself: WaterWorks when that pack is imported, otherwise "Simple stylized water" (no more magenta water).
    // Everything goes under one root "Dressing", rebuilt on every run (fixed seed). Build Thuy Tinh Map calls this at the end; the menu
    // dresses the open map without rebuilding the terrain. Nothing goes on the causeway, the bridges, the crocodile's island and lair, the
    // islets' houses, the flooded shrine or the gate.
    public static partial class ThuyTinhMapBuilder
    {
        const string Idyllic = "Assets/Idyllic Fantasy Nature/Prefabs/";
        const string DressingRoot = "Dressing";
        static System.Random dressRng;
        static Terrain dressTerrain;
        static readonly List<Bounds> dressBlocked = new();
        static readonly List<Vector2> dressTrees = new();

        [MenuItem("Tools/Son Tinh Thuy Tinh/Dress Thuy Tinh Map (Idyllic marsh plants, water)")]
        public static void DressMenu()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("Stop Play mode first."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Dress(Object.FindFirstObjectByType<Terrain>());
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            FixFinalBattleWater();
        }

        // The water material for this map (and the final battle's pool): WaterWorks' SSR water when the pack is there, else Simple water.
        internal static Material WaterMaterial()
        {
            Material m = EnsureWaterWorksMaterial();
            if (m == null || m.shader == null || !m.shader.isSupported || m.shader.name == "Hidden/InternalErrorShader") m = EnsureWaterMaterial();
            return m;
        }

        // Map_FinalBattle's pool used Mat_WaterWorks directly: magenta without the pack. Swapped for the same water as this map.
        static void FixFinalBattleWater()
        {
            const string path = "Assets/Scenes/Map_FinalBattle.unity";
            var broken = AssetDatabase.LoadAssetAtPath<Material>(DataDir + "/Mat_WaterWorks.mat");
            Material water = WaterMaterial();
            if (water == null || water == broken) return;
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int swapped = 0;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    bool changed = false;
                    for (int i = 0; i < mats.Length; i++) if (mats[i] == broken) { mats[i] = water; changed = true; swapped++; }
                    if (changed) r.sharedMaterials = mats;
                }
            if (swapped > 0) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
            Debug.Log($"Map_FinalBattle: {swapped} water surface(s) now use {water.name}.");
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        // Builds the "Dressing" root in the open Map_ThuyTinh scene and puts the right water on the surface (does not save).
        public static void Dress(Terrain mapTerrain)
        {
            Transform generated = GameObject.Find(GeneratedRoot)?.transform;
            Transform surface = generated != null ? generated.Find("Water") : null;
            Material water = WaterMaterial();
            if (surface != null && water != null) surface.GetComponent<MeshRenderer>().sharedMaterial = water;

            if (!AssetDatabase.IsValidFolder(Idyllic.TrimEnd('/'))) { Debug.LogError("Idyllic Fantasy Nature is not imported (docs/progress.md 9.6)."); return; }
            dressTerrain = mapTerrain;
            dressRng = new System.Random(20261009);
            DestroyByName(DressingRoot);
            CollectDressObstacles(generated);
            var root = new GameObject(DressingRoot).transform;
            Transform wet = Group(root, "WaterPlants"), trees = Group(root, "Trees"), bushes = Group(root, "Bushes"),
                flowers = Group(root, "Flowers"), rocks = Group(root, "Rocks");
            DressWater(wet);
            DressTrees(trees);
            DressBushes(bushes);
            DressFlowers(flowers);
            DressRocks(rocks);
            DressButterflies(root);
            Debug.Log($"Map_ThuyTinh dressed: {wet.childCount} water plants, {trees.childCount} trees, {bushes.childCount} bushes, {flowers.childCount} flowers, {rocks.childCount} rocks; water {water?.name}.");
        }

        // ---------------------------------------------------------------- helpers

        static float DR(float a, float b) => a + (float)dressRng.NextDouble() * (b - a);
        static T DPick<T>(T[] items) => items[dressRng.Next(items.Length)];
        static float TerrainY(float x, float z) => dressTerrain.SampleHeight(new Vector3(x, 0f, z)) + dressTerrain.transform.position.y;

        static GameObject[] IdyllicPrefabs(params string[] names)
        {
            var list = new List<GameObject>();
            foreach (string name in names)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Idyllic + name + ".prefab");
                if (prefab != null) list.Add(prefab); else Debug.LogWarning("Prefab missing (is Idyllic Fantasy Nature imported?): " + name);
            }
            return list.ToArray();
        }

        static GameObject Plant(GameObject prefab, Vector3 position, float scale, Transform parent)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, DR(0f, 360f), 0f));
            go.transform.localScale = Vector3.one * scale;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            SonTinhMapDressing.Retint(go);   // the pack's lime-yellow greens toned down to forest green (same copies as the Sơn Tinh map)
            return go;
        }

        // Footprints of everything the builders put in the map, so nothing grows through a house, a bridge, a torch or the arena.
        static void CollectDressObstacles(Transform generated)
        {
            dressBlocked.Clear();
            dressTrees.Clear();
            if (generated != null)
            {
                foreach (Transform t in generated)
                {
                    if (t.name == "Trees" || t.name == "Rocks" || t.name == "Undergrowth" || t.name == "Water") continue;
                    foreach (var r in t.GetComponentsInChildren<Renderer>(true))
                        if (r is not ParticleSystemRenderer && r.bounds.size.x < 40f && r.bounds.size.z < 40f) dressBlocked.Add(r.bounds);
                }
                Transform forest = generated.Find("Trees");
                if (forest != null) foreach (Transform t in forest) dressTrees.Add(new Vector2(t.position.x, t.position.z));
            }
            var arena = GameObject.Find(SauChinDuoiBuilder.ArenaRoot);
            if (arena != null) foreach (var r in arena.GetComponentsInChildren<Renderer>(true)) if (r.bounds.size.x < 40f) dressBlocked.Add(r.bounds);
        }

        static bool OnDressObstacle(float x, float z, float margin)
        {
            foreach (var b in dressBlocked)
                if (x > b.min.x - margin && x < b.max.x + margin && z > b.min.z - margin && z < b.max.z + margin) return true;
            return false;
        }

        // the crocodile's lair: open water beside the island, kept clear so its back and tails show
        static bool NearLair(float x, float z)
        {
            Vector2 lair = Stop(1) + LairSide() * (ArenaRadius + 7f);
            return Vector2.Distance(new Vector2(x, z), lair) < 9f;
        }

        // Anywhere in the valley off the causeway and the special places.
        static bool Open(float x, float z, float pathMargin, float margin)
        {
            if (z < -6f || z > PathEnd + 2f || Mathf.Abs(x) > Width * 0.5f - 6f) return false;
            if (Mathf.Abs(x - PathX(z)) < pathMargin) return false;
            if (NearStops(x, z, margin) || NearLair(x, z) || OnDressObstacle(x, z, margin)) return false;
            if (z > ClimbStart - 4f && Mathf.Abs(x - PathX(z)) < 7f) return false;
            return true;
        }

        static bool Gentle(float x, float z)
        {
            Vector3 n = dressTerrain.terrainData.GetInterpolatedNormal((x - dressTerrain.transform.position.x) / dressTerrain.terrainData.size.x,
                (z - dressTerrain.transform.position.z) / dressTerrain.terrainData.size.z);
            return n.y > 0.85f;
        }

        // ---------------------------------------------------------------- the marsh

        static void DressWater(Transform wet)
        {
            GameObject[] reeds = IdyllicPrefabs("Reeds_01", "Reeds_02", "Reeds_03"), cattails = IdyllicPrefabs("Cattail_01", "Cattail_02", "Cattail_03");
            GameObject[] lilies = IdyllicPrefabs("Waterlily_01", "Waterlily_02"), pads = IdyllicPrefabs("LilyPads_01", "LilyPads_02", "LilyPads_03");
            // reeds and cattails where the ground meets the water (a band from knee-deep to just above the waterline), in clumps
            int placed = 0;
            for (int attempt = 0; attempt < 20000 && placed < 420; attempt++)
            {
                float z = DR(-4f, PathEnd), x = PathX(z) + DR(-50f, 50f);
                float h = TerrainY(x, z);
                if (h < WaterY - 0.8f || h > WaterY + 0.35f || !Open(x, z, 3.2f, 0f)) continue;
                int clump = 2 + dressRng.Next(4);
                for (int k = 0; k < clump && placed < 420; k++)
                {
                    float cx = x + DR(-1.6f, 1.6f), cz = z + DR(-1.6f, 1.6f);
                    float ch = TerrainY(cx, cz);
                    if (ch < WaterY - 0.9f || ch > WaterY + 0.45f || !Open(cx, cz, 3.2f, 0f)) continue;
                    var set = dressRng.Next(3) == 0 ? cattails : reeds;
                    if (set.Length == 0) continue;
                    Plant(DPick(set), new Vector3(cx, Mathf.Max(ch, WaterY - 0.35f), cz), DR(0.9f, 1.5f), wet);
                    placed++;
                }
            }
            // lilies and pads on the open water (not on the causeway's line of sight right next to it, not in the river crossing's raft)
            int floating = 0;
            for (int attempt = 0; attempt < 15000 && floating < 230; attempt++)
            {
                float z = DR(-4f, PathEnd - 12f), x = PathX(z) + DR(-45f, 45f);
                if (TerrainY(x, z) > WaterY - 0.35f || !Open(x, z, 4.5f, 1f)) continue;
                if (z > ChannelStart - 3f && z < ChannelEnd + 3f && Mathf.Abs(x - PathX(z)) < 7f) continue;   // the floating logs
                var set = dressRng.Next(3) == 0 ? pads : lilies;
                if (set.Length == 0) continue;
                Plant(DPick(set), new Vector3(x, WaterY + 0.015f, z), DR(0.8f, 1.4f), wet);
                floating++;
            }
        }

        // Willows on dry ground near the water, a few blossom trees beside the stilt-house islets.
        static void DressTrees(Transform trees)
        {
            GameObject[] willows = IdyllicPrefabs("WillowTree_01_Green", "WillowTree_02_Green", "WillowTree_03_Green", "WillowTree_04_Green", "WillowTree_05_Green");
            GameObject[] blossom = IdyllicPrefabs("BlossomTree_01", "BlossomTree_02", "BlossomTree_03", "BlossomTree_04", "BlossomTree_05");
            int placed = 0;
            for (int attempt = 0; attempt < 8000 && placed < 42 && willows.Length > 0; attempt++)
            {
                float z = DR(-4f, PathEnd), x = PathX(z) + DR(-48f, 48f);
                float h = TerrainY(x, z);
                if (h < WaterY + 0.35f || h > WaterY + 6f || !Open(x, z, 7f, 2.5f) || !Gentle(x, z)) continue;
                var p = new Vector2(x, z);
                if (dressTrees.Exists(o => Vector2.Distance(o, p) < 7f)) continue;
                Plant(DPick(willows), new Vector3(x, h - 0.15f, z), DR(1.2f, 1.7f), trees);
                dressTrees.Add(p);
                placed++;
            }
            if (blossom.Length == 0) return;
            foreach (int i in new[] { 0, 2 })   // the islets with a stilt house (Bays 0 and 2)
            {
                Vector2 c = Bay(i);
                for (int k = 0, tries = 0; k < 3 && tries < 60; tries++)
                {
                    float a = DR(0f, Mathf.PI * 2f), r = DR(5.5f, Bays[i].z + 1f);
                    float x = c.x + Mathf.Sin(a) * r, z = c.y + Mathf.Cos(a) * r;
                    if (TerrainY(x, z) < WaterY + 0.4f || OnDressObstacle(x, z, 1.5f)) continue;
                    var p = new Vector2(x, z);
                    if (dressTrees.Exists(o => Vector2.Distance(o, p) < 5f)) continue;
                    Plant(DPick(blossom), new Vector3(x, TerrainY(x, z) - 0.15f, z), DR(1.1f, 1.4f), trees);
                    dressTrees.Add(p);
                    k++;
                }
            }
        }

        static void DressBushes(Transform bushes)
        {
            GameObject[] set = IdyllicPrefabs("Bush_02_01", "Bush_02_02");
            if (set.Length == 0) return;
            int placed = 0;
            for (int attempt = 0; attempt < 6000 && placed < 80; attempt++)
            {
                float z = DR(-4f, PathEnd), side = dressRng.Next(2) == 0 ? -1f : 1f, x = PathX(z) + side * DR(5f, 30f);
                float h = TerrainY(x, z);
                if (h < WaterY + 0.3f || !Open(x, z, 5f, 0.5f) || !Gentle(x, z)) continue;
                Plant(DPick(set), new Vector3(x, h - 0.05f, z), DR(0.9f, 1.3f), bushes);
                placed++;
            }
        }

        // Cool-coloured flowers (blue, white, purple) on the dry verges of the causeway.
        static void DressFlowers(Transform flowers)
        {
            GameObject[] single = IdyllicPrefabs("Flower_Blue_01", "Flower_Blue_02", "Flower_White", "Flower_Purple", "Flower_Pink");
            GameObject[] meadows = IdyllicPrefabs("FlowerMeadow_Blue", "FlowerMeadow_BluePurple", "FlowerMeadow_White", "FlowerMeadow_Purple", "FlowerMeadow_Pink");
            int placed = 0;
            for (int attempt = 0; attempt < 12000 && placed < 320; attempt++)
            {
                float z = DR(-2f, PathEnd), side = dressRng.Next(2) == 0 ? -1f : 1f;
                float x = PathX(z) + side * (2.4f + Mathf.Pow((float)dressRng.NextDouble(), 1.6f) * 6f);
                float h = TerrainY(x, z);
                if (h < WaterY + 0.25f || !Open(x, z, 2.4f, 0f)) continue;
                bool patch = dressRng.Next(5) < 2;
                GameObject[] set = patch ? meadows : single;
                if (set.Length == 0) continue;
                Plant(DPick(set), new Vector3(x, h - 0.03f, z), patch ? DR(1.8f, 3f) : DR(1f, 1.5f), flowers);
                placed++;
            }
        }

        static void DressRocks(Transform rocks)
        {
            GameObject[] set = IdyllicPrefabs("Rock_Medium_01", "Rock_Medium_02", "Rock_Medium_03", "Stone_Medium_01", "Stone_Medium_02", "Stone_Medium_03");
            if (set.Length == 0) return;
            int placed = 0;
            for (int attempt = 0; attempt < 3000 && placed < 30; attempt++)
            {
                float z = DR(0f, PathEnd), side = dressRng.Next(2) == 0 ? -1f : 1f, x = PathX(z) + side * DR(26f, 40f);
                float h = TerrainY(x, z);
                if (h < WaterY - 0.3f || !Open(x, z, 12f, 1f)) continue;
                Plant(DPick(set), new Vector3(x, h - 0.4f, z), DR(0.9f, 1.6f), rocks);
                placed++;
            }
        }

        static void DressButterflies(Transform root)
        {
            var spawner = AssetDatabase.LoadAssetAtPath<GameObject>(Idyllic + "ButterflySpawnArea.prefab");
            if (spawner == null) return;
            foreach (int i in new[] { 0, 2 })
            {
                Vector2 at = Bay(i);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(spawner, root);
                go.transform.position = new Vector3(at.x, TerrainY(at.x, at.y) + 1.2f, at.y);
                foreach (var c in go.GetComponentsInChildren<Collider>(true)) c.isTrigger = true;
            }
        }
    }
}
