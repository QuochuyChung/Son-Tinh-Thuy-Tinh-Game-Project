using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace SonTinhThuyTinh.EditorTools
{
    // The second set of Meshy props in Assets/Art/Props (made by tools/prep_prop.py: embedded material stripped, the hat without its chin strap):
    // the brazier torch, the royal throne (with its two banners), the conical hat, the carved longboat and the bronze drum. One URP Lit
    // material per model (base colour + normal + metallic/smoothness), prefabs in Assets/Prefabs/Props whose origin is the bottom centre of the
    // model, scaled to metres, with +Z = the model's front (the throne's steps, the boat's bow).
    // The Dress* helpers swap the old block-built props of the map builders (MapDecor.Torch / BronzeDrum, ThuyTinhMapSea.Boat, the villagers'
    // straw hats) for the models, so a rebuilt map keeps them; the menu Tools > Son Tinh Thuy Tinh > New Props > Swap Props In Maps does the
    // same to the props already standing in the saved map scenes.
    public static class NewProps
    {
        const string Dir = "Assets/Art/Props/";
        const string PrefabDir = "Assets/Prefabs/Props";

        public enum Kind { Brazier, Throne, Hat, Longboat, Drum, Banner, Bridge, GemRed, CrystalBlue, RockSlate, RockKaya, RockMonolith, RockFormation, RockFragment, RockBig, RockTundra, RockCave, Watchtower, TikiGate, Fence, GrassTuft, Lotus, Log, RockGolem, RockWall, RockSpire }

        struct Spec
        {
            public string file, prefab; public float size; public bool byHeight; public string orient;
        }

        // size: metres (height for byHeight, else the longest horizontal side). orient: "bow" = the highest point goes to +Z, "front" = the
        // upper part of the model goes to -Z (so the front is +Z), "wide" = the longest horizontal side along X, "" = as it is.
        static readonly Dictionary<Kind, Spec> Specs = new()
        {
            [Kind.Brazier] = new Spec { file = "brazier", prefab = "Brazier", size = 2.3f, byHeight = true },
            [Kind.Throne] = new Spec { file = "throne", prefab = "RoyalThrone", size = 4.6f, orient = "front" },
            [Kind.Banner] = new Spec { file = "banner", prefab = "RoyalBanner", size = 4.8f, byHeight = true },
            [Kind.Hat] = new Spec { file = "hat", prefab = "ConicalHat", size = 0.62f },
            [Kind.Longboat] = new Spec { file = "longboat", prefab = "Longboat", size = 6.6f, orient = "bow" },
            [Kind.Drum] = new Spec { file = "bronze_drum", prefab = "BronzeDrum", size = 1.3f, byHeight = true },
            [Kind.Bridge] = new Spec { file = "bridge", prefab = "WoodenBridge", size = 2.0f, orient = "bow" },
            [Kind.GemRed] = new Spec { file = "gem_red", prefab = "GemRed", size = 1.6f, byHeight = true },
            [Kind.CrystalBlue] = new Spec { file = "crystal_blue", prefab = "CrystalBlue", size = 1.6f, byHeight = true },
            [Kind.RockSlate] = new Spec { file = "rock_slate", prefab = "RockSlate", size = 4f },
            [Kind.RockKaya] = new Spec { file = "rock_kaya", prefab = "RockKaya", size = 4f },
            [Kind.RockMonolith] = new Spec { file = "rock_monolith", prefab = "RockMonolith", size = 4f },
            [Kind.RockFormation] = new Spec { file = "rock_formation", prefab = "RockFormation", size = 4f },
            [Kind.RockFragment] = new Spec { file = "rock_fragment", prefab = "RockFragment", size = 4f },
            [Kind.RockBig] = new Spec { file = "rock_big", prefab = "RockBig", size = 4f },
            [Kind.RockTundra] = new Spec { file = "rock_tundra", prefab = "RockTundra", size = 4f },
            [Kind.RockCave] = new Spec { file = "rock_cave", prefab = "RockCave", size = 4f },
            [Kind.Watchtower] = new Spec { file = "watchtower", prefab = "Watchtower", size = 18f, byHeight = true },
            [Kind.TikiGate] = new Spec { file = "tiki_gate", prefab = "TikiGate", size = 12f },
            [Kind.Fence] = new Spec { file = "fence", prefab = "WoodFence", size = 4f },
            [Kind.GrassTuft] = new Spec { file = "grass_tuft", prefab = "GrassTuft", size = 1.75f },
            [Kind.Lotus] = new Spec { file = "lotus", prefab = "Lotus", size = 1.0f },
            [Kind.Log] = new Spec { file = "log", prefab = "BarkLog", size = 5.6f },
            // Sơn Tinh's rock skills (no colliders: RockGolemCharge / RockWallBarrier / RockSpikeLine do their own hits)
            [Kind.RockGolem] = new Spec { file = "rock_golem", prefab = "RockGolem", size = 2.5f, byHeight = true },
            [Kind.RockWall] = new Spec { file = "rock_wall", prefab = "RockWall", size = 4f, orient = "wide" },
            [Kind.RockSpire] = new Spec { file = "rock_spire", prefab = "RockSpire", size = 2.8f, byHeight = true },
        };

        public static GameObject Prefab(Kind kind) => Prefab(kind, false);

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
            if (!AssetDatabase.IsValidFolder(PrefabDir)) AssetDatabase.CreateFolder("Assets/Prefabs", "Props");

            var root = new GameObject(spec.prefab);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath), root.transform);
            model.name = "Model";
            model.transform.localScale = Vector3.one;
            // orientation
            float yaw = 0f;
            if (spec.orient == "wide")
            {
                Bounds b0 = VillageModelSetup.LocalBounds(model, root.transform);
                if (b0.size.z > b0.size.x) yaw = 90f;
                model.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * model.transform.localRotation;
            }
            if (spec.orient == "bow" || spec.orient == "front")
            {
                Bounds b0 = VillageModelSetup.LocalBounds(model, root.transform);
                if (spec.orient == "bow" && b0.size.x > b0.size.z) yaw = 90f;   // the long axis along Z
                model.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * model.transform.localRotation;
                yaw = FixDirection(model, root.transform, spec.orient, yaw);
            }
            Bounds b = VillageModelSetup.LocalBounds(model, root.transform);
            float measured = spec.byHeight ? b.size.y : Mathf.Max(b.size.x, b.size.z);
            model.transform.localScale = Vector3.one * (spec.size / measured);
            b = VillageModelSetup.LocalBounds(model, root.transform);
            model.transform.localPosition = -new Vector3(b.center.x, b.min.y, b.center.z);
            b = VillageModelSetup.LocalBounds(model, root.transform);

            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                r.sharedMaterials = Enumerable.Repeat(material, r.sharedMaterials.Length).ToArray();

            switch (kind)
            {
                case Kind.Brazier:
                {
                    var col = root.AddComponent<CapsuleCollider>();
                    col.radius = 0.2f; col.height = b.size.y; col.center = new Vector3(0f, b.size.y * 0.5f, 0f);
                    break;
                }
                case Kind.Bridge:
                {
                    float deckTop = TopAt(root, 0f, 0f, 0f, 3f);
                    var col = root.AddComponent<BoxCollider>();           // the deck: walked on at its centre line height
                    col.size = new Vector3(b.size.x, deckTop, b.size.z); col.center = new Vector3(0f, deckTop * 0.5f, 0f);
                    break;
                }
                case Kind.GemRed:
                case Kind.CrystalBlue:
                {
                    var col = root.AddComponent<CapsuleCollider>();
                    col.radius = Mathf.Max(b.size.x, b.size.z) * 0.3f; col.height = b.size.y * 0.9f; col.center = new Vector3(0f, b.size.y * 0.45f, 0f);
                    break;
                }
                case Kind.RockSlate: case Kind.RockKaya: case Kind.RockMonolith: case Kind.RockFormation:
                case Kind.RockFragment: case Kind.RockBig: case Kind.RockTundra: case Kind.RockCave: case Kind.Watchtower:
                {
                    foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
                        mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                    break;
                }
                case Kind.TikiGate:
                {
                    // only the two pillars are solid (the opening and the lintel stay free): a box around each one's lower part
                    var pts = new List<Vector3>();
                    foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
                        foreach (var v in mf.sharedMesh.vertices) pts.Add(root.transform.InverseTransformPoint(mf.transform.TransformPoint(v)));
                    foreach (float side in new[] { -1f, 1f })
                    {
                        var part = pts.Where(q => Mathf.Sign(q.x) == side && Mathf.Abs(q.x) > 0.28f * b.size.x && q.y < b.min.y + 0.75f * b.size.y).ToList();
                        if (part.Count == 0) continue;
                        Vector3 lo = part[0], hi = part[0];
                        foreach (var q in part) { lo = Vector3.Min(lo, q); hi = Vector3.Max(hi, q); }
                        var col = root.AddComponent<BoxCollider>();
                        col.center = (lo + hi) * 0.5f; col.size = hi - lo;
                    }
                    break;
                }
                case Kind.Banner:
                {
                    var col = root.AddComponent<CapsuleCollider>();   // the pole
                    col.radius = 0.12f; col.height = b.size.y; col.center = new Vector3(0f, b.size.y * 0.5f, 0f);
                    break;
                }
                case Kind.Drum:
                {
                    var col = root.AddComponent<CapsuleCollider>();
                    col.radius = Mathf.Max(b.size.x, b.size.z) * 0.5f; col.height = b.size.y; col.center = new Vector3(0f, b.size.y * 0.5f, 0f);
                    break;
                }
                case Kind.Longboat:
                {
                    var col = root.AddComponent<BoxCollider>();   // the hull (without the tall prows): nobody walks through it
                    col.size = new Vector3(b.size.x * 0.95f, b.size.y * 0.35f, b.size.z * 0.8f); col.center = new Vector3(0f, b.size.y * 0.2f, 0f);
                    break;
                }
            }
            bool moves = kind == Kind.RockGolem || kind == Kind.RockWall || kind == Kind.RockSpire;   // spell pieces fly and rise: never static
            if (!moves)
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            b = VillageModelSetup.LocalBounds(prefab, prefab.transform);
            Debug.Log($"NewProps: {path} size {b.size.x:F2} x {b.size.y:F2} x {b.size.z:F2} m (yaw {yaw})");
            return prefab;
        }

        // Turns the model about the vertical until the wanted end is at +Z / the upper part at -Z; returns the total yaw.
        static float FixDirection(GameObject model, Transform frame, string orient, float yaw)
        {
            var points = new List<Vector3>();
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
                foreach (var v in mf.sharedMesh.vertices) points.Add(frame.InverseTransformPoint(mf.transform.TransformPoint(v)));
            float yMin = points.Min(p => p.y), yMax = points.Max(p => p.y);
            Vector3 key;
            if (orient == "bow") { key = points.OrderByDescending(p => p.y).Take(60).Aggregate(Vector3.zero, (a, p) => a + p) / 60f; key.y = 0f; }
            else
            {
                var upper = points.Where(p => p.y > yMin + 0.45f * (yMax - yMin)).ToList();
                key = upper.Aggregate(Vector3.zero, (a, p) => a + p) / upper.Count; key.y = 0f;
                key = -key;                          // the upper part goes behind: its opposite is the front
            }
            // rotate so that `key` points to +Z
            float ang = Mathf.Atan2(key.x, key.z) * Mathf.Rad2Deg;     // angle of key from +Z, clockwise seen from above
            model.transform.localRotation = Quaternion.Euler(0f, -ang, 0f) * model.transform.localRotation;
            return yaw - ang;
        }

        static void ConfigureModel(string path)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            if (importer.materialImportMode == ModelImporterMaterialImportMode.None && !importer.importAnimation) return;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
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
            string baseMap = Dir + file + "_basecolor.png", normalMap = Dir + file + "_normal.png", metalMap = Dir + file + "_metallic_smoothness.png", emitMap = Dir + file + "_emission.png";
            bool hasNormal = System.IO.File.Exists(normalMap), hasMetal = System.IO.File.Exists(metalMap), hasEmit = System.IO.File.Exists(emitMap);
            SetTexture(baseMap, TextureImporterType.Default, true);
            if (hasNormal) SetTexture(normalMap, TextureImporterType.NormalMap, false);
            if (hasMetal) SetTexture(metalMap, TextureImporterType.Default, false);
            if (hasEmit) SetTexture(emitMap, TextureImporterType.Default, true);
            string path = Dir + "M_" + file + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.shader = Shader.Find("Universal Render Pipeline/Lit");
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(baseMap));
            m.SetColor("_BaseColor", Color.white);
            if (hasNormal) { m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalMap)); m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP"); }
            if (hasMetal)
            {
                m.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(metalMap));
                m.EnableKeyword("_METALLICSPECGLOSSMAP");
                m.SetFloat("_WorkflowMode", 1f); m.SetFloat("_Smoothness", (file == "throne" || file == "banner") ? 0.3f : 1f);   // (the throne's bronze panels mirrored the sky as white patches at full smoothness) m.SetFloat("_SmoothnessTextureChannel", 0f);
            }
            else { m.SetFloat("_Metallic", 0f); m.SetFloat("_Smoothness", 0.22f); }       // no maps for this model: matte
            if (hasEmit)
            {
                m.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(emitMap));
                m.SetColor("_EmissionColor", Color.white * 1.6f); m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            bool singleSided = false;
            m.SetFloat("_Cull", (float)(singleSided ? CullMode.Back : CullMode.Off)); m.doubleSidedGI = !singleSided;   // banners, the hat's thin straw, the boat's rails
            EditorUtility.SetDirty(m);
            return m;
        }

        // ---------------------------------------------------------------- dressing

        static GameObject Instance(Kind kind, Transform parent, string name)
        {
            var prefab = Prefab(kind);
            if (prefab == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            return go;
        }

        static void Remove(Transform root, params string[] names)
        {
            foreach (var t in root.Cast<Transform>().Where(c => names.Contains(c.name)).ToList()) Object.DestroyImmediate(t.gameObject);
        }

        public static bool IsDressed(Transform root, string modelName) => root.Find(modelName) != null;

        // MapDecor.Torch: the pole and the bowl go, the brazier stands in their place; the fire and the light stay at the bowl.
        public static bool DressTorch(Transform torch)
        {
            if (IsDressed(torch, "Brazier")) return true;
            if (Prefab(Kind.Brazier) == null) return false;
            Remove(torch, "Pole", "Bowl");
            var model = Instance(Kind.Brazier, torch, "Brazier");
            model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.identity;
            // the old torch had its colliders on the pole / bowl parts (PropColliderFixer): the prefab carries its own now
            foreach (var c in torch.GetComponents<Collider>()) Object.DestroyImmediate(c);
            return true;
        }

        // MapDecor.Banner: the pole, crossbar and cloth go, the banner of the throne set (pole, crossbar, cloth, tassels) stands in their place.
        public static bool DressBanner(Transform banner)
        {
            if (IsDressed(banner, "BannerModel")) return true;
            if (banner.Find("Pole") == null || Prefab(Kind.Banner) == null) return false;
            Remove(banner, "Pole", "Crossbar", "Cloth");
            var model = Instance(Kind.Banner, banner, "BannerModel");
            model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.identity;
            return true;
        }

        // The highest point of the mesh(es) under `root` above the world position (x, z), ignoring everything higher than `cap` metres above
        // `floor`: where something that stands on the model stands (the throne's steps, the seat). NaN when the model is not there.
        public static float TopAt(GameObject root, float x, float z, float floor, float cap)
        {
            float best = float.NaN;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = mf.sharedMesh; if (mesh == null) continue;
                var v = mesh.vertices; var t = mesh.triangles; var m = mf.transform.localToWorldMatrix;
                for (int i = 0; i < t.Length; i += 3)
                {
                    Vector3 a = m.MultiplyPoint3x4(v[t[i]]), b = m.MultiplyPoint3x4(v[t[i + 1]]), c = m.MultiplyPoint3x4(v[t[i + 2]]);
                    float d = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                    if (Mathf.Abs(d) < 1e-9f) continue;
                    float u = ((b.z - c.z) * (x - c.x) + (c.x - b.x) * (z - c.z)) / d, w = ((c.z - a.z) * (x - c.x) + (a.x - c.x) * (z - c.z)) / d, k = 1f - u - w;
                    if (u < 0f || w < 0f || k < 0f) continue;
                    float y = u * a.y + w * b.y + k * c.y;
                    if (y > floor + cap) continue;
                    if (float.IsNaN(best) || y > best) best = y;
                }
            }
            return best;
        }

        // MapDecor.Log (the raft of floating logs): the trunk cylinder goes, the Meshy bark log takes its place at the same length, a little thicker
        // than the model's own proportions so it fills the old diameter. The box collider of the log stays.
        public static bool DressLog(Transform log, float length, float diameter)
        {
            var old = log.Find("Trunk"); var model0 = log.Find("LogModel");
            if (old == null && model0 == null) return false;
            if (Prefab(Kind.Log) == null) return false;
            if (old != null) Object.DestroyImmediate(old.gameObject);
            if (model0 != null) Object.DestroyImmediate(model0.gameObject);
            var model = Instance(Kind.Log, log, "LogModel");
            // the prefab is 5.6 long and about 1.4 thick: scaled to this log's length and its diameter, its bottom half a diameter under the centre
            float k = length / 5.6f, t = diameter / 1.4f;
            model.transform.localPosition = new Vector3(0f, -diameter * 0.5f, 0f); model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = new Vector3(k, t, t);
            return true;
        }

        // MapDecor.BronzeDrum: the model takes the old drum's height (it is built from MapDecor's scale).
        public static bool DressDrum(Transform drum)
        {
            if (IsDressed(drum, "DrumModel")) return true;
            var old = drum.Find("Drum");
            if (old == null || Prefab(Kind.Drum) == null) return false;
            float height = 1.3f * old.localScale.x;
            Object.DestroyImmediate(old.gameObject);
            var model = Instance(Kind.Drum, drum, "DrumModel");
            model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one * (height / 1.3f);
            return true;
        }

        // ThuyTinhMapSea.Boat: the planks, mast, sail and cabin go, the longboat takes the junk's place (hull on the water, bow on +Z). The
        // walkable box collider of the moored boats stays on the root.
        public static bool DressBoat(Transform boat, bool wreck)
        {
            if (IsDressed(boat, "Longboat")) return true;
            if (Prefab(Kind.Longboat) == null) return false;
            foreach (var t in boat.Cast<Transform>().ToList()) Object.DestroyImmediate(t.gameObject);
            var model = Instance(Kind.Longboat, boat, "Longboat");
            model.transform.localPosition = new Vector3(0f, wreck ? -0.1f : -0.15f, 0f); model.transform.localRotation = Quaternion.identity;
            return true;
        }

        // A villager's conical hat on top of the head: the straw hat of the placeholder (HatBrim / HatTop) is replaced.
        public static bool DressHat(Transform npc, float headTop = 1.74f)
        {
            Remove(npc, "HatBrim", "HatTop", "ConicalHat");
            if (Prefab(Kind.Hat) == null) return false;
            var hat = Instance(Kind.Hat, npc, "ConicalHat");
            hat.transform.localPosition = new Vector3(0f, headTop, 0f); hat.transform.localRotation = Quaternion.Euler(-6f, 0f, 0f);
            return true;
        }

        public static void RemoveHat(Transform npc) => Remove(npc, "HatBrim", "HatTop", "ConicalHat");

        // The throne with its banners (a model, instead of CineSetBuilder's block throne): its front towards -Z, the base at `root`.
        public static GameObject Throne(Transform root)
        {
            var prefab = Prefab(Kind.Throne);
            if (prefab == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
            go.name = "RoyalThrone";
            go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            return go;
        }

        // MapDecor.PlankBridge (and the saved scenes): the Meshy bridge is one short piece (its planks, posts and ropes are not the same on both
        // ends), so end to end copies zig-zag. The bridge stays what it was: straight boards, posts and two rope rails along one line, but with the
        // weathered plank texture of the Meshy bridge on the boards and its rope look on the rails.
        public static bool DressBridge(Transform bridge)
        {
            if (Prefab(Kind.Bridge) == null) return false;                // (makes the material too)
            Vector3 from, to; float width = 2.4f;
            var segs = bridge.Find("BridgeSegments");
            if (bridge.Find("Straight") != null) return true;
            if (segs != null)
            {
                if (segs.childCount < 2) return false;
                Vector3 s0 = segs.GetChild(0).position, s1 = segs.GetChild(1).position, sl = segs.GetChild(segs.childCount - 1).position;
                Vector3 dir0 = (sl - s0).normalized; float seg0 = (s1 - s0).magnitude;
                from = s0 - dir0 * (seg0 * 0.5f); to = sl + dir0 * (seg0 * 0.5f);
                var prefab = Prefab(Kind.Bridge); var pb = VillageModelSetup.LocalBounds(prefab, prefab.transform);
                width = segs.GetChild(0).localScale.x * pb.size.x;
                // the span centres sit at deck height minus a model offset: the boards' top is the first span's lowest point
                var rs0 = segs.GetChild(0).GetComponentsInChildren<MeshRenderer>(); float deckTop = TopAt(segs.GetChild(0).gameObject, s0.x, s0.z, s0.y - 1f, 0.6f);
                if (!float.IsNaN(deckTop)) { float lift = deckTop - s0.y; from.y += lift; to.y += lift; }
                foreach (var c in bridge.Cast<Transform>().ToList()) Object.DestroyImmediate(c.gameObject);
            }
            else
            {
                var boards = bridge.Cast<Transform>().Where(c => c.name == "Board").ToList();
                if (boards.Count < 2) return false;
                Vector3 first = boards.OrderBy(b => b.position.x * 7f + b.position.z * 13f + b.position.y).First().position;
                Vector3 last = boards.OrderByDescending(b => (b.position - first).sqrMagnitude).First().position;
                if (first == last) return false;
                boards = boards.OrderBy(b => Vector3.Dot(b.position - first, last - first)).ToList();
                Vector3 step = (last - first) / (boards.Count - 1);
                from = first - step * 0.5f; to = last + step * 0.5f;
                width = boards[0].localScale.x;
                from.y += boards[0].localScale.y * 0.5f; to.y += boards[0].localScale.y * 0.5f;   // board top
                foreach (var c in bridge.Cast<Transform>().ToList()) Object.DestroyImmediate(c.gameObject);
            }
            BuildStraight(bridge, from, to, width);
            return true;
        }

        // `from` / `to` are the deck top at both ends.
        public static void BuildStraight(Transform bridge, Vector3 from, Vector3 to, float width)
        {
            var holder = new GameObject("Straight").transform; holder.SetParent(bridge, false);
            if (BridgeModelSwap.BuildModelRun(holder, from, to, width, true) > 0) return;   // the Meshy bridge itself, copies end to end
            var plankMat = AssetDatabase.LoadAssetAtPath<Material>(Dir + "M_bridge.mat");
            var boardMat = new Material(plankMat) { name = "M_bridge_boards" };
            boardMat.SetTextureScale("_BaseMap", new Vector2(0.5f, 0.5f)); boardMat.SetTextureScale("_BumpMap", new Vector2(0.5f, 0.5f));
            var wood = MapDecor.Lit("Mat_Wood", new Color(0.33f, 0.22f, 0.12f), 0f, 0.15f);
            var rope = MapDecor.Lit("Mat_Rope", new Color(0.72f, 0.66f, 0.52f), 0f, 0.1f);
            Vector3 d = to - from; float length = d.magnitude; Vector3 dir = d / length, side = Vector3.Cross(Vector3.up, new Vector3(dir.x, 0f, dir.z).normalized);
            Quaternion rotation = Quaternion.LookRotation(dir);
            int boards = Mathf.Max(1, Mathf.CeilToInt(length / 0.62f));
            for (int i = 0; i < boards; i++)
                MapDecor.Part(PrimitiveType.Cube, "Board", holder, from + d * ((i + 0.5f) / boards) - Vector3.up * 0.07f, new Vector3(width, 0.14f, length / boards * 0.94f), rotation, boardMat, true);
            int posts = Mathf.Max(2, Mathf.CeilToInt(length / 3.5f) + 1);
            for (int i = 0; i < posts; i++)
            {
                Vector3 p = from + d * (i / (float)(posts - 1));
                foreach (float sign in new[] { -1f, 1f })
                    MapDecor.Part(PrimitiveType.Cylinder, "Post", holder, p + side * (sign * (width * 0.5f + 0.1f)) + Vector3.up * 0.25f, new Vector3(0.16f, 0.85f, 0.16f), Quaternion.identity, wood);
            }
            foreach (float sign in new[] { -1f, 1f })
                foreach (float h in new[] { 0.55f, 0.95f })
                    MapDecor.Part(PrimitiveType.Cube, "Rail", holder, (from + to) * 0.5f + side * (sign * (width * 0.5f + 0.1f)) + Vector3.up * h, new Vector3(0.05f, 0.05f, length), rotation, rope);
        }

        // MapDecor.Crystals (and the saved scenes): the stretched cubes go, the red gem cluster (Son Tinh) or the azure crystal cluster (Thuy Tinh)
        // grows in their place: one to four copies, as tall as the tallest cube.
        public static bool DressCrystals(Transform crystals, bool red)
        {
            if (IsDressed(crystals, "GemCluster0")) return true;
            var kind = red ? Kind.GemRed : Kind.CrystalBlue;
            if (Prefab(kind) == null) return false;
            var cubes = crystals.Cast<Transform>().Where(c => c.name == "Crystal").ToList();
            if (cubes.Count == 0) return false;
            float height = cubes.Max(c => c.localScale.y);
            int n = Mathf.Clamp(Mathf.RoundToInt(cubes.Count / 4f), 1, 4);
            var rng = new System.Random(Mathf.RoundToInt(crystals.position.x * 10f) * 31 + Mathf.RoundToInt(crystals.position.z * 10f));
            foreach (var c in cubes) Object.DestroyImmediate(c.gameObject);
            for (int i = 0; i < n; i++)
            {
                var go = Instance(kind, crystals, "GemCluster" + i);
                float a = i == 0 ? 0f : (i * 2.4f + (float)rng.NextDouble() * 0.8f), r = i == 0 ? 0f : height * (0.55f + 0.25f * i);
                float h = height * (i == 0 ? 1.0f : 0.65f + (float)rng.NextDouble() * 0.25f);
                go.transform.localPosition = new Vector3(Mathf.Sin(a) * r, -0.05f, Mathf.Cos(a) * r);
                go.transform.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                go.transform.localScale = Vector3.one * (h / 1.6f);
            }
            return true;
        }

        // natural height / longest horizontal side of each rock model (prefab size 4 m)
        static readonly (Kind kind, float ratio)[] RockShapes =
        {
            (Kind.RockFragment, 0.13f), (Kind.RockSlate, 0.37f), (Kind.RockBig, 0.56f), (Kind.RockCave, 0.58f), (Kind.RockFormation, 0.62f),
            (Kind.RockTundra, 0.93f), (Kind.RockMonolith, 1.45f), (Kind.RockKaya, 1.55f),
        };

        static Transform UnitRootOf(Transform t)
        {
            var u = t;
            while (u.parent != null && u.parent.parent != null && u.parent.childCount <= 6) u = u.parent;
            return u;
        }

        // The lowest terrain under the middle of a footprint (a rock must not float over a slope or stand taller than the ground it sits on).
        static float GroundBelow(Bounds b)
        {
            var t = Terrain.activeTerrain; if (t == null) return b.min.y;
            float min = float.MaxValue;
            for (int i = -1; i <= 1; i++) for (int j = -1; j <= 1; j++)
                min = Mathf.Min(min, t.SampleHeight(new Vector3(b.center.x + i * b.extents.x * 0.5f, 0f, b.center.z + j * b.extents.z * 0.5f)) + t.transform.position.y);
            return Mathf.Min(min, b.min.y + 0.5f) < min ? Mathf.Min(min, b.min.y + 0.5f) : min;
        }

        // About `fraction` of the big rocks of the scene are swapped for the new rock models (shape picked from the old rock's proportions, same
        // footprint, random yaw): the rest stay, so the maps have both. Deterministic: the same rocks every time, a second run changes nothing.
        public static int ReplaceRocks(float fraction = 0.42f)
        {
            if (Prefab(Kind.RockBig) == null) return 0;
            var units = new HashSet<Transform>();
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                if (r.GetComponentInParent<Canvas>() == null) units.Add(UnitRootOf(r.transform));
            int n = 0;
            foreach (var u in units.OrderBy(x => x.position.x * 1000f + x.position.z))
            {
                if (u == null || u.parent == null) continue;
                string name = u.name, container = u.parent.name;
                if (name.StartsWith("NewRock") || name.Contains("Stage")) continue;
                bool rock = (name.Contains("Rock") || name.Contains("Stone_Big") || name.Contains("Stone_Medium")) && (container == "Rocks" || container.StartsWith("Stones") || container == "Dressing" || container == "Generated");
                if (!rock) continue;
                var rs = u.GetComponentsInChildren<Renderer>().Where(r => !(r is ParticleSystemRenderer)).ToArray();
                if (rs.Length == 0) continue;
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                float horiz = Mathf.Max(b.size.x, b.size.z);
                if (horiz < 2.4f && b.size.y < 1.8f) continue;                       // only the big ones
                int h = Mathf.Abs(Mathf.RoundToInt(b.center.x * 37f) * 7919 + Mathf.RoundToInt(b.center.z * 53f) * 104729);
                if (h % 100 >= fraction * 100f) continue;
                float ratio = b.size.y / Mathf.Max(horiz, 0.1f);
                var near = RockShapes.OrderBy(s => Mathf.Abs(s.ratio - ratio)).Take(3).ToArray();
                var kind = near[h / 100 % near.Length].kind;
                var go = Instance(kind, u.parent, "NewRock_" + kind);
                if (go == null) continue;
                float sc = Mathf.Clamp(horiz * 0.95f / 4f, 0.4f, 2.4f);
                go.transform.localScale = Vector3.one * sc;
                go.transform.rotation = Quaternion.Euler(0f, h % 360, 0f);
                go.transform.position = new Vector3(b.center.x, GroundBelow(b) - 0.12f * sc, b.center.z);
                Object.DestroyImmediate(u.gameObject);
                n++;
            }
            return n;
        }

        // ---------------------------------------------------------------- menu

        static readonly string[] MapScenes =
        {
            "Assets/Scenes/Map_HungVuong.unity", "Assets/Scenes/Map_SonTinh.unity", "Assets/Scenes/Map_ThuyTinh.unity", "Assets/Scenes/Map_FinalBattle.unity",
        };

        // The new rocks of the open scene back onto the ground (rescaled to at most 2.4 x), the plank bridges redone as continuous runs.
        public static string FixRocksAndBridges()
        {
            int rocks = 0, bridges = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).ToList())
            {
                if (t == null) continue;
                if (t.name.StartsWith("NewRock_"))
                {
                    var rs = t.GetComponentsInChildren<Renderer>();
                    if (rs.Length == 0) continue;
                    var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                    float sc = Mathf.Min(t.localScale.x, 2.4f); t.localScale = Vector3.one * sc;
                    var terrain = Terrain.activeTerrain;
                    if (terrain != null)
                    {
                        float min = float.MaxValue;
                        for (int i = -1; i <= 1; i++) for (int j = -1; j <= 1; j++)
                            min = Mathf.Min(min, terrain.SampleHeight(new Vector3(t.position.x + i * sc * 1.2f, 0f, t.position.z + j * sc * 1.2f)) + terrain.transform.position.y);
                        t.position = new Vector3(t.position.x, min - 0.12f * sc, t.position.z);
                    }
                    rocks++;
                }
                else if (t.name == "PlankBridge" && t.Find("BridgeSegments") != null && DressBridge(t)) bridges++;
            }
            return rocks + " rocks put on the ground, " + bridges + " bridges rebuilt";
        }

        public static string FixScene(string path)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            string r = FixRocksAndBridges();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            return scene.name + ": " + r;
        }

        [MenuItem("Tools/Son Tinh Thuy Tinh/New Props/Fix Rocks And Bridges (open scene)")]
        public static void FixMenu()
        {
            RebuildPrefabs();
            Debug.Log("NewProps: " + FixRocksAndBridges());
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        [MenuItem("Tools/Son Tinh Thuy Tinh/New Props/Swap Props In Maps")]
        public static void SwapInMaps()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("Stop Play mode first."); return; }
            RebuildPrefabs();
            foreach (string path in MapScenes) Debug.Log("NewProps: " + SwapScene(path));
        }

        // Opens the scene, swaps its torches, drums, boats and the villagers' hats, saves it. Returns a short report.
        public static string SwapScene(string path)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int torches = 0, drums = 0, boats = 0, hats = 0, banners = 0, bridges = 0, crystals = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t == null) continue;
                if (t.name == "Torch" && t.Find("Pole") != null && DressTorch(t)) torches++;
                else if (t.name == "BronzeDrum" && t.Find("Drum") != null && DressDrum(t)) drums++;
                else if ((t.name == "Boat" || t.name == "Shipwreck") && t.Find("Keel") != null && DressBoat(t, t.name == "Shipwreck")) boats++;
                else if (t.name == "Banner" && t.Find("Pole") != null && DressBanner(t)) banners++;
                else if (t.name == "PlankBridge" && t.Find("Board") != null && DressBridge(t)) bridges++;
                else if (t.name == "Crystals" && t.Find("Crystal") != null && DressCrystals(t, scene.name.Contains("SonTinh"))) crystals++;
                else if (t.name == "Fisherman" && DressHat(t)) hats++;
                else if (t.name == "VillageElder") RemoveHat(t);
                else if (t.name == "HorseKeeper" && DressHat(t)) hats++;
            }
            int rocks = ReplaceRocks();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return $"{scene.name}: {torches} torches, {drums} drums, {boats} boats, {banners} banners, {hats} hats, {bridges} bridges, {crystals} crystal groups, {rocks} rocks";
        }
    }
}
