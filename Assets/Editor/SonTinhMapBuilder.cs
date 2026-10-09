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
using UnityEngine.SceneManagement;

namespace SonTinhThuyTinh.EditorTools
{
    // Builds Assets/Scenes/Map_SonTinh.unity: a winding forest path about 200 m long with three gift stops (docs/progress.md 8.2).
    // The scene starts as a copy of Sandbox_Combat (camera, player spawn, HUD, pause menu, gift pickups), then gets a generated terrain,
    // forest props, lighting and an exit gate. Safe to run again: everything it generates is rebuilt, hand-made objects are left alone.
    // Needs the Asset Store packs from docs/progress.md 9.6 (they are not in git); without them the terrain still builds but has no props.
    public static class SonTinhMapBuilder
    {
        const string ScenePath = "Assets/Scenes/Map_SonTinh.unity";
        const string SandboxPath = "Assets/Scenes/Sandbox_Combat.unity";
        const string DataDir = "Assets/Art/Maps/SonTinh";
        const string TerrainDataPath = DataDir + "/Terrain_SonTinh.asset";
        const string Pack = "Assets/TriForge Assets/Fantasy Worlds - DEMO Content/";
        const string PackCommon = "Assets/TriForge Assets/Fantasy Worlds - DEMO Common Files/";
        const string GeneratedRoot = "Generated";

        // Terrain covers x -Width/2..Width/2 and z ZMin..ZMin+Length; the walkable path runs from z = 0 to PathEnd.
        // The valley is ValleyHalfWidth wide on each side of the path before the walls start; "bays" are side pockets where the walls
        // step back to make room for a village, a stone circle and a grove.
        const float Width = 220f, Length = 370f, Height = 60f, ZMin = -25f;
        const int HeightRes = 1025, AlphaRes = 512;
        const float PathEnd = 295f;
        const float WallHeight = 24f;
        const float ValleyHalfWidth = 30f;
        // The last stretch of the route climbs up to the gate to the palace plateau (the world map shows both routes going "up").
        internal const float ClimbStart = 262f, ClimbEnd = 292f, ClimbHeight = 7f;
        static readonly float[] StopZ = { 85f, 175f, 250f };   // horse (traversal), rooster (arena), elephant (altar)
        // z along the path, lateral offset from the path (negative = left), radius of the flat floor
        static readonly Vector3[] Bays = { new(45f, -36f, 12f), new(130f, 38f, 11f), new(212f, -37f, 12f) };   // village, stone circle, grove

        internal static System.Random rng;
        static float slabTop;   // world height of the altar slab at the third stop, measured after it is placed

