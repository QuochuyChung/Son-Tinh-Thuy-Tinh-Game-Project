using System.Collections.Generic;
using System.IO;
using SonTinhThuyTinh.Characters;
using SonTinhThuyTinh.Flow;
using SonTinhThuyTinh.Player;
using SonTinhThuyTinh.Quest;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using static SonTinhThuyTinh.EditorTools.SonTinhMapBuilder;

namespace SonTinhThuyTinh.EditorTools
{
    // Builds Assets/Scenes/Map_ThuyTinh.unity: a causeway winding through a marsh, ~200 m long, with three landmarks (docs/progress.md 8.3):
    // a river crossing on a raft of floating logs, an island in the middle of a deep lake and a flooded shrine. Since 09/10 the island is the
    // arena of Sấu Chín Đuôi, the nine-tailed crocodile (SauChinDuoiBuilder, docs/task-sau-chin-duoi.md), and this map's only gift is the pearl
    // it guards (Minh châu đáy vực, Quest_SinhLe_ThuyTinh): the three walk-in pickups of the Sơn Tinh gifts are switched off here.
    // Same recipe as SonTinhMapBuilder (copy of Sandbox_Combat plus generated terrain, props, lighting, exit), with water instead of mountains.
    // Needs the Asset Store packs from docs/progress.md 9.6.
    public static partial class ThuyTinhMapBuilder
    {
        const string ScenePath = "Assets/Scenes/Map_ThuyTinh.unity";
        const string SandboxPath = "Assets/Scenes/Sandbox_Combat.unity";
        const string DataDir = "Assets/Art/Maps/ThuyTinh";
        const string TerrainDataPath = DataDir + "/Terrain_ThuyTinh.asset";
        const string Pack = "Assets/TriForge Assets/Fantasy Worlds - DEMO Content/";
        const string PackCommon = "Assets/TriForge Assets/Fantasy Worlds - DEMO Common Files/";
        const string GeneratedRoot = "Generated";

        const float Width = 220f, Length = 370f, Height = 60f, ZMin = -25f;
        const int HeightRes = 1025, AlphaRes = 512;
        const float PathEnd = 295f, WallHeight = 24f;
        const float ValleyHalfWidth = 30f;                        // the marsh spreads this far on each side of the path before the walls start
        const float WaterY = 2.4f;                                // water surface; the path stays about 0.75 m above it
        const float ChannelStart = 62f, ChannelEnd = 82f;         // the river crossing
        static readonly float[] StopZ = { 92f, 175f, 252f };      // bank beyond the raft, the crocodile's island, flooded shrine
        const float IslandRadius = 16f;                           // flat dry top of the island (the arena); the shore slopes down to 19.5 m
        const float ArenaRadius = 14.5f;                          // how far from the centre the crocodile may go
        // z along the path, lateral offset (negative = left), radius of the dry islet. Each carries a stilt house or a cluster of stones.
        static readonly Vector3[] Bays = { new(40f, -30f, 9f), new(135f, 32f, 9f), new(215f, -31f, 9f) };

        static float slabTop;

        const string WaterWorksMaterial = "Assets/WaterWorks/Materials/SSR_Water.mat";

        // Which water the map uses: WaterWorks' screen-space reflection shader when the pack is there, otherwise "Simple stylized water".
        static bool preferWaterWorks = true;

        [MenuItem("Tools/Son Tinh Thuy Tinh/Build Thuy Tinh Map")]
        public static void Build() => Build(true);

        [MenuItem("Tools/Son Tinh Thuy Tinh/Build Thuy Tinh Map (Simple stylized water)")]
        static void BuildWithSimpleWater() => Build(false);

