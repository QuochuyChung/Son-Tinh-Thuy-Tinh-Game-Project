using System.Collections.Generic;
using System.IO;
using SonTinhThuyTinh.Combat;
using UnityEditor;
using UnityEngine;

namespace SonTinhThuyTinh.EditorTools
{
    // The upgraded earth effects of Son Tinh's fighting (docs/progress.md 9.14):
    // Faceted 3D rock shard and mountain spike meshes generated procedurally, glowing ground cracks,
    // 3D crescent earth slash energy arcs, URP shockwave distortion rings,
    // dense dust shockwaves and dynamic impact lighting for Wukong-tier cinematic weight.
    public static class EarthVfxBuilder
    {
        const string ArtDir = "Assets/Art/Combat/";

        static Material rock, slashEarth;
        static Mesh shardMesh;
        static Mesh spikeMesh;

        static readonly Color Sand = new(0.85f, 0.70f, 0.46f);
        static readonly Color Clay = new(0.58f, 0.42f, 0.28f);
        static readonly Color Gold = new(1f, 0.82f, 0.45f);

        public static (GameObject hit, GameObject shock, GameObject quake, GameObject zone, GameObject rockWave, GameObject slash, GameObject heavySlash) Build()
        {
            CombatVfxBuilder.EnsureMaterials();
            rock = RockMaterial();
            slashEarth = SlashEarthMaterial();
            shardMesh = GenerateRockShard();
            spikeMesh = GenerateRockSpike();
            var result = (Hit(), Shock(), Quake(), Zone(), RockWave(), SlashEarth(false), SlashEarth(true));
            AssetDatabase.SaveAssets();
            return result;
        }

        static Material RockMaterial()
        {
            string path = ArtDir + "Mat_VfxRock.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Directory.CreateDirectory(ArtDir);
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Lit") ?? Shader.Find("Universal Render Pipeline/Lit");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", new Color(0.65f, 0.52f, 0.38f));
            material.SetFloat("_Smoothness", 0.12f);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material SlashEarthMaterial()
        {
            string path = ArtDir + "Mat_SlashEarth.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtDir + "vfx_slash_arc.png");
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Legacy Shaders/Particles/Additive");
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            if (tex != null)
            {
                if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", tex);
                if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", tex);
            }
            // HDR intense glowing molten gold
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", new Color(1.8f, 1.25f, 0.42f, 1f));
            if (material.HasProperty("_TintColor")) material.SetColor("_TintColor", new Color(0.8f, 0.6f, 0.2f, 0.8f));
            EditorUtility.SetDirty(material);
            return material;
        }

        // Generates an irregular faceted polyhedron with flat normals so it catches light like chiseled rock.
        static Mesh GenerateRockShard()
        {
            string path = ArtDir + "rock_shard.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;

            var mesh = new Mesh { name = "RockShard" };
            Vector3[] baseV = {
                new Vector3( 0.0f,  0.55f,  0.0f),
                new Vector3( 0.45f, 0.15f,  0.25f),
                new Vector3(-0.4f,  0.2f,   0.3f),
                new Vector3(-0.48f, 0.18f, -0.32f),
                new Vector3( 0.38f, 0.12f, -0.42f),
                new Vector3( 0.5f, -0.25f,  0.2f),
                new Vector3(-0.35f,-0.22f,  0.45f),
                new Vector3(-0.52f,-0.28f, -0.22f),
                new Vector3( 0.32f,-0.2f,  -0.45f),
                new Vector3( 0.0f, -0.58f,  0.0f)
            };
            int[] tris = {
                0,1,2,  0,2,3,  0,3,4,  0,4,1,
                1,5,6,  1,6,2,  2,6,7,  2,7,3,  3,7,8,  3,8,4,  4,8,5,  4,5,1,
                9,6,5,  9,7,6,  9,8,7,  9,5,8
            };

            var flatVerts = new List<Vector3>();
            var flatNormals = new List<Vector3>();
            var flatIndices = new List<int>();

            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector3 v0 = baseV[tris[i]];
                Vector3 v1 = baseV[tris[i + 1]];
                Vector3 v2 = baseV[tris[i + 2]];
                Vector3 norm = Vector3.Cross(v1 - v0, v2 - v0).normalized;
                int idx = flatVerts.Count;
                flatVerts.Add(v0); flatVerts.Add(v1); flatVerts.Add(v2);
                flatNormals.Add(norm); flatNormals.Add(norm); flatNormals.Add(norm);
                flatIndices.Add(idx); flatIndices.Add(idx + 1); flatIndices.Add(idx + 2);
            }

