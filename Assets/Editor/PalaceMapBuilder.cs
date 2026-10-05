using System.Collections.Generic;
using System.IO;
using SonTinhThuyTinh.Characters;
using SonTinhThuyTinh.Flow;
using SonTinhThuyTinh.Player;
using SonTinhThuyTinh.Quest;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using static SonTinhThuyTinh.EditorTools.SonTinhMapBuilder;

namespace SonTinhThuyTinh.EditorTools
{
    // Builds Assets/Scenes/Map_HungVuong.unity: the plateau where the two gift routes meet and King Hung's palace stands (docs/progress.md 9.10).
    // The middle of the world map. Sơn Tinh arrives from the west gate, Thủy Tinh from the east gate, both walk to the plaza with the
    // bronze drum and on to the palace. The palace does not exist yet: a placeholder (walls, towers, a two-tier hall, a label) stands
    // where it will go, and its gate leads to Sandbox_Combat until the judgement scene exists.
    // Vegetation, terrain layers, sky and sea come from the Asset Store pack "Idyllic Fantasy Nature" (not in git, docs/progress.md 9.6).
    public static class PalaceMapBuilder
    {
        const string ScenePath = "Assets/Scenes/Map_HungVuong.unity";
        const string SandboxPath = "Assets/Scenes/Sandbox_Combat.unity";
        const string DataDir = "Assets/Art/Maps/HungVuong";
        const string TerrainDataPath = DataDir + "/Terrain_HungVuong.asset";
        const string Pack = "Assets/Idyllic Fantasy Nature/";
        const string FontPath = "Assets/Art/Fonts/Roboto-Bold SDF.asset";
        const string GeneratedRoot = "Generated";

        // Terrain: Size x Size metres, its lowest point (sea floor) at BaseY. The plateau top is at Top, the sea surface at SeaY = 0.
        // The sea floor is deep so the water shader shows deep blue (it turns clear in shallows) and the cliffs drop sheer into it.
        const float Size = 300f, BaseY = -50f, TerrainHeight = 80f, Top = 14f, SeaFloor = -45f, SeaY = 0f;
        const int HeightRes = 1025, AlphaRes = 512;
        const float PlateauR = 104f, CliffBand = 30f;
        const float EntryX = 62f;                 // the two gates the routes arrive through, west (Son Tinh) and east (Thuy Tinh)
        const float PlazaR = 15f;                 // cobbled plaza with the bronze drum, in the middle
        // The palace compound (x, z, width, depth); its gate is in the south side. The Meshy citadel (Assets/Art/Palace) takes ModelWidth metres
        // of width, and its depth follows from the model's proportions; without the model the old placeholder fills PlaceholderRect.
        static readonly Rect PlaceholderRect = new(-34f, 38f, 68f, 44f);
        const float ModelWidth = 48f, ModelFront = 36f;
        static Rect Compound = PlaceholderRect;
        static GameObject modelPrefab;
        static float DoorZ => Compound.yMin;

        // Winding road from the west gate through the plaza to the east gate; it meets the plaza at the centre.
        static float RoadZ(float x) => 5f * Mathf.Sin(Mathf.PI * Mathf.Clamp(x, -EntryX, EntryX) / EntryX);