        public static void Build(bool waterWorks)
        {
            preferWaterWorks = waterWorks;
            if (EditorApplication.isPlaying) { Debug.LogError("Stop Play mode first."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (!File.Exists(ScenePath)) AssetDatabase.CopyAsset(SandboxPath, ScenePath);
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            rng = new System.Random(20261004);

            DestroyByName("Ground");
            DestroyByName("Pillars");
            DestroyByName("DebugHUD");
            DestroyByName(GeneratedRoot);

            Terrain terrain = BuildTerrain();
            var root = new GameObject(GeneratedRoot).transform;

            Vector3 spawn = Ground(PathX(0f), 0f) + Vector3.up * 0.15f;
            float yaw = Mathf.Atan2(PathX(8f) - PathX(0f), 8f) * Mathf.Rad2Deg;

            SetupPlayer(spawn, yaw);
            SetupLighting();
            BuildWater(root);
            ScatterProps(root);
            BuildStops(root);
            BuildTheme(root);
            BuildExit(root);
            SetupGiftPickups();
            BuildBoss(root);
            ManyFinnedSharkSetup.AddToMap(root);
            NineLeggedTurtleSetup.AddToMap(root);
            Dress(terrain);
            BuildMapHud();

            RegisterScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Map_ThuyTinh built.");
        }

        // ---------------------------------------------------------------- path and height field

        static float PathX(float z)
        {
            float ramp = Smooth(0f, 50f, z);
            return ramp * (13f * Mathf.Sin(z * 0.02f + 0.6f) + 7f * Mathf.Sin(z * 0.055f + 2.1f));
        }

        static Vector2 Stop(int i) => new(PathX(StopZ[i]), StopZ[i]);

        static Vector2 Bay(int i) => new(PathX(Bays[i].x) + Bays[i].y, Bays[i].x);

        static Vector2 Tangent(float z) => new Vector2((PathX(z + 1f) - PathX(z - 1f)) / 2f, 1f).normalized;

        static float GroundHeight(float x, float z)
        {
            float d = Mathf.Abs(x - PathX(z));
            var p = new Vector2(x, z);
            float r1 = Vector2.Distance(p, Stop(1)), r2 = Vector2.Distance(p, Stop(2));
            float n1 = (Mathf.PerlinNoise(x * 0.13f + 3f, z * 0.13f + 77f) - 0.5f) * 2f;

            // marsh floor: deeper around the island, with grassy hummocks away from the path, the crossing and the stops
            float crossing = Smooth(ChannelStart - 12f, ChannelStart - 4f, z) * (1f - Smooth(ChannelEnd + 4f, ChannelEnd + 12f, z));
            float hummock = Smooth(0.52f, 0.72f, Mathf.PerlinNoise(x * 0.05f + 20f, z * 0.05f + 40f)) * 2.7f
                          * Smooth(7f, 11f, d) * Smooth(26f, 34f, r1) * Smooth(16f, 24f, r2) * (1f - crossing);
            float floor = WaterY - 1.5f + 0.25f * n1 - 0.9f * (1f - Smooth(22f, 38f, r1)) + hummock;   // the deep lake round the island

            float pathHeight = WaterY + 0.75f + 0.1f * n1;
            float h = Mathf.Lerp(pathHeight, floor, Smooth(3f, 8f, d));

            // river crossing: the whole width dips below the water; slopes stay gentle (about 14 degrees) so nobody gets stuck
            float channel = Smooth(ChannelStart - 6f, ChannelStart + 4f, z) * (1f - Smooth(ChannelEnd - 4f, ChannelEnd + 6f, z));
            h = Mathf.Lerp(h, Mathf.Min(h, WaterY - 1.7f), channel);

            h = Mathf.Max(h, WaterY + 0.8f - 3.8f * Smooth(IslandRadius, IslandRadius + 3.5f, r1));   // the island (the crocodile's arena)
            h = Mathf.Lerp(h, WaterY - 0.55f, 1f - Smooth(10f, 16f, r2));      // the flooded shrine, knee deep

            // dry islets in the marsh, and the walls step back around them
            float bayOpen = 0f;
            for (int i = 0; i < Bays.Length; i++)
            {
                float rb = Vector2.Distance(p, Bay(i));
                h = Mathf.Max(h, WaterY + 0.9f - 3.8f * Smooth(Bays[i].z, Bays[i].z + 3.5f, rb));
                bayOpen = Mathf.Max(bayOpen, 1f - Smooth(Bays[i].z + 4f, Bays[i].z + 18f, rb));
            }

            float wobble = (Mathf.PerlinNoise(x * 0.05f, z * 0.05f + 50f) - 0.5f) * 2f * 4f;
            float wallStart = ValleyHalfWidth + 26f * bayOpen;
            h += WallHeight * Mathf.Pow(Mathf.Clamp01((d + wobble - wallStart) / 24f), 1.4f);
            h += WallHeight * Mathf.Pow(Mathf.Clamp01((-3f - z) / 14f), 1.4f);
            h += WallHeight * Mathf.Pow(Mathf.Clamp01((z - (PathEnd + 3f)) / 10f), 1.4f);   // closed right behind the exit gate
            h += ClimbHeight * Smooth(ClimbStart, ClimbEnd, z);   // the way up to the palace gate, out of the water
            return h;
        }

        static Vector3 Ground(float x, float z) => new(x, GroundHeight(x, z), z);

        static Vector3 Beside(float z, float lateral) => Ground(PathX(z) + lateral, z);

        // ---------------------------------------------------------------- terrain and water

        static Terrain BuildTerrain()
        {
            Directory.CreateDirectory(DataDir);
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath);
            if (data == null)
            {
                data = new TerrainData();
                AssetDatabase.CreateAsset(data, TerrainDataPath);
            }

            data.heightmapResolution = HeightRes;
            data.size = new Vector3(Width, Height, Length);
            var heights = new float[HeightRes, HeightRes];
            for (int j = 0; j < HeightRes; j++)
                for (int i = 0; i < HeightRes; i++)
                {
                    float x = -Width * 0.5f + Width * i / (HeightRes - 1);
                    float z = ZMin + Length * j / (HeightRes - 1);
                    heights[j, i] = Mathf.Clamp01(GroundHeight(x, z) / Height);
                }
            data.SetHeights(0, 0, heights);

            TerrainLayer[] layers = { FindLayer("TL_fwOF_Grass_01"), FindLayer("TL_fwOF_Soil_01"), FindLayer("TL_fwOF_RockSurf_02"), FindLayer("TL_fwOF_TerrainMoss_01") };
            data.terrainLayers = System.Array.FindAll(layers, l => l != null);
            data.alphamapResolution = AlphaRes;
            if (data.terrainLayers.Length == 4) Paint(data);

            Terrain terrain = Object.FindFirstObjectByType<Terrain>();
            if (terrain == null)
            {
                GameObject go = Terrain.CreateTerrainGameObject(data);
                go.name = "Terrain";
                terrain = go.GetComponent<Terrain>();
            }
            terrain.terrainData = data;
            terrain.GetComponent<TerrainCollider>().terrainData = data;
            terrain.transform.position = new Vector3(-Width * 0.5f, 0f, ZMin);
            terrain.heightmapPixelError = 4f;
            terrain.basemapDistance = 400f;
            EditorUtility.SetDirty(data);
            return terrain;
        }

        // Soil on the path and under the water, moss along the waterline, rock on steep walls, grass on the rest.
        static void Paint(TerrainData data)
        {
            int res = data.alphamapResolution;
            var map = new float[res, res, 4];
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                {
                    float u = (x + 0.5f) / res, v = (y + 0.5f) / res;
                    float wx = -Width * 0.5f + u * Width, wz = ZMin + v * Length;
                    float h = GroundHeight(wx, wz);
                    float d = Mathf.Abs(wx - PathX(wz));
                    float slope = data.GetSteepness(u, v);

                    float wet = 1f - Smooth(WaterY - 0.1f, WaterY + 0.45f, h);
                    float path = 1f - Smooth(1.6f, 3.6f, d);
                    float rock = Smooth(28f, 44f, slope);
                    float soil = Mathf.Max(wet * 0.95f, path) * (1f - rock);
                    float moss = (1f - Smooth(0.15f, 0.9f, Mathf.Abs(h - (WaterY + 0.25f)))) * 0.75f * (1f - path) * (1f - rock);
                    float grass = Mathf.Max(0.0001f, 1f - soil - rock - moss);
                    float sum = grass + soil + rock + moss;
                    map[y, x, 0] = grass / sum;
                    map[y, x, 1] = soil / sum;
                    map[y, x, 2] = rock / sum;
                    map[y, x, 3] = moss / sum;
                }
            data.SetAlphamaps(0, 0, map);
        }

        static Material FindWaterMaterial()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Houidisoft technology" }))
                return AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            Debug.LogWarning("Water material not found (is the Houidisoft 'Simple stylized water' pack imported?).");
            return null;
        }