            mesh.SetVertices(flatVerts);
            mesh.SetNormals(flatNormals);
            mesh.SetTriangles(flatIndices, 0);
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        // Generates a sharp mountain needle / stalagmite mesh for RockWave.
        static Mesh GenerateRockSpike()
        {
            string path = ArtDir + "rock_spike.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;

            var mesh = new Mesh { name = "RockSpike" };
            Vector3 apex = new Vector3(0f, 2.3f, 0f);
            Vector3 baseCenter = new Vector3(0f, -0.1f, 0f);
            const int sides = 6;
            Vector3[] midRing = new Vector3[sides];
            Vector3[] baseRing = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                float rBase = 0.42f + 0.08f * Mathf.Sin(i * 3f);
                float rMid = 0.32f + 0.09f * Mathf.Cos(i * 2f);
                baseRing[i] = new Vector3(Mathf.Cos(a) * rBase, 0f, Mathf.Sin(a) * rBase);
                midRing[i] = new Vector3(Mathf.Cos(a) * rMid, 1.1f + 0.12f * Mathf.Sin(i * 4f), Mathf.Sin(a) * rMid);
            }

            var flatVerts = new List<Vector3>();
            var flatNormals = new List<Vector3>();
            var flatIndices = new List<int>();

            void AddTri(Vector3 v0, Vector3 v1, Vector3 v2)
            {
                Vector3 n = Vector3.Cross(v1 - v0, v2 - v0).normalized;
                int idx = flatVerts.Count;
                flatVerts.Add(v0); flatVerts.Add(v1); flatVerts.Add(v2);
                flatNormals.Add(n); flatNormals.Add(n); flatNormals.Add(n);
                flatIndices.Add(idx); flatIndices.Add(idx + 1); flatIndices.Add(idx + 2);
            }

            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                AddTri(apex, midRing[i], midRing[next]);
                AddTri(midRing[i], baseRing[i], baseRing[next]);
                AddTri(midRing[i], baseRing[next], midRing[next]);
                AddTri(baseCenter, baseRing[next], baseRing[i]);
            }

            mesh.SetVertices(flatVerts);
            mesh.SetNormals(flatNormals);
            mesh.SetTriangles(flatIndices, 0);
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        // Rock chunks tumbling as faceted meshes.
        static ParticleSystem Rocks(Transform parent, string name, Vector3 position, Vector3 euler, Mesh customMesh = null)
        {
            ParticleSystem ps = CombatVfxBuilder.Particles(parent, name, rock, position, euler);
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = customMesh != null ? customMesh : shardMesh;
            var main = ps.main;
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            main.startColor = new ParticleSystem.MinMaxGradient(Clay, new Color(0.74f, 0.65f, 0.52f));
            var spin = ps.rotationOverLifetime;
            spin.enabled = true;
            spin.separateAxes = true;
            spin.x = new ParticleSystem.MinMaxCurve(-4.5f, 4.5f);
            spin.y = new ParticleSystem.MinMaxCurve(-4.5f, 4.5f);
            spin.z = new ParticleSystem.MinMaxCurve(-4.5f, 4.5f);
            return ps;
        }