        [MenuItem("Tools/Son Tinh Thuy Tinh/Build Palace Map")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("Stop Play mode first."); return; }
            if (!AssetDatabase.IsValidFolder(Pack.TrimEnd('/'))) { Debug.LogError("Idyllic Fantasy Nature is not imported (docs/progress.md 9.6)."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (!File.Exists(ScenePath)) AssetDatabase.CopyAsset(SandboxPath, ScenePath);
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            rng = new System.Random(20261005);

            DestroyByName("Ground");
            DestroyByName("Pillars");
            DestroyByName("DebugHUD");
            DestroyByName("GiftPickups");
            DestroyByName("GiftPickups (test)");
            DestroyByName("TriForge Wind Controller");
            DestroyByName(GeneratedRoot);

            // the Meshy citadel when it is in the project, otherwise the placeholder; the terrain is levelled under whichever it is
            modelPrefab = PalaceModelSetup.EnsurePrefab();
            Compound = modelPrefab != null ? ModelRect(modelPrefab) : PlaceholderRect;

            Terrain terrain = BuildTerrain();
            var root = new GameObject(GeneratedRoot).transform;

            SetupLighting();
            BuildSea(root);
            Scatter(root);
            BuildPlaza(root);
            BuildRoadside(root);
            if (modelPrefab != null) BuildModel(root); else BuildPlaceholder(root);
            BuildEdgeWall(root);
            BuildEntries(root);
            Transform exit = BuildExit(root);
            SetupPlayer(root);
            BuildMapHud();
            RegisterScene();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Map_HungVuong built (exit trigger at " + exit.position + ").");
        }

        // ---------------------------------------------------------------- height field

        // Radius of the plateau in the direction of (x, z): a wavy coast line.
        static float EdgeRadius(float x, float z)
        {
            float a = Mathf.Atan2(z, x), c = Mathf.Cos(a), s = Mathf.Sin(a);
            return PlateauR + 7f * (Mathf.PerlinNoise(c * 1.6f + 20f, s * 1.6f + 20f) - 0.5f) * 2f
                            + 2.5f * (Mathf.PerlinNoise(c * 5f + 3f, s * 5f + 7f) - 0.5f) * 2f;
        }

        // Distance from the palace compound's rectangle (0 inside).
        static float CompoundDistance(float x, float z)
        {
            float dx = Mathf.Max(Compound.xMin - x, 0f, x - Compound.xMax), dz = Mathf.Max(Compound.yMin - z, 0f, z - Compound.yMax);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        // Distance from the winding road between the two gates.
        static float RoadDistance(float x, float z)
        {
            float dz = Mathf.Abs(z - RoadZ(x)), dx = Mathf.Max(Mathf.Abs(x) - EntryX, 0f);
            return Mathf.Sqrt(dz * dz + dx * dx);
        }

        // Distance from the straight avenue from the plaza to the palace gate.
        static float AvenueDistance(float x, float z)
        {
            float dz = Mathf.Max(0f - z, z - DoorZ, 0f);
            return Mathf.Sqrt(x * x + dz * dz);
        }

        static float GroundHeight(float x, float z)
        {
            float r = Mathf.Sqrt(x * x + z * z);
            float edge = EdgeRadius(x, z);

            // gentle meadow rolling, calm along the roads, in the plaza and around the palace
            float noise = (Mathf.PerlinNoise(x * 0.025f + 10f, z * 0.025f + 20f) - 0.5f) * 2f * 2.4f
                        + (Mathf.PerlinNoise(x * 0.09f + 3f, z * 0.09f + 40f) - 0.5f) * 2f * 0.6f;
            float calm = Smooth(4f, 11f, RoadDistance(x, z)) * Smooth(PlazaR + 1f, PlazaR + 12f, r) * Smooth(3f, 10f, AvenueDistance(x, z))
                       * Smooth(6f, 18f, CompoundDistance(x, z));
            float top = Top + noise * calm;

            // the sea cliffs all round
            float t = Smooth(edge - 2f, edge + CliffBand, r);
            float rough = (Mathf.PerlinNoise(x * 0.07f + 50f, z * 0.07f + 5f) - 0.5f) * 2f * 4f * t * (1f - t) * 4f;
            return Mathf.Lerp(top, SeaFloor, t) + rough;
        }

        static Vector3 Ground(float x, float z) => new(x, GroundHeight(x, z), z);

        // ---------------------------------------------------------------- terrain

        static TerrainLayer Layer(string name)
        {
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(Pack + "Terrain Layer/" + name + ".terrainlayer");
            if (layer == null) Debug.LogWarning("Terrain layer missing: " + name);
            return layer;
        }

        // The pack's grass layer is a strong yellow-green; our own copy (same textures) is shifted toward the green of the gift maps.
        static TerrainLayer GrassLayer()
        {
            const string path = DataDir + "/TL_Grass.terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer == null)
            {
                Directory.CreateDirectory(DataDir);
                if (!AssetDatabase.CopyAsset(Pack + "Terrain Layer/Grass_Layer.terrainlayer", path)) return Layer("Grass_Layer");
                layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            }
            layer.diffuseRemapMin = Vector4.zero;
            layer.diffuseRemapMax = new Vector4(0.60f, 0.88f, 0.52f, 1f);
            EditorUtility.SetDirty(layer);
            return layer;
        }

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
            data.size = new Vector3(Size, TerrainHeight, Size);
            var heights = new float[HeightRes, HeightRes];
            for (int j = 0; j < HeightRes; j++)
                for (int i = 0; i < HeightRes; i++)
                {
                    float x = -Size * 0.5f + Size * i / (HeightRes - 1);
                    float z = -Size * 0.5f + Size * j / (HeightRes - 1);
                    heights[j, i] = Mathf.Clamp01((GroundHeight(x, z) - BaseY) / TerrainHeight);
                }
            data.SetHeights(0, 0, heights);

            TerrainLayer[] layers = { GrassLayer(), Layer("Dirt_Layer"), Layer("Rock_Layer"), Layer("Cobblestone_Layer") };
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
            terrain.transform.position = new Vector3(-Size * 0.5f, BaseY, -Size * 0.5f);
            terrain.heightmapPixelError = 4f;
            terrain.basemapDistance = 500f;
            EditorUtility.SetDirty(data);
            return terrain;
        }

        // Grass; dirt on the roads; cobblestones on the plaza, the avenue and inside the compound; rock on the cliffs.
        static void Paint(TerrainData data)
        {
            int res = data.alphamapResolution;
            var map = new float[res, res, 4];
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                {
                    float u = (x + 0.5f) / res, v = (y + 0.5f) / res;
                    float wx = -Size * 0.5f + u * Size, wz = -Size * 0.5f + v * Size;
                    float r = Mathf.Sqrt(wx * wx + wz * wz), slope = data.GetSteepness(u, v);

                    float rock = Smooth(30f, 46f, slope);
                    float cobble = Mathf.Max(1f - Smooth(PlazaR - 1.5f, PlazaR, r), 1f - Smooth(2.8f, 4.2f, AvenueDistance(wx, wz)));
                    cobble = Mathf.Max(cobble, 1f - Smooth(0f, 1.5f, CompoundDistance(wx, wz)));
                    float dirt = (1f - Smooth(1.8f, 3.4f, RoadDistance(wx, wz))) * 0.95f;
                    dirt = Mathf.Max(dirt, (1f - Smooth(4.2f, 6.5f, AvenueDistance(wx, wz))) * 0.6f);   // trodden verge beside the avenue
                    dirt *= 1f - cobble;
                    cobble *= 1f - rock;
                    dirt *= 1f - rock;
                    float grass = Mathf.Max(0.0001f, 1f - dirt - cobble - rock);
                    float sum = grass + dirt + rock + cobble;
                    map[y, x, 0] = grass / sum;
                    map[y, x, 1] = dirt / sum;
                    map[y, x, 2] = rock / sum;
                    map[y, x, 3] = cobble / sum;
                }
            data.SetAlphamaps(0, 0, map);
        }

        // ---------------------------------------------------------------- lighting, sky, sea

        static void SetupLighting()
        {
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional)
                {
                    light.transform.rotation = Quaternion.Euler(46f, -38f, 0f);
                    light.color = new Color(1f, 0.95f, 0.82f);
                    light.intensity = 2.1f;
                    light.shadows = LightShadows.Soft;
                    light.shadowStrength = 0.75f;
                }

            var sky = AssetDatabase.LoadAssetAtPath<Material>(Pack + "Materials/Skybox/Skybox.mat");
            if (sky != null) RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.6f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0022f;
            RenderSettings.fogColor = new Color(0.68f, 0.86f, 0.90f);

            var volume = Object.FindFirstObjectByType<Volume>();
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Pack + "Demo/Settings/Post-Processing.asset");
            if (volume != null && profile != null) volume.sharedProfile = profile;