        // Our own copy of WaterWorks' SSR water (reflections, refraction, shore foam), tinted blue-green. Null when the pack is not imported.
        static Material EnsureWaterWorksMaterial()
        {
            const string path = DataDir + "/Mat_WaterWorks.mat";
            var sample = AssetDatabase.LoadAssetAtPath<Material>(WaterWorksMaterial);
            if (sample == null) return null;   // the pack is not imported (our copy would render magenta: no shader)
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Directory.CreateDirectory(DataDir);
                material = new Material(sample);
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_Color", new Color(0.04f, 0.30f, 0.40f, 1f));
            material.SetColor("_EdgeColor", new Color(0.30f, 0.62f, 0.62f, 1f));
            material.SetColor("_FoamColor", Color.white);
            material.SetFloat("_Caustic_Strength", 0.15f);   // the default (2) turns the whole surface into glitter
            material.SetFloat("_NormalStrength", 0.06f);
            EditorUtility.SetDirty(material);
            return material;
        }

        // Our own copy of the pack's sample water, tuned: thinner shore foam, a deeper blue.
        static Material EnsureWaterMaterial()
        {
            const string path = DataDir + "/Mat_Water.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Material sample = FindWaterMaterial();
                if (sample == null) return null;
                Directory.CreateDirectory(DataDir);
                material = new Material(sample);
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetFloat("_FoamDistance", 0.45f);
            material.SetFloat("_WaterDepth", 1.8f);
            material.SetColor("_ShallowColor", new Color(0.45f, 0.78f, 0.82f, 0.1f));
            material.SetColor("_DeepColor", new Color(0.03f, 0.30f, 0.46f, 1f));
            EditorUtility.SetDirty(material);
            return material;
        }

        static void BuildWater(Transform root)
        {
            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "Water";
            plane.transform.SetParent(root, false);
            plane.transform.position = new Vector3(0f, WaterY, ZMin + Length * 0.5f);
            plane.transform.localScale = new Vector3(Width / 10f * 1.2f, 1f, Length / 10f * 1.2f);
            Object.DestroyImmediate(plane.GetComponent<Collider>());   // water is only a surface, nothing to stand on
            var renderer = plane.GetComponent<MeshRenderer>();
            Material material = preferWaterWorks ? WaterMaterial() : EnsureWaterMaterial();
            if (material != null) renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        // ---------------------------------------------------------------- player, camera, lighting

        static void SetupPlayer(Vector3 spawn, float yaw)
        {
            var spawner = Object.FindFirstObjectByType<PlayerSpawner>();
            spawner.transform.SetPositionAndRotation(spawn, Quaternion.Euler(0f, yaw, 0f));
            var so = new SerializedObject(spawner);
            so.FindProperty("fallbackCharacter").enumValueIndex = System.Array.IndexOf(System.Enum.GetNames(typeof(CharacterId)), nameof(CharacterId.ThuyTinh));
            so.FindProperty("debugHud").objectReferenceValue = null;
            so.ApplyModifiedProperties();

            Quaternion look = Quaternion.Euler(10f, yaw, 0f);
            Vector3 behind = spawn + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 2.76f, -4.35f);
            foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                camera.transform.SetPositionAndRotation(behind, look);
                camera.farClipPlane = 900f;
            }
            var cinemachine = GameObject.Find("PlayerFollowCamera");
            if (cinemachine != null) cinemachine.transform.SetPositionAndRotation(behind, look);
        }

