using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SonTinhThuyTinh.Combat
{
    // Runtime-built ground fracture used by the elephant's slam. Keeping it procedural
    // means both the authored battle scene and the runtime bootstrap get the same effect.
    [DisallowMultipleComponent]
    public sealed class ElephantStompVfx : MonoBehaviour
    {
        sealed class CrackBranch
        {
            public LineRenderer dark;
            public LineRenderer glow;
            public Vector3[] points;
            public float darkWidth;
            public float glowWidth;
        }

        sealed class RockShard
        {
            public Transform transform;
            public Vector3 velocity;
            public Vector3 angularVelocity;
            public bool settled;
        }

        const float GrowDuration = 0.28f;
        const float GlowDuration = 0.62f;
        const float FadeStart = 1.55f;
        const float Lifetime = 2.25f;

        static Mesh sharedShardMesh;
        static Material sharedRockMaterial;
        static Material sharedDustMaterial;

        readonly List<CrackBranch> branches = new List<CrackBranch>();
        readonly List<RockShard> shards = new List<RockShard>();

        Material darkCrackMaterial;
        Material glowCrackMaterial;
        float elapsed;

        public static GameObject Spawn(Vector3 groundPosition, float radius)
        {
            var root = new GameObject("VFX_ElephantGroundFracture_Runtime");
            root.transform.position = groundPosition + Vector3.up * 0.055f;
            root.AddComponent<ElephantStompVfx>().Initialize(Mathf.Max(1f, radius));
            return root;
        }

        void Initialize(float radius)
        {
            int seed = GetInstanceID() ^ Mathf.RoundToInt(Time.time * 1000f);
            var random = new System.Random(seed);

            darkCrackMaterial = CreateLineMaterial(new Color(0.055f, 0.025f, 0.012f, 0.98f));
            glowCrackMaterial = CreateLineMaterial(new Color(1f, 0.23f, 0.025f, 0.95f));

            int mainBranchCount = Mathf.Clamp(Mathf.RoundToInt(radius * 2.2f), 14, 22);
            for (int i = 0; i < mainBranchCount; i++)
            {
                float baseAngle = Mathf.PI * 2f * i / mainBranchCount;
                baseAngle += Range(random, -0.15f, 0.15f);
                float length = radius * Range(random, 0.68f, 0.96f);
                Vector3[] mainPoints = BuildJaggedPath(random, Vector3.zero, baseAngle, length, radius, 6, 9);
                AddBranch(mainPoints, Range(random, 0.12f, 0.2f), true);

                if (i % 2 == 0)
                {
                    int forkIndex = Next(random, 2, mainPoints.Length - 2);
                    float forkAngle = baseAngle + (Next(random, 0, 2) == 0 ? -1f : 1f) * Range(random, 0.45f, 0.82f);
                    float forkLength = radius * Range(random, 0.18f, 0.34f);
                    Vector3[] fork = BuildJaggedPath(random, mainPoints[forkIndex], forkAngle, forkLength, radius, 3, 5);
                    AddBranch(fork, Range(random, 0.07f, 0.12f), false);
                }
            }

            BuildRockShards(random, radius);
            BuildDust(radius);
            UpdateCracks(0f, 1f);
        }

        void Update()
        {
            float deltaTime = Time.unscaledDeltaTime;
            elapsed += deltaTime;

            float grow = Smooth01(elapsed / GrowDuration);
            float fade = elapsed <= FadeStart ? 1f : 1f - Mathf.Clamp01((elapsed - FadeStart) / (Lifetime - FadeStart));
            UpdateCracks(grow, fade);
            UpdateShards(deltaTime);

            if (elapsed >= Lifetime)
                Destroy(gameObject);
        }

        void AddBranch(Vector3[] points, float width, bool main)
        {
            var branch = new CrackBranch
            {
                points = points,
                darkWidth = width,
                glowWidth = width * (main ? 0.26f : 0.2f)
            };

            branch.dark = CreateLine("Crack_Dark", darkCrackMaterial, points.Length, branch.darkWidth, -0.006f);
            branch.glow = CreateLine("Crack_Heat", glowCrackMaterial, points.Length, branch.glowWidth, 0.006f);
            branches.Add(branch);
        }

        LineRenderer CreateLine(string lineName, Material material, int pointCount, float width, float heightOffset)
        {
            var lineObject = new GameObject(lineName);
            lineObject.transform.SetParent(transform, false);
            lineObject.transform.localPosition = Vector3.up * heightOffset;

            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = false;
            line.positionCount = pointCount;
            line.startWidth = width;
            line.endWidth = width * 0.55f;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.textureMode = LineTextureMode.Stretch;
            line.sharedMaterial = material;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        void UpdateCracks(float grow, float fade)
        {
            bool showGlow = elapsed <= GlowDuration;
            float glowStrength = 1f - Mathf.Clamp01(elapsed / GlowDuration);
            SetMaterialColor(darkCrackMaterial, new Color(0.055f, 0.025f, 0.012f, fade));
            SetMaterialColor(glowCrackMaterial, new Color(1f, 0.12f + glowStrength * 0.28f, 0.015f, glowStrength));

            for (int branchIndex = 0; branchIndex < branches.Count; branchIndex++)
            {
                CrackBranch branch = branches[branchIndex];
                Reveal(branch.dark, branch.points, grow);
                Reveal(branch.glow, branch.points, grow);
                branch.dark.startWidth = branch.darkWidth * fade;
                branch.dark.endWidth = branch.darkWidth * 0.55f * fade;
                branch.glow.enabled = showGlow;
                if (showGlow)
                {
                    branch.glow.startWidth = branch.glowWidth * glowStrength;
                    branch.glow.endWidth = branch.glowWidth * 0.45f * glowStrength;
                }
            }
        }

        static void Reveal(LineRenderer line, Vector3[] points, float progress)
        {
            if (line == null || points == null || points.Length == 0) return;
            for (int i = 0; i < points.Length; i++)
            {
                float pointProgress = i / (float)(points.Length - 1);
                if (progress >= pointProgress)
                {
                    line.SetPosition(i, points[i]);
                    continue;
                }

                int previous = Mathf.Max(0, i - 1);
                float previousProgress = previous / (float)(points.Length - 1);
                float segmentProgress = Mathf.InverseLerp(previousProgress, pointProgress, progress);
                line.SetPosition(i, Vector3.Lerp(points[previous], points[i], segmentProgress));
            }
        }

        static Vector3[] BuildJaggedPath(System.Random random, Vector3 start, float angle, float length,
            float maximumRadius, int minimumPoints, int maximumPoints)
        {
            int count = Next(random, minimumPoints, maximumPoints + 1);
            var points = new Vector3[count];
            points[0] = start;
            Vector3 current = start;
            float step = length / (count - 1);

            for (int i = 1; i < count; i++)
            {
                angle += Range(random, -0.2f, 0.2f);
                float segment = step * Range(random, 0.78f, 1.22f);
                current += new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * segment;
                Vector2 flat = new Vector2(current.x, current.z);
                float limit = maximumRadius * 0.96f;
                if (flat.magnitude > limit)
                {
                    flat = flat.normalized * limit;
                    current = new Vector3(flat.x, 0f, flat.y);
                }
                points[i] = current;
            }

            return points;
        }

        void BuildRockShards(System.Random random, float radius)
        {
            EnsureSharedRockAssets();
            int count = Mathf.Clamp(Mathf.RoundToInt(radius * 2.4f), 16, 26);
            for (int i = 0; i < count; i++)
            {
                float angle = Range(random, 0f, Mathf.PI * 2f);
                float distance = radius * Range(random, 0.18f, 0.86f);
                Vector3 outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

                var rockObject = new GameObject("StompRockShard", typeof(MeshFilter), typeof(MeshRenderer));
                rockObject.transform.SetParent(transform, false);
                rockObject.transform.localPosition = outward * distance + Vector3.up * 0.04f;
                float scale = Range(random, 0.18f, 0.52f) * (1f - distance / (radius * 1.4f));
                rockObject.transform.localScale = new Vector3(scale, scale * Range(random, 0.55f, 1.15f), scale);
                rockObject.transform.localRotation = RandomRotation(random);
                rockObject.GetComponent<MeshFilter>().sharedMesh = sharedShardMesh;
                rockObject.GetComponent<MeshRenderer>().sharedMaterial = sharedRockMaterial;

                shards.Add(new RockShard
                {
                    transform = rockObject.transform,
                    velocity = outward * Range(random, 1.2f, 4.2f) + Vector3.up * Range(random, 2.8f, 6.8f),
                    angularVelocity = new Vector3(Range(random, -360f, 360f), Range(random, -360f, 360f), Range(random, -360f, 360f))
                });
            }
        }

        void UpdateShards(float deltaTime)
        {
            for (int i = 0; i < shards.Count; i++)
            {
                RockShard shard = shards[i];
                if (shard.transform == null || shard.settled) continue;

                shard.velocity += Vector3.down * (18f * deltaTime);
                shard.transform.localPosition += shard.velocity * deltaTime;
                shard.transform.Rotate(shard.angularVelocity * deltaTime, Space.Self);

                if (shard.transform.localPosition.y <= 0.025f && shard.velocity.y < 0f)
                {
                    Vector3 position = shard.transform.localPosition;
                    position.y = 0.025f;
                    shard.transform.localPosition = position;
                    shard.settled = true;
                }
            }
        }

        void BuildDust(float radius)
        {
            EnsureSharedDustMaterial();
            var dustObject = new GameObject("StompDust");
            dustObject.transform.SetParent(transform, false);
            ParticleSystem particles = dustObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particles.main;
            main.loop = false;
            main.duration = 0.45f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.05f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 7.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 1.15f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.32f, 0.22f, 0.12f, 0.72f),
                new Color(0.62f, 0.46f, 0.27f, 0.45f));
            main.gravityModifier = 0.42f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 90;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 68) });

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.radius = radius * 0.72f;
            shape.radiusThickness = 1f;
            shape.angle = 78f;
            shape.length = 0.15f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.45f, 0.35f, 0.24f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.8f, 0.08f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = gradient;

            ParticleSystemRenderer particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.sharedMaterial = sharedDustMaterial;
            particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
            particles.Play();
        }

        static void EnsureSharedRockAssets()
        {
            if (sharedShardMesh == null)
            {
                sharedShardMesh = new Mesh { name = "ElephantStompShard_Runtime" };
                sharedShardMesh.vertices = new[]
                {
                    new Vector3(-0.65f, 0f, -0.45f), new Vector3(0.55f, 0.04f, -0.5f),
                    new Vector3(0.5f, 0f, 0.55f), new Vector3(-0.5f, 0.08f, 0.45f),
                    new Vector3(0.08f, 1.25f, -0.08f)
                };
                sharedShardMesh.triangles = new[]
                {
                    0, 1, 4, 1, 2, 4, 2, 3, 4, 3, 0, 4,
                    0, 3, 2, 0, 2, 1
                };
                sharedShardMesh.RecalculateNormals();
                sharedShardMesh.RecalculateBounds();
            }

            if (sharedRockMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                sharedRockMaterial = new Material(shader) { name = "ElephantStompRock_Runtime" };
                SetMaterialColor(sharedRockMaterial, new Color(0.19f, 0.14f, 0.095f, 1f));
                if (sharedRockMaterial.HasProperty("_Smoothness")) sharedRockMaterial.SetFloat("_Smoothness", 0.08f);
            }
        }

        static void EnsureSharedDustMaterial()
        {
            if (sharedDustMaterial != null) return;
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Particles/Standard Unlit")
                ?? Shader.Find("Sprites/Default");
            sharedDustMaterial = new Material(shader) { name = "ElephantStompDust_Runtime" };
            ConfigureTransparent(sharedDustMaterial);

            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "ElephantStompDustSoftCircle_Runtime",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float alpha = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                alpha = alpha * alpha * (3f - 2f * alpha);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            if (sharedDustMaterial.HasProperty("_BaseMap")) sharedDustMaterial.SetTexture("_BaseMap", texture);
            else if (sharedDustMaterial.HasProperty("_MainTex")) sharedDustMaterial.SetTexture("_MainTex", texture);
            SetMaterialColor(sharedDustMaterial, Color.white);
        }

        static Material CreateLineMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Unlit/Color");
            var material = new Material(shader) { name = "ElephantStompCrack_Runtime" };
            ConfigureTransparent(material);
            SetMaterialColor(material, color);
            return material;
        }

        static void ConfigureTransparent(Material material)
        {
            if (material == null) return;
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        static void SetMaterialColor(Material material, Color color)
        {
            if (material == null) return;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        }

        void OnDestroy()
        {
            if (darkCrackMaterial != null) Destroy(darkCrackMaterial);
            if (glowCrackMaterial != null) Destroy(glowCrackMaterial);
        }

        static Quaternion RandomRotation(System.Random random)
        {
            return Quaternion.Euler(Range(random, 0f, 360f), Range(random, 0f, 360f), Range(random, 0f, 360f));
        }

        static int Next(System.Random random, int minimum, int maximum) => random.Next(minimum, maximum);
        static float Range(System.Random random, float minimum, float maximum) =>
            minimum + (float)random.NextDouble() * (maximum - minimum);
        static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }
    }
}