        [MenuItem("Tools/Son Tinh Thuy Tinh/Build Son Tinh Map")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("Stop Play mode first."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (!File.Exists(ScenePath)) AssetDatabase.CopyAsset(SandboxPath, ScenePath);
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            rng = new System.Random(20261003);

            // sandbox leftovers (only present on the first run)
            DestroyByName("Ground");
            DestroyByName("Pillars");
            DestroyByName("DebugHUD");
            DestroyByName(GeneratedRoot);

            PlanPools();
            Terrain terrain = BuildTerrain();
            var root = new GameObject(GeneratedRoot).transform;

            Vector3 spawn = Ground(PathX(0f), 0f) + Vector3.up * 0.15f;
            float yaw = Mathf.Atan2(PathX(8f) - PathX(0f), 8f) * Mathf.Rad2Deg;

            SetupPlayer(spawn, yaw);
            SetupLighting();
            ScatterProps(terrain, root);
            BuildStops(root);
            BuildTheme(root);
            BuildExit(root);
            SetupGiftPickups();
            BuildMapHud();

            RegisterScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Map_SonTinh built.");
        }

        // ---------------------------------------------------------------- path and height field

        internal static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        // Centre line of the path, straight for the first ~20 m then swinging left and right.
        static float PathX(float z)
        {
            float ramp = Smooth(0f, 50f, z);
            return ramp * (15f * Mathf.Sin(z * 0.021f) + 6f * Mathf.Sin(z * 0.058f + 1.3f));
        }

        static Vector2 Stop(int i) => new(PathX(StopZ[i]), StopZ[i]);

        static Vector2 Bay(int i) => new(PathX(Bays[i].x) + Bays[i].y, Bays[i].x);

        // The village and the grove each have a waterfall pouring down the wall behind them into a pool on a flat pad.
        static readonly int[] FallBays = { 0, 2 };

        static Vector2 FallFoot(int bay) => Bay(bay) + new Vector2(Mathf.Sign(Bays[bay].y) * 17f, 0f);

        // The Mystic Grove (the stone circle, bay 1) has a small pond beside the glowing stone, in the gap between two standing stones.
        const float PondRadius = 3.1f;
        static Vector2 PondCentre => Bay(1) + new Vector2(1.7f, 4.0f);

        static float PadHeight(int bay) => 3f + Bays[bay].x * 0.018f;   // the base height of the valley floor at that z

        // Where each waterfall's pool lies (indexed like Bays). Planned before the terrain exists, because the basin is dug into the terrain.
        static Vector2[] poolCenters;

        static void PlanPools()
        {
            poolCenters = null;   // GroundHeight must not see half-planned pools
            var centres = new Vector2[Bays.Length];
            foreach (int i in FallBays)
            {
                Vector2 foot = FallFoot(i), dir = new(Mathf.Sign(Bays[i].y), 0f);
                centres[i] = MapDecor.FindSlope(GroundHeight, foot, dir, PadHeight(i), 14f, out Vector2 bottom, out _) ? bottom - dir * 3f : foot;
            }
            poolCenters = centres;
        }

        // Direction of the path (x, z) at height z, unit length.
        static Vector2 Tangent(float z) => new Vector2((PathX(z + 1f) - PathX(z - 1f)) / 2f, 1f).normalized;

        static float GroundHeight(float x, float z)
        {
            float d = Mathf.Abs(x - PathX(z));
            var p = new Vector2(x, z);
            float r0 = Vector2.Distance(p, Stop(0)), r1 = Vector2.Distance(p, Stop(1)), r2 = Vector2.Distance(p, Stop(2));

            // the bays: flat floor in the middle, and the walls step back around them
            float bayFlat = 1f, bayOpen = 0f;
            for (int i = 0; i < Bays.Length; i++)
            {
                float rb = Vector2.Distance(p, Bay(i));
                bayFlat *= Smooth(Bays[i].z, Bays[i].z + 6f, rb);
                bayOpen = Mathf.Max(bayOpen, 1f - Smooth(Bays[i].z + 4f, Bays[i].z + 18f, rb));
            }

            float noise = (Mathf.PerlinNoise(x * 0.022f + 31.7f, z * 0.022f + 7.3f) - 0.5f) * 2f * 3.2f
                        + (Mathf.PerlinNoise(x * 0.075f + 5.1f, z * 0.075f + 91.2f) - 0.5f) * 2f * 1.1f;
            float flat = Smooth(3f, 24f, d) * Smooth(9f, 16f, r1) * Smooth(7f, 13f, r2) * bayFlat;   // path, clearings and bays stay calm
            float h = 3f + z * 0.018f + noise * flat;

            // flat pads at the foot of the waterfalls, where their pools lie
            foreach (int i in FallBays) h = Mathf.Lerp(h, PadHeight(i), 1f - Smooth(5f, 9f, Vector2.Distance(p, FallFoot(i))));
            if (poolCenters != null)   // and the basin dug into the pad for the pool
                foreach (int i in FallBays) h = Mathf.Lerp(h, PadHeight(i) - 0.9f, 1f - Smooth(2.4f, 4.0f, Vector2.Distance(p, poolCenters[i])));

            // steep walls on both sides keep the player in the valley, the closed ends do the same (right behind the exit gate)
            float wobble = (Mathf.PerlinNoise(x * 0.05f, z * 0.05f + 50f) - 0.5f) * 2f * 4f;
            float wallStart = ValleyHalfWidth + 26f * bayOpen;
            h += WallHeight * Mathf.Pow(Mathf.Clamp01((d + wobble - wallStart) / 24f), 1.4f);
            h += WallHeight * Mathf.Pow(Mathf.Clamp01((-3f - z) / 14f), 1.4f);
            h += WallHeight * Mathf.Pow(Mathf.Clamp01((z - (PathEnd + 3f)) / 10f), 1.4f);

            h = Mathf.Lerp(h, PadHeight(1) - 0.9f, 1f - Smooth(PondRadius - 1.1f, PondRadius + 0.3f, Vector2.Distance(p, PondCentre)));   // the grove pond

            h += 3.2f * Mathf.Exp(-(r0 * r0) / (2f * 5f * 5f));   // mound with the first gift on top
            h += ClimbHeight * Smooth(ClimbStart, ClimbEnd, z);   // the way up to the palace gate
            return h;
        }

        static Vector3 Ground(float x, float z) => new(x, GroundHeight(x, z), z);

        // ---------------------------------------------------------------- terrain

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

            TerrainLayer[] layers =
            {
                FindLayer("TL_fwOF_Grass_01"), FindLayer("TL_fwOF_Soil_01"), FindLayer("TL_fwOF_RockSurf_02"), FindLayer("TL_fwOF_TerrainMoss_01"),
            };
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

        internal static TerrainLayer FindLayer(string namePart)
        {
            foreach (string guid in AssetDatabase.FindAssets(namePart + " t:TerrainLayer"))
                return AssetDatabase.LoadAssetAtPath<TerrainLayer>(AssetDatabase.GUIDToAssetPath(guid));
            Debug.LogWarning("Terrain layer not found (is the TriForge pack imported?): " + namePart);
            return null;
        }

        // Grass everywhere, soil on the path and in the clearings, rock on steep walls, patches of moss in the shade of the walls.
        static void Paint(TerrainData data)
        {
            int res = data.alphamapResolution;
            var map = new float[res, res, 4];
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                {
                    float u = (x + 0.5f) / res, v = (y + 0.5f) / res;
                    float wx = -Width * 0.5f + u * Width, wz = ZMin + v * Length;
                    var p = new Vector2(wx, wz);
                    float d = Mathf.Abs(wx - PathX(wz));
                    float slope = data.GetSteepness(u, v);

                    float soil = 1f - Smooth(1.6f, 3.6f, d);
                    soil = Mathf.Max(soil, (1f - Smooth(8f, 11f, Vector2.Distance(p, Stop(1)))) * 0.8f);
                    soil = Mathf.Max(soil, (1f - Smooth(4f, 7f, Vector2.Distance(p, Stop(2)))) * 0.8f);
                    soil = Mathf.Max(soil, (1f - Smooth(6f, 9f, Vector2.Distance(p, Bay(0)))) * 0.75f);   // the village yard
                    soil = Mathf.Max(soil, (1f - Smooth(3f, 6f, Vector2.Distance(p, Bay(1)))) * 0.75f);   // inside the stone circle
                    float rock = Smooth(28f, 44f, slope);
                    float moss = Smooth(0.5f, 0.72f, Mathf.PerlinNoise(wx * 0.06f + 3f, wz * 0.06f + 9f)) * Smooth(7f, 14f, d) * (1f - rock);
                    soil *= 1f - rock;
                    float grass = Mathf.Max(0.0001f, 1f - soil - rock - moss);
                    float sum = grass + soil + rock + moss;
                    map[y, x, 0] = grass / sum;
                    map[y, x, 1] = soil / sum;
                    map[y, x, 2] = rock / sum;
                    map[y, x, 3] = moss / sum;
                }
            data.SetAlphamaps(0, 0, map);
        }

        // ---------------------------------------------------------------- player, camera, lighting

        static void SetupPlayer(Vector3 spawn, float yaw)
        {
            var spawner = Object.FindFirstObjectByType<PlayerSpawner>();
            spawner.transform.SetPositionAndRotation(spawn, Quaternion.Euler(0f, yaw, 0f));
            var so = new SerializedObject(spawner);
            so.FindProperty("fallbackCharacter").enumValueIndex = System.Array.IndexOf(System.Enum.GetNames(typeof(CharacterId)), nameof(CharacterId.SonTinh));
            so.FindProperty("debugHud").objectReferenceValue = null;
            so.ApplyModifiedProperties();

            // start the cameras behind the player, looking down the path
            Quaternion look = Quaternion.Euler(10f, yaw, 0f);
            foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                camera.transform.SetPositionAndRotation(spawn + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 2.76f, -4.35f), look);
                camera.farClipPlane = 900f;   // far enough for the distant mountains
            }
            var cinemachine = GameObject.Find("PlayerFollowCamera");
            if (cinemachine != null) cinemachine.transform.SetPositionAndRotation(spawn + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 2.76f, -4.35f), look);
        }

        static void SetupLighting()
        {
            var sun = Object.FindFirstObjectByType<Light>();
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional) sun = light;
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(46f, -32f, 0f);
                sun.color = new Color(1f, 0.93f, 0.78f);
                sun.intensity = 2.1f;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 0.72f;   // lighter shadows so the player stays readable under the canopy
            }

            var sky = AssetDatabase.LoadAssetAtPath<Material>(Pack + "Materials/M_fwOF_SkyBox_01.mat");
            if (sky != null) RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.8f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0038f;
            RenderSettings.fogColor = new Color(0.60f, 0.72f, 0.66f);

            var volume = Object.FindFirstObjectByType<Volume>();
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Pack + "Scenes/URP/fwOF_FreeDemo_OldForest/Global Volume Profile.asset");
            if (volume != null && profile != null) volume.sharedProfile = profile;

            var wind = AssetDatabase.LoadAssetAtPath<GameObject>(PackCommon + "TriForge Wind Controller.prefab");
            DestroyByName("TriForge Wind Controller");
            if (wind != null) PrefabUtility.InstantiatePrefab(wind);
        }

        // ---------------------------------------------------------------- props

        internal static float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        internal static GameObject Load(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Pack + "Prefabs/" + path);
            if (prefab == null) Debug.LogWarning("Prefab missing (is the TriForge pack imported?): " + path);
            return prefab;
        }

        internal static GameObject Place(GameObject prefab, Vector3 position, float yaw, Vector3 scale, Transform parent, bool isStatic)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = scale;
            if (isStatic) go.isStatic = true;
            return go;
        }

        static bool InClearing(float x, float z, float extra)
        {
            var p = new Vector2(x, z);
            for (int i = 0; i < Bays.Length; i++)
                if (Vector2.Distance(p, Bay(i)) < Bays[i].z + 2f + extra) return true;
            return Vector2.Distance(p, Stop(0)) < 8f + extra || Vector2.Distance(p, Stop(1)) < 15f + extra
                || Vector2.Distance(p, Stop(2)) < 12f + extra || (z > PathEnd - 8f && z < PathEnd + 8f && Mathf.Abs(x - PathX(z)) < 12f + extra);
        }

        static void ScatterProps(Terrain terrain, Transform root)
        {
            GameObject tree = Load("P_fwOF_Tree_M_2.prefab"), sapling = Load("P_fwOF_TreeSapling_02B.prefab"), rock = Load("P_fwOF_Rock_01.prefab"),
                stone = Load("P_fwOF_Stone_01.prefab"), grass = Load("P_fwOF_Grass_M_1.prefab"), plant = Load("P_fwOF_ForestPlant_B_02.prefab");
            Transform trees = Group(root, "Trees"), rocks = Group(root, "Rocks"), small = Group(root, "Undergrowth");

            // big trees: out of the path and the clearings, not too close to each other
            var placed = new List<Vector2>();
            for (int attempt = 0; attempt < 60000 && placed.Count < 300 && tree != null; attempt++)
            {
                float z = R(-14f, PathEnd + 10f), x = PathX(z) + R(-58f, 58f);
                float d = Mathf.Abs(x - PathX(z));
                if (d < 6f || Mathf.Abs(x) > Width * 0.5f - 4f || InClearing(x, z, 2f)) continue;
                var p = new Vector2(x, z);
                if (placed.Exists(o => Vector2.Distance(o, p) < 9f)) continue;
                placed.Add(p);
                Place(tree, Ground(x, z) - Vector3.up * 0.2f, R(0f, 360f), Vector3.one * R(0.8f, 1.25f), trees, false);
            }

            // boulders and stones
            for (int i = 0; i < 150 && rock != null; i++)
            {
                float z = R(-10f, PathEnd + 8f), x = PathX(z) + R(-40f, 40f);
                if (Mathf.Abs(x - PathX(z)) < 4.5f || InClearing(x, z, 0f)) continue;
                float s = R(2.2f, 7f);
                Place(rock, Ground(x, z) - Vector3.up * s * 0.12f, R(0f, 360f), new Vector3(s, s * R(0.7f, 1.2f), s), rocks, false);
            }
            for (int i = 0; i < 400 && stone != null; i++)
            {
                float z = R(-10f, PathEnd + 8f), x = PathX(z) + R(-34f, 34f);
                if (Mathf.Abs(x - PathX(z)) < 1.2f) continue;
                float s = R(1.5f, 4f);
                Place(stone, Ground(x, z) - Vector3.up * 0.05f, R(0f, 360f), Vector3.one * s, small, true);
            }

            // undergrowth, denser close to the path so it reads as a dense forest floor
            for (int i = 0; i < 250 && sapling != null; i++)
            {
                float z = R(-8f, PathEnd + 6f), x = PathX(z) + R(-34f, 34f);
                if (Mathf.Abs(x - PathX(z)) < 3.5f || InClearing(x, z, -4f)) continue;
                Place(sapling, Ground(x, z), R(0f, 360f), Vector3.one * R(1.6f, 3f), small, true);
            }
            for (int i = 0; i < 1400 && grass != null; i++)
            {
                float z = R(-8f, PathEnd + 6f), x = PathX(z) + R(-26f, 26f);
                if (Mathf.Abs(x - PathX(z)) < 1.8f) continue;
                Place(grass, Ground(x, z), R(0f, 360f), Vector3.one * R(3.5f, 6f), small, true);
            }
            for (int i = 0; i < 480 && plant != null; i++)
            {
                float z = R(-8f, PathEnd + 6f), x = PathX(z) + R(-30f, 30f);
                if (Mathf.Abs(x - PathX(z)) < 2.2f) continue;
                Place(plant, Ground(x, z), R(0f, 360f), Vector3.one * R(3f, 5.5f), small, true);
            }
        }

        internal static Transform Group(Transform root, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(root, false);
            return t;
        }

        // ---------------------------------------------------------------- stops, exit, gifts

        // Landmarks around each stop so the three places look different: a rocky mound, a ring of boulders (arena), an altar.
        static void BuildStops(Transform root)
        {
            GameObject rock = Load("P_fwOF_Rock_01.prefab");
            if (rock == null) return;
            Transform group = Group(root, "StopLandmarks");

            // 1: horse, steps up the mound
            Vector2 s0 = Stop(0);
            for (int i = 0; i < 7; i++)
            {
                float a = i * 51f + 20f, r = R(5.5f, 7.5f);
                Vector2 p = s0 + new Vector2(Mathf.Sin(a * Mathf.Deg2Rad), Mathf.Cos(a * Mathf.Deg2Rad)) * r;
                float s = R(2.5f, 4f);
                Place(rock, Ground(p.x, p.y) - Vector3.up * 0.3f, R(0f, 360f), Vector3.one * s, group, false);
            }

            // 2: rooster, ring of boulders with the path entering and leaving
            Vector2 s1 = Stop(1);
            Vector2 tangent = Tangent(StopZ[1]);
            for (int i = 0; i < 20; i++)
            {
                float a = i * 18f * Mathf.Deg2Rad;
                Vector2 dir = new(Mathf.Sin(a), Mathf.Cos(a));
                if (Mathf.Abs(Vector2.Dot(dir, tangent)) > Mathf.Cos(30f * Mathf.Deg2Rad)) continue;
                Vector2 p = s1 + dir * 11.5f;
                float s = R(4f, 6f);
                Place(rock, Ground(p.x, p.y) - Vector3.up * 0.5f, R(0f, 360f), new Vector3(s, s * R(0.8f, 1.1f), s), group, false);
            }

            // 3: elephant, a slab between two standing stones
            Vector2 s2 = Stop(2);
            tangent = Tangent(StopZ[2]);
            Vector2 side = new(tangent.y, -tangent.x);
            foreach (float sign in new[] { -1f, 1f })
            {
                Vector2 p = s2 + side * sign * 4.5f + tangent * 3f;
                Place(rock, Ground(p.x, p.y) - Vector3.up * 0.4f, R(0f, 360f), new Vector3(3.2f, 9f, 3.2f), group, false);
            }
            GameObject slab = Place(rock, Ground(s2.x, s2.y) - Vector3.up * 0.15f, Mathf.Atan2(tangent.x, tangent.y) * Mathf.Rad2Deg, new Vector3(4.2f, 1.1f, 4.2f), group, false);
            slabTop = Ground(s2.x, s2.y).y + 0.8f;
            var slabBounds = new Bounds();
            bool first = true;
            foreach (Renderer r in slab.GetComponentsInChildren<Renderer>())
            {
                if (first) slabBounds = r.bounds; else slabBounds.Encapsulate(r.bounds);
                first = false;
            }
            if (!first) slabTop = slabBounds.max.y;
        }

        static void BuildExit(Transform root)
        {
            GameObject rock = Load("P_fwOF_Rock_01.prefab");
            float z = PathEnd;
            Vector3 center = Ground(PathX(z), z);
            float slopeX = (PathX(z + 1f) - PathX(z - 1f)) / 2f;
            Vector3 tangent = new Vector3(slopeX, 0f, 1f).normalized, side = new(tangent.z, 0f, -tangent.x);
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
            so.FindProperty("requiredQuest").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GiftQuest>("Assets/Data/Gifts/Quest_SinhLe.asset");
            so.ApplyModifiedProperties();

            // the gate (a dinh-style arch with a tiled roof) on top of the climb
            MapDecor.Gate(root, center - Vector3.up * 0.3f, Mathf.Atan2(tangent.x, tangent.z) * Mathf.Rad2Deg, 8f, new Color(0.62f, 0.12f, 0.08f));

            if (rock == null) return;
            foreach (float sign in new[] { -1f, 1f })
            {
                Vector3 p = center + side * sign * 8.5f;
                Place(rock, Ground(p.x, p.z) - Vector3.up * 0.5f, R(0f, 360f), new Vector3(4f, 11f, 4f), root, false);
            }
        }

        // Boards laid up the climb to the gate, with a torch every 12 m.
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

                Vector2 s = Stop(i);
                float y = i == 2 ? slabTop : GroundHeight(s.x, s.y);   // the elephant gift sits on the slab
                pickup.position = new Vector3(s.x, y, s.y);
                pickup.rotation = Quaternion.identity;
                var sphere = pickup.GetComponent<SphereCollider>();
                if (sphere != null) { sphere.isTrigger = true; sphere.radius = 2f; sphere.center = new Vector3(0f, 1.2f, 0f); }
                Transform visual = pickup.Find("Visual");
                if (visual != null) visual.localPosition = new Vector3(0f, visual.Find(RoosterGiftSetup.ChildName) != null ? RoosterGiftSetup.VisualHeight : 1.4f, 0f);   // the chicken gift (RoosterGiftSetup) sits lower than the yellow placeholder
                if (i == 2) VoiChinNgaSetup.ApplyPickupVisual(pickup);

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
                light.color = new Color(1f, 0.78f, 0.35f);
                light.range = 14f;
                light.intensity = 5f;
            }
        }

        // ---------------------------------------------------------------- Son Tinh theme

        // Ground point beside the path at height z, `lateral` metres to the side (x axis).
        static Vector3 Beside(float z, float lateral) => Ground(PathX(z) + lateral, z);

        // The mountain god's world: hazy Tan Vien skyline, bronze drums, torches and banners marking the way, stilt houses of the hill folk, fireflies.
        static void BuildTheme(Transform root)
        {
            Transform decor = Group(root, "Decor");
            MapDecor.DistantMountains(decor, new Vector3(0f, 0f, 160f), 10, MapDecor.MountainGreen, 300f, 400f, 7);

            Color red = new(0.62f, 0.12f, 0.08f), ochre = new(0.78f, 0.58f, 0.18f);

            // the start of the path: a drum, banners either side, torches
            MapDecor.BronzeDrum(decor, Beside(7f, -6.5f), 25f, 1.6f);
            foreach (float side in new[] { -1f, 1f })
            {
                MapDecor.Banner(decor, Beside(2f, side * 7f), 0f, side < 0f ? red : ochre);
                MapDecor.Torch(decor, Beside(5f, side * 4.2f));
                MapDecor.Torch(decor, Beside(17f, side * 4.6f));
            }

            // stop 1, the mound: torches at the foot, banners before it
            foreach (float side in new[] { -1f, 1f })
            {
                MapDecor.Torch(decor, Beside(StopZ[0] - 5f, side * 4.2f));
                MapDecor.Banner(decor, Beside(StopZ[0] - 14f, side * 6.5f), 0f, side < 0f ? ochre : red);
            }

            // stop 2, the arena: four torches inside the boulder ring
            foreach (float dz in new[] { -8f, 8f })
                foreach (float side in new[] { -1f, 1f })
                    MapDecor.Torch(decor, Beside(StopZ[1] + dz, side * 8f));

            // stop 3, the altar: torches round the slab, a bronze drum beside the gift
            foreach (float dz in new[] { -5f, 5f })
                foreach (float side in new[] { -1f, 1f })
                    MapDecor.Torch(decor, Beside(StopZ[2] + dz, side * 5.8f));
            Vector2 s2 = Stop(2);
            MapDecor.BronzeDrum(decor, new Vector3(s2.x + 1.7f, slabTop, s2.y + 0.5f), 0f, 1.1f);

            // the exit: banners outside the gate stones
            foreach (float side in new[] { -1f, 1f })
            {
                MapDecor.Banner(decor, Beside(PathEnd - 2f, side * 10.5f), 0f, side < 0f ? red : ochre);
                MapDecor.Torch(decor, Beside(PathEnd - 4f, side * 5.2f));
            }

            // stilt houses on flat ground off the path, the forest around them cleared
            PlaceHouse(root, decor, 30f, -1f);
            PlaceHouse(root, decor, 105f, 1f);
            PlaceHouse(root, decor, 200f, 1f);

            BuildBays(decor);
            BuildWaterfalls(decor);
            BuildClimbWalk(decor);

            // a boardwalk from the path to the village yard
            Vector2 from = new(PathX(48f) - 2.5f, 48f), to = Bay(0) + new Vector2(5.5f, 0f);
            MapDecor.PlankWalk(decor, GroundHeight, new[] { from, Vector2.Lerp(from, to, 0.5f) + new Vector2(0f, 2f), to }, 2.4f);

            for (float z = 25f; z < PathEnd; z += 40f)
                MapDecor.Fireflies(decor, Beside(z, 0f) + Vector3.up * 2.2f, new Vector3(24f, 4f, 34f), new Color(0.88f, 1f, 0.5f));
        }

        // Mountain streams: a sheet of falling water down the wall behind two of the bays, landing in a pool.
        static void BuildWaterfalls(Transform decor)
        {
            Transform group = Group(decor, "Waterfalls");
            Material pool = EnsurePoolMaterial();
            foreach (int i in FallBays)
            {
                Vector2 foot = FallFoot(i), dir = new(Mathf.Sign(Bays[i].y), 0f);
                if (!MapDecor.FindSlope(GroundHeight, foot, dir, PadHeight(i), 14f, out Vector2 bottom, out Vector2 top))
                {
                    Debug.LogWarning("No slope high enough for a waterfall behind bay " + i);
                    continue;
                }
                MapDecor.Waterfall(group, GroundHeight, bottom, top, 5.5f, "SonTinh" + i);
                Vector2 centre = poolCenters[i];
                MapDecor.Pool(group, new Vector3(centre.x, PadHeight(i) - 0.2f, centre.y), 3.6f, pool);
            }
        }

        // The pool uses our own copy of the "Simple stylized water" sample, clearer and greener than the marsh water.
        static Material EnsurePoolMaterial()
        {
            const string path = DataDir + "/Mat_Pool.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Material sample = null;
                foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Houidisoft technology" }))
                {
                    sample = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                    break;
                }
                if (sample == null) return null;
                material = new Material(sample);
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetFloat("_FoamDistance", 0.5f);
            material.SetFloat("_WaterDepth", 1f);
            material.SetColor("_DeepColor", new Color(0.04f, 0.36f, 0.38f, 1f));
            EditorUtility.SetDirty(material);
            return material;
        }

        // Lowest ground under a footprint, to seat a house or a drum on a floor that is nearly flat.
        static float LowestGround(Vector2 centre, float radius)
        {
            float min = float.MaxValue;
            foreach (Vector2 o in new[] { Vector2.zero, new Vector2(radius, 0f), new Vector2(-radius, 0f), new Vector2(0f, radius), new Vector2(0f, -radius) })
                min = Mathf.Min(min, GroundHeight(centre.x + o.x, centre.y + o.y));
            return min;
        }

        // The three side pockets of the valley: a hill village, a circle of standing stones and a grove of one ancient tree with banners.
        static void BuildBays(Transform decor)
        {
            GameObject rock = Load("P_fwOF_Rock_01.prefab"), tree = Load("P_fwOF_Tree_M_2.prefab");
            Color red = new(0.62f, 0.12f, 0.08f), ochre = new(0.78f, 0.58f, 0.18f);
            Transform group = Group(decor, "Bays");

            // 0: the village, two stilt houses facing a bronze drum, torches lighting the yard
            Vector2 village = Bay(0);
            foreach (float sign in new[] { -1f, 1f })
            {
                Vector2 house = village + new Vector2(sign * 6.8f, -sign * 2.5f);
                Vector2 away = (house - village).normalized;
                MapDecor.StiltHouse(group, new Vector3(house.x, LowestGround(house, 2.8f) + 0.2f, house.y), Mathf.Atan2(away.x, away.y) * Mathf.Rad2Deg);
            }
            MapDecor.BronzeDrum(group, Ground(village.x, village.y), 0f, 1.5f);
            foreach (Vector2 o in new[] { new Vector2(0f, 4.6f), new Vector2(0f, -4.6f), new Vector2(9f, 6f), new Vector2(-9f, -6f) })
                MapDecor.Torch(group, Ground(village.x + o.x, village.y + o.y));

            // 1: the Mystic Grove (stone circle): a slab with a glowing stone on it, a pond and torches (the world map's "Khu rừng linh")
            Vector2 circle = Bay(1);
            if (rock != null)
            {
                for (int i = 0; i < 8; i++)
                {
                    float a = i * 45f * Mathf.Deg2Rad;
                    Vector2 p = circle + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * 8.5f;
                    Place(rock, Ground(p.x, p.y) - Vector3.up * 0.7f, R(0f, 360f), new Vector3(2.4f, 5.5f, 2.4f), group, false);
                }
                Place(rock, Ground(circle.x, circle.y) - Vector3.up * 0.15f, R(0f, 360f), new Vector3(3.4f, 0.8f, 3.4f), group, false);
            }
            Vector3 slabCentre = Ground(circle.x, circle.y);
            MapDecor.Crystals(group, slabCentre + Vector3.up * 0.55f, 10, new Color(0.30f, 1f, 0.78f), 1.8f);
            Vector2 pond = PondCentre;
            MapDecor.Pool(group, new Vector3(pond.x, PadHeight(1) - 0.2f, pond.y), PondRadius, EnsurePoolMaterial());
            for (int i = 0; i < 3; i++)
            {
                float a = (112.5f + i * 90f) * Mathf.Deg2Rad;
                Vector2 p = circle + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * 5.6f;
                MapDecor.Torch(group, Ground(p.x, p.y));
            }

            // 2: the Tangled Woods camp (the world map's "Rừng Rối"): an ancient tree, a camp fire with three tents round it, banners
            Vector2 grove = Bay(2);
            if (tree != null) Place(tree, Ground(grove.x, grove.y) - Vector3.up * 0.2f, R(0f, 360f), Vector3.one * 1.7f, group, false);
            Vector2 fire = grove + new Vector2(5.5f, -1.5f);
            MapDecor.Campfire(group, Ground(fire.x, fire.y));
            var canvases = new[] { new Color(0.80f, 0.74f, 0.58f), new Color(0.62f, 0.30f, 0.20f), new Color(0.70f, 0.66f, 0.50f) };
            var tents = new List<Vector2>();
            for (int i = 0; i < 3; i++)
            {
                float a = 20f + i * 90f;
                Vector2 p = fire + new Vector2(Mathf.Sin(a * Mathf.Deg2Rad), Mathf.Cos(a * Mathf.Deg2Rad)) * 4.4f;
                tents.Add(p);
                MapDecor.Tent(group, new Vector3(p.x, LowestGround(p, 1.6f) - 0.05f, p.y), a, canvases[i]);
            }
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad;
                Vector2 p = grove + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * 9f;
                if (tents.Exists(t => Vector2.Distance(t, p) < 4.5f) || Vector2.Distance(fire, p) < 5f) continue;
                MapDecor.Banner(group, Ground(p.x, p.y), i * 60f, i % 2 == 0 ? red : ochre);
            }
            foreach (Vector2 o in new[] { new Vector2(8f, 6f), new Vector2(-6f, -6f) })
                MapDecor.Torch(group, Ground(grove.x + o.x, grove.y + o.y));
        }

        static void PlaceHouse(Transform root, Transform decor, float z, float preferredSide)
        {
            // flat enough ground close to the foot of the walls, on the preferred side first
            foreach (float sign in new[] { preferredSide, -preferredSide })
            for (float lateral = 9f; lateral <= 26f; lateral += 1.5f)
            {
                float x = PathX(z) + sign * lateral;
                float min = float.MaxValue, max = float.MinValue;
                foreach (Vector2 o in new[] { Vector2.zero, new Vector2(3f, 2.5f), new Vector2(-3f, 2.5f), new Vector2(3f, -2.5f), new Vector2(-3f, -2.5f) })
                {
                    float h = GroundHeight(x + o.x, z + o.y);
                    min = Mathf.Min(min, h);
                    max = Mathf.Max(max, h);
                }
                if (max - min > 1.5f) continue;

                var position = new Vector3(x, min + 0.2f, z);
                foreach (string group in new[] { "Trees", "Rocks", "Undergrowth" })
                {
                    Transform parent = root.Find(group);
                    if (parent == null) continue;
                    for (int i = parent.childCount - 1; i >= 0; i--)
                    {
                        Vector3 p = parent.GetChild(i).position;
                        if (Vector2.Distance(new Vector2(p.x, p.z), new Vector2(x, z)) < (group == "Trees" ? 9f : 5.5f)) Object.DestroyImmediate(parent.GetChild(i).gameObject);
                    }
                }
                MapDecor.StiltHouse(decor, position, 90f * sign);
                return;
            }
            Debug.LogWarning("No flat spot for a stilt house near z=" + z);
        }

        // ---------------------------------------------------------------- map HUD

        // The map circle and the full map (docs/progress.md 9.9). The picture is a top-down view of this very scene (the left column of it).
        static void BuildMapHud()
        {
            static GiftItem Gift(string name) => AssetDatabase.LoadAssetAtPath<GiftItem>("Assets/Data/Gifts/Gift_" + name + ".asset");
            MapHudBuilder.Build("SƠN TINH  ·  ĐƯỜNG NÚI", new Color(1f, 0.45f, 0.12f), WorldMapLayout.SonTinh, new[]
            {
                (Gift("NguaChinHongMao"), Stop(0)),
                (Gift("GaChinCua"), Stop(1)),
                (Gift("VoiChinNga"), Stop(2)),
            });
        }

        // Rebuilds only the map UI of this scene (after the world map picture changed).
        public static void RefreshMapHud()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
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

            var character = AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/Data/Characters/Character_SonTinh.asset");
            if (character != null)
            {
                var so = new SerializedObject(character);
                so.FindProperty("giftBranchScene").stringValue = SceneNames.SonTinhMap;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(character);
            }
        }

        internal static void DestroyByName(string name)
        {
            GameObject go;
            while ((go = GameObject.Find(name)) != null) Object.DestroyImmediate(go);
        }
    }
}