        // Cooler and mistier than the mountain map.
        static void SetupLighting()
        {
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional)
                {
                    light.transform.rotation = Quaternion.Euler(50f, 38f, 0f);
                    light.color = new Color(0.86f, 0.93f, 1f);
                    light.intensity = 2f;
                    light.shadows = LightShadows.Soft;
                    light.shadowStrength = 0.72f;
                }

            var sky = AssetDatabase.LoadAssetAtPath<Material>(Pack + "Materials/M_fwOF_SkyBox_01.mat");
            if (sky != null) RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.7f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0042f;
            RenderSettings.fogColor = new Color(0.58f, 0.72f, 0.80f);

            var volume = Object.FindFirstObjectByType<Volume>();
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Pack + "Scenes/URP/fwOF_FreeDemo_OldForest/Global Volume Profile.asset");
            if (volume != null && profile != null) volume.sharedProfile = profile;

            var wind = AssetDatabase.LoadAssetAtPath<GameObject>(PackCommon + "TriForge Wind Controller.prefab");
            DestroyByName("TriForge Wind Controller");
            if (wind != null) PrefabUtility.InstantiatePrefab(wind);
        }

        // ---------------------------------------------------------------- props

        static bool NearStops(float x, float z, float extra)
        {
            var p = new Vector2(x, z);
            bool crossing = z > ChannelStart - 10f && z < StopZ[0] + 8f && Mathf.Abs(x - PathX(z)) < 24f + extra;
            for (int i = 0; i < Bays.Length; i++)
                if (Vector2.Distance(p, Bay(i)) < Bays[i].z + 3f + extra) return true;
            return crossing || Vector2.Distance(p, Stop(1)) < IslandRadius + 7f + extra || Vector2.Distance(p, Stop(2)) < 17f + extra
                || (z > PathEnd - 8f && z < PathEnd + 8f && Mathf.Abs(x - PathX(z)) < 12f + extra);
        }

        static void ScatterProps(Transform root)
        {
            GameObject tree = Load("P_fwOF_Tree_M_2.prefab"), sapling = Load("P_fwOF_TreeSapling_02B.prefab"), rock = Load("P_fwOF_Rock_01.prefab"),
                stone = Load("P_fwOF_Stone_01.prefab"), grass = Load("P_fwOF_Grass_M_1.prefab"), plant = Load("P_fwOF_ForestPlant_B_02.prefab");
            Transform trees = Group(root, "Trees"), rocks = Group(root, "Rocks"), small = Group(root, "Undergrowth");

            // big trees only on dry ground (hummocks and the foot of the walls), off the path
            var placed = new List<Vector2>();
            for (int attempt = 0; attempt < 80000 && placed.Count < 230 && tree != null; attempt++)
            {
                float z = R(-14f, PathEnd + 10f), x = PathX(z) + R(-58f, 58f);
                float h = GroundHeight(x, z), d = Mathf.Abs(x - PathX(z));
                if (h < WaterY + 0.45f || d < 5.5f || Mathf.Abs(x) > Width * 0.5f - 4f || NearStops(x, z, 2f)) continue;
                var p = new Vector2(x, z);
                if (placed.Exists(o => Vector2.Distance(o, p) < 9f)) continue;
                placed.Add(p);
                Place(tree, new Vector3(x, h - 0.2f, z), R(0f, 360f), Vector3.one * R(0.8f, 1.25f), trees, false);
            }

            for (int i = 0; i < 700 && rock != null && rocks.childCount < 110; i++)
            {
                float z = R(-10f, PathEnd + 8f), x = PathX(z) + R(-40f, 40f);
                float h = GroundHeight(x, z);
                if (h < WaterY - 0.7f || Mathf.Abs(x - PathX(z)) < 4.5f || NearStops(x, z, 0f)) continue;
                float s = R(2.2f, 6f);
                Place(rock, new Vector3(x, h - s * 0.15f, z), R(0f, 360f), new Vector3(s, s * R(0.7f, 1.2f), s), rocks, false);
            }
            ScatterSmall(stone, 380, small, -0.4f, 1.5f, 4f, 34f, 1.2f);
            ScatterSmall(sapling, 200, small, 0.5f, 1.6f, 3f, 34f, 3.5f);
            ScatterSmall(grass, 900, small, 0.2f, 3.5f, 6f, 28f, 1.8f);
            ScatterSmall(grass, 800, small, -0.9f, 5.5f, 8.5f, 28f, 2.2f, WaterY + 0.25f);   // reeds standing in the shallows
            ScatterSmall(plant, 400, small, 0.3f, 3f, 5.5f, 30f, 2.2f);
        }

        // Scatters `count` tries of a prefab where the ground is at least minHeight above the water (and below maxHeight if given).
        static void ScatterSmall(GameObject prefab, int count, Transform parent, float minAboveWater, float minScale, float maxScale, float spread, float minPathDistance, float maxHeight = float.MaxValue)
        {
            if (prefab == null) return;
            for (int i = 0; i < count * 3 && parent.childCount < 100000; i++)
            {
                float z = R(-8f, PathEnd + 6f), x = PathX(z) + R(-spread, spread);
                float h = GroundHeight(x, z);
                if (h < WaterY + minAboveWater || h > maxHeight || Mathf.Abs(x - PathX(z)) < minPathDistance || NearStops(x, z, -6f)) continue;
                Place(prefab, new Vector3(x, h, z), R(0f, 360f), Vector3.one * R(minScale, maxScale), parent, true);
                if (--count <= 0) break;
            }
        }

        // Top height for a standing stone of the given vertical scale (the rock prefab is ~0.71 m tall) that sinks about 1 m into the ground.
        static float StandingTop(float x, float z, float scaleY) => GroundHeight(x, z) + scaleY * 0.71f - 1f;

        // Instantiates a prefab and moves it so the highest point of its renderers is at topY.
        static GameObject PlaceWithTop(GameObject prefab, float x, float z, float topY, float yaw, Vector3 scale, Transform parent)
        {
            GameObject go = Place(prefab, new Vector3(x, topY, z), yaw, scale, parent, false);
            var bounds = new Bounds();
            bool first = true;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                if (first) bounds = r.bounds; else bounds.Encapsulate(r.bounds);
                first = false;
            }
            if (!first) go.transform.position += Vector3.up * (topY - bounds.max.y);
            return go;
        }

        // ---------------------------------------------------------------- stops, exit, gifts

        static void BuildStops(Transform root)
        {
            GameObject rock = Load("P_fwOF_Rock_01.prefab");
            Transform group = Group(root, "StopLandmarks");

            // 1: the raft of floating logs across the river, laid along the path
            for (float z = ChannelStart - 2f; z <= ChannelEnd + 2.5f; z += 1.12f)
            {
                float x = PathX(z) + R(-0.25f, 0.25f);
                MapDecor.Log(group, new Vector3(x, WaterY - 0.25f, z), R(-5f, 5f) + Mathf.Atan2(PathX(z + 1f) - PathX(z - 1f), 2f) * Mathf.Rad2Deg, R(-2.5f, 2.5f), 5.6f, 1.0f);
            }

            if (rock == null) return;

            // 1: the far bank, two standing stones either side of the gift
            Vector2 s0 = Stop(0), t0 = Tangent(StopZ[0]), side0 = new(t0.y, -t0.x);
            foreach (float sign in new[] { -1f, 1f })
            {
                Vector2 p = s0 + side0 * sign * 4.8f + t0 * 2f;
                PlaceWithTop(rock, p.x, p.y, StandingTop(p.x, p.y, 6.5f), R(0f, 360f), new Vector3(2.4f, 6.5f, 2.4f), group);
            }

            // 2: the crocodile's island, a ring of boulders on its shore with the causeway entering and leaving, and a gap on the lair side
            BuildIsland(group, rock);

            // 3: the flooded shrine, a slab with four pillars and two gate stones
            Vector2 s2 = Stop(2), t2 = Tangent(StopZ[2]), side2 = new(t2.y, -t2.x);
            float yaw2 = Mathf.Atan2(t2.x, t2.y) * Mathf.Rad2Deg;
            PlaceWithTop(rock, s2.x, s2.y, WaterY + 0.45f, yaw2, new Vector3(4.6f, 1.1f, 4.6f), group);
            slabTop = WaterY + 0.45f;
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                {
                    Vector2 p = s2 + side2 * sx * 4.6f + t2 * sz * 4.6f;
                    PlaceWithTop(rock, p.x, p.y, StandingTop(p.x, p.y, 8.5f), R(0f, 360f), new Vector3(2.2f, 8.5f, 2.2f), group);
                }
            foreach (float sign in new[] { -1f, 1f })
            {
                Vector2 p = s2 - t2 * 10f + side2 * sign * 4f;
                PlaceWithTop(rock, p.x, p.y, StandingTop(p.x, p.y, 11f), R(0f, 360f), new Vector3(3.2f, 11f, 3.2f), group);
            }
        }

        static void BuildTheme(Transform root)
        {
            Transform decor = Group(root, "Decor");
            MapDecor.DistantMountains(decor, new Vector3(0f, 0f, 160f), 9, MapDecor.MountainBlue, 300f, 400f, 11);

            // torches at the start, the far bank, the island and the shrine so the way is lit through the mist
            foreach (float side in new[] { -1f, 1f })
            {
                MapDecor.Torch(decor, Beside(5f, side * 3.6f));
                MapDecor.Torch(decor, Beside(ChannelStart - 4f, side * 3.2f));
                MapDecor.Torch(decor, Beside(ChannelEnd + 7f, side * 3.2f));
                MapDecor.Torch(decor, Beside(PathEnd - 4f, side * 5.2f));
            }
            // four torches round the arena, at the diagonals (clear of the causeway and of the lair)
            Vector2 c1 = Stop(1), t1 = Tangent(StopZ[1]), n1 = new(t1.y, -t1.x);
            foreach (float a in new[] { 45f, 135f, 225f, 315f })
            {
                Vector2 dir = t1 * Mathf.Cos(a * Mathf.Deg2Rad) + n1 * Mathf.Sin(a * Mathf.Deg2Rad);
                Vector2 p = c1 + dir * (IslandRadius - 0.8f);
                MapDecor.Torch(decor, Ground(p.x, p.y));
            }

            BuildBays(decor);
            BuildBridges(decor);
            BuildWaterfalls(decor);
            BuildClimbWalk(decor);

            // pale blue fireflies hovering over the water
            for (float z = 25f; z < PathEnd; z += 38f)
                MapDecor.Fireflies(decor, Beside(z, 0f) + Vector3.up * 1.8f, new Vector3(26f, 3.5f, 34f), new Color(0.6f, 0.95f, 1f));
        }

        // Two waterfalls pouring down the walls into the marsh, one on each side of the valley.
        static void BuildWaterfalls(Transform decor)
        {
            Transform group = Group(decor, "Waterfalls");
            foreach (var (z, side) in new[] { (105f, -1f), (185f, 1f) })
            {
                Vector2 from = new(PathX(z) + side * 20f, z), dir = new(side, 0f);
                if (!MapDecor.FindSlope(GroundHeight, from, dir, WaterY, 14f, out Vector2 bottom, out Vector2 top))
                {
                    Debug.LogWarning("No slope high enough for a waterfall near z=" + z);
                    continue;
                }
                MapDecor.Waterfall(group, GroundHeight, bottom, top, 6f, "ThuyTinh" + (side < 0f ? "L" : "R") + z);
            }
        }

        // The dry islets scattered in the marsh: two with a stilt house of the river folk, one with a cluster of standing stones.
        static void BuildBays(Transform decor)
        {
            GameObject rock = Load("P_fwOF_Rock_01.prefab");
            Color teal = new(0.10f, 0.45f, 0.55f), pale = new(0.80f, 0.86f, 0.90f);
            Transform group = Group(decor, "Islets");

            for (int i = 0; i < Bays.Length; i++)
            {
                Vector2 c = Bay(i);
                float y = GroundHeight(c.x, c.y);
                float toPath = Mathf.Sign(-Bays[i].y);   // the door faces the path

                if (i == 1)
                {
                    // the Sunken Grotto (the world map's "Hang Chìm"): a rock arch over a cluster of glowing crystals, a few stones round it
                    if (rock != null)
                    {
                        float pillarTop = 0f;
                        foreach (float sign in new[] { -1f, 1f })
                        {
                            float top = StandingTop(c.x + sign * 3.4f, c.y, 7.5f);
                            pillarTop = Mathf.Max(pillarTop, top);
                            PlaceWithTop(rock, c.x + sign * 3.4f, c.y, top, 90f + sign * 12f, new Vector3(3.2f, 7.5f, 3.2f), group);
                        }
                        PlaceWithTop(rock, c.x, c.y, pillarTop + 0.55f, 90f, new Vector3(10f, 2.4f, 4.4f), group);
                        for (int k = 0; k < 5; k++)
                        {
                            float a = (k * 72f + 36f) * Mathf.Deg2Rad;
                            Vector2 p = c + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * 6.8f;
                            PlaceWithTop(rock, p.x, p.y, StandingTop(p.x, p.y, 3.2f), R(0f, 360f), new Vector3(2.4f, 3.2f, 2.4f), group);
                        }
                    }
                    ClearAround(decor.parent, c, 6.5f);   // no bushes in front of the crystals
                    MapDecor.Crystals(group, new Vector3(c.x, y - 0.05f, c.y), 11, new Color(0.45f, 0.62f, 1f), 2.1f);
                    MapDecor.Crystals(group, new Vector3(c.x - 1.6f, y - 0.05f, c.y + 2.6f), 5, new Color(0.80f, 0.45f, 1f), 1.3f);
                    MapDecor.Torch(group, new Vector3(c.x + toPath * 7.4f, y, c.y + 2.6f));
                }
                else
                {
                    MapDecor.StiltHouse(group, new Vector3(c.x, y + 0.2f, c.y), 90f * toPath);
                    MapDecor.Torch(group, new Vector3(c.x + toPath * 5.5f, y, c.y + 3f));
                    MapDecor.Banner(group, new Vector3(c.x + toPath * 5.5f, y, c.y - 3f), 0f, i == 0 ? teal : pale);
                }
            }
        }

        // Removes scattered trees, rocks and undergrowth (children of the generated root) within `radius` of a spot.
        static void ClearAround(Transform root, Vector2 centre, float radius)
        {
            foreach (string name in new[] { "Trees", "Rocks", "Undergrowth" })
            {
                Transform parent = root.Find(name);
                if (parent == null) continue;
                for (int i = parent.childCount - 1; i >= 0; i--)
                {
                    Vector3 p = parent.GetChild(i).position;
                    if (Vector2.Distance(new Vector2(p.x, p.z), centre) < radius) Object.DestroyImmediate(parent.GetChild(i).gameObject);
                }
            }
        }

        // Plank bridges from the causeway to each islet (the world map draws wooden bridges on this route). The boards have colliders.
        static void BuildBridges(Transform decor)
        {
            Transform group = Group(decor, "Bridges");
            for (int i = 0; i < Bays.Length; i++)
            {
                Vector2 c = Bay(i);
                float towardPath = Mathf.Sign(-Bays[i].y);
                Vector2 near = new(PathX(c.y) - towardPath * 2.6f, c.y);   // just beside the causeway, on the islet's side
                Vector2 far = new(c.x + towardPath * 8.4f, c.y);           // the edge of the islet facing the causeway
                float startY = GroundHeight(near.x, near.y), endY = GroundHeight(far.x, far.y);
                MapDecor.PlankBridge(group, new Vector3(near.x, startY + 0.05f, near.y), new Vector3(far.x, endY + 0.05f, far.y), 2.4f);
            }
        }

        // Boards up the final climb out of the marsh, with torches either side.
        static void BuildClimbWalk(Transform decor)
        {
            var points = new List<Vector2>();
            for (float z = ClimbStart - 4f; z < PathEnd - 2f; z += 6f) points.Add(new Vector2(PathX(z), z));
            points.Add(new Vector2(PathX(PathEnd - 2f), PathEnd - 2f));
            MapDecor.PlankWalk(decor, GroundHeight, points, 3.2f);
            foreach (float z in new[] { ClimbStart + 6f, ClimbStart + 18f })
                foreach (float side in new[] { -1f, 1f })
                    MapDecor.Torch(decor, Beside(z, side * 3.4f));
        }

        static void BuildExit(Transform root)
        {
            GameObject rock = Load("P_fwOF_Rock_01.prefab");
            float z = PathEnd;
            Vector3 center = Ground(PathX(z), z);
            Vector2 t = Tangent(z);
            Vector3 tangent = new(t.x, 0f, t.y), side = new(t.y, 0f, -t.x);
            var gate = new GameObject("Exit_To_Next").transform;
            gate.SetParent(root, false);
            gate.position = center + Vector3.up * 3f;
            gate.rotation = Quaternion.LookRotation(tangent);
            var box = gate.gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(14f, 8f, 3f);

            var trigger = gate.gameObject.AddComponent<SceneTransitionTrigger>();
            var so = new SerializedObject(trigger);
            so.FindProperty("sceneName").stringValue = SceneNames.PalaceMap;   // the plateau of King Hung's palace (PalaceMapBuilder)
            so.FindProperty("requiredQuest").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GiftQuest>(SauChinDuoiBuilder.QuestPath);
            so.ApplyModifiedProperties();

            MapDecor.Gate(root, center - Vector3.up * 0.3f, Mathf.Atan2(tangent.x, tangent.z) * Mathf.Rad2Deg, 8f, new Color(0.10f, 0.45f, 0.55f));

            if (rock == null) return;
            foreach (float sign in new[] { -1f, 1f })
            {
                Vector3 p = center + side * sign * 8.5f;
                Place(rock, new Vector3(p.x, GroundHeight(p.x, p.z) - 0.5f, p.z), R(0f, 360f), new Vector3(4f, 11f, 4f), root, false);
            }
        }

        // ---------------------------------------------------------------- the crocodile's island

        // Lair side of the island: the deep water away from the waterfall at z 185 (which falls on the +x side).
        static Vector2 LairSide() { Vector2 t = Tangent(StopZ[1]); return -new Vector2(t.y, -t.x); }

        static void BuildIsland(Transform group, GameObject rock)
        {
            Vector2 c = Stop(1), t = Tangent(StopZ[1]), lair = LairSide();
            Transform island = Group(group, "CrocodileIsland");
            // boulders round the shore, open where the causeway comes in and goes out and towards the lair (the crocodile climbs out there)
            for (int i = 0; i < 30; i++)
            {
                float a = i * 12f * Mathf.Deg2Rad;
                Vector2 dir = new(Mathf.Sin(a), Mathf.Cos(a));
                if (Mathf.Abs(Vector2.Dot(dir, t)) > Mathf.Cos(24f * Mathf.Deg2Rad) || Vector2.Dot(dir, lair) > Mathf.Cos(28f * Mathf.Deg2Rad)) continue;
                Vector2 p = c + dir * (IslandRadius + 0.9f);   // on the rim: further out the shore drops under the water
                float s = R(3.6f, 5.6f);
                Place(rock, new Vector3(p.x, GroundHeight(p.x, p.y) - 0.4f, p.y), R(0f, 360f), new Vector3(s, s * R(0.8f, 1.2f), s), island, false);
            }
            // two tall stones either side of the lair gap, like a gate the crocodile comes through, and pale-blue crystals (the pearl's
            // glow) at their feet
            Vector2 side = new(lair.y, -lair.x);
            foreach (float sign in new[] { -1f, 1f })
            {
                Vector2 p = c + lair * (IslandRadius - 0.5f) + side * sign * 6.5f;
                PlaceWithTop(rock, p.x, p.y, StandingTop(p.x, p.y, 9f), R(0f, 360f), new Vector3(2.6f, 9f, 2.6f), island);
                Vector2 q = c + lair * (IslandRadius - 2.6f) + side * sign * 4.6f;
                MapDecor.Crystals(island, Ground(q.x, q.y) - Vector3.up * 0.05f, 6, new Color(0.45f, 0.75f, 1f), 1.2f);
            }
            // reeds standing in the shallows round the island, a few stones on its rim
            GameObject grass = Load("P_fwOF_Grass_M_1.prefab"), stone = Load("P_fwOF_Stone_01.prefab");
            for (int i = 0; i < 70 && grass != null; i++)
            {
                float a = R(0f, 360f) * Mathf.Deg2Rad, r = R(IslandRadius + 4f, IslandRadius + 13f);
                Vector2 p = c + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * r;
                if (Mathf.Abs(Vector2.Dot((p - c).normalized, t)) > 0.85f) continue;
                Place(grass, new Vector3(p.x, GroundHeight(p.x, p.y), p.y), R(0f, 360f), Vector3.one * R(2.4f, 3.6f), island, true);
            }
            for (int i = 0; i < 18 && stone != null; i++)
            {
                float a = R(0f, 360f) * Mathf.Deg2Rad;
                Vector2 p = c + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * R(IslandRadius - 1.2f, IslandRadius + 0.5f);
                if (Mathf.Abs(Vector2.Dot((p - c).normalized, t)) > 0.9f) continue;
                Place(stone, new Vector3(p.x, GroundHeight(p.x, p.y), p.y), R(0f, 360f), Vector3.one * R(1.2f, 2.2f), island, true);
            }
            MapDecor.Fireflies(island, Ground(c.x, c.y) + Vector3.up * 2f, new Vector3(30f, 3f, 30f), new Color(0.6f, 0.95f, 1f));
        }

        static void BuildBoss(Transform root)
        {
            Vector2 c = Stop(1), t = Tangent(StopZ[1]), lair = LairSide();
            SauChinDuoiBuilder.BuildArena(root, Ground(c.x, c.y), new Vector3(t.x, 0f, t.y), new Vector3(lair.x, 0f, lair.y), WaterY, ArenaRadius);
        }

        // Reuse the three pickup anchors from Sandbox_Combat. The setup passes below
        // replace the first and last anchors with Map 9 Vay and Rua 9 Chan; the middle
        // anchor remains disabled because the crocodile encounter owns the island.
        static void SetupGiftPickups()
        {
            GameObject container = GameObject.Find("GiftPickups (test)");
            if (container == null) container = GameObject.Find("GiftPickups");
            if (container == null) { Debug.LogWarning("No GiftPickups object in the scene."); return; }
            container.name = "GiftPickups";

            string[] names = { "Pickup_NguaChinHongMao", "Pickup_GaChinCua", "Pickup_VoiChinNga" };
            for (int i = 0; i < names.Length; i++)
            {
                Transform pickup = container.transform.Find(names[i]);
                if (pickup == null) { Debug.LogWarning("Missing " + names[i]); continue; }

                pickup.gameObject.SetActive(false);
                Vector2 s = Stop(i);
                float y = i == 2 ? slabTop : GroundHeight(s.x, s.y);
                pickup.position = new Vector3(s.x, y, s.y);
                pickup.rotation = Quaternion.identity;
                var sphere = pickup.GetComponent<SphereCollider>();
                if (sphere != null) { sphere.isTrigger = true; sphere.radius = 2f; sphere.center = new Vector3(0f, 1.2f, 0f); }
                Transform visual = pickup.Find("Visual");
                if (visual != null) visual.localPosition = new Vector3(0f, visual.Find(RoosterGiftSetup.ChildName) != null ? RoosterGiftSetup.VisualHeight : 1.4f, 0f);   // the chicken gift (RoosterGiftSetup) sits lower than the yellow placeholder

                Transform glow = pickup.Find("Glow");
                if (glow == null)
                {
                    glow = new GameObject("Glow").transform;
                    glow.SetParent(pickup, false);
                }
                glow.localPosition = new Vector3(0f, 2.4f, 0f);
                var light = glow.GetComponent<Light>();
                if (light == null) light = glow.gameObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(0.55f, 0.9f, 1f);
                light.range = 14f;
                light.intensity = 5f;
            }
        }

        // ---------------------------------------------------------------- map HUD

        // The map circle and the full map (docs/progress.md 9.9). The picture is a top-down view of this very scene (the right column of it).
        static void BuildMapHud()
        {
            static GiftItem Gift(string name) => AssetDatabase.LoadAssetAtPath<GiftItem>("Assets/Data/Gifts/Gift_" + name + ".asset");
            MapHudBuilder.Build("THỦY TINH  ·  ĐƯỜNG THỦY", new Color(0.25f, 0.82f, 1f), WorldMapLayout.ThuyTinh, new[]
            {
                (Gift("Map9Vay"), Stop(0)),
                (Gift("MinhChauDayVuc"), Stop(1)),   // the crocodile's island
                (Gift("Rua9Chan"), Stop(2)),
            });
        }

        // Rebuilds only the map UI of this scene (after the world map picture changed).
        public static void RefreshMapHud()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            BuildMapHud();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        // ---------------------------------------------------------------- registration

        static void RegisterScene()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == ScenePath))
            {
                int sandbox = scenes.FindIndex(s => s.path == SandboxPath);
                scenes.Insert(sandbox >= 0 ? sandbox : scenes.Count, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }

            var character = AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/Data/Characters/Character_ThuyTinh.asset");
            if (character != null)
            {
                var so = new SerializedObject(character);
                so.FindProperty("giftBranchScene").stringValue = SceneNames.ThuyTinhMap;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(character);
            }
        }
    }
}