            var wind = AssetDatabase.LoadAssetAtPath<GameObject>(Pack + "Prefabs/Code Related/WindControl.prefab");
            DestroyByName("WindControl");
            if (wind != null) PrefabUtility.InstantiatePrefab(wind);
        }

        // The sea all round the plateau and hazy karst islands on the horizon (the same cones as the gift maps, tinted blue).
        static void BuildSea(Transform root)
        {
            Transform decor = Group(root, "Sea");
            var water = AssetDatabase.LoadAssetAtPath<GameObject>(Pack + "Prefabs/Water.prefab");
            if (water != null)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(water, decor);
                go.name = "Sea";
                go.transform.SetPositionAndRotation(new Vector3(0f, SeaY, 0f), Quaternion.identity);
                go.transform.localScale = new Vector3(100f, 1f, 100f);
            }
            else Debug.LogWarning("Water prefab missing (is Idyllic Fantasy Nature imported?).");
            MapDecor.DistantMountains(decor, new Vector3(0f, SeaY + 25f, 0f), 11, MapDecor.MountainBlue, 320f, 430f, 23);
        }

        // ---------------------------------------------------------------- vegetation

        static GameObject Prefab(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Pack + "Prefabs/" + name + ".prefab");
            if (prefab == null) Debug.LogWarning("Prefab missing (is Idyllic Fantasy Nature imported?): " + name);
            return prefab;
        }

        static GameObject[] Prefabs(params string[] names)
        {
            var list = new List<GameObject>();
            foreach (string n in names)
            {
                GameObject p = Prefab(n);
                if (p != null) list.Add(p);
            }
            return list.ToArray();
        }

        static T Pick<T>(T[] items) => items[rng.Next(items.Length)];

        // True when a spot must stay free of trees and bushes: roads, plaza, avenue, palace compound.
        static bool Blocked(float x, float z, float margin)
        {
            float r = Mathf.Sqrt(x * x + z * z);
            return RoadDistance(x, z) < 6f + margin || r < PlazaR + 4f + margin || AvenueDistance(x, z) < 7f + margin
                || CompoundDistance(x, z) < 9f + margin || r > EdgeRadius(x, z) - 6f;
        }

        static void Scatter(Transform root)
        {
            GameObject[] broadleaf = Prefabs("BroadleafTree_01_Green", "BroadleafTree_02_Green", "BroadleafTree_03_Green", "BroadleafTree_04_Green", "BroadleafTree_05_Green");
            GameObject[] blossom = Prefabs("BlossomTree_01", "BlossomTree_02", "BlossomTree_03", "BlossomTree_04", "BlossomTree_05");
            GameObject[] willow = Prefabs("WillowTree_01_Green", "WillowTree_02_Green", "WillowTree_03_Green", "WillowTree_04_Green", "WillowTree_05_Green");
            GameObject[] bushes = Prefabs("Bush_01_01", "Bush_01_02", "Bush_02_01", "Bush_02_02", "Bush_03_01", "Bush_03_02");
            GameObject[] plants = Prefabs("Plant_01", "Plant_02", "Plant_03", "Plant_04", "Plant_05", "Plant_06", "Plant_07", "Plant_08", "Grass_01", "Grass_02", "Grass_03");
            GameObject[] flowers = Prefabs("Flower_Blue_01", "Flower_Blue_02", "Flower_Orange", "Flower_Pink", "Flower_Purple", "Flower_Red", "Flower_White", "Flower_Yellow", "Flower_YellowRed");
            GameObject[] meadows = Prefabs("FlowerMeadow_Blue", "FlowerMeadow_Orange", "FlowerMeadow_Pink", "FlowerMeadow_Red", "FlowerMeadow_White", "FlowerMeadow_RedOrange",
                "FlowerMeadow_OrangePinkRedPurpleBlue", "FlowerMeadow_Purple");
            GameObject[] rocks = Prefabs("Rock_Big_01", "Rock_Big_02", "Rock_Big_03", "Rock_Medium_01", "Rock_Medium_02", "Rock_Medium_03");
            GameObject[] stones = Prefabs("Stone_Medium_01", "Stone_Medium_02", "Stone_Medium_03", "Stones_01", "Stones_02", "Stones_03", "Rock_Small_01", "Rock_Small_02", "Rock_Small_03");

            Transform trees = Group(root, "Trees"), small = Group(root, "Undergrowth"), rockGroup = Group(root, "Rocks");

            // trees, not too close to each other; willows nearer the sea, blossom trees in the open meadow
            var placed = new List<Vector2>();
            for (int attempt = 0; attempt < 40000 && placed.Count < 170 && broadleaf.Length > 0; attempt++)
            {
                float a = R(0f, Mathf.PI * 2f), r = Mathf.Sqrt(R(0.04f, 1f)) * (PlateauR - 8f);
                float x = Mathf.Cos(a) * r, z = Mathf.Sin(a) * r;
                if (Blocked(x, z, 0f)) continue;
                var p = new Vector2(x, z);
                if (placed.Exists(o => Vector2.Distance(o, p) < 11f)) continue;
                placed.Add(p);
                GameObject prefab = r > PlateauR - 30f && willow.Length > 0 && rng.NextDouble() < 0.4 ? Pick(willow)
                    : blossom.Length > 0 && rng.NextDouble() < 0.3 ? Pick(blossom) : Pick(broadleaf);
                Place(prefab, Ground(x, z) - Vector3.up * 0.1f, R(0f, 360f), Vector3.one * R(0.9f, 1.3f), trees, false);
            }

            Scatter(bushes, 230, small, 1.0f, 1.6f, 3f);
            Scatter(plants, 700, small, 0.8f, 1.5f, 2f);
            Scatter(flowers, 500, small, 0.9f, 1.4f, 2f);
            Scatter(meadows, 160, small, 0.8f, 1.5f, 2f);
            Scatter(stones, 160, small, 1.2f, 2.2f, 2f);
            Scatter(rocks, 36, rockGroup, 1.5f, 3.0f, 3f, true);   // boulders on the cliff edge
        }

        // Scatters count prefabs over the plateau where nothing blocks, at a random scale in [minScale, maxScale].
        static void Scatter(GameObject[] prefabs, int count, Transform parent, float minScale, float maxScale, float margin, bool atEdge = false)
        {
            if (prefabs.Length == 0) return;
            for (int attempt = 0; attempt < count * 20 && count > 0; attempt++)
            {
                float a = R(0f, Mathf.PI * 2f);
                float x0 = Mathf.Cos(a), z0 = Mathf.Sin(a);
                float edge = EdgeRadius(x0, z0);
                float r = atEdge ? edge - R(7f, 12f) : Mathf.Sqrt(R(0.02f, 1f)) * (edge - 6f);
                float x = x0 * r, z = z0 * r;
                if (atEdge ? CompoundDistance(x, z) < 8f : Blocked(x, z, atEdge ? 0f : -margin * 0.4f)) continue;
                Place(Pick(prefabs), Ground(x, z), R(0f, 360f), Vector3.one * R(minScale, maxScale), parent, true);
                count--;
            }
        }

        // ---------------------------------------------------------------- plaza and roadside

        static void BuildPlaza(Transform root)
        {
            Transform decor = Group(root, "Plaza");
            Vector3 centre = Ground(0f, 0f);
            MapDecor.BronzeDrum(decor, centre, 0f, 3.4f);
            Color red = new(0.62f, 0.12f, 0.08f), ochre = new(0.78f, 0.58f, 0.18f);
            for (int i = 0; i < 4; i++)
            {
                float a = (45f + i * 90f) * Mathf.Deg2Rad;
                Vector3 p = Ground(Mathf.Sin(a) * 10f, Mathf.Cos(a) * 10f);
                MapDecor.Banner(decor, p, 45f + i * 90f, i % 2 == 0 ? red : ochre);
                float b = (i * 90f) * Mathf.Deg2Rad;
                MapDecor.Torch(decor, Ground(Mathf.Sin(b) * 6.2f, Mathf.Cos(b) * 6.2f));
            }
            MapDecor.Fireflies(decor, centre + Vector3.up * 2f, new Vector3(34f, 4f, 34f), new Color(1f, 0.9f, 0.55f));
        }

        // Torches along the roads from the two gates and banners lining the avenue to the palace.
        static void BuildRoadside(Transform root)
        {
            Transform decor = Group(root, "Roadside");
            Color red = new(0.62f, 0.12f, 0.08f), ochre = new(0.78f, 0.58f, 0.18f);
            foreach (float sign in new[] { -1f, 1f })
                for (float ax = 22f; ax < EntryX - 2f; ax += 14f)
                {
                    float x = sign * ax;
                    foreach (float side in new[] { -1f, 1f })
                        MapDecor.Torch(decor, Ground(x, RoadZ(x) + side * 3.6f));
                }
            for (float z = PlazaR + 2f; z < DoorZ - 3f; z += 7.5f)
                foreach (float side in new[] { -1f, 1f })
                    MapDecor.Banner(decor, Ground(side * 6.2f, z), 0f, side < 0f ? red : ochre);
            foreach (float side in new[] { -1f, 1f })
                MapDecor.Torch(decor, Ground(side * 4.4f, DoorZ - 5f));
        }

        // Each route arrives through its own gate; the character starts just inside it.
        static void BuildEntries(Transform root)
        {
            Transform decor = Group(root, "Entries");
            MapDecor.Gate(decor, Ground(-EntryX - 2f, RoadZ(-EntryX)) - Vector3.up * 0.3f, 90f, 8f, new Color(0.62f, 0.12f, 0.08f));
            MapDecor.Gate(decor, Ground(EntryX + 2f, RoadZ(EntryX)) - Vector3.up * 0.3f, -90f, 8f, new Color(0.10f, 0.45f, 0.55f));
        }

        // ---------------------------------------------------------------- the placeholder palace

        static Transform Box(Transform parent, string name, Vector3 centre, Vector3 size, Material material, bool collider = true)
        {
            return MapDecor.Part(PrimitiveType.Cube, name, parent, centre, size, Quaternion.identity, material, collider).transform;
        }

        // ---------------------------------------------------------------- the Meshy citadel

        // Size of a prefab's meshes at its own scale, in metres (from the asset's hierarchy, so it works before anything is in a scene).
        static Bounds MeasurePrefab(GameObject prefab)
        {
            // measured on a temporary instance at the origin, scale 1, so every node scale and rotation of the FBX hierarchy counts
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetPositionAndRotation(Vector3.zero, prefab.transform.rotation);
            instance.transform.localScale = Vector3.one;
            var bounds = new Bounds();
            bool first = true;
            foreach (MeshFilter mf in instance.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                Bounds mb = mf.sharedMesh.bounds;
                Matrix4x4 toWorld = mf.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    Vector3 p = toWorld.MultiplyPoint3x4(corner);
                    if (first) { bounds = new Bounds(p, Vector3.zero); first = false; } else bounds.Encapsulate(p);
                }
            }
            Debug.Log($"Palace model measured: size {bounds.size}, centre {bounds.center}, root rotation {prefab.transform.rotation.eulerAngles}");
            Object.DestroyImmediate(instance);
            return bounds;
        }

        // Footprint of the citadel on the plateau: ModelWidth wide, as deep as the model's proportions give, its front at ModelFront.
        static Rect ModelRect(GameObject prefab)
        {
            Bounds size = MeasurePrefab(prefab);
            float scale = ModelWidth / Mathf.Max(size.size.x, 0.0001f);
            return new Rect(-ModelWidth * 0.5f, ModelFront, ModelWidth, size.size.z * scale);
        }

        // The Meshy "Crimson Phoenix Citadel" (ArtSource/Meshy/Palace_HungVuong): a walled gate fortress with stairs up to the gate, a pavilion over
        // it and red phoenix banners. Its front (the stairs) faces south, towards the plaza. Every mesh gets a mesh collider so the walls are solid
        // and the gate passage stays open; the exit trigger sits inside the gate.
        static void BuildModel(Transform root)
        {
            var palace = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab, root);
            palace.name = "Palace_HungVuong";
            Bounds local = MeasurePrefab(modelPrefab);
            float scale = ModelWidth / local.size.x;
            palace.transform.localScale = Vector3.one * scale;   // the prefab keeps its own rotation (it stands upright with it)
            // bottom of the model a hair below the plateau, its footprint centred on the compound
            palace.transform.position = new Vector3(Compound.center.x - local.center.x * scale, Top - 0.15f - local.min.y * scale, Compound.center.y - local.center.z * scale);

            foreach (MeshFilter mf in palace.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                mf.gameObject.isStatic = true;
            }

            // bronze drums and torches either side of the stairs
            Transform decor = Group(root, "PalaceFront");
            foreach (float side in new[] { -1f, 1f })
            {
                MapDecor.BronzeDrum(decor, Ground(side * 15f, Compound.yMin - 4f), 0f, 1.8f);
                MapDecor.Torch(decor, Ground(side * 11f, Compound.yMin - 4f));
            }
        }

        // ---------------------------------------------------------------- the placeholder palace (used when the model is missing)

        // Walls, corner towers, a gate and a two-tier hall standing on terraces, all boxes and roof slabs from MapDecor. It is a stand-in:
        // the real palace replaces the object "PalacePlaceholder" (keep the gate position or move the exit trigger with it).
        static void BuildPlaceholder(Transform root)
        {
            Transform palace = Group(root, "PalacePlaceholder");
            float y = Top;
            Material plaster = MapDecor.Lit("Mat_PalacePlaster", new Color(0.90f, 0.84f, 0.68f), 0f, 0.1f);
            Material stone = MapDecor.Lit("Mat_PalaceStone", new Color(0.62f, 0.58f, 0.52f), 0f, 0.15f);
            Material wood = MapDecor.Lit("Mat_RedWood", new Color(0.45f, 0.13f, 0.08f), 0f, 0.2f);
            Material dark = MapDecor.Lit("Mat_PalaceDoor", new Color(0.18f, 0.08f, 0.05f), 0f, 0.1f);
            Material gold = MapDecor.Lit("Mat_Gold", new Color(0.93f, 0.72f, 0.20f), 0.8f, 0.6f);
            Color tile = new(0.30f, 0.17f, 0.12f);

            float x0 = Compound.xMin, x1 = Compound.xMax, z0 = Compound.yMin, z1 = Compound.yMax, cx = Compound.center.x, cz = Compound.center.y;
            const float wallH = 4.4f, wallT = 1.3f, doorHalf = 5f;

            // perimeter wall: north and sides whole, south wall in two halves with the gate opening between them
            Box(palace, "WallNorth", new Vector3(cx, y + wallH * 0.5f, z1), new Vector3(x1 - x0, wallH, wallT), plaster);
            Box(palace, "WallWest", new Vector3(x0, y + wallH * 0.5f, cz), new Vector3(wallT, wallH, z1 - z0), plaster);
            Box(palace, "WallEast", new Vector3(x1, y + wallH * 0.5f, cz), new Vector3(wallT, wallH, z1 - z0), plaster);
            float halfLength = (x1 - x0) * 0.5f - doorHalf;
            Box(palace, "WallSouthW", new Vector3(x0 + halfLength * 0.5f, y + wallH * 0.5f, z0), new Vector3(halfLength, wallH, wallT), plaster);
            Box(palace, "WallSouthE", new Vector3(x1 - halfLength * 0.5f, y + wallH * 0.5f, z0), new Vector3(halfLength, wallH, wallT), plaster);

            // tiled cap along the top of the walls
            foreach (var (name, centre, size) in new[]
            {
                ("CapNorth", new Vector3(cx, y + wallH + 0.2f, z1), new Vector3(x1 - x0 + 1f, 0.4f, wallT + 0.7f)),
                ("CapWest", new Vector3(x0, y + wallH + 0.2f, cz), new Vector3(wallT + 0.7f, 0.4f, z1 - z0 + 1f)),
                ("CapEast", new Vector3(x1, y + wallH + 0.2f, cz), new Vector3(wallT + 0.7f, 0.4f, z1 - z0 + 1f)),
                ("CapSouthW", new Vector3(x0 + halfLength * 0.5f, y + wallH + 0.2f, z0), new Vector3(halfLength + 0.4f, 0.4f, wallT + 0.7f)),
                ("CapSouthE", new Vector3(x1 - halfLength * 0.5f, y + wallH + 0.2f, z0), new Vector3(halfLength + 0.4f, 0.4f, wallT + 0.7f)),
            })
                Box(palace, name, centre, size, MapDecor.Lit("Mat_RoofTile_" + ColorUtility.ToHtmlStringRGB(tile), tile, 0f, 0.25f, true), false);

            // corner towers
            foreach (float sx in new[] { x0, x1 })
                foreach (float sz in new[] { z0, z1 })
                {
                    Box(palace, "Tower", new Vector3(sx, y + 3.6f, sz), new Vector3(5.4f, 7.2f, 5.4f), plaster);
                    MapDecor.Roof(palace, new Vector3(sx, y + 7.2f, sz), 6.6f, 6.6f, 3f, tile);
                    MapDecor.Part(PrimitiveType.Sphere, "Finial", palace, new Vector3(sx, y + 10.4f, sz), Vector3.one * 0.5f, Quaternion.identity, gold);
                }

            // the gate in the south wall
            MapDecor.Gate(palace, new Vector3(cx, y - 0.1f, z0), 0f, doorHalf * 2f - 1f, new Color(0.62f, 0.12f, 0.08f));

            // two terraces and the main hall with a two-tier roof
            Box(palace, "Terrace1", new Vector3(cx, y + 0.5f, 66f), new Vector3(52f, 1f, 34f), stone);
            Box(palace, "Terrace2", new Vector3(cx, y + 1.5f, 68f), new Vector3(42f, 1f, 26f), stone);
            Box(palace, "Steps1", new Vector3(cx, y + 0.25f, 48.2f), new Vector3(12f, 0.5f, 2.4f), stone);
            Box(palace, "Steps2", new Vector3(cx, y + 0.75f, 49.6f), new Vector3(12f, 0.5f, 2.4f), stone);
            Box(palace, "Steps3", new Vector3(cx, y + 1.25f, 51.0f), new Vector3(12f, 0.5f, 2.4f), stone);
            float floor = y + 2f;
            Box(palace, "HallBody", new Vector3(cx, floor + 3.5f, 69f), new Vector3(30f, 7f, 18f), plaster);
            Box(palace, "HallDoor", new Vector3(cx, floor + 2.5f, 69f - 9.05f), new Vector3(6f, 5f, 0.2f), dark, false);
            for (int i = 0; i < 8; i++)
                MapDecor.Part(PrimitiveType.Cylinder, "Column", palace, new Vector3(cx - 14f + i * 4f, floor + 3.5f, 69f - 10.6f), new Vector3(0.9f, 3.5f, 0.9f), Quaternion.identity, wood, true);
            Box(palace, "Lintel", new Vector3(cx, floor + 7.2f, 69f - 10.6f), new Vector3(31f, 0.7f, 1.3f), wood, false);
            MapDecor.Roof(palace, new Vector3(cx, floor + 7.4f, 69f), 36f, 24f, 4.6f, tile);
            Box(palace, "UpperBody", new Vector3(cx, floor + 12.9f, 69f), new Vector3(18f, 5f, 9f), plaster, false);
            MapDecor.Roof(palace, new Vector3(cx, floor + 15.4f, 69f), 24f, 15f, 4.4f, tile);
            MapDecor.Part(PrimitiveType.Sphere, "Finial", palace, new Vector3(cx, floor + 20.2f, 69f), Vector3.one * 0.8f, Quaternion.identity, gold);

            // label, so nobody mistakes the stand-in for the finished building
            var labelObject = new GameObject("Label");
            labelObject.transform.SetParent(palace, false);
            labelObject.transform.position = new Vector3(cx, y + 15f, z0 - 0.5f);
            var label = labelObject.AddComponent<TextMeshPro>();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font != null) label.font = font;
            label.text = "CUNG ĐIỆN VUA HÙNG\n<size=55%>(chỗ giữ chỗ — cung điện thật sẽ thêm sau)</size>";
            label.fontSize = 9f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(1f, 0.92f, 0.62f);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            labelObject.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            // lanterns and drums by the gate
            foreach (float side in new[] { -1f, 1f })
            {
                MapDecor.BronzeDrum(palace, Ground(cx + side * 9f, z0 - 3f), 0f, 1.8f);
                MapDecor.Torch(palace, Ground(cx + side * 7f, z0 - 3f));
            }
        }

        // ---------------------------------------------------------------- edge, exit, player

        // Invisible wall a few metres inside the cliff edge, so nobody walks off the plateau.
        static void BuildEdgeWall(Transform root)
        {
            Transform group = Group(root, "EdgeWall");
            const int count = 72;
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2f / count;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                float r = EdgeRadius(c, s) - 3.5f;   // EdgeRadius only depends on the direction
                var go = new GameObject("Wall_" + i);
                go.transform.SetParent(group, false);
                go.transform.position = new Vector3(c * r, Top + 3f, s * r);
                go.transform.rotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg + 90f, 0f);
                go.AddComponent<BoxCollider>().size = new Vector3(2f * Mathf.PI * r / count * 1.3f, 14f, 1.2f);
            }
        }

        // The palace gate leads on; until the judgement scene exists that is the combat sandbox.
        static Transform BuildExit(Transform root)
        {
            var trigger = new GameObject("Exit_To_Next").transform;
            trigger.SetParent(root, false);
            var box = trigger.gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            if (modelPrefab != null)
            {
                // inside the citadel's gate passage, a little way past the stairs
                trigger.position = new Vector3(Compound.center.x, Top + 3.5f, Compound.yMin + Compound.height * 0.16f);
                box.size = new Vector3(9f, 7f, 3f);
            }
            else
            {
                trigger.position = new Vector3(Compound.center.x, Top + 3f, Compound.yMin + 1.6f);
                box.size = new Vector3(8f, 6f, 2.4f);
            }
            var transition = trigger.gameObject.AddComponent<SceneTransitionTrigger>();
            var so = new SerializedObject(transition);
            so.FindProperty("sceneName").stringValue = SceneNames.Sandbox;   // placeholder until the judgement scene exists
            so.FindProperty("requiredQuest").objectReferenceValue = null;
            so.ApplyModifiedProperties();
            return trigger;
        }

        static void SetupPlayer(Transform root)
        {
            Transform spawns = Group(root, "Spawns");
            Transform west = Spawn(spawns, "Spawn_SonTinh", -EntryX + 5f, 90f), east = Spawn(spawns, "Spawn_ThuyTinh", EntryX - 5f, -90f);

            var spawner = Object.FindFirstObjectByType<PlayerSpawner>();
            spawner.transform.SetPositionAndRotation(west.position, west.rotation);
            var so = new SerializedObject(spawner);
            so.FindProperty("fallbackCharacter").enumValueIndex = System.Array.IndexOf(System.Enum.GetNames(typeof(CharacterId)), nameof(CharacterId.SonTinh));
            so.FindProperty("debugHud").objectReferenceValue = null;
            SerializedProperty points = so.FindProperty("spawnPoints");
            points.arraySize = 2;
            points.GetArrayElementAtIndex(0).FindPropertyRelative("character").enumValueIndex = System.Array.IndexOf(System.Enum.GetNames(typeof(CharacterId)), nameof(CharacterId.SonTinh));
            points.GetArrayElementAtIndex(0).FindPropertyRelative("point").objectReferenceValue = west;
            points.GetArrayElementAtIndex(1).FindPropertyRelative("character").enumValueIndex = System.Array.IndexOf(System.Enum.GetNames(typeof(CharacterId)), nameof(CharacterId.ThuyTinh));
            points.GetArrayElementAtIndex(1).FindPropertyRelative("point").objectReferenceValue = east;
            so.ApplyModifiedProperties();

            // start the cameras behind the player at the west gate, looking along the road
            Quaternion look = Quaternion.Euler(10f, 90f, 0f);
            Vector3 behind = west.position + Quaternion.Euler(0f, 90f, 0f) * new Vector3(0f, 2.76f, -4.35f);
            foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                camera.transform.SetPositionAndRotation(behind, look);
                camera.farClipPlane = 1200f;
            }
            var cinemachine = GameObject.Find("PlayerFollowCamera");
            if (cinemachine != null) cinemachine.transform.SetPositionAndRotation(behind, look);
        }

        static Transform Spawn(Transform parent, string name, float x, float yaw)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.SetPositionAndRotation(Ground(x, RoadZ(x)) + Vector3.up * 0.15f, Quaternion.Euler(0f, yaw, 0f));
            return t;
        }

        // ---------------------------------------------------------------- map HUD and registration

        // On the world map the plateau is the middle of the picture (a top-down view of this very scene).
        static void BuildMapHud()
        {
            MapHudBuilder.Build("CUNG ĐIỆN VUA HÙNG", new Color(1f, 0.82f, 0.30f), WorldMapLayout.Palace, new (GiftItem gift, Vector2 world)[0]);
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

        static void RegisterScene()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == ScenePath)) return;
            int sandbox = scenes.FindIndex(s => s.path == SandboxPath);
            scenes.Insert(sandbox >= 0 ? sandbox : scenes.Count, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
