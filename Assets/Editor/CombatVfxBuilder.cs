using System.Collections.Generic;
using System.IO;
using SonTinhThuyTinh.Combat;
using UnityEditor;
using UnityEngine;

namespace SonTinhThuyTinh.EditorTools
{
    // The upgraded water effects of Thuy Tinh's fighting:
    // Particle systems, 3D crescent slash arcs, URP shockwave distortion rings,
    // procedural textures, and dynamic impact lighting for Black Myth Wukong tier feel.
    // Prefabs go to Assets/Prefabs/Combat, materials and textures to Assets/Art/Combat. Safe to run again.
    public static class CombatVfxBuilder
    {
        const string ArtDir = "Assets/Art/Combat/";
        public const string PrefabDir = "Assets/Prefabs/Combat/";
        const string SwordPrefab = "Assets/Prefabs/Weapons/Sword.prefab";

        internal static Material additive, alpha, ripple, trailMaterial, crack, splash, distortion, slashWater;
        internal static Mesh slashArcMesh;

        // ---------------------------------------------------------------- materials and textures

        static Texture2D SoftTexture(string path, bool ring)
        {
            if (!File.Exists(path))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                const int size = 128;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f)) / (size * 0.5f);
                        float a = ring ? Mathf.Clamp01(1f - Mathf.Abs(d - 0.82f) / 0.14f) : Mathf.Pow(Mathf.Clamp01(1f - d), 1.6f);
                        a *= Mathf.Clamp01((1f - d) * 8f);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        internal static Texture2D CrackTexture(string path)
        {
            if (!File.Exists(path))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                const int size = 256;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
                float radius = size * 0.48f;
                const int numBranches = 9;
                float[] branchAngles = new float[numBranches];
                for (int b = 0; b < numBranches; b++)
                    branchAngles[b] = (b * Mathf.PI * 2f / numBranches) + (Mathf.Sin(b * 3.7f) * 0.22f);

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        Vector2 pos = new Vector2(x + 0.5f, y + 0.5f);
                        float dist = Vector2.Distance(pos, center);
                        float normDist = dist / radius;
                        if (normDist > 1f)
                        {
                            tex.SetPixel(x, y, new Color(1f, 1f, 1f, 0f));
                            continue;
                        }

                        float angle = Mathf.Atan2(pos.y - center.y, pos.x - center.x);
                        if (angle < 0f) angle += Mathf.PI * 2f;

                        float minDiff = float.MaxValue;
                        for (int b = 0; b < numBranches; b++)
                        {
                            float noise = Mathf.Sin(dist * 0.25f + b * 2.2f) * 0.12f + Mathf.Cos(dist * 0.5f + b * 1.5f) * 0.05f;
                            float targetAngle = branchAngles[b] + noise;
                            float diff = Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, targetAngle * Mathf.Rad2Deg) * Mathf.Deg2Rad);
                            if (diff < minDiff) minDiff = diff;
                        }

                        float crackWidth = 0.06f / Mathf.Max(0.18f, normDist);
                        float crackAlpha = Mathf.Clamp01(1f - (minDiff / crackWidth));
                        crackAlpha = Mathf.Pow(crackAlpha, 2f);

                        float coreAlpha = Mathf.Pow(Mathf.Clamp01(1f - normDist / 0.32f), 1.8f);
                        float edgeFade = Mathf.Clamp01((1f - normDist) * 3f);
                        float alpha = Mathf.Clamp01(crackAlpha * 1.25f + coreAlpha * 0.85f) * edgeFade;

                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                }
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Texture2D SplashTexture(string path)
        {
            if (!File.Exists(path))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                const int size = 128;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                        float d = Vector2.Distance(p, center) / (size * 0.5f);
                        float a = Mathf.Atan2(p.y - center.y, p.x - center.x);
                        float spike = 0.65f + 0.35f * Mathf.Cos(a * 6f + Mathf.Sin(d * 8f));
                        float alpha = Mathf.Clamp01(1f - (d / spike));
                        alpha = Mathf.Pow(alpha, 1.6f);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        internal static Texture2D SlashArcTexture(string path)
        {
            if (!File.Exists(path))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                const int w = 256, h = 64;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                for (int y = 0; y < h; y++)
                {
                    float v = (float)y / (h - 1);
                    for (int x = 0; x < w; x++)
                    {
                        float u = (float)x / (w - 1);
                        float crest = Mathf.Pow(v, 2.2f) * 1.4f + Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(v - 0.88f) / 0.12f), 2f);
                        float tail = Mathf.Pow(u, 1.6f);
                        float a = Mathf.Clamp01(crest * tail);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                }
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        internal static Mesh GenerateSlashArcMesh()
        {
            string path = ArtDir + "slash_arc.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;

            var mesh = new Mesh { name = "SlashArc" };
            const int segments = 32;
            float startAngle = -110f * Mathf.Deg2Rad;
            float endAngle = 110f * Mathf.Deg2Rad;
            float rInner = 0.85f;
            float rOuter = 2.6f;

            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var colors = new List<Color>();
            var indices = new List<int>();

            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float a = Mathf.Lerp(startAngle, endAngle, t);
                float cos = Mathf.Cos(a);
                float sin = Mathf.Sin(a);

                float edgeTaper = Mathf.Sin(t * Mathf.PI);
                float outerCur = Mathf.Lerp(rInner + 0.2f, rOuter, Mathf.Pow(edgeTaper, 0.42f));

                Vector3 vInner = new Vector3(cos * rInner, 0f, sin * rInner);
                Vector3 vOuter = new Vector3(cos * outerCur, 0.08f * edgeTaper, sin * outerCur);

                verts.Add(vInner);
                verts.Add(vOuter);

                uvs.Add(new Vector2(t, 0f));
                uvs.Add(new Vector2(t, 1f));

                float alpha = Mathf.Pow(t, 1.4f) * edgeTaper;
                colors.Add(new Color(1f, 1f, 1f, alpha * 0.45f));
                colors.Add(new Color(1f, 1f, 1f, alpha));
            }

            for (int i = 0; i < segments; i++)
            {
                int i0 = i * 2;
                int i1 = i0 + 1;
                int i2 = i0 + 2;
                int i3 = i0 + 3;

                indices.Add(i0); indices.Add(i1); indices.Add(i2);
                indices.Add(i1); indices.Add(i3); indices.Add(i2);
            }

            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        static Material Mat(string name, string shaderName, Texture2D texture)
        {
            string path = ArtDir + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find(shaderName) ?? Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Legacy Shaders/Particles/Additive");
            if (material == null)
            {
                Directory.CreateDirectory(ArtDir);
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            if (texture != null)
            {
                if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
                if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            }
            if (material.HasProperty("_TintColor")) material.SetColor("_TintColor", new Color(0.6f, 0.6f, 0.6f, 0.6f));
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(material);
            return material;
        }

        internal static void EnsureMaterials()
        {
            Texture2D dot = SoftTexture(ArtDir + "vfx_dot.png", false);
            Texture2D ring = SoftTexture(ArtDir + "vfx_ring.png", true);
            Texture2D crackTex = CrackTexture(ArtDir + "vfx_crack.png");
            Texture2D splashTex = SplashTexture(ArtDir + "vfx_splash.png");
            Texture2D slashArcTex = SlashArcTexture(ArtDir + "vfx_slash_arc.png");
            slashArcMesh = GenerateSlashArcMesh();

            additive = Mat("Mat_VfxAdditive", "Legacy Shaders/Particles/Additive", dot);
            alpha = Mat("Mat_VfxAlpha", "Legacy Shaders/Particles/Alpha Blended", dot);
            ripple = Mat("Mat_VfxRing", "Legacy Shaders/Particles/Additive", ring);
            trailMaterial = Mat("Mat_SwordTrail", "Legacy Shaders/Particles/Additive", dot);
            crack = Mat("Mat_VfxCrack", "Legacy Shaders/Particles/Additive", crackTex);
            splash = Mat("Mat_VfxSplash", "Legacy Shaders/Particles/Additive", splashTex);

            distortion = Mat("Mat_VfxDistortion", "Custom/URP_ShockwaveDistortion", ring);
            distortion.SetFloat("_DistortionStrength", 0.045f);

            slashWater = Mat("Mat_SlashWater", "Universal Render Pipeline/Particles/Unlit", slashArcTex);
            // HDR intense glowing oceanic cyan
            slashWater.SetColor("_BaseColor", new Color(0.35f, 0.95f, 1.8f, 1f));
        }

        // ---------------------------------------------------------------- particle helpers

        internal static ParticleSystem Particles(Transform parent, string name, Material material, Vector3 position, Vector3 euler)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(euler);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
            var main = ps.main;
            main.playOnAwake = true;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            return ps;
        }

        internal static Gradient Fade(Color from, Color to, float alphaStart = 1f)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(from, 0f), new GradientColorKey(to, 1f) },
                new[] { new GradientAlphaKey(alphaStart, 0f), new GradientAlphaKey(alphaStart * 0.6f, 0.5f), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        internal static void FadeOverLife(ParticleSystem ps, Color from, Color to, float alphaStart = 1f)
        {
            var c = ps.colorOverLifetime;
            c.enabled = true;
            c.color = Fade(from, to, alphaStart);
        }

        internal static void Shrink(ParticleSystem ps, float end = 0.1f)
        {
            var s = ps.sizeOverLifetime;
            s.enabled = true;
            s.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, end)));
        }

        internal static void Grow(ParticleSystem ps, float from, float to)
        {
            var s = ps.sizeOverLifetime;
            s.enabled = true;
            s.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, from), new Keyframe(1f, to)));
        }

        internal static GameObject Save(GameObject root, string name)
        {
            Directory.CreateDirectory(PrefabDir);
            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>()) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + name + ".prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        // ---------------------------------------------------------------- the effects

        static GameObject HitSplash()
        {
            var root = new GameObject("VFX_HitSplash");

            ParticleSystem drops = Particles(root.transform, "Drops", splash, Vector3.zero, Vector3.zero);
            var main = drops.main;
            main.duration = 0.35f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 8.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.32f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.85f, 0.98f, 1f, 1f), new Color(0.35f, 0.75f, 1f, 0.95f));
            main.gravityModifier = 1.6f;
            main.maxParticles = 80;
            drops.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 42) });
            var shape = drops.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 50f;
            shape.radius = 0.1f;
            Shrink(drops, 0.1f);
            FadeOverLife(drops, Color.white, new Color(0.4f, 0.8f, 1f), 0.95f);

            ParticleSystem flash = Particles(root.transform, "Flash", additive, Vector3.zero, Vector3.zero);
            var fm = flash.main;
            fm.duration = 0.2f;
            fm.startLifetime = 0.15f;
            fm.startSpeed = 0f;
            fm.startSize = 1.4f;
            fm.startColor = new Color(0.85f, 0.98f, 1f, 0.95f);
            flash.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            Shrink(flash, 0.2f);

            ParticleSystem ringPs = Particles(root.transform, "Ring", ripple, Vector3.zero, Vector3.zero);
            var rm = ringPs.main;
            rm.duration = 0.3f;
            rm.startLifetime = 0.32f;
            rm.startSpeed = 0f;
            rm.startSize = 2.2f;
            rm.startColor = new Color(0.6f, 0.92f, 1f, 0.95f);
            ringPs.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            Grow(ringPs, 0.25f, 1.5f);
            FadeOverLife(ringPs, Color.white, Color.white);

            CombatLightFlash.Attach(root, new Color(0.35f, 0.82f, 1f), range: 6.5f, intensity: 4f, duration: 0.22f);

            return Save(root, "VFX_HitSplash");
        }

        // The shock-wave of the spin finisher and the heavy attack: includes a URP distortion shockwave!
        static GameObject SpinWave()
        {
            var root = new GameObject("VFX_SpinWave");

            // Refraction / distortion shockwave ring
            ParticleSystem distPs = Particles(root.transform, "Distortion", distortion, new Vector3(0f, 0.15f, 0f), Vector3.zero);
            distPs.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            var dm = distPs.main;
            dm.duration = 0.25f;
            dm.startLifetime = 0.55f;
            dm.startSpeed = 0f;
            dm.startSize = 14f;
            dm.startColor = new Color(1f, 1f, 1f, 0.95f);
            distPs.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            Grow(distPs, 0.1f, 1f);
            FadeOverLife(distPs, Color.white, Color.white);

            // Energetic outward spray
            ParticleSystem drops = Particles(root.transform, "Spray", splash, new Vector3(0f, 0.3f, 0f), new Vector3(90f, 0f, 0f));
            var main = drops.main;
            main.duration = 0.4f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(8f, 13f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.85f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.9f, 0.98f, 1f, 0.95f), new Color(0.3f, 0.7f, 1f, 0.85f));
            main.maxParticles = 200;
            drops.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 130) });
            var shape = drops.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.6f;
            shape.radiusThickness = 0f;
            Shrink(drops, 0.2f);
            FadeOverLife(drops, Color.white, new Color(0.35f, 0.7f, 1f), 0.9f);

            // Expanding ground ripple ring
            ParticleSystem ringPs = Particles(root.transform, "Ring", ripple, new Vector3(0f, 0.12f, 0f), Vector3.zero);
            ringPs.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            var rm = ringPs.main;
            rm.duration = 0.25f;
            rm.startLifetime = 0.6f;
            rm.startSpeed = 0f;
            rm.startSize = 14f;
            rm.startColor = new Color(0.7f, 0.96f, 1f, 1f);
            ringPs.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            Grow(ringPs, 0.15f, 1f);
            FadeOverLife(ringPs, Color.white, Color.white);

            CombatLightFlash.Attach(root, new Color(0.35f, 0.8f, 1f), range: 9.5f, intensity: 5.5f, duration: 0.35f, offset: new Vector3(0f, 0.4f, 0f));

            return Save(root, "VFX_SpinWave");
        }

        static GameObject Wind()
        {
            var root = new GameObject("VFX_Wind");
            root.AddComponent<WindBlast>();

            ParticleSystem swirl = Particles(root.transform, "Swirl", splash, new Vector3(0f, 0.3f, 0f), new Vector3(-90f, 0f, 0f));
            var main = swirl.main;
            main.duration = 0.75f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.2f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.45f, 1.0f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.92f, 1f, 1f, 0.85f), new Color(0.45f, 0.85f, 1f, 0.65f));
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 320;
            var emission = swirl.emission;
            emission.rateOverTime = 320f;
            var shape = swirl.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 4.8f;
            shape.radiusThickness = 0.3f;
            var velocity = swirl.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = 0f;
            velocity.y = 0f;
            velocity.z = 4.2f;
            velocity.orbitalZ = 6.5f;
            velocity.radial = -4.8f;
            Shrink(swirl, 0.25f);
            FadeOverLife(swirl, Color.white, new Color(0.5f, 0.85f, 1f), 0.85f);

            ParticleSystem ringPs = Particles(root.transform, "Ring", ripple, new Vector3(0f, 0.15f, 0f), Vector3.zero);
            ringPs.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            var rm = ringPs.main;
            rm.duration = 0.2f;
            rm.startLifetime = 0.75f;
            rm.startSpeed = 0f;
            rm.startSize = 19f;
            rm.startColor = new Color(0.85f, 1f, 1f, 0.95f);
            ringPs.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            Grow(ringPs, 1f, 0.15f);
            FadeOverLife(ringPs, Color.white, Color.white);

            CombatLightFlash.Attach(root, new Color(0.5f, 0.95f, 1f), range: 11f, intensity: 4.5f, duration: 0.65f, offset: new Vector3(0f, 0.8f, 0f));

            return Save(root, "VFX_Wind");
        }

        static GameObject Rain()
        {
            var root = new GameObject("VFX_Rain");
            root.AddComponent<RainZone>();

            ParticleSystem drops = Particles(root.transform, "Drops", alpha, new Vector3(0f, 9.5f, 0f), new Vector3(90f, 0f, 0f));
            var dr = drops.GetComponent<ParticleSystemRenderer>();
            dr.renderMode = ParticleSystemRenderMode.Stretch;
            dr.velocityScale = 0.055f;
            dr.lengthScale = 2.4f;
            var main = drops.main;
            main.loop = true;
            main.duration = 6f;
            main.startLifetime = 0.62f;
            main.startSpeed = 15f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.18f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.8f, 0.92f, 1f, 0.85f), new Color(0.45f, 0.75f, 1f, 0.75f));
            main.maxParticles = 1400;
            var emission = drops.emission;
            emission.rateOverTime = 1300f;
            var shape = drops.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 0f;
            shape.radius = 6.2f;
            shape.radiusThickness = 1f;

            ParticleSystem splashes = Particles(root.transform, "Splashes", splash, new Vector3(0f, 0.1f, 0f), new Vector3(90f, 0f, 0f));
            var sm = splashes.main;
            sm.loop = true;
            sm.duration = 6f;
            sm.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
            sm.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
            sm.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.38f);
            sm.startColor = new Color(0.85f, 0.96f, 1f, 0.85f);
            sm.maxParticles = 400;
            var sem = splashes.emission;
            sem.rateOverTime = 160f;
            var sshape = splashes.shape;
            sshape.shapeType = ParticleSystemShapeType.Circle;
            sshape.radius = 6f;
            sshape.radiusThickness = 1f;
            Shrink(splashes, 0.2f);

            ParticleSystem ripples = Particles(root.transform, "Ripples", ripple, new Vector3(0f, 0.08f, 0f), new Vector3(90f, 0f, 0f));
            ripples.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            var rmain = ripples.main;
            rmain.loop = true;
            rmain.duration = 6f;
            rmain.startLifetime = 0.6f;
            rmain.startSpeed = 0f;
            rmain.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
            rmain.startColor = new Color(0.7f, 0.92f, 1f, 0.75f);
            rmain.maxParticles = 350;
            var remission = ripples.emission;
            remission.rateOverTime = 110f;
            var rshape = ripples.shape;
            rshape.shapeType = ParticleSystemShapeType.Circle;
            rshape.radius = 6f;
            rshape.radiusThickness = 1f;
            Grow(ripples, 0.2f, 1f);
            FadeOverLife(ripples, Color.white, Color.white);

            return Save(root, "VFX_Rain");
        }

        static GameObject Wave()
        {
            var root = new GameObject("VFX_Wave");
            root.AddComponent<WaveProjectile>();

            ParticleSystem body = Particles(root.transform, "Body", alpha, new Vector3(0f, 1.1f, 0f), Vector3.zero);
            var main = body.main;
            main.loop = true;
            main.duration = 3f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 0.95f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.18f, 0.62f, 1f, 0.65f), new Color(0.4f, 0.88f, 1f, 0.55f));
            main.maxParticles = 1000;
            var emission = body.emission;
            emission.rateOverTime = 520f;
            var shape = body.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(5.8f, 2.0f, 0.5f);
            var velocity = body.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = 0f;
            velocity.y = 1.3f;
            velocity.z = 1.8f;
            Shrink(body, 0.35f);
            FadeOverLife(body, Color.white, Color.white, 0.95f);

            ParticleSystem foam = Particles(root.transform, "Foam", additive, new Vector3(0f, 2.1f, 0f), Vector3.zero);
            var fmain = foam.main;
            fmain.loop = true;
            fmain.duration = 3f;
            fmain.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.85f);
            fmain.startSpeed = new ParticleSystem.MinMaxCurve(0f, 1.8f);
            fmain.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
            fmain.startColor = new ParticleSystem.MinMaxGradient(new Color(0.96f, 1f, 1f, 0.98f), new Color(0.7f, 0.95f, 1f, 0.9f));
            fmain.gravityModifier = 0.7f;
            fmain.maxParticles = 450;
            var foamEmission = foam.emission; foamEmission.rateOverTime = 260f;
            var fshape = foam.shape;
            fshape.shapeType = ParticleSystemShapeType.Box;
            fshape.scale = new Vector3(5.8f, 0.35f, 0.5f);
            Shrink(foam, 0.2f);
            FadeOverLife(foam, Color.white, new Color(0.6f, 0.9f, 1f), 0.95f);

            ParticleSystem spray = Particles(root.transform, "Spray", splash, new Vector3(0f, 0.4f, 0f), Vector3.zero);
            var smain = spray.main;
            smain.loop = true;
            smain.duration = 3f;
            smain.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
            smain.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6.5f);
            smain.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
            smain.startColor = new Color(0.85f, 0.98f, 1f, 0.95f);
            smain.gravityModifier = 1.3f;
            smain.maxParticles = 350;
            var sprayEmission = spray.emission; sprayEmission.rateOverTime = 200f;
            var sshape = spray.shape;
            sshape.shapeType = ParticleSystemShapeType.Box;
            sshape.scale = new Vector3(5.8f, 0.25f, 0.4f);
            Shrink(spray, 0.2f);

            CombatLightFlash.Attach(root, new Color(0.22f, 0.75f, 1f), range: 9.5f, intensity: 4.5f, duration: 1.4f, offset: new Vector3(0f, 1f, 0f));

            return Save(root, "VFX_Wave");
        }

        // 3D Crescent Slash Arc for Thuy Tinh
        public static GameObject SlashWater(bool heavy = false)
        {
            string name = heavy ? "VFX_SlashWaterHeavy" : "VFX_SlashWater";
            var root = new GameObject(name);

            var meshGo = new GameObject("ArcMesh");
            meshGo.transform.SetParent(root.transform, false);
            meshGo.transform.localPosition = new Vector3(0f, 1.0f, 0.4f);
            // Angle the crescent arc in the slash direction
            meshGo.transform.localRotation = Quaternion.Euler(heavy ? 25f : -15f, 0f, heavy ? -20f : 15f);

            var mf = meshGo.AddComponent<MeshFilter>();
            mf.sharedMesh = slashArcMesh;
            var mr = meshGo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = slashWater;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            var runner = root.AddComponent<SlashArcRunner>();
            runner.duration = heavy ? 0.28f : 0.20f;
            runner.startScale = heavy ? new Vector3(0.7f, 0.7f, 0.7f) : new Vector3(0.5f, 0.5f, 0.5f);
            runner.endScale = heavy ? new Vector3(1.5f, 1.5f, 1.5f) : new Vector3(1.15f, 1.15f, 1.15f);
            runner.sweepAngle = heavy ? 65f : 45f;
            runner.meshRenderer = mr;

            // Forward water spray along the slash cut
            ParticleSystem spray = Particles(root.transform, "Spray", splash, new Vector3(0f, 1.0f, 0.8f), new Vector3(0f, 0f, 0f));
            var sm = spray.main;
            sm.duration = 0.25f;
            sm.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
            sm.startSpeed = new ParticleSystem.MinMaxCurve(5f, 10f);
            sm.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
            sm.startColor = new Color(0.85f, 0.98f, 1f, 0.95f);
            spray.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, heavy ? 45 : 28) });
            var shape = spray.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.4f;
            Shrink(spray, 0.1f);

            CombatLightFlash.Attach(root, new Color(0.35f, 0.9f, 1.6f), range: 7.5f, intensity: heavy ? 6.5f : 4.5f, duration: heavy ? 0.3f : 0.22f);

            return Save(root, name);
        }

        static void SwordTrails()
        {
            var root = PrefabUtility.LoadPrefabContents(SwordPrefab);
            foreach (var old in root.GetComponentsInChildren<TrailRenderer>(true)) Object.DestroyImmediate(old.gameObject);
            var old2 = root.GetComponent<SwordTrail>();
            if (old2 != null) Object.DestroyImmediate(old2);

            var trails = new TrailRenderer[2];
            float[] heights = { 0.75f, 1.18f };
            float[] widths = { 0.58f, 0.35f };
            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject(i == 0 ? "TrailMid" : "TrailTip");
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = new Vector3(0f, heights[i], 0f);
                var t = go.AddComponent<TrailRenderer>();
                t.sharedMaterial = trailMaterial;
                t.time = 0.2f;
                t.minVertexDistance = 0.035f;
                t.widthMultiplier = widths[i];
                t.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
                t.textureMode = LineTextureMode.Stretch;
                t.alignment = LineAlignment.View;
                t.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                t.receiveShadows = false;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(new Color(0.7f, 1f, 1f), 0f), new GradientColorKey(new Color(0.18f, 0.6f, 1f), 1f) },
                    new[] { new GradientAlphaKey(0.98f, 0f), new GradientAlphaKey(0f, 1f) });
                t.colorGradient = g;
                t.emitting = false;
                trails[i] = t;
            }
            var trail = root.AddComponent<SwordTrail>();
            var so = new SerializedObject(trail);
            SerializedProperty list = so.FindProperty("trails");
            list.arraySize = trails.Length;
            for (int i = 0; i < trails.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = trails[i];
            so.ApplyModifiedProperties();
            PrefabUtility.SaveAsPrefabAsset(root, SwordPrefab);
            PrefabUtility.UnloadPrefabContents(root);
        }

        public static (GameObject hitSplash, GameObject spinWave, GameObject wind, GameObject rain, GameObject wave, GameObject slash, GameObject heavySlash) Build()
        {
            EnsureMaterials();
            var result = (HitSplash(), SpinWave(), Wind(), Rain(), Wave(), SlashWater(false), SlashWater(true));
            SwordTrails();
            AssetDatabase.SaveAssets();
            return result;
        }
    }
}
