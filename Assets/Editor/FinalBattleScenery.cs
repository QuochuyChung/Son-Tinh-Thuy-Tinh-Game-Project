using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SonTinhThuyTinh.EditorTools
{
    // The scenery of the final battle, "Valley of the End" style (docs/progress.md 9.15), built by FinalBattleArenaBuilder: a canyon carved
    // in a terrain, a big pool, a bright waterfall pouring through a notch in the back wall and, on two stone ledges at the foot of the
    // wall either side of it, the two giant statues (Sơn Tinh left, Thủy Tinh right) facing each other. The arena platform stands in the
    // pool in front of them (built by FinalBattleArenaBuilder.BuildArenaPlatform).
    //
    // Coordinates: the arena is at the origin, the players start on the z axis and look towards +z, where the waterfall is.
    public static class FinalBattleScenery
    {
        const string DataDir = "Assets/Art/Maps/FinalBattle";
        const string Pack = "Assets/Idyllic Fantasy Nature/";

        // terrain: 220 m x 220 m, from -110..110 in x and -60..160 in z, world height from BaseY to BaseY + TerrainHeight
        const float TerrainSize = 220f, XMin = -110f, ZMin = -60f, BaseY = -6f, TerrainHeight = 96f;
        const int HeightRes = 513, AlphaRes = 512;

        public const float WaterLevel = 0.02f;
        const float BedDepth = -2.2f;
        // the basin (pool) is an ellipse; the cliffs rise outside it
        static readonly Vector2 BasinCentre = new(0f, 6f);
        const float BasinRadiusX = 50f, BasinRadiusZ = 40f;
        const float CliffHeight = 72f, NotchTop = 46f, NotchHalfWidth = 7f;
        // the waterfall: foot at the wall, lip 11 m behind it
        static readonly Vector2 FallFoot = new(0f, 46.5f), FallLip = new(0f, 57f);
        public const float FallWidth = 15f;
        // the statues
        public const float StatueX = 19f, StatueZ = 34f, LedgeHeight = 0.6f, StatueHeight = 28f;

        static float Smooth(float a, float b, float v) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, v));

        // ---------------------------------------------------------------- the ground

        public static float Ground(float x, float z)
        {
            float ex = (x - BasinCentre.x) / BasinRadiusX, ez = (z - BasinCentre.y) / BasinRadiusZ;
            float outside = Mathf.Max(0f, Mathf.Sqrt(ex * ex + ez * ez) - 1f) * 40f;   // metres beyond the edge of the pool, roughly

            float h = BedDepth + Mathf.PerlinNoise(x * 0.09f + 11f, z * 0.09f + 4f) * 0.5f;
            h += CliffHeight * Smooth(0f, 12f, outside);
            h += (Mathf.PerlinNoise(x * 0.045f + 3f, z * 0.045f + 8f) - 0.5f) * 22f * Smooth(8f, 26f, outside);   // ragged cliff tops
            h += (Mathf.PerlinNoise(x * 0.3f, z * 0.3f) - 0.5f) * 2.2f * Smooth(2f, 6f, outside);                  // rough rock face

            // the notch the river comes through: capped at NotchTop, so the water has a lip to pour over
            float notch = (1f - Smooth(NotchHalfWidth, NotchHalfWidth + 5f, Mathf.Abs(x))) * Smooth(36f, 44f, z);
            h = Mathf.Lerp(h, Mathf.Min(h, NotchTop), notch);

            // the two ledges at the foot of the wall that the statues stand on
            foreach (float side in new[] { -1f, 1f })
            {
                float d = Vector2.Distance(new Vector2(x, z), new Vector2(side * StatueX, StatueZ));
                h = Mathf.Lerp(h, LedgeHeight, 1f - Smooth(9f, 13.5f, d));
            }
            return h;
        }

        public static Terrain BuildTerrain(Transform root)
        {
            Directory.CreateDirectory(DataDir);
            string path = DataDir + "/Terrain_FinalBattle.asset";
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
            if (data == null)
            {
                data = new TerrainData();
                AssetDatabase.CreateAsset(data, path);
            }
            data.heightmapResolution = HeightRes;
            data.size = new Vector3(TerrainSize, TerrainHeight, TerrainSize);
            var heights = new float[HeightRes, HeightRes];
            for (int j = 0; j < HeightRes; j++)
                for (int i = 0; i < HeightRes; i++)
                {
                    float x = XMin + TerrainSize * i / (HeightRes - 1);
                    float z = ZMin + TerrainSize * j / (HeightRes - 1);
                    heights[j, i] = Mathf.Clamp01((Ground(x, z) - BaseY) / TerrainHeight);
                }
            data.SetHeights(0, 0, heights);

            var layers = new[] { GrassLayer(), Layer("Dirt_Layer"), Layer("Rock_Layer"), Layer("Dirt_Stone_Layer") };
            data.terrainLayers = layers.Where(l => l != null).ToArray();
            data.alphamapResolution = AlphaRes;
            if (data.terrainLayers.Length == 4) Paint(data);

            GameObject go = Terrain.CreateTerrainGameObject(data);
            go.name = "Terrain_Gorge";
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(XMin, BaseY, ZMin);
            var terrain = go.GetComponent<Terrain>();
            terrain.heightmapPixelError = 4f;
            terrain.basemapDistance = 500f;
            terrain.shadowCastingMode = ShadowCastingMode.On;
            EditorUtility.SetDirty(data);
            return terrain;
        }

        // the greener copy of the pack's grass that the palace map made (the pack's own is a strong yellow); falls back to the original
        static TerrainLayer GrassLayer()
        {
            var tinted = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/Art/Maps/HungVuong/TL_Grass.terrainlayer");
            return tinted != null ? tinted : Layer("Grass_Layer");
        }

        static TerrainLayer Layer(string name)
        {
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(Pack + "Terrain Layer/" + name + ".terrainlayer");
            if (layer == null) Debug.LogWarning("Terrain layer missing (is Idyllic Fantasy Nature imported?): " + name);
            return layer;
        }

        // Rock on the cliffs and under the water, stone on the ledges, soil on the shore, grass on the cliff tops.
        static void Paint(TerrainData data)
        {
            var map = new float[AlphaRes, AlphaRes, 4];
            for (int y = 0; y < AlphaRes; y++)
                for (int x = 0; x < AlphaRes; x++)
                {
                    float u = (x + 0.5f) / AlphaRes, v = (y + 0.5f) / AlphaRes;
                    float wx = XMin + u * TerrainSize, wz = ZMin + v * TerrainSize;
                    float height = Ground(wx, wz), slope = data.GetSteepness(u, v);

                    float rock = Smooth(24f, 40f, slope);
                    float stone = 0f;
                    foreach (float side in new[] { -1f, 1f })
                        stone = Mathf.Max(stone, 1f - Smooth(5f, 9f, Vector2.Distance(new Vector2(wx, wz), new Vector2(side * StatueX, StatueZ))));
                    stone *= 1f - rock;
                    float shore = (1f - Smooth(-1.6f, 0.1f, height)) * (1f - rock);   // soil only under the water (the ledges at +0.6 m stay bare rock)
                    float top = Smooth(45f, 62f, height) * (1f - rock);               // grass up on the plateau
                    float dirt = shore * (1f - stone);
                    float grass = Mathf.Max(0.0001f, top * (1f - stone));
                    float rockWeight = Mathf.Max(rock, 0.0001f + (1f - top) * 0.35f * (1f - stone));
                    float sum = grass + dirt + rockWeight + stone;
                    map[y, x, 0] = grass / sum;
                    map[y, x, 1] = dirt / sum;
                    map[y, x, 2] = (rockWeight + stone) / sum;   // the ledges are bare grey rock
                    map[y, x, 3] = 0f;
                }
            data.SetAlphamaps(0, 0, map);
        }

        // ---------------------------------------------------------------- sky and light

        public static void BuildAtmosphere()
        {
            var sky = AssetDatabase.LoadAssetAtPath<Material>(Pack + "Materials/Skybox/Skybox.mat");
            if (sky != null) RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.5f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0032f;
            RenderSettings.fogColor = new Color(0.66f, 0.77f, 0.88f);

            // the sun low behind the players, so the statues and the waterfall in front of them are lit
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (light.type == LightType.Directional)
                {
                    light.transform.rotation = Quaternion.Euler(36f, 18f, 0f);
                    light.color = new Color(1f, 0.95f, 0.84f);
                    light.intensity = 2.2f;
                    light.shadows = LightShadows.Soft;
                    light.shadowStrength = 0.7f;
                }
        }

        // ---------------------------------------------------------------- statues

        // Each prefab is 1 m tall at its own scale (100) and faces +z with its own rotation (270 about x); this stands it on its ledge, `height` metres tall,
        // turned to face the centre line (the waterfall): the left one looks towards +x, the right one towards -x.
        public static void BuildStatues(Transform root, GameObject stonePrefab, GameObject featheredPrefab)
        {
            var group = new GameObject("Giant_Statues").transform;
            group.SetParent(root, false);
            Place(group, stonePrefab, "Statue_SonTinh_StoneSovereign", -1f);
            Place(group, featheredPrefab, "Statue_ThuyTinh_FeatheredSovereign", 1f);
        }

        static void Place(Transform parent, GameObject prefab, string name, float side)
        {
            var statue = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            statue.name = name;
            statue.transform.localScale = Vector3.one * (prefab.transform.localScale.x * StatueHeight);
            statue.transform.rotation = Quaternion.Euler(270f, -side * 90f, 0f);
            statue.transform.position = new Vector3(side * StatueX, LedgeHeight + StatueHeight * 0.5f, StatueZ);
            foreach (var r in statue.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.On;
        }

        // ---------------------------------------------------------------- the waterfall and the pool

        public static void BuildWaterfall(Transform root)
        {
            var group = new GameObject("Waterfall_Center").transform;
            group.SetParent(root, false);
            GameObject fall = MapDecor.Waterfall(group, Ground, FallFoot, FallLip, FallWidth, "FinalBattle");
            if (fall == null) return;
            var sheet = fall.transform.Find("Sheet");
            if (sheet != null) sheet.GetComponent<MeshRenderer>().sharedMaterial = BrightWaterfallMaterial();
            // a bigger cloud at the foot than the small maps have: the fall is 45 m high
            foreach (var ps in fall.GetComponentsInChildren<ParticleSystem>())
            {
                var main = ps.main;
                if (ps.name == "Mist") { main.startSize = new ParticleSystem.MinMaxCurve(6f, 12f); main.startColor = new Color(1f, 1f, 1f, 0.2f); main.maxParticles = 140; var e = ps.emission; e.rateOverTime = 30f; }
            }
        }

        // The shared Mat_Waterfall is a pale veil for the small maps; this one is brighter and denser so the fall reads from 50 m away.
        static Material BrightWaterfallMaterial()
        {
            const string path = DataDir + "/Mat_Waterfall_FinalBattle.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Directory.CreateDirectory(DataDir);
                material = new Material(Shader.Find("SonTinhThuyTinh/Waterfall"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_Color", new Color(0.62f, 0.84f, 0.97f, 0.92f));
            material.SetColor("_FoamColor", Color.white);
            material.SetFloat("_Speed", 2.6f);
            material.SetFloat("_Streaks", 16f);
            EditorUtility.SetDirty(material);
            return material;
        }

        // The pool: one big disc of water (the WaterWorks material of the Thuy Tinh map) covering the basin; it rises in phase 2 against Thuy Tinh.
        public static void BuildWater(Transform root, out Transform waterTransform, out GameObject stormFx)
        {
            var waterRoot = new GameObject("Water_Gorge_Pool");
            waterRoot.transform.SetParent(root, false);
            waterTransform = waterRoot.transform;

            Material waterMat = ThuyTinhMapBuilder.WaterMaterial();   // WaterWorks when imported, else Simple water (never the magenta copy)
            if (waterMat == null) waterMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Maps/SonTinh/Mat_Pool.mat");
            if (waterMat == null) waterMat = MapDecor.Lit("Mat_Water_Fallback", new Color(0.12f, 0.42f, 0.52f, 0.8f), 0.1f, 0.95f);

            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "Water_Surface";
            disc.transform.SetParent(waterRoot.transform, false);
            disc.transform.localPosition = new Vector3(BasinCentre.x, WaterLevel, BasinCentre.y);
            disc.transform.localScale = new Vector3(130f, 0.04f, 130f);   // a little wider than the basin; the cliffs hide the rim
            disc.GetComponent<Renderer>().sharedMaterial = waterMat;
            Object.DestroyImmediate(disc.GetComponent<Collider>());   // the terrain is the bed of the pool

            // phase 2 storm against Thuy Tinh: rain from above the arena
            var stormObj = new GameObject("Phase2_WaterStorm_FX");
            stormObj.transform.SetParent(waterRoot.transform, false);
            stormObj.transform.position = new Vector3(0f, 18f, 0f);
            var ps = stormObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 1.2f;
            main.startSpeed = 24f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
            main.startColor = new Color(0.85f, 0.92f, 1f, 0.8f);
            main.maxParticles = 800;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 350f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(36f, 1f, 36f);
            // rain streaks (without a material a particle system renders magenta)
            var stormRenderer = stormObj.GetComponent<ParticleSystemRenderer>();
            stormRenderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Combat/Mat_VfxAlpha.mat");
            stormRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            stormRenderer.velocityScale = 0.05f;
            stormRenderer.lengthScale = 2.2f;
            stormObj.transform.rotation = Quaternion.Euler(75f, 30f, 0f);
            stormObj.SetActive(false);
            stormFx = stormObj;
        }

        // ---------------------------------------------------------------- dressing: rocks, fir trees, cliff faces, mountains

        public static void BuildDressing(Transform root)
        {
            var group = new GameObject("Dressing").transform;
            group.SetParent(root, false);
            var rng = new System.Random(20261007);

            GameObject[] rocks = Load("Rock_Big_01", "Rock_Big_02", "Rock_Big_03", "Rock_Medium_01", "Rock_Medium_02", "Stone_Big_01");
            GameObject[] cliffs = Load("Cliff_01", "Cliff_02", "Cliff_03", "Cliff_04", "Cliff_05");
            GameObject[] firs = Load("Fir_01", "Fir_02", "Fir_03", "Fir_04", "Fir_05");

            // big rocks on the shore, at the foot of the walls and around the ledges
            for (int i = 0; i < 40 && rocks.Length > 0; i++)
            {
                float angle = (i + (float)rng.NextDouble()) / 46f * Mathf.PI * 2f;
                float k = Mathf.Lerp(0.88f, 1.05f, (float)rng.NextDouble());
                float x = BasinCentre.x + Mathf.Cos(angle) * BasinRadiusX * k, z = BasinCentre.y + Mathf.Sin(angle) * BasinRadiusZ * k;
                if (InSight(x, z)) continue;   // the players must see the statues and the fall
                Scatter(group, rocks[rng.Next(rocks.Length)], x, z, 0.5f + (float)rng.NextDouble() * 0.7f, rng, 0f);
            }
            for (int i = 0; i < 14 && rocks.Length > 0; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = 9f + (float)rng.NextDouble() * 4f;
                Scatter(group, rocks[rng.Next(rocks.Length)], side * StatueX + Mathf.Cos(a) * r, StatueZ + Mathf.Sin(a) * r, 0.35f + (float)rng.NextDouble() * 0.4f, rng, 0f);
            }

            // cliff models on the faces of the walls to break up the smooth terrain
            for (int i = 0; i < 70 && cliffs.Length > 0; i++)
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float k = Mathf.Lerp(1.2f, 1.5f, (float)rng.NextDouble());   // on the steep part of the walls, not over the water
                float x = BasinCentre.x + Mathf.Cos(angle) * BasinRadiusX * k, z = BasinCentre.y + Mathf.Sin(angle) * BasinRadiusZ * k;
                if (InSight(x, z) || z > 30f) continue;   // not across the statues and the fall, nor on the back wall
                if (z < -20f && Mathf.Abs(x) < 20f) continue;                     // nor behind the players where the camera is
                Scatter(group, cliffs[rng.Next(cliffs.Length)], x, z, 0.9f + (float)rng.NextDouble() * 1.1f, rng, 0f, faceCentre: true);
            }

            // fir trees along the top of the cliffs
            for (int i = 0; i < 160 && firs.Length > 0; i++)
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float k = Mathf.Lerp(1.45f, 2.3f, (float)rng.NextDouble());
                float x = BasinCentre.x + Mathf.Cos(angle) * BasinRadiusX * k, z = BasinCentre.y + Mathf.Sin(angle) * BasinRadiusZ * k;
                if (x < XMin + 6f || x > XMin + TerrainSize - 6f || z < ZMin + 6f || z > ZMin + TerrainSize - 6f) continue;
                if (Ground(x, z) < 50f) continue;   // only up on the plateau
                Scatter(group, firs[rng.Next(firs.Length)], x, z, 1.1f + (float)rng.NextDouble() * 0.9f, rng, 0f, upright: true);
            }

            // torches round the dais are made by the arena builder; blue mountains far behind the notch
            MapDecor.DistantMountains(root, new Vector3(0f, 0f, 430f), 12, MapDecor.MountainBlue, 40f, 170f, 20261007);   // the cones are 150-240 m high and 100 m wide: well behind the notch
        }

        // the part of the pool between the players and the foot of the wall: nothing may stand in the way of the camera there
        static bool InSight(float x, float z) => z > 4f && Mathf.Abs(x) < 46f;

        static GameObject[] Load(params string[] names) =>
            names.Select(n => AssetDatabase.LoadAssetAtPath<GameObject>(Pack + "Prefabs/" + n + ".prefab")).Where(p => p != null).ToArray();

        static void Scatter(Transform parent, GameObject prefab, float x, float z, float scale, System.Random rng, float sink, bool faceCentre = false, bool upright = false)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.position = new Vector3(x, Ground(x, z) + sink * scale * 0.25f, z);
            float yaw = faceCentre ? Mathf.Atan2(BasinCentre.x - x, BasinCentre.y - z) * Mathf.Rad2Deg + ((float)rng.NextDouble() - 0.5f) * 50f : (float)rng.NextDouble() * 360f;
            go.transform.rotation = upright ? Quaternion.Euler(0f, yaw, 0f) : Quaternion.Euler(((float)rng.NextDouble() - 0.5f) * 14f, yaw, ((float)rng.NextDouble() - 0.5f) * 14f);
            go.transform.localScale = Vector3.one * scale;
            go.isStatic = true;
        }
    }
}
