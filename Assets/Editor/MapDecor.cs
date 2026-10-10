using System.Collections.Generic;
using System.IO;
using SonTinhThuyTinh.Environment;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SonTinhThuyTinh.EditorTools
{
    // Decoration shared by the gift maps: bronze drum, torches, banners, stilt houses, fireflies and distant mountains.
    // Everything is built from primitives, lathe meshes and particle systems, so no art assets are needed. Materials and meshes
    // are saved in Assets/Art/Maps/Decor so scenes keep referencing real assets.
    public static class MapDecor
    {
        const string Dir = "Assets/Art/Maps/Decor";

        public static readonly Color MountainGreen = new(0.14f, 0.24f, 0.18f);
        public static readonly Color MountainBlue = new(0.20f, 0.30f, 0.42f);

        // ---------------------------------------------------------------- materials and generated assets

        internal static Material Lit(string name, Color color, float metallic, float smoothness, bool doubleSided = false)
        {
            string path = $"{Dir}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Directory.CreateDirectory(Dir);
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Cull", doubleSided ? 0f : 2f);
            EditorUtility.SetDirty(material);
            return material;
        }

        // Soft round dot used for fire and fireflies (additive particles).
        static Material Glow()
        {
            string texturePath = $"{Dir}/glow_dot.png";
            if (!File.Exists(texturePath))
            {
                Directory.CreateDirectory(Dir);
                const int size = 64;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f)) / (size * 0.5f);
                        float a = Mathf.Pow(Mathf.Clamp01(1f - d), 2f);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                File.WriteAllBytes(texturePath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(texturePath);
            }

            string path = $"{Dir}/Mat_Glow.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Legacy Shaders/Particles/Additive"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Mesh SavedMesh(string name)
        {
            string path = $"{Dir}/{name}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                Directory.CreateDirectory(Dir);
                mesh = new Mesh { name = name };
                AssetDatabase.CreateAsset(mesh, path);
            }
            else mesh.Clear();
            return mesh;
        }

        // Surface of revolution around the Y axis. profile = (radius, height) from bottom to top.
        static void Revolve(Mesh mesh, IList<Vector2> profile, int segments)
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (int i = 0; i < profile.Count; i++)
                for (int s = 0; s <= segments; s++)
                {
                    float a = s * Mathf.PI * 2f / segments;
                    vertices.Add(new Vector3(Mathf.Cos(a) * profile[i].x, profile[i].y, Mathf.Sin(a) * profile[i].x));
                    uvs.Add(new Vector2(s / (float)segments, i / (float)(profile.Count - 1)));
                }
            int row = segments + 1;
            for (int i = 0; i < profile.Count - 1; i++)
                for (int s = 0; s < segments; s++)
                {
                    int a = i * row + s, b = a + 1, c = a + row, d = c + 1;
                    triangles.AddRange(new[] { a, c, b, b, c, d });
                }
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
        }

        static Mesh BronzeDrumMesh()
        {
            Mesh mesh = SavedMesh("Mesh_BronzeDrum");
            // Dong Son drum: flared foot, narrow waist, rounded chest, wide overhanging tympanum with a raised sun disc in the middle.
            var profile = new List<Vector2>
            {
                new(0.62f, 0f), new(0.60f, 0.12f), new(0.50f, 0.30f), new(0.40f, 0.45f), new(0.37f, 0.58f), new(0.43f, 0.74f), new(0.55f, 0.90f),
                new(0.68f, 1.00f), new(0.76f, 1.06f), new(0.76f, 1.14f), new(0.70f, 1.17f),
                new(0.50f, 1.17f), new(0.50f, 1.19f), new(0.34f, 1.19f), new(0.34f, 1.21f), new(0.16f, 1.21f), new(0.16f, 1.25f), new(0f, 1.26f),
            };
            Revolve(mesh, profile, 48);
            return mesh;
        }

        // Unit mountain: radius 1, height 1 (scale it in the scene). Radial falloff with noisy ridges.
        static Mesh MountainMesh(int seed)
        {
            Mesh mesh = SavedMesh("Mesh_Mountain_" + seed);
            const int rings = 36, segments = 48;
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            float o = seed * 17.3f;
            for (int k = 0; k <= rings; k++)
                for (int s = 0; s <= segments; s++)
                {
                    float r = k / (float)rings, a = s * Mathf.PI * 2f / segments;
                    float x = Mathf.Cos(a) * r, z = Mathf.Sin(a) * r;
                    float ridge = Mathf.PerlinNoise(x * 3.2f + o, z * 3.2f + o) * 0.6f + Mathf.PerlinNoise(x * 9f + o, z * 9f + o) * 0.25f;
                    float h = Mathf.Clamp01(1f - r) * (0.45f + 1.1f * ridge);   // plain cone shape broken up by noisy ridges
                    vertices.Add(new Vector3(x, h, z));
                    uvs.Add(new Vector2(x * 0.5f + 0.5f, z * 0.5f + 0.5f));
                }
            int row = segments + 1;
            for (int k = 0; k < rings; k++)
                for (int s = 0; s < segments; s++)
                {
                    int a = k * row + s, b = a + 1, c = a + row, d = c + 1;
                    triangles.AddRange(new[] { a, b, c, b, d, c });
                }
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        // ---------------------------------------------------------------- building blocks

        internal static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 localScale, Quaternion localRotation, Material material, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        internal static Transform Empty(string name, Transform parent, Vector3 position, float yaw)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            return t;
        }

        // ---------------------------------------------------------------- props

        public static GameObject BronzeDrum(Transform parent, Vector3 groundPosition, float yaw, float scale)
        {
            Transform root = Empty("BronzeDrum", parent, groundPosition, yaw);
            var mesh = new GameObject("Drum");
            mesh.transform.SetParent(root, false);
            mesh.transform.localScale = Vector3.one * scale;
            mesh.AddComponent<MeshFilter>().sharedMesh = BronzeDrumMesh();
            mesh.AddComponent<MeshRenderer>().sharedMaterial = Lit("Mat_Bronze", new Color(0.66f, 0.47f, 0.2f), 0.9f, 0.55f, true);
            var collider = mesh.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0f, 0.6f, 0f);
            collider.radius = 0.6f;
            collider.height = 1.25f;
            return root.gameObject;
        }

        // A floating log lying across the path (axis along local X), walkable thanks to a box collider.
        public static GameObject Log(Transform parent, Vector3 center, float yaw, float roll, float length, float diameter)
        {
            Transform root = Empty("Log", parent, center, yaw);
            root.rotation = Quaternion.Euler(0f, yaw, roll);
            Part(PrimitiveType.Cylinder, "Trunk", root, Vector3.zero, new Vector3(diameter, length * 0.5f, diameter), Quaternion.Euler(0f, 0f, 90f),
                Lit("Mat_Log", new Color(0.26f, 0.19f, 0.12f), 0f, 0.3f));
            var box = root.gameObject.AddComponent<BoxCollider>();
            box.size = new Vector3(length, diameter * 0.9f, diameter);
            return root.gameObject;
        }

        public static GameObject Torch(Transform parent, Vector3 groundPosition)
        {
            Transform root = Empty("Torch", parent, groundPosition, 0f);
            Material wood = Lit("Mat_Wood", new Color(0.33f, 0.22f, 0.12f), 0f, 0.15f);
            Part(PrimitiveType.Cylinder, "Pole", root, new Vector3(0f, 0.95f, 0f), new Vector3(0.14f, 0.95f, 0.14f), Quaternion.identity, wood);
            Part(PrimitiveType.Cylinder, "Bowl", root, new Vector3(0f, 1.95f, 0f), new Vector3(0.32f, 0.1f, 0.32f), Quaternion.identity, wood);

            var fire = new GameObject("Fire");
            fire.transform.SetParent(root, false);
            fire.transform.localPosition = new Vector3(0f, 2.05f, 0f);
            fire.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var ps = fire.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 0.7f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.55f);
            main.startColor = new Color(1f, 0.62f, 0.2f, 0.9f);
            main.maxParticles = 60;
            main.prewarm = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 26f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8f;
            shape.radius = 0.07f;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.1f)));
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.85f, 0.4f), 0f), new GradientColorKey(new Color(1f, 0.35f, 0.1f), 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
            fire.GetComponent<ParticleSystemRenderer>().sharedMaterial = Glow();

            var lightObject = new GameObject("Light");
            lightObject.transform.SetParent(root, false);
            lightObject.transform.localPosition = new Vector3(0f, 2.4f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.62f, 0.28f);
            light.range = 9f;
            light.intensity = 3.2f;
            light.shadows = LightShadows.None;
            lightObject.AddComponent<LightFlicker>();
            return root.gameObject;
        }

        public static GameObject Banner(Transform parent, Vector3 groundPosition, float yaw, Color cloth)
        {
            Transform root = Empty("Banner", parent, groundPosition, yaw);
            Material wood = Lit("Mat_Wood", new Color(0.33f, 0.22f, 0.12f), 0f, 0.15f);
            Part(PrimitiveType.Cylinder, "Pole", root, new Vector3(0f, 2.6f, 0f), new Vector3(0.12f, 2.6f, 0.12f), Quaternion.identity, wood, true);
            Part(PrimitiveType.Cylinder, "Crossbar", root, new Vector3(0.45f, 4.9f, 0f), new Vector3(0.06f, 0.5f, 0.06f), Quaternion.Euler(0f, 0f, 90f), wood);
            Part(PrimitiveType.Quad, "Cloth", root, new Vector3(0.45f, 3.7f, 0f), new Vector3(0.9f, 2.4f, 1f), Quaternion.identity,
                Lit("Mat_Banner_" + ColorUtility.ToHtmlStringRGB(cloth), cloth, 0f, 0.1f, true));
            return root.gameObject;
        }

        static int houseVariant;

        // Stilt house: one of the two Meshy village houses (VillageModelSetup, alternating), its stairs on local -Z. Without the models:
        // platform on posts, low walls with a doorway, A-frame thatch roof and a ladder.
        public static GameObject StiltHouse(Transform parent, Vector3 groundPosition, float yaw)
        {
            Transform root = Empty("StiltHouse", parent, groundPosition, yaw);
            if (VillageModelSetup.DressHouse(root, VillageModelSetup.VariantFor(groundPosition, houseVariant++)) != null) return root.gameObject;
            Material wood = Lit("Mat_Wood", new Color(0.33f, 0.22f, 0.12f), 0f, 0.15f);
            Material plank = Lit("Mat_Plank", new Color(0.45f, 0.31f, 0.18f), 0f, 0.1f);
            Material thatch = Lit("Mat_Thatch", new Color(0.50f, 0.40f, 0.20f), 0f, 0.05f, true);

            const float floorY = 2.1f, width = 4.6f, depth = 3.8f;
            foreach (float sx in new[] { -1f, 0f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                    Part(PrimitiveType.Cylinder, "Post", root, new Vector3(sx * (width * 0.5f - 0.2f), floorY * 0.5f - 0.8f, sz * (depth * 0.5f - 0.2f)),
                        new Vector3(0.28f, floorY * 0.5f + 0.8f, 0.28f), Quaternion.identity, wood, true);
            Part(PrimitiveType.Cube, "Floor", root, new Vector3(0f, floorY, 0f), new Vector3(width, 0.22f, depth), Quaternion.identity, plank, true);

            // back and side walls, front wall with a doorway
            Part(PrimitiveType.Cube, "WallBack", root, new Vector3(0f, floorY + 0.7f, depth * 0.5f - 0.06f), new Vector3(width, 1.4f, 0.12f), Quaternion.identity, plank);
            Part(PrimitiveType.Cube, "WallLeft", root, new Vector3(-width * 0.5f + 0.06f, floorY + 0.7f, 0f), new Vector3(0.12f, 1.4f, depth), Quaternion.identity, plank);
            Part(PrimitiveType.Cube, "WallRight", root, new Vector3(width * 0.5f - 0.06f, floorY + 0.7f, 0f), new Vector3(0.12f, 1.4f, depth), Quaternion.identity, plank);
            Part(PrimitiveType.Cube, "WallFrontL", root, new Vector3(-width * 0.5f + 0.9f, floorY + 0.7f, -depth * 0.5f + 0.06f), new Vector3(1.8f, 1.4f, 0.12f), Quaternion.identity, plank);
            Part(PrimitiveType.Cube, "WallFrontR", root, new Vector3(width * 0.5f - 0.9f, floorY + 0.7f, -depth * 0.5f + 0.06f), new Vector3(1.8f, 1.4f, 0.12f), Quaternion.identity, plank);

            // roof: two slabs meeting at the ridge, overhanging the walls
            float pitch = 38f, slab = depth * 0.5f / Mathf.Cos(pitch * Mathf.Deg2Rad) + 0.7f;
            float ridgeY = floorY + 1.4f + depth * 0.5f * Mathf.Tan(pitch * Mathf.Deg2Rad) * 0.55f + 0.25f;
            Part(PrimitiveType.Cube, "RoofFront", root, new Vector3(0f, ridgeY - 0.3f, -depth * 0.28f), new Vector3(width + 1.2f, 0.16f, slab), Quaternion.Euler(-pitch, 0f, 0f), thatch);
            Part(PrimitiveType.Cube, "RoofBack", root, new Vector3(0f, ridgeY - 0.3f, depth * 0.28f), new Vector3(width + 1.2f, 0.16f, slab), Quaternion.Euler(pitch, 0f, 0f), thatch);
            Part(PrimitiveType.Cube, "Ridge", root, new Vector3(0f, ridgeY + 0.12f, 0f), new Vector3(width + 1.3f, 0.2f, 0.4f), Quaternion.identity, wood);

            // ladder to the doorway
            Part(PrimitiveType.Cube, "LadderL", root, new Vector3(-0.35f, floorY * 0.5f - 0.1f, -depth * 0.5f - 0.7f), new Vector3(0.1f, 2.5f, 0.1f), Quaternion.Euler(-24f, 0f, 0f), wood, true);
            Part(PrimitiveType.Cube, "LadderR", root, new Vector3(0.35f, floorY * 0.5f - 0.1f, -depth * 0.5f - 0.7f), new Vector3(0.1f, 2.5f, 0.1f), Quaternion.Euler(-24f, 0f, 0f), wood, true);
            return root.gameObject;
        }

        // Walks from `from` along `dir` over the terrain to the first point `rise` metres above baseHeight (the lip), then walks back down to
        // where the ground drops under 0.6 m above baseHeight (the foot). Hummocks before the wall do not fool it. `ground` gives the height at (x, z).
        public static bool FindSlope(System.Func<float, float, float> ground, Vector2 from, Vector2 dir, float baseHeight, float rise, out Vector2 bottom, out Vector2 top)
        {
            bottom = top = from;
            float lip = -1f;
            for (float t = 0f; t < 100f; t += 0.5f)
            {
                Vector2 p = from + dir * t;
                if (ground(p.x, p.y) > baseHeight + rise) { lip = t; break; }
            }
            if (lip < 0f) return false;

            float foot = lip;
            while (foot > 0f && ground(from.x + dir.x * foot, from.y + dir.y * foot) > baseHeight + 0.6f) foot -= 0.5f;
            bottom = from + dir * foot;
            top = from + dir * lip;
            return true;
        }

        static Material WaterfallMaterial()
        {
            string path = $"{Dir}/Mat_Waterfall.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = AssetDatabase.LoadAssetAtPath<Shader>($"{Dir}/Waterfall.shader");
                if (shader == null) shader = Shader.Find("SonTinhThuyTinh/Waterfall");
                if (shader == null) { Debug.LogWarning("Waterfall shader not found."); return null; }
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_Color", new Color(0.55f, 0.80f, 0.92f, 0.5f));
            material.SetColor("_FoamColor", Color.white);
            EditorUtility.SetDirty(material);
            return material;
        }

        // A waterfall pouring down a slope: a ribbon mesh that follows the ground from `top` to `bottom` (x, z positions) with a scrolling water
        // shader, plus mist and spray at the foot. `id` names the generated mesh asset.
        public static GameObject Waterfall(Transform parent, System.Func<float, float, float> ground, Vector2 bottom, Vector2 top, float width, string id)
        {
            Transform root = Empty("Waterfall_" + id, parent, Vector3.zero, 0f);
            Vector2 flow = (bottom - top).normalized, side = new(-flow.y, flow.x);

            const int rows = 30, columns = 8;
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (int r = 0; r <= rows; r++)
            {
                float t = r / (float)rows;
                Vector2 centre = Vector2.Lerp(top, bottom, t);
                for (int c = 0; c <= columns; c++)
                {
                    float u = c / (float)columns;
                    Vector2 p = centre + side * ((u - 0.5f) * width);
                    float lift = 0.25f + 0.25f * Mathf.Sin(u * Mathf.PI);   // the sheet bulges a little in the middle
                    vertices.Add(new Vector3(p.x, ground(p.x, p.y) + lift, p.y));
                    uvs.Add(new Vector2(u, t));
                }
            }
            int row = columns + 1;
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < columns; c++)
                {
                    int a = r * row + c, b = a + 1, d = a + row, e = d + 1;
                    triangles.AddRange(new[] { a, d, b, b, d, e });
                }
            Mesh mesh = SavedMesh("Mesh_Waterfall_" + id);
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);

            var sheet = new GameObject("Sheet");
            sheet.transform.SetParent(root, false);
            sheet.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = sheet.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = WaterfallMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            Vector3 foot = new(bottom.x, ground(bottom.x, bottom.y) + 0.4f, bottom.y);
            Material glow = Glow();

            var mistObject = new GameObject("Mist");
            mistObject.transform.SetParent(root, false);
            mistObject.transform.position = foot;
            var mist = mistObject.AddComponent<ParticleSystem>();
            var mistMain = mist.main;
            mistMain.startLifetime = 3.5f;
            mistMain.startSpeed = 0.5f;
            mistMain.startSize = new ParticleSystem.MinMaxCurve(3f, 6f);
            mistMain.startColor = new Color(1f, 1f, 1f, 0.12f);
            mistMain.maxParticles = 80;
            mistMain.prewarm = true;
            mistMain.simulationSpace = ParticleSystemSimulationSpace.World;
            var mistEmission = mist.emission;
            mistEmission.rateOverTime = 14f;
            var mistShape = mist.shape;
            mistShape.shapeType = ParticleSystemShapeType.Box;
            mistShape.scale = new Vector3(width * 0.8f, 0.5f, 2f);
            mistShape.rotation = new Vector3(0f, Mathf.Atan2(side.x, side.y) * Mathf.Rad2Deg, 0f);
            var mistFade = mist.colorOverLifetime;
            mistFade.enabled = true;
            var mistGradient = new Gradient();
            mistGradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0f, 1f) });
            mistFade.color = mistGradient;
            mistObject.GetComponent<ParticleSystemRenderer>().sharedMaterial = glow;

            var sprayObject = new GameObject("Spray");
            sprayObject.transform.SetParent(root, false);
            sprayObject.transform.position = foot;
            sprayObject.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            var spray = sprayObject.AddComponent<ParticleSystem>();
            var sprayMain = spray.main;
            sprayMain.startLifetime = 0.9f;
            sprayMain.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 5f);
            sprayMain.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
            sprayMain.startColor = new Color(1f, 1f, 1f, 0.7f);
            sprayMain.gravityModifier = 1.2f;
            sprayMain.maxParticles = 120;
            sprayMain.prewarm = true;
            sprayMain.simulationSpace = ParticleSystemSimulationSpace.World;
            var sprayEmission = spray.emission;
            sprayEmission.rateOverTime = 40f;
            var sprayShape = spray.shape;
            sprayShape.shapeType = ParticleSystemShapeType.Cone;
            sprayShape.angle = 30f;
            sprayShape.radius = width * 0.35f;
            sprayObject.GetComponent<ParticleSystemRenderer>().sharedMaterial = glow;
            return root.gameObject;
        }

        // A round pool of water lying on flat ground, where a waterfall lands.
        public static GameObject Pool(Transform parent, Vector3 centre, float radius, Material water)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Pool";
            go.transform.SetParent(parent, false);
            go.transform.position = centre;
            go.transform.localScale = new Vector3(radius * 2f, 0.02f, radius * 2f);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var renderer = go.GetComponent<MeshRenderer>();
            if (water != null) renderer.sharedMaterial = water;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
        }

        // ---------------------------------------------------------------- camp, crystals, gate, roofs, plank walks (from the world map reference)

        // Flames (particles) and a flickering light, put under `parent` at localPosition.
        static void AddFlame(Transform parent, Vector3 localPosition, float scale, float lightRange, float lightIntensity)
        {
            var fire = new GameObject("Fire");
            fire.transform.SetParent(parent, false);
            fire.transform.localPosition = localPosition;
            fire.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var ps = fire.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 0.8f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.7f, 1.3f) ;
            main.startSize = new ParticleSystem.MinMaxCurve(0.4f * scale, 0.7f * scale);
            main.startColor = new Color(1f, 0.62f, 0.2f, 0.9f);
            main.maxParticles = 80;
            main.prewarm = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 30f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 10f;
            shape.radius = 0.25f * scale;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.1f)));
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.85f, 0.4f), 0f), new GradientColorKey(new Color(1f, 0.35f, 0.1f), 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
            fire.GetComponent<ParticleSystemRenderer>().sharedMaterial = Glow();

            var lightObject = new GameObject("Light");
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localPosition = localPosition + Vector3.up * 0.6f;
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.62f, 0.28f);
            light.range = lightRange;
            light.intensity = lightIntensity;
            light.shadows = LightShadows.None;
            lightObject.AddComponent<LightFlicker>();
        }

        // A camp fire: a ring of stones, crossed logs, flames and a warm light.
        public static GameObject Campfire(Transform parent, Vector3 groundPosition)
        {
            Transform root = Empty("Campfire", parent, groundPosition, 0f);
            Material stone = Lit("Mat_CampStone", new Color(0.42f, 0.41f, 0.39f), 0f, 0.1f);
            Material wood = Lit("Mat_Wood", new Color(0.33f, 0.22f, 0.12f), 0f, 0.15f);
            for (int i = 0; i < 9; i++)
            {
                float a = i * 40f * Mathf.Deg2Rad;
                Part(PrimitiveType.Sphere, "Stone", root, new Vector3(Mathf.Sin(a), 0.12f, Mathf.Cos(a)) * 0.85f, new Vector3(0.38f, 0.26f, 0.34f), Quaternion.Euler(0f, i * 40f, 0f), stone);
            }
            for (int i = 0; i < 3; i++)
                Part(PrimitiveType.Cylinder, "Log", root, new Vector3(0f, 0.22f, 0f), new Vector3(0.16f, 0.55f, 0.16f), Quaternion.Euler(78f, i * 60f, 0f), wood);
            AddFlame(root, new Vector3(0f, 0.35f, 0f), 1f, 12f, 3.6f);
            return root.gameObject;
        }

        // A canvas tent: two slanted sheets and a ridge pole.
        public static GameObject Tent(Transform parent, Vector3 groundPosition, float yaw, Color canvas)
        {
            Transform root = Empty("Tent", parent, groundPosition, yaw);
            Material cloth = Lit("Mat_Canvas_" + ColorUtility.ToHtmlStringRGB(canvas), canvas, 0f, 0.05f, true);
            Material wood = Lit("Mat_Wood", new Color(0.33f, 0.22f, 0.12f), 0f, 0.15f);
            const float length = 3.2f, half = 1.35f, height = 1.6f;
            float pitch = Mathf.Atan2(height, half) * Mathf.Rad2Deg, slope = Mathf.Sqrt(half * half + height * height) + 0.1f;
            Part(PrimitiveType.Cube, "SheetLeft", root, new Vector3(-half * 0.5f, height * 0.5f, 0f), new Vector3(slope, 0.05f, length), Quaternion.Euler(0f, 0f, pitch), cloth);
            Part(PrimitiveType.Cube, "SheetRight", root, new Vector3(half * 0.5f, height * 0.5f, 0f), new Vector3(slope, 0.05f, length), Quaternion.Euler(0f, 0f, -pitch), cloth);
            Part(PrimitiveType.Cylinder, "Pole", root, new Vector3(0f, height * 0.5f, 0f), new Vector3(0.07f, length * 0.5f + 0.2f, 0.07f), Quaternion.Euler(90f, 0f, 0f), wood);
            Part(PrimitiveType.Cube, "Back", root, new Vector3(0f, height * 0.38f, length * 0.5f), new Vector3(half * 1.5f, height * 0.8f, 0.05f), Quaternion.identity, cloth);
            return root.gameObject;
        }

        static Material CrystalMaterial(Color color)
        {
            Material material = Lit("Mat_Crystal_" + ColorUtility.ToHtmlStringRGB(color), color, 0.2f, 0.92f);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 2.2f);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(material);
            return material;
        }

        // A cluster of glowing crystals (stretched cubes set at angles) with a coloured light.
        public static GameObject Crystals(Transform parent, Vector3 groundPosition, int count, Color color, float scale)
        {
            Transform root = Empty("Crystals", parent, groundPosition, 0f);
            Material glow = CrystalMaterial(color);
            var rng = new System.Random(groundPosition.GetHashCode());
            for (int i = 0; i < count; i++)
            {
                float a = (float)rng.NextDouble() * 360f, lean = 8f + (float)rng.NextDouble() * 24f, r = 0.15f + (float)rng.NextDouble() * 0.6f * scale;
                float h = (0.9f + (float)rng.NextDouble() * 1.4f) * scale;
                Vector3 p = new(Mathf.Sin(a * Mathf.Deg2Rad) * r, h * 0.45f, Mathf.Cos(a * Mathf.Deg2Rad) * r);
                Part(PrimitiveType.Cube, "Crystal", root, p, new Vector3(0.28f * scale, h, 0.28f * scale), Quaternion.Euler(lean, a, lean * 0.6f) * Quaternion.Euler(0f, 45f, 0f), glow);
            }
            var lightObject = new GameObject("Light");
            lightObject.transform.SetParent(root, false);
            lightObject.transform.localPosition = new Vector3(0f, 1.2f * scale, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = 9f * scale;
            light.intensity = 3f;
            light.shadows = LightShadows.None;
            return root.gameObject;
        }

        // Hip roof: four slanted slabs rising from the base rectangle (width x depth, `centre` is in the parent's space) to a ridge `height` higher.
        public static GameObject Roof(Transform parent, Vector3 centre, float width, float depth, float height, Color tile)
        {
            Transform root = Empty("Roof", parent, Vector3.zero, 0f);
            root.localPosition = centre;
            root.localRotation = Quaternion.identity;
            Material material = Lit("Mat_RoofTile_" + ColorUtility.ToHtmlStringRGB(tile), tile, 0f, 0.25f, true);
            float hd = depth * 0.5f, hw = width * 0.5f;
            float pitchZ = Mathf.Atan2(height, hd) * Mathf.Rad2Deg, lengthZ = Mathf.Sqrt(hd * hd + height * height) + 0.6f;
            float pitchX = Mathf.Atan2(height, hw) * Mathf.Rad2Deg, lengthX = Mathf.Sqrt(hw * hw + height * height) + 0.6f;
            Part(PrimitiveType.Cube, "Front", root, new Vector3(0f, height * 0.5f, -hd * 0.5f), new Vector3(width + 0.8f, 0.2f, lengthZ), Quaternion.Euler(-pitchZ, 0f, 0f), material);
            Part(PrimitiveType.Cube, "Back", root, new Vector3(0f, height * 0.5f, hd * 0.5f), new Vector3(width + 0.8f, 0.2f, lengthZ), Quaternion.Euler(pitchZ, 0f, 0f), material);
            Part(PrimitiveType.Cube, "Left", root, new Vector3(-hw * 0.5f, height * 0.5f, 0f), new Vector3(lengthX, 0.2f, depth - 0.2f), Quaternion.Euler(0f, 0f, pitchX), material);
            Part(PrimitiveType.Cube, "Right", root, new Vector3(hw * 0.5f, height * 0.5f, 0f), new Vector3(lengthX, 0.2f, depth - 0.2f), Quaternion.Euler(0f, 0f, -pitchX), material);
            return root.gameObject;
        }

        // A gate in the Vietnamese village-gate manner: two red-brown pillars, two beams, a small tiled roof and two banners.
        public static GameObject Gate(Transform parent, Vector3 groundPosition, float yaw, float width, Color banner)
        {
            Transform root = Empty("Gate", parent, groundPosition, yaw);
            Material wood = Lit("Mat_RedWood", new Color(0.45f, 0.13f, 0.08f), 0f, 0.2f);
            foreach (float s in new[] { -1f, 1f })
                Part(PrimitiveType.Cylinder, "Pillar", root, new Vector3(s * width * 0.5f, 3.6f, 0f), new Vector3(0.8f, 3.6f, 0.8f), Quaternion.identity, wood, true);
            Part(PrimitiveType.Cube, "Beam", root, new Vector3(0f, 6.6f, 0f), new Vector3(width + 1.6f, 0.6f, 1f), Quaternion.identity, wood);
            Part(PrimitiveType.Cube, "LowerBeam", root, new Vector3(0f, 5.4f, 0f), new Vector3(width + 0.4f, 0.4f, 0.7f), Quaternion.identity, wood);
            // two tiers of tiled roof, a gold finial on the ridge (a dinh gate rather than a torii)
            Roof(root, new Vector3(0f, 6.9f, 0f), width + 3.6f, 3.4f, 1.7f, new Color(0.30f, 0.17f, 0.12f));
            Roof(root, new Vector3(0f, 8.5f, 0f), width - 0.6f, 2.2f, 1.2f, new Color(0.30f, 0.17f, 0.12f));
            Part(PrimitiveType.Sphere, "Finial", root, new Vector3(0f, 9.9f, 0f), Vector3.one * 0.55f, Quaternion.identity,
                Lit("Mat_Gold", new Color(0.93f, 0.72f, 0.20f), 0.8f, 0.6f));
            foreach (float s in new[] { -1f, 1f })
                Part(PrimitiveType.Quad, "Banner", root, new Vector3(s * (width * 0.5f - 1.3f), 3.5f, -0.1f), new Vector3(1.1f, 3.4f, 1f), Quaternion.identity,
                    Lit("Mat_Banner_" + ColorUtility.ToHtmlStringRGB(banner), banner, 0f, 0.1f, true));
            return root.gameObject;
        }

        // Wooden boards laid on the ground along a polyline (decoration only, no colliders), with posts and rope rails.
        public static GameObject PlankWalk(Transform parent, System.Func<float, float, float> ground, IList<Vector2> points, float width)
        {
            Transform root = Empty("PlankWalk", parent, Vector3.zero, 0f);
            Material plank = Lit("Mat_Plank", new Color(0.45f, 0.31f, 0.18f), 0f, 0.1f);
            Material wood = Lit("Mat_Wood", new Color(0.33f, 0.22f, 0.12f), 0f, 0.15f);
            Vector3 lastPost = default;
            bool hasPost = false;
            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector2 a = points[i], b = points[i + 1], d = b - a;
                float length = d.magnitude;
                int steps = Mathf.Max(1, Mathf.CeilToInt(length / 0.62f));
                Vector2 dir = d / length;
                for (int s = 0; s < steps; s++)
                {
                    Vector2 p = a + d * ((s + 0.5f) / steps);
                    Vector2 ahead = p + dir * 0.3f, behind = p - dir * 0.3f;
                    float y = ground(p.x, p.y) + 0.08f, rise = ground(ahead.x, ahead.y) - ground(behind.x, behind.y);
                    Quaternion rotation = Quaternion.LookRotation(new Vector3(dir.x, rise / 0.6f, dir.y));
                    Part(PrimitiveType.Cube, "Plank", root, new Vector3(p.x, y, p.y), new Vector3(width, 0.12f, 0.54f), rotation, plank);
                }
                for (float t = 0f; t < length; t += 3f)
                {
                    Vector2 p = a + dir * t, side = new(-dir.y, dir.x);
                    foreach (float sign in new[] { -1f, 1f })
                    {
                        Vector2 q = p + side * (sign * (width * 0.5f + 0.1f));
                        float y = ground(q.x, q.y);
                        Part(PrimitiveType.Cylinder, "Post", root, new Vector3(q.x, y + 0.5f, q.y), new Vector3(0.12f, 0.6f, 0.12f), Quaternion.identity, wood);
                    }
                    if (hasPost)
                    {
                        Vector3 newPost = new(p.x, ground(p.x, p.y), p.y);
                        foreach (float sign in new[] { -1f, 1f })
                        {
                            Vector3 offset = new Vector3(side.x, 0f, side.y) * (sign * (width * 0.5f + 0.1f)) + Vector3.up * 1.0f;
                            Vector3 from = lastPost + offset, to = newPost + offset;
                            Part(PrimitiveType.Cube, "Rail", root, (from + to) * 0.5f, new Vector3(0.05f, 0.05f, Vector3.Distance(from, to)), Quaternion.LookRotation(to - from), wood);
                        }
                    }
                    lastPost = new Vector3(p.x, ground(p.x, p.y), p.y);
                    hasPost = true;
                }
            }
            return root.gameObject;
        }

        // A walkable plank bridge between two world points (boards have box colliders), on posts, with rope rails.
        public static GameObject PlankBridge(Transform parent, Vector3 from, Vector3 to, float width)
        {
            Transform root = Empty("PlankBridge", parent, Vector3.zero, 0f);
            Material plank = Lit("Mat_Plank", new Color(0.45f, 0.31f, 0.18f), 0f, 0.1f);
            Material wood = Lit("Mat_Wood", new Color(0.33f, 0.22f, 0.12f), 0f, 0.15f);
            Vector3 d = to - from;
            float length = d.magnitude;
            Vector3 dir = d / length, side = Vector3.Cross(Vector3.up, new Vector3(dir.x, 0f, dir.z).normalized);
            Quaternion rotation = Quaternion.LookRotation(dir);
            int boards = Mathf.Max(1, Mathf.CeilToInt(length / 0.62f));
            for (int i = 0; i < boards; i++)
                Part(PrimitiveType.Cube, "Board", root, from + d * ((i + 0.5f) / boards), new Vector3(width, 0.14f, length / boards * 0.9f), rotation, plank, true);

            int posts = Mathf.Max(2, Mathf.CeilToInt(length / 3.5f) + 1);
            for (int i = 0; i < posts; i++)
            {
                Vector3 p = from + d * (i / (float)(posts - 1));
                foreach (float sign in new[] { -1f, 1f })
                    Part(PrimitiveType.Cylinder, "Post", root, p + side * (sign * (width * 0.5f + 0.1f)) + Vector3.up * (-0.2f), new Vector3(0.16f, 1.2f, 0.16f), Quaternion.identity, wood);
            }
            foreach (float sign in new[] { -1f, 1f })
                Part(PrimitiveType.Cube, "Rail", root, (from + to) * 0.5f + side * (sign * (width * 0.5f + 0.1f)) + Vector3.up * 0.95f, new Vector3(0.06f, 0.06f, length), rotation, wood);
            return root.gameObject;
        }

        public static GameObject Fireflies(Transform parent, Vector3 center, Vector3 size, Color color)
        {
            var go = new GameObject("Fireflies");
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 8f;
            main.startSpeed = 0.1f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
            main.startColor = color;
            main.maxParticles = 160;
            main.prewarm = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 14f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = size;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.9f;
            noise.frequency = 0.25f;
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0.4f, 0.5f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = Glow();
            return go;
        }

        // Hazy mountains far beyond the playable area, the "Tan Vien" skyline. Fog does most of the work.
        public static void DistantMountains(Transform parent, Vector3 center, int count, Color tint, float minDistance, float maxDistance, int seed)
        {
            var rng = new System.Random(seed);
            Material material = Lit(tint == MountainBlue ? "Mat_Mountain_Blue" : "Mat_Mountain_Green", tint, 0f, 0.05f, true);
            var group = new GameObject("DistantMountains").transform;
            group.SetParent(parent, false);
            for (int i = 0; i < count; i++)
            {
                float angle = (i + (float)rng.NextDouble() * 0.7f) / count * Mathf.PI * 2f;
                float distance = Mathf.Lerp(minDistance, maxDistance, (float)rng.NextDouble());
                // big cones that start well outside the playable area (it spans about 215 m from the map centre to a corner)
                float height = Mathf.Lerp(150f, 240f, (float)rng.NextDouble()), radius = Mathf.Lerp(90f, 120f, (float)rng.NextDouble());

                var go = new GameObject("Mountain_" + i);
                go.transform.SetParent(group, false);
                go.transform.position = center + new Vector3(Mathf.Sin(angle) * distance, -25f, Mathf.Cos(angle) * distance);
                go.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                go.transform.localScale = new Vector3(radius, height, radius);
                go.AddComponent<MeshFilter>().sharedMesh = MountainMesh(i % 3);
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                go.isStatic = true;
            }
        }
    }
}