        static ParticleSystem Dust(Transform parent, string name, Vector3 position, Vector3 euler)
        {
            ParticleSystem ps = CombatVfxBuilder.Particles(parent, name, CombatVfxBuilder.alpha, position, euler);
            var main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.8f, 0.66f, 0.46f, 0.9f), new Color(0.62f, 0.48f, 0.32f, 0.85f));
            return ps;
        }

        static GameObject Hit()
        {
            var root = new GameObject("VFX_EarthHit");

            ParticleSystem chips = Rocks(root.transform, "Chips", Vector3.zero, Vector3.zero);
            var main = chips.main;
            main.duration = 0.35f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.95f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
            main.gravityModifier = 2.2f;
            main.maxParticles = 50;
            chips.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });
            var shape = chips.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 50f;
            shape.radius = 0.1f;
            CombatVfxBuilder.Shrink(chips, 0.35f);

            ParticleSystem puff = Dust(root.transform, "Puff", Vector3.zero, Vector3.zero);
            var pm = puff.main;
            pm.duration = 0.3f;
            pm.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
            pm.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
            pm.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            pm.maxParticles = 35;
            puff.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 14) });
            var ps = puff.shape;
            ps.shapeType = ParticleSystemShapeType.Sphere;
            ps.radius = 0.15f;
            CombatVfxBuilder.Grow(puff, 0.5f, 1.6f);
            CombatVfxBuilder.FadeOverLife(puff, Color.white, Color.white, 0.85f);

            ParticleSystem flash = CombatVfxBuilder.Particles(root.transform, "Flash", CombatVfxBuilder.additive, Vector3.zero, Vector3.zero);
            var fm = flash.main;
            fm.duration = 0.2f;
            fm.startLifetime = 0.14f;
            fm.startSpeed = 0f;
            fm.startSize = 1.3f;
            fm.startColor = new Color(Gold.r, Gold.g, Gold.b, 0.9f);
            flash.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            CombatVfxBuilder.Shrink(flash, 0.2f);

            CombatLightFlash.Attach(root, new Color(1f, 0.8f, 0.45f), range: 6f, intensity: 4f, duration: 0.22f);

            return CombatVfxBuilder.Save(root, "VFX_EarthHit");
        }

        // Shock-wave of finisher, heavy attack, and jump attack:
        // Includes URP distortion shockwave ring, glowing ground cracks, and rock debris!
        static GameObject Shock()
        {
            var root = new GameObject("VFX_EarthShock");

            // Refraction / distortion shockwave ring
            ParticleSystem distPs = CombatVfxBuilder.Particles(root.transform, "Distortion", CombatVfxBuilder.distortion, new Vector3(0f, 0.12f, 0f), Vector3.zero);
            distPs.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            var dm = distPs.main;
            dm.duration = 0.25f;
            dm.startLifetime = 0.55f;
            dm.startSpeed = 0f;
            dm.startSize = 13.5f;
            dm.startColor = new Color(1f, 1f, 1f, 0.95f);
            distPs.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            CombatVfxBuilder.Grow(distPs, 0.1f, 1f);
            CombatVfxBuilder.FadeOverLife(distPs, Color.white, Color.white);

            // Glowing ground crack fracture
            ParticleSystem crackPs = CombatVfxBuilder.Particles(root.transform, "Crack", CombatVfxBuilder.crack, new Vector3(0f, 0.08f, 0f), Vector3.zero);
            crackPs.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            var cm = crackPs.main;
            cm.duration = 0.3f;
            cm.startLifetime = 0.65f;
            cm.startSpeed = 0f;
            cm.startSize = 7.5f;
            cm.startColor = new Color(1f, 0.78f, 0.35f, 0.95f);
            crackPs.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            CombatVfxBuilder.Grow(crackPs, 0.2f, 1f);
            CombatVfxBuilder.FadeOverLife(crackPs, Color.white, new Color(0.85f, 0.45f, 0.15f), 0.95f);

            // Outward dust shockwave ring
            ParticleSystem ring = CombatVfxBuilder.Particles(root.transform, "Ring", CombatVfxBuilder.ripple, new Vector3(0f, 0.12f, 0f), Vector3.zero);
            ring.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            var rm = ring.main;
            rm.duration = 0.2f;
            rm.startLifetime = 0.55f;
            rm.startSpeed = 0f;
            rm.startSize = 13.5f;
            rm.startColor = new Color(Sand.r, Sand.g, Sand.b, 1f);
            ring.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            CombatVfxBuilder.Grow(ring, 0.15f, 1f);
            CombatVfxBuilder.FadeOverLife(ring, Color.white, Color.white);

            // Fast dust cloud burst
            ParticleSystem dust = Dust(root.transform, "Dust", new Vector3(0f, 0.3f, 0f), new Vector3(90f, 0f, 0f));
            var dustm = dust.main;
            dustm.duration = 0.4f;
            dustm.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            dustm.startSpeed = new ParticleSystem.MinMaxCurve(7f, 12f);
            dustm.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            dustm.maxParticles = 140;
            dust.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 85) });
            var ds = dust.shape;
            ds.shapeType = ParticleSystemShapeType.Circle;
            ds.radius = 0.55f;
            ds.radiusThickness = 0f;
            CombatVfxBuilder.Grow(dust, 0.5f, 1.6f);
            CombatVfxBuilder.FadeOverLife(dust, Color.white, Color.white, 0.85f);

            // Faceted rock shards bursting up
            ParticleSystem chunks = Rocks(root.transform, "Chunks", new Vector3(0f, 0.2f, 0f), new Vector3(-90f, 0f, 0f));
            var chm = chunks.main;
            chm.duration = 0.3f;
            chm.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.2f);
            chm.startSpeed = new ParticleSystem.MinMaxCurve(5f, 11f);
            chm.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.32f);
            chm.gravityModifier = 2.8f;
            chm.maxParticles = 80;
            chunks.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 36) });
            var cs = chunks.shape;
            cs.shapeType = ParticleSystemShapeType.Cone;
            cs.angle = 45f;
            cs.radius = 1.4f;
            CombatVfxBuilder.Shrink(chunks, 0.5f);

            CombatLightFlash.Attach(root, new Color(1f, 0.72f, 0.32f), range: 9.5f, intensity: 5.5f, duration: 0.35f, offset: new Vector3(0f, 0.4f, 0f));

            return CombatVfxBuilder.Save(root, "VFX_EarthShock");
        }

        // Sức mạnh của núi non (O): includes massive URP distortion shockwave!
        static GameObject Quake()
        {
            var root = new GameObject("VFX_Earthquake");
            root.AddComponent<WindBlast>();

            // Large distortion shockwave ring
            ParticleSystem distPs = CombatVfxBuilder.Particles(root.transform, "Distortion", CombatVfxBuilder.distortion, new Vector3(0f, 0.15f, 0f), Vector3.zero);
            distPs.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            var dm = distPs.main;
            dm.duration = 0.3f;
            dm.startLifetime = 0.85f;
            dm.startSpeed = 0f;
            dm.startSize = 19f;
            dm.startColor = new Color(1f, 1f, 1f, 1f);
            distPs.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            CombatVfxBuilder.Grow(distPs, 0.1f, 1f);
            CombatVfxBuilder.FadeOverLife(distPs, Color.white, Color.white);

            // Large glowing fracture pattern on the ground
            ParticleSystem crackPs = CombatVfxBuilder.Particles(root.transform, "Crack", CombatVfxBuilder.crack, new Vector3(0f, 0.08f, 0f), Vector3.zero);
            crackPs.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            var crm = crackPs.main;
            crm.duration = 0.3f;
            crm.startLifetime = 1.1f;
            crm.startSpeed = 0f;
            crm.startSize = 16f;
            crm.startColor = new Color(1f, 0.72f, 0.28f, 1f);
            crackPs.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            CombatVfxBuilder.Grow(crackPs, 0.1f, 1f);
            CombatVfxBuilder.FadeOverLife(crackPs, Color.white, new Color(0.9f, 0.4f, 0.1f), 0.95f);

            // Expanding ground ripple shockwave
            ParticleSystem ring = CombatVfxBuilder.Particles(root.transform, "Ring", CombatVfxBuilder.ripple, new Vector3(0f, 0.15f, 0f), Vector3.zero);
            ring.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            var rm = ring.main;
            rm.duration = 0.2f;
            rm.startLifetime = 0.85f;
            rm.startSpeed = 0f;
            rm.startSize = 19f;
            rm.startColor = new Color(Sand.r, Sand.g, Sand.b, 0.95f);
            ring.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            CombatVfxBuilder.Grow(ring, 0.1f, 1f);
            CombatVfxBuilder.FadeOverLife(ring, Color.white, Color.white);

            // Boulders flying up all around
            ParticleSystem boulders = Rocks(root.transform, "Boulders", new Vector3(0f, 0.2f, 0f), new Vector3(-90f, 0f, 0f));
            var bm = boulders.main;
            bm.duration = 0.7f;
            bm.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.6f);
            bm.startSpeed = new ParticleSystem.MinMaxCurve(6f, 12f);
            bm.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
            bm.gravityModifier = 2.4f;
            bm.maxParticles = 200;
            var be = boulders.emission;
            be.rateOverTime = 180f;
            var bs = boulders.shape;
            bs.shapeType = ParticleSystemShapeType.Circle;
            bs.radius = 4.8f;
            bs.radiusThickness = 0.8f;
            CombatVfxBuilder.Shrink(boulders, 0.6f);

            // Rising dust wall
            ParticleSystem cloud = Dust(root.transform, "Cloud", new Vector3(0f, 0.3f, 0f), new Vector3(-90f, 0f, 0f));
            var cm = cloud.main;
            cm.duration = 0.8f;
            cm.startLifetime = new ParticleSystem.MinMaxCurve(1f, 1.8f);
            cm.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4f);
            cm.startSize = new ParticleSystem.MinMaxCurve(1.4f, 2.8f);
            cm.maxParticles = 200;
            var ce = cloud.emission;
            ce.rateOverTime = 200f;
            var cs = cloud.shape;
            cs.shapeType = ParticleSystemShapeType.Circle;
            cs.radius = 5.2f;
            cs.radiusThickness = 0.9f;
            CombatVfxBuilder.Grow(cloud, 0.6f, 1.6f);
            CombatVfxBuilder.FadeOverLife(cloud, Color.white, Color.white, 0.85f);

            CombatLightFlash.Attach(root, new Color(1f, 0.68f, 0.25f), range: 14f, intensity: 7f, duration: 0.65f, offset: new Vector3(0f, 0.6f, 0f));

            return CombatVfxBuilder.Save(root, "VFX_Earthquake");
        }

        static GameObject Zone()
        {
            var root = new GameObject("VFX_EarthZone");
            root.AddComponent<RainZone>();

            // Ground fracture pattern
            ParticleSystem crackPs = CombatVfxBuilder.Particles(root.transform, "GroundCrack", CombatVfxBuilder.crack, new Vector3(0f, 0.08f, 0f), Vector3.zero);
            crackPs.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            var crm = crackPs.main;
            crm.loop = true;
            crm.duration = 6f;
            crm.startLifetime = 1.2f;
            crm.startSpeed = 0f;
            crm.startSize = 13.5f;
            crm.startColor = new Color(1f, 0.75f, 0.32f, 0.85f);
            var cre = crackPs.emission;
            cre.rateOverTime = 1.2f;
            CombatVfxBuilder.Grow(crackPs, 0.85f, 1f);
            CombatVfxBuilder.FadeOverLife(crackPs, Color.white, Color.white, 0.85f);

            // Cluster of 7 3D Mountain Peaks that rise from the earth
            var mountainZone = root.AddComponent<RisingMountainZone>();
            mountainZone.totalDuration = 6.0f;
            mountainZone.riseDuration = 0.55f;
            mountainZone.sinkDuration = 0.75f;

            var peakList = new List<RisingMountainZone.MountainPeak>();

            (Vector3 pos, Vector3 rot, Vector3 scale, float targetY, float delay)[] peakData = {
                // Central majestic peak
                (new Vector3(0f, 0f, 0f), new Vector3(0f, 35f, 0f), new Vector3(2.2f, 2.5f, 2.2f), 0f, 0f),
                // Outer jagged mountain crown
                (new Vector3(2.4f, 0f, 0.6f), new Vector3(8f, 120f, -6f), new Vector3(1.5f, 1.8f, 1.5f), 0f, 0.06f),
                (new Vector3(-2.2f, 0f, 1.0f), new Vector3(-6f, 45f, 9f), new Vector3(1.6f, 1.9f, 1.6f), 0f, 0.12f),
                (new Vector3(0.5f, 0f, 2.8f), new Vector3(10f, -70f, 4f), new Vector3(1.4f, 1.7f, 1.4f), 0f, 0.08f),
                (new Vector3(-0.6f, 0f, -2.6f), new Vector3(-8f, 160f, -8f), new Vector3(1.5f, 1.9f, 1.5f), 0f, 0.15f),
                (new Vector3(2.0f, 0f, -2.0f), new Vector3(-5f, 80f, 7f), new Vector3(1.3f, 1.6f, 1.3f), 0f, 0.10f),
                (new Vector3(-2.5f, 0f, -1.5f), new Vector3(7f, -130f, -5f), new Vector3(1.4f, 1.7f, 1.4f), 0f, 0.18f),
            };

            for (int i = 0; i < peakData.Length; i++)
            {
                var d = peakData[i];
                var peakGo = new GameObject("MountainPeak_" + (i == 0 ? "Center" : i.ToString()));
                peakGo.transform.SetParent(root.transform, false);
                peakGo.transform.localPosition = d.pos;
                peakGo.transform.localRotation = Quaternion.Euler(d.rot);
                peakGo.transform.localScale = d.scale;

                var mf = peakGo.AddComponent<MeshFilter>();
                mf.sharedMesh = spikeMesh;
                var mr = peakGo.AddComponent<MeshRenderer>();
                mr.sharedMaterial = rock;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                mr.receiveShadows = true;

                peakList.Add(new RisingMountainZone.MountainPeak {
                    transform = peakGo.transform,
                    targetY = d.targetY,
                    delay = d.delay
                });
            }

            mountainZone.peaks = peakList.ToArray();

            // Dust burst when mountains erupt from the ground
            ParticleSystem eruptionDust = Dust(root.transform, "EruptionDust", new Vector3(0f, 0.3f, 0f), new Vector3(-90f, 0f, 0f));
            var edm = eruptionDust.main;
            edm.duration = 0.8f;
            edm.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 1.8f);
            edm.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6.5f);
            edm.startSize = new ParticleSystem.MinMaxCurve(1.5f, 3.2f);
            edm.maxParticles = 180;
            eruptionDust.emission.SetBursts(new[] { new ParticleSystem.Burst(0.05f, 90), new ParticleSystem.Burst(0.2f, 60) });
            var eds = eruptionDust.shape;
            eds.shapeType = ParticleSystemShapeType.Circle;
            eds.radius = 4.5f;
            CombatVfxBuilder.Grow(eruptionDust, 0.5f, 1.6f);
            CombatVfxBuilder.FadeOverLife(eruptionDust, Color.white, Color.white, 0.85f);

            // Ambient mist lingering over the mountain range
            ParticleSystem mist = Dust(root.transform, "Mist", new Vector3(0f, 0.4f, 0f), new Vector3(-90f, 0f, 0f));
            var mm = mist.main;
            mm.loop = true;
            mm.duration = 6f;
            mm.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.5f);
            mm.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
            mm.startSize = new ParticleSystem.MinMaxCurve(2.0f, 3.6f);
            mm.startColor = new ParticleSystem.MinMaxGradient(new Color(0.76f, 0.62f, 0.42f, 0.5f), new Color(0.60f, 0.46f, 0.32f, 0.45f));
            mm.maxParticles = 180;
            var me = mist.emission; me.rateOverTime = 55f;
            var ms = mist.shape;
            ms.shapeType = ParticleSystemShapeType.Circle;
            ms.radius = 5.5f;
            CombatVfxBuilder.FadeOverLife(mist, Color.white, Color.white, 0.85f);

            // Mountain perimeter boundary ring
            ParticleSystem edge = CombatVfxBuilder.Particles(root.transform, "Edge", CombatVfxBuilder.ripple, new Vector3(0f, 0.06f, 0f), Vector3.zero);
            edge.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            var em = edge.main;
            em.loop = true;
            em.duration = 6f;
            em.startLifetime = 1.2f;
            em.startSpeed = 0f;
            em.startSize = 12.8f;
            em.startColor = new Color(Sand.r, Sand.g, Sand.b, 0.6f);
            em.maxParticles = 4;
            var ee = edge.emission; ee.rateOverTime = 1.6f;
            CombatVfxBuilder.FadeOverLife(edge, Color.white, Color.white, 0.85f);

            // Warm golden light illuminating the mountain zone
            CombatLightFlash.Attach(root, new Color(1f, 0.75f, 0.35f), range: 12f, intensity: 5.5f, duration: 1.5f, offset: new Vector3(0f, 2.5f, 0f));

            return CombatVfxBuilder.Save(root, "VFX_EarthZone");
        }

        static GameObject RockWave()
        {
            var root = new GameObject("VFX_RockWave");
            root.AddComponent<WaveProjectile>();

            ParticleSystem spikes = Rocks(root.transform, "Spikes", new Vector3(0f, 0.1f, 0f), new Vector3(-90f, 0f, 0f), spikeMesh);
            var sm = spikes.main;
            sm.loop = true;
            sm.duration = 3f;
            sm.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            sm.startSpeed = new ParticleSystem.MinMaxCurve(4f, 9.5f);
            sm.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            sm.gravityModifier = 2.4f;
            sm.maxParticles = 320;
            var se = spikes.emission;
            se.rateOverTime = 140f;
            var ss = spikes.shape;
            ss.shapeType = ParticleSystemShapeType.Box;
            ss.scale = new Vector3(5.4f, 0.8f, 0.2f);
            CombatVfxBuilder.Shrink(spikes, 0.6f);

            ParticleSystem boulders = Rocks(root.transform, "Boulders", new Vector3(0f, 0.1f, 0f), new Vector3(-90f, 0f, 0f));
            var bm = boulders.main;
            bm.loop = true;
            bm.duration = 3f;
            bm.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.2f);
            bm.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6.5f);
            bm.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.0f);
            bm.gravityModifier = 2.6f;
            bm.maxParticles = 75;
            var bem = boulders.emission;
            bem.rateOverTime = 18f;
            var bs = boulders.shape;
            bs.shapeType = ParticleSystemShapeType.Box;
            bs.scale = new Vector3(5.2f, 0.7f, 0.2f);
            CombatVfxBuilder.Shrink(boulders, 0.7f);

            ParticleSystem dust = Dust(root.transform, "Dust", new Vector3(0f, 0.7f, 0f), Vector3.zero);
            var dm = dust.main;
            dm.loop = true;
            dm.duration = 3f;
            dm.startLifetime = new ParticleSystem.MinMaxCurve(0.65f, 1.1f);
            dm.startSpeed = 0f;
            dm.startSize = new ParticleSystem.MinMaxCurve(1.1f, 2.0f);
            dm.maxParticles = 550;
            var de = dust.emission;
            de.rateOverTime = 340f;
            var dsh = dust.shape;
            dsh.shapeType = ParticleSystemShapeType.Box;
            dsh.scale = new Vector3(5.6f, 1.4f, 0.6f);
            var velocity = dust.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = 0f;
            velocity.y = 1.1f;
            velocity.z = 0.8f;
            CombatVfxBuilder.Grow(dust, 0.6f, 1.4f);
            CombatVfxBuilder.FadeOverLife(dust, Color.white, Color.white, 0.85f);

            CombatLightFlash.Attach(root, new Color(1f, 0.72f, 0.32f), range: 9f, intensity: 4.8f, duration: 1.2f, offset: new Vector3(0f, 0.8f, 0f));

            return CombatVfxBuilder.Save(root, "VFX_RockWave");
        }

        // 3D Crescent Slash Arc for Son Tinh (Golden amber earth energy)
        public static GameObject SlashEarth(bool heavy = false)
        {
            string name = heavy ? "VFX_SlashEarthHeavy" : "VFX_SlashEarth";
            var root = new GameObject(name);

            var meshGo = new GameObject("ArcMesh");
            meshGo.transform.SetParent(root.transform, false);
            meshGo.transform.localPosition = new Vector3(0f, 1.0f, 0.4f);
            meshGo.transform.localRotation = Quaternion.Euler(heavy ? -25f : 15f, 0f, heavy ? 20f : -15f);

            var mf = meshGo.AddComponent<MeshFilter>();
            mf.sharedMesh = CombatVfxBuilder.slashArcMesh;
            var mr = meshGo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = slashEarth;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            var runner = root.AddComponent<SlashArcRunner>();
            runner.duration = heavy ? 0.28f : 0.20f;
            runner.startScale = heavy ? new Vector3(0.7f, 0.7f, 0.7f) : new Vector3(0.5f, 0.5f, 0.5f);
            runner.endScale = heavy ? new Vector3(1.5f, 1.5f, 1.5f) : new Vector3(1.15f, 1.15f, 1.15f);
            runner.sweepAngle = heavy ? 65f : 45f;
            runner.meshRenderer = mr;

            // Rock chips bursting along the punch swing
            ParticleSystem chips = Rocks(root.transform, "Chips", new Vector3(0f, 1.0f, 0.7f), Vector3.zero);
            var cm = chips.main;
            cm.duration = 0.25f;
            cm.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            cm.startSpeed = new ParticleSystem.MinMaxCurve(4f, 9f);
            cm.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
            cm.gravityModifier = 2.4f;
            cm.maxParticles = 40;
            chips.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, heavy ? 30 : 18) });
            var shape = chips.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.35f;
            CombatVfxBuilder.Shrink(chips, 0.4f);

            CombatLightFlash.Attach(root, new Color(1f, 0.75f, 0.35f), range: 7.5f, intensity: heavy ? 6.5f : 4.5f, duration: heavy ? 0.3f : 0.22f);

            return CombatVfxBuilder.Save(root, name);
        }

        public static void Trails(GameObject player)
        {
            foreach (var old in player.GetComponentsInChildren<TrailRenderer>(true)) Object.DestroyImmediate(old.gameObject);
            var existing = player.GetComponent<SwordTrail>();
            if (existing != null) Object.DestroyImmediate(existing);

            string[] bones = { "mixamorig:RightHand", "mixamorig:LeftHand", "mixamorig:RightFoot", "mixamorig:LeftFoot" };
            float[] widths = { 0.32f, 0.32f, 0.38f, 0.38f };
            var trails = new System.Collections.Generic.List<TrailRenderer>();
            for (int i = 0; i < bones.Length; i++)
            {
                Transform bone = null;
                foreach (Transform t in player.GetComponentsInChildren<Transform>(true)) if (t.name == bones[i]) { bone = t; break; }
                if (bone == null) { Debug.LogWarning("No bone " + bones[i] + " to hang a trail on."); continue; }
                var go = new GameObject("Trail" + bones[i].Replace("mixamorig:", ""));
                go.transform.SetParent(bone, false);
                var trail = go.AddComponent<TrailRenderer>();
                trail.sharedMaterial = CombatVfxBuilder.trailMaterial;
                trail.time = 0.22f;
                trail.minVertexDistance = 0.035f;
                trail.widthMultiplier = widths[i];
                trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
                trail.textureMode = LineTextureMode.Stretch;
                trail.alignment = LineAlignment.View;
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.receiveShadows = false;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.95f, 0.7f), 0f), new GradientColorKey(new Color(0.85f, 0.52f, 0.2f), 1f) },
                    new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0f, 1f) });
                trail.colorGradient = g;
                trail.emitting = false;
                trails.Add(trail);
            }

            var switcher = player.AddComponent<SwordTrail>();
            var so = new SerializedObject(switcher);
            SerializedProperty list = so.FindProperty("trails");
            list.arraySize = trails.Count;
            for (int i = 0; i < trails.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = trails[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
