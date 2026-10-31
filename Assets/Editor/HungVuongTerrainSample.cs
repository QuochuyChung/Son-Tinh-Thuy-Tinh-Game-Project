using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SonTinhThuyTinh.EditorTools
{
    // Map_HungVuong rebuilt from the "Unity URP Terrain | Terrain Sample Project" asset (Assets/TerrainDemoScene_URP) only: its terrain layers,
    // its pines / conifers / cypresses, its rocks, its grass and ferns (terrain details), its water plane and its distant mountains.
    // The palace, the plaza, the roads, the torches and banners and the 46 Meshy trees stay. Everything from "Idyllic Fantasy Nature"
    // (pink blossom trees, colourful flowers, bushes, boulders, water, the cone mountains) is removed from this scene.
    // Each step works on the open Map_HungVuong scene and can be run again (it replaces what it made before).
    public static class HungVuongTerrainSample
    {
        const string Root = "Assets/TerrainDemoScene_URP/";

        static Transform Gen => GameObject.Find("Generated").transform;

        static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(Root + path);

        [MenuItem("Tools/Son Tinh Thuy Tinh/Hung Vuong ▸ Terrain Sample (all steps)")]
        public static void All()
        {
            Debug.Log(Layers());
            Debug.Log(Trees());
            Debug.Log(Rocks());
            Debug.Log(Undergrowth());
            Debug.Log(Water());
            Debug.Log(Mountains());
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        // ---------------------------------------------------------------- terrain layers (same slots as before: grass, dirt, rock, cobble)

        public static string Layers()
        {
            Terrain terrain = Terrain.activeTerrain;
            TerrainLayer[] layers =
            {
                Load<TerrainLayer>("Terrain/Layers/Grass_B.terrainlayer"),     // the greener of the sample's grasses
                Load<TerrainLayer>("Terrain/Layers/Grass_Soil_A.terrainlayer"),
                Load<TerrainLayer>("Terrain/Layers/Cliff_Mossy_E.terrainlayer"),
                Load<TerrainLayer>("Terrain/Layers/Pebbles_B.terrainlayer"),
            };
            if (layers.Any(l => l == null)) return "terrain layers of the sample are missing";
            terrain.terrainData.terrainLayers = layers;
            EditorUtility.SetDirty(terrain.terrainData);
            return "terrain layers: " + string.Join(", ", layers.Select(l => l.name));
        }

        // ---------------------------------------------------------------- trees (the places of the old Idyllic trees)

        public static string Trees()
        {
            string[] paths =
            {
                "Prefabs/Trees/Pines/Pine_A/Pine_A.prefab", "Prefabs/Trees/Pines/Pine_B/Pine_B.prefab", "Prefabs/Trees/Pines/Pine_C/Pine_C.prefab",
                "Prefabs/Trees/Pines/Pine_D/Pine_D.prefab", "Prefabs/Trees/Conifer/Conifer.prefab", "Prefabs/Trees/Cypress/Cypress_Forest_Desktop.prefab",
            };
            float[] baseScale = { 0.6f, 0.6f, 0.6f, 0.6f, 0.5f, 1.2f };          // pines ~17 m, conifer ~15 m, cypress ~13 m
            var prefabs = paths.Select(p => Load<GameObject>(p)).ToArray();
            if (prefabs.Any(p => p == null)) return "tree prefabs of the sample are missing";

            Transform group = Gen.Find("Trees");
            var spots = new List<(Vector3 pos, float yaw)>();
            foreach (Transform t in group) spots.Add((t.position, t.eulerAngles.y));
            if (spots.Count == 0 && group.Find("SampleTrees") != null) return "trees already replaced";
            for (int i = group.childCount - 1; i >= 0; i--) Object.DestroyImmediate(group.GetChild(i).gameObject);

            var rng = new System.Random(20261024);
            Terrain terrain = Terrain.activeTerrain;
            int made = 0;
            foreach (var s in spots)
            {
                int k = rng.NextDouble() < 0.35 ? 5 : rng.Next(0, 5);            // some cypresses among the pines
                var tree = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[k], group);
                float scale = baseScale[k] * (0.85f + (float)rng.NextDouble() * 0.35f);
                float y = terrain.SampleHeight(s.pos) + terrain.transform.position.y;
                tree.transform.SetPositionAndRotation(new Vector3(s.pos.x, y - 0.1f, s.pos.z), Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));
                tree.transform.localScale = Vector3.one * scale;
                if (tree.GetComponentInChildren<Collider>() == null)
                {
                    var col = tree.AddComponent<CapsuleCollider>();            // trunk
                    col.radius = 0.45f; col.height = 6f; col.center = new Vector3(0f, 3f, 0f);
                }
                made++;
            }
            return made + " sample trees replace the Idyllic trees";
        }

        // ---------------------------------------------------------------- rocks (the places and sizes of the old boulders)

        public static string Rocks()
        {
            string[] paths = { "Prefabs/Rocks/Rock_Overgrown_A.prefab", "Prefabs/Rocks/Rock_Overgrown_B.prefab", "Prefabs/Rocks/Rock_Overgrown_C.prefab", "Prefabs/Rocks/Rock_Overgrown_D.prefab" };
            var prefabs = paths.Select(p => Load<GameObject>(p)).ToArray();
            if (prefabs.Any(p => p == null)) return "rock prefabs of the sample are missing";

            Transform group = Gen.Find("Rocks");
            var spots = new List<(Vector3 pos, float footprint, float height, float minY)>();
            foreach (Transform t in group)
            {
                Bounds b = BoundsOf(t.gameObject);
                spots.Add((t.position, Mathf.Max(b.size.x, b.size.z), b.size.y, b.min.y));
            }
            if (spots.Count == 0) return "rocks already replaced";
            for (int i = group.childCount - 1; i >= 0; i--) Object.DestroyImmediate(group.GetChild(i).gameObject);

            var rng = new System.Random(20261025);
            int made = 0;
            foreach (var s in spots)
            {
                var rock = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[rng.Next(prefabs.Length)], group);
                Bounds nat = BoundsOf(rock);
                float h = Mathf.Clamp(s.height * 1.3f / Mathf.Max(0.1f, nat.size.y), 0.4f, 1.1f);
                float stretch = Mathf.Clamp(s.footprint / (Mathf.Max(nat.size.x, nat.size.z) * h), 0.8f, 1.8f);
                rock.transform.SetPositionAndRotation(s.pos, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));
                rock.transform.localScale = new Vector3(h * stretch, h, h * stretch);
                Physics.SyncTransforms();
                Bounds b = BoundsOf(rock);
                rock.transform.position += new Vector3(0f, s.minY - 0.4f - b.min.y, 0f);   // sunk a little into the ground
                if (rock.GetComponentInChildren<Collider>() == null)
                {
                    var mf = rock.GetComponentsInChildren<MeshFilter>().OrderByDescending(f => f.sharedMesh != null ? f.sharedMesh.vertexCount : 0).FirstOrDefault();
                    if (mf != null) mf.gameObject.AddComponent<MeshCollider>();
                }
                made++;
            }
            return made + " sample rocks replace the Idyllic boulders";
        }

        // ---------------------------------------------------------------- grass, ferns, shrubs: terrain details

        public static string Undergrowth()
        {
            Terrain terrain = Terrain.activeTerrain;
            TerrainData td = terrain.terrainData;
            var demo = AssetDatabase.LoadAssetAtPath<TerrainData>(Root + "Terrain/Data/Terrain_1_1_4bf22c8f-eb55-436d-84e3-dedfa85665b1.asset");
            if (demo == null) return "the sample terrain data is missing";

            // the green plants of the sample's own terrain (first prototype of each name; no red or flowering ones)
            string[] wanted = { "Grass_A", "Grass_B", "Grass_C", "Fern_A", "Bush_A", "Shrub" };
            var protos = new List<DetailPrototype>();
            foreach (string n in wanted)
            {
                DetailPrototype d = demo.detailPrototypes.FirstOrDefault(x => x.prototype != null && x.prototype.name == n);
                if (d == null) continue;
                protos.Add(new DetailPrototype
                {
                    prototype = d.prototype, usePrototypeMesh = true, renderMode = d.renderMode,
                    minWidth = d.minWidth, maxWidth = d.maxWidth, minHeight = d.minHeight, maxHeight = d.maxHeight,
                    noiseSeed = d.noiseSeed, noiseSpread = d.noiseSpread, holeEdgePadding = d.holeEdgePadding,
                    useInstancing = true, alignToGround = d.alignToGround, positionJitter = d.positionJitter,
                    density = d.density, healthyColor = d.healthyColor, dryColor = d.dryColor,
                });
            }
            if (protos.Count == 0) return "no detail prototypes found in the sample";

            // old Idyllic undergrowth (the flowers and bushes) out
            Transform old = Gen.Find("Undergrowth");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            td.SetDetailResolution(512, 16);          // 0.6 m cells (1024 over 300 m put ~1M instanced meshes in view and hung the GPU)
            td.detailPrototypes = protos.ToArray();
            float[,,] alpha = td.GetAlphamaps(0, 0, td.alphamapWidth, td.alphamapHeight);
            int res = td.detailResolution;
            var rng = new System.Random(20261026);
            Vector3 origin = terrain.transform.position;
            for (int p = 0; p < protos.Count; p++)
            {
                var map = new int[res, res];
                string n = protos[p].prototype.name;
                float cutoff = n.StartsWith("Grass") ? 0.0f : n == "Fern_A" ? 0.72f : 0.8f;     // ferns and bushes only in patches
                int max = n.StartsWith("Grass") ? 3 : 1;
                for (int z = 0; z < res; z++)
                    for (int x = 0; x < res; x++)
                    {
                        float u = (x + 0.5f) / res, v = (z + 0.5f) / res;
                        float wy = terrain.SampleHeight(new Vector3(origin.x + u * td.size.x, 0f, origin.z + v * td.size.z)) + origin.y;
                        if (wy < 11.5f) continue;                                                      // plateau only
                        int ax = Mathf.Min(td.alphamapWidth - 1, (int)(u * td.alphamapWidth)), az = Mathf.Min(td.alphamapHeight - 1, (int)(v * td.alphamapHeight));
                        float grass = alpha[az, ax, 0] + alpha[az, ax, 1] * 0.25f;
                        if (grass < 0.55f) continue;                                                   // not on the road, the plaza, the cliffs
                        float noise = Mathf.PerlinNoise(u * 14f + p * 37f, v * 14f + p * 11f);
                        if (n.StartsWith("Grass"))
                        {
                            if ((p == 0 && noise < 0.35f) || (p == 1 && noise >= 0.35f && noise < 0.7f) || (p == 2 && noise >= 0.7f) || rng.NextDouble() < 0.2) map[z, x] = rng.Next(1, max + 1);
                        }
                        else if (noise > cutoff && rng.NextDouble() < 0.5) map[z, x] = rng.Next(1, max + 1);
                    }
                td.SetDetailLayer(0, 0, p, map);
            }
            terrain.detailObjectDistance = 70f;
            terrain.detailObjectDensity = 1f;
            EditorUtility.SetDirty(td);
            return "terrain details: " + string.Join(", ", protos.Select(d => d.prototype.name));
        }

        // ---------------------------------------------------------------- the sea and the mountains of the sample

        public static string Water()
        {
            var plane = Load<GameObject>("Prefabs/Water/WaterPlane.prefab");
            if (plane == null) return "WaterPlane of the sample is missing";
            Transform sea = Gen.Find("Sea");
            Transform old = sea.Find("Sea");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var existing = sea.Find("SampleWater");
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
            var w = (GameObject)PrefabUtility.InstantiatePrefab(plane, sea);
            w.name = "SampleWater";
            w.transform.localPosition = new Vector3(0f, 0f, 0f);
            w.transform.localScale = new Vector3(150f, 1f, 150f);
            return "water: WaterPlane (sample)";
        }

        public static string Mountains()
        {
            var prefab = Load<GameObject>("Prefabs/Skybox/SkyboxMountains.prefab");
            if (prefab == null) return "SkyboxMountains of the sample is missing";
            Transform sea = Gen.Find("Sea");
            Transform old = sea.Find("DistantMountains");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var existing = sea.Find("SampleMountains");
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
            var m = (GameObject)PrefabUtility.InstantiatePrefab(prefab, sea);
            m.name = "SampleMountains";
            m.transform.localPosition = Vector3.zero;
            // the prefab is a 9 km ring for a skybox camera; here it is shrunk into the camera's 1200 m far plane, its feet in the sea
            m.transform.localScale *= 0.06f;
            Physics.SyncTransforms();
            Bounds b = BoundsOf(m);
            m.transform.position += new Vector3(-b.center.x, -8f - b.min.y, -b.center.z);
            return "distant mountains: SkyboxMountains (sample), " + b.size.ToString("F0");
        }

        // ---------------------------------------------------------------- a picture of the open scene from a given place (for checking)

        public static string Shot(string path, Vector3 pos, Vector3 euler, float fov = 60f, int width = 1600, int height = 900)
        {
            var go = new GameObject("~Shot");
            var cam = go.AddComponent<Camera>();
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            cam.fieldOfView = fov; cam.nearClipPlane = 0.2f; cam.farClipPlane = 3000f;
            cam.transform.SetPositionAndRotation(pos, Quaternion.Euler(euler));
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex); cam.targetTexture = null; rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(go);
            return path;
        }

        static Bounds BoundsOf(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            Bounds b = rs[0].bounds;
            foreach (Renderer r in rs) b.Encapsulate(r.bounds);
            return b;
        }
    }
}
