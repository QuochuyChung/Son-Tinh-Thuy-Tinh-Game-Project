using System.Collections.Generic;
using SonTinhThuyTinh.Player;
using UnityEngine;

namespace SonTinhThuyTinh.Combat
{
    public enum VanThuyTramPhase
    {
        Idle,
        Casting,
        Absorbing,
        Charging,
        Slashing,
        Finishing,
        Cancelled,
        Cooldown
    }

    public static class ThuyTinhWaterSpells
    {
        public const int MaximumWaterSlashes = 5;

        static Material waterMaterial;
        static Material environmentWaterMaterial;
        static Material shieldWaterMaterial;
        static Mesh shieldDomeMesh;
        public static Material WaterMaterial
        {
            get
            {
                if (waterMaterial != null) return waterMaterial;
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                waterMaterial = new Material(shader) { color = new Color(0.03f, 0.48f, 1f, 0.34f) };
                waterMaterial.EnableKeyword("_EMISSION");
                waterMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                waterMaterial.SetColor("_EmissionColor", new Color(0f, 0.35f, 1.2f));
                waterMaterial.SetFloat("_Surface", 1f);
                waterMaterial.SetFloat("_Blend", 0f);
                waterMaterial.SetFloat("_ZWrite", 0f);
                waterMaterial.SetFloat("_Cull", 0f);
                waterMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                return waterMaterial;
            }
        }

        public static Material EnvironmentWaterMaterial
        {
            get
            {
                if (environmentWaterMaterial != null) return environmentWaterMaterial;
                environmentWaterMaterial = Resources.Load<Material>("VFX/ThuyTinh/MAT_SkillZ_EnvironmentWater");
                return environmentWaterMaterial != null ? environmentWaterMaterial : WaterMaterial;
            }
        }

        public static Material ShieldWaterMaterial
        {
            get
            {
                if (shieldWaterMaterial != null) return shieldWaterMaterial;
                shieldWaterMaterial = Resources.Load<Material>("VFX/ThuyTinh/MAT_SkillX_TransparentWater");
                return shieldWaterMaterial != null ? shieldWaterMaterial : EnvironmentWaterMaterial;
            }
        }

        public static Mesh ShieldDomeMesh
        {
            get
            {
                if (shieldDomeMesh == null)
                    shieldDomeMesh = CreateDomeMesh(1.72f, 2.45f);
                return shieldDomeMesh;
            }
        }

        static Mesh CreateDomeMesh(float radius, float height, int segments = 40, int rings = 14)
        {
            var vertices = new Vector3[(rings + 1) * (segments + 1)];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[rings * segments * 6];

            for (int ring = 0; ring <= rings; ring++)
            {
                float v = ring / (float)rings;
                float polar = v * Mathf.PI * 0.5f;
                float horizontal = Mathf.Sin(polar) * radius;
                float y = Mathf.Cos(polar) * height;
                for (int segment = 0; segment <= segments; segment++)
                {
                    float u = segment / (float)segments;
                    float angle = u * Mathf.PI * 2f;
                    int index = ring * (segments + 1) + segment;
                    vertices[index] = new Vector3(Mathf.Cos(angle) * horizontal, y, Mathf.Sin(angle) * horizontal);
                    uv[index] = new Vector2(u, v);
                }
            }

            int triangle = 0;
            for (int ring = 0; ring < rings; ring++)
            for (int segment = 0; segment < segments; segment++)
            {
                int current = ring * (segments + 1) + segment;
                int next = current + segments + 1;
                triangles[triangle++] = current;
                triangles[triangle++] = current + 1;
                triangles[triangle++] = next;
                triangles[triangle++] = current + 1;
                triangles[triangle++] = next + 1;
                triangles[triangle++] = next;
            }

            var mesh = new Mesh { name = "Procedural Water Shield Dome" };
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static void CastGroundWave(PlayerController caster, SpellData spell)
        {
            var root = new GameObject("Ground Water Wave");
            root.AddComponent<GroundWaterWave>().Init(caster, spell);
        }

        public static void CastShield(PlayerController caster, SpellData spell)
        {
            caster.Health.GrantInvulnerability(spell.effectDuration);
            var root = new GameObject("Aqua Shield Aura");
            root.AddComponent<AquaShieldAura>().Init(caster, spell);
        }

        public static WaterTowerEffect BeginTower(PlayerController caster, SpellData spell)
        {
            var root = new GameObject("Water Tower");
            var effect = root.AddComponent<WaterTowerEffect>();
            effect.Init(caster, spell);
            return effect;
        }

        public static void CastSingleWaterSlash(PlayerController caster, SpellData spell, float side)
        {
            if (caster == null || spell == null) return;

            if (spell.effectPrefab != null)
            {
                GameObject slash = Object.Instantiate(spell.effectPrefab);
                slash.name = "Aerial Water Slash";
                WaveProjectile projectile = slash.GetComponent<WaveProjectile>();
                if (projectile != null)
                {
                    projectile.Init(caster, spell);
                    slash.transform.rotation = caster.transform.rotation * Quaternion.Euler(0f, 0f, side * 30f);
                    slash.transform.position += caster.transform.right * side * 0.3f + Vector3.up * 0.45f;
                    caster.Shake(0.24f);
                    return;
                }
                Object.Destroy(slash);
            }

            Vector3 origin = caster.transform.position + caster.transform.right * side * 0.65f +
                             Vector3.up * 0.95f + caster.transform.forward * 0.8f;
            var fallback = new GameObject("Aerial Water Slash");
            fallback.transform.SetPositionAndRotation(origin, caster.transform.rotation * Quaternion.Euler(0f, 0f, side * 30f));
            fallback.AddComponent<WaterSlashProjectile>().Init(caster, spell, side);
            caster.Shake(0.24f);
        }

        public static LineRenderer Ring(Transform parent, string name, float radius, float width,
            Material material = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.loop = true;
            line.useWorldSpace = false;
            line.positionCount = 49;
            line.widthMultiplier = width;
            line.sharedMaterial = material != null ? material : WaterMaterial;
            line.startColor = new Color(0.15f, 0.85f, 1f, 0.9f);
            line.endColor = line.startColor;
            for (int i = 0; i < line.positionCount; i++)
            {
                float a = i / 48f * Mathf.PI * 2f;
                line.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
            }
            return line;
        }
    }

    public sealed class GroundWaterWave : MonoBehaviour
    {
        PlayerController caster;
        SpellData spell;
        Vector3 start;
        readonly HashSet<Health> hit = new();
        GameObject waveVisual;
        ParticleSystem foam, spray;
        float elapsed;

        public void Init(PlayerController owner, SpellData data)
        {
            caster = owner;
            spell = data;
            transform.SetPositionAndRotation(owner.transform.position + owner.transform.forward * 0.8f + Vector3.up * 0.08f, owner.transform.rotation);
            start = transform.position;
            CreateEnvironmentWaveVisual();

            var foamObject = new GameObject("Foam and Droplets");
            foamObject.transform.SetParent(transform, false);
            foam = foamObject.AddComponent<ParticleSystem>();
            var main = foam.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.55f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.18f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.75f, 0.98f, 1f, 0.95f), new Color(0.05f, 0.55f, 1f, 0.65f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 240;
            var emission = foam.emission;
            emission.rateOverTime = 150f;
            var shape = foam.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(data.radius * 2f, 0.12f, 0.35f);
            var velocity = foam.velocityOverLifetime;
            velocity.enabled = true;
            // a velocity curve of one axis in another mode than the others is an error in the editor (and Error Pause freezes the game)
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(1.2f, 3.4f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            foamObject.GetComponent<ParticleSystemRenderer>().sharedMaterial = ThuyTinhWaterSpells.EnvironmentWaterMaterial;

            var sprayObject = new GameObject("Crescent Spray");
            sprayObject.transform.SetParent(transform, false);
            spray = sprayObject.AddComponent<ParticleSystem>();
            var sprayMain = spray.main;
            sprayMain.loop = true;
            sprayMain.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.48f);
            sprayMain.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 5.5f);
            sprayMain.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.14f);
            sprayMain.startColor = new ParticleSystem.MinMaxGradient(new Color(0.85f, 1f, 1f, 1f), new Color(0.02f, 0.3f, 1f, 0.45f));
            sprayMain.simulationSpace = ParticleSystemSimulationSpace.World;
            sprayMain.maxParticles = 180;
            var sprayEmission = spray.emission;
            sprayEmission.rateOverTime = 110f;
            var sprayShape = spray.shape;
            sprayShape.shapeType = ParticleSystemShapeType.Box;
            sprayShape.scale = new Vector3(data.radius * 1.9f, 1.6f, 0.18f);
            var sprayVelocity = spray.velocityOverLifetime;
            sprayVelocity.enabled = true;
            sprayVelocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            sprayVelocity.y = new ParticleSystem.MinMaxCurve(1.5f, 4f);
            sprayVelocity.z = new ParticleSystem.MinMaxCurve(-1.2f, 0.4f);
            sprayObject.GetComponent<ParticleSystemRenderer>().sharedMaterial = ThuyTinhWaterSpells.EnvironmentWaterMaterial;
        }

        void CreateEnvironmentWaveVisual()
        {
            GameObject prefab = Resources.Load<GameObject>("VFX/ThuyTinh/SkillZ_GroundWave");
            if (prefab == null)
            {
                Debug.LogWarning("Skill Z wave FBX was not found in Resources/VFX/ThuyTinh.");
                return;
            }

            waveVisual = Instantiate(prefab, transform);
            waveVisual.name = "Environment Matched Ground Wave";
            waveVisual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, 180f, 0f));
            waveVisual.transform.localScale = Vector3.one;
            Material environmentWater = ThuyTinhWaterSpells.EnvironmentWaterMaterial;
            foreach (Renderer renderer in waveVisual.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterial = environmentWater;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        void Update()
        {
            elapsed += Time.deltaTime;
            float easeOut = 1f - Mathf.Pow(1f - Mathf.Clamp01(elapsed / 0.18f), 3f);
            transform.position += transform.forward * (spell.speed * Time.deltaTime);
            transform.localScale = new Vector3(easeOut, Mathf.Lerp(0.45f, 1f, easeOut), 1f);
            HitTargets();
            if (Vector3.Distance(start, transform.position) >= spell.range)
            {
                foam.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                spray.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                Destroy(gameObject, 0.6f);
                enabled = false;
            }
        }

        void HitTargets()
        {
            Vector3 centre = transform.position + transform.forward * 0.2f + Vector3.up * 1.05f;
            Collider[] found = Physics.OverlapBox(centre, new Vector3(spell.radius, 1.25f, 0.95f), transform.rotation, ~0, QueryTriggerInteraction.Ignore);
            foreach (Health target in SpellEffects.Targets(found, caster.Health))
            {
                if (!hit.Add(target)) continue;
                var info = new DamageInfo(spell.damage, caster.gameObject, target.transform.position, true);
                if (!target.TakeDamage(info)) continue;
                target.GetComponentInParent<IHitReceiver>()?.OnHit(info, transform.forward * spell.knockback + Vector3.up * 1.1f);
                SpawnImpact(target.transform.position);
                caster.HitStop(0.045f);
                caster.Shake(0.28f);
            }
        }

        void SpawnImpact(Vector3 position)
        {
            var impact = new GameObject("Water Wave Impact");
            impact.transform.SetPositionAndRotation(position + Vector3.up * 0.08f, transform.rotation);
            impact.AddComponent<WaterWaveImpact>().Init();
        }
    }

    // A short, layered contact burst using the exact same material and colour family as
    // the travelling ground wave. Keeping it procedural also avoids a mismatched prefab.
    public sealed class WaterWaveImpact : MonoBehaviour
    {
        const float Lifetime = 0.42f;
        readonly List<LineRenderer> rings = new();
        readonly List<Color> ringColors = new();
        ParticleSystem splash;
        Light flash;
        float elapsed;

        public void Init()
        {
            Material water = ThuyTinhWaterSpells.EnvironmentWaterMaterial;
            CreateRing("Impact Ground Ripple", 0.65f, 0.16f,
                new Color(0.1f, 0.72f, 1f, 0.82f), Quaternion.identity, water);
            CreateRing("Impact Rising Crown", 0.52f, 0.11f,
                new Color(0.72f, 0.98f, 1f, 0.92f), Quaternion.Euler(68f, 0f, 0f), water);

            var splashObject = new GameObject("Matched Water Spray");
            splashObject.transform.SetParent(transform, false);
            splashObject.transform.localPosition = Vector3.up * 0.12f;
            splashObject.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            splash = splashObject.AddComponent<ParticleSystem>();
            var main = splash.main;
            main.loop = false;
            main.duration = 0.18f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.38f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.2f, 4.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.045f, 0.15f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.78f, 0.98f, 1f, 0.95f),
                new Color(0.04f, 0.48f, 1f, 0.62f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 42;
            var emission = splash.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 34) });
            var shape = splash.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 38f;
            shape.radius = 0.22f;
            ParticleSystemRenderer splashRenderer = splashObject.GetComponent<ParticleSystemRenderer>();
            splashRenderer.sharedMaterial = water;
            splashRenderer.renderMode = ParticleSystemRenderMode.Billboard;

            flash = gameObject.AddComponent<Light>();
            flash.type = LightType.Point;
            flash.color = new Color(0.15f, 0.72f, 1f);
            flash.range = 4.2f;
            flash.intensity = 2.8f;
            splash.Play();
        }

        void CreateRing(string objectName, float radius, float width, Color color,
            Quaternion rotation, Material material)
        {
            LineRenderer ring = ThuyTinhWaterSpells.Ring(transform, objectName, radius, width, material);
            ring.transform.localRotation = rotation;
            ring.startColor = color;
            ring.endColor = color;
            rings.Add(ring);
            ringColors.Add(color);
        }

        void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Lifetime);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            float alpha = 1f - t;
            for (int i = 0; i < rings.Count; i++)
            {
                if (rings[i] == null) continue;
                float scale = Mathf.Lerp(0.25f, i == 0 ? 2.5f : 1.75f, eased);
                rings[i].transform.localScale = Vector3.one * scale;
                Color faded = ringColors[i];
                faded.a *= alpha * alpha;
                rings[i].startColor = faded;
                rings[i].endColor = faded;
            }
            if (flash != null) flash.intensity = 2.8f * alpha * alpha;
            if (elapsed >= Lifetime) Destroy(gameObject);
        }
    }

    public sealed class AquaShieldAura : MonoBehaviour
    {
        PlayerController caster;
        SpellData spell;
        float endAt, nextTick;
        float elapsed;
        Transform dome;
        ParticleSystem droplets;
        readonly List<LineRenderer> waterRipples = new();
        readonly List<LineRenderer> shieldCurrents = new();
        readonly HashSet<Health> explosionHit = new();

        public void Init(PlayerController owner, SpellData data)
        {
            caster = owner;
            spell = data;
            endAt = Time.time + spell.effectDuration;
            nextTick = Time.time + spell.tickInterval;
            transform.SetParent(owner.transform, false);
            transform.localPosition = Vector3.zero;
            CreateDome();
            CreateGroundVortex();
            CreateDroplets();
            Damage(true);
        }

        void Update()
        {
            elapsed += Time.deltaTime;
            AnimateDome();
            AnimateGroundVortex();
            if (Time.time >= endAt) { Destroy(gameObject); return; }
            if (Time.time >= nextTick) { nextTick = Time.time + spell.tickInterval; Damage(false); }
        }

        void CreateDome()
        {
            var domeObject = new GameObject("Living Water Dome");
            dome = domeObject.transform;
            dome.SetParent(transform, false);
            dome.localPosition = Vector3.up * 0.03f;
            domeObject.AddComponent<MeshFilter>().sharedMesh = ThuyTinhWaterSpells.ShieldDomeMesh;
            MeshRenderer domeRenderer = domeObject.AddComponent<MeshRenderer>();
            domeRenderer.sharedMaterial = ThuyTinhWaterSpells.ShieldWaterMaterial;
            domeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            domeRenderer.receiveShadows = false;

            // Tilted currents wrap the shield instead of reading as stacked flat circles.
            Vector3[] currentTilts =
            {
                new(18f, 0f, 12f), new(-22f, 42f, -15f),
                new(58f, 18f, 8f), new(-52f, -24f, -10f)
            };
            for (int currentIndex = 0; currentIndex < currentTilts.Length; currentIndex++)
            {
                LineRenderer current = ThuyTinhWaterSpells.Ring(dome, $"Wrapping Current {currentIndex + 1}", 1.76f + currentIndex * 0.08f, 0.12f - currentIndex * 0.015f);
                current.sharedMaterial = ThuyTinhWaterSpells.ShieldWaterMaterial;
                current.transform.localPosition = Vector3.up * 1.15f;
                current.transform.localRotation = Quaternion.Euler(currentTilts[currentIndex]);
                current.transform.localScale = new Vector3(1f, 0.72f + currentIndex * 0.05f, 1f);
                shieldCurrents.Add(current);
            }

        }

        void CreateGroundVortex()
        {
            const int rippleCount = 6;
            for (int i = 0; i < rippleCount; i++)
            {
                float normalized = i / (float)(rippleCount - 1);
                float radius = Mathf.Lerp(0.85f, spell.radius, normalized);
                LineRenderer ripple = ThuyTinhWaterSpells.Ring(transform, $"Ground Current {i + 1}", radius, Mathf.Lerp(0.22f, 0.065f, normalized));
                ripple.sharedMaterial = ThuyTinhWaterSpells.ShieldWaterMaterial;
                ripple.positionCount = 81;
                ripple.transform.localPosition = Vector3.up * (0.035f + i * 0.006f);
                ripple.startColor = Color.Lerp(new Color(0.72f, 0.98f, 1f, 0.96f), new Color(0.02f, 0.35f, 1f, 0.36f), normalized);
                ripple.endColor = ripple.startColor;
                waterRipples.Add(ripple);
            }
        }

        void CreateDroplets()
        {
            var particleObject = new GameObject("Rising Water Sparks");
            particleObject.transform.SetParent(transform, false);
            droplets = particleObject.AddComponent<ParticleSystem>();
            var main = droplets.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 1.25f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.11f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.75f, 0.98f, 1f, 0.95f), new Color(0.05f, 0.35f, 1f, 0.25f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 90;
            var emission = droplets.emission;
            emission.rateOverTime = 42f;
            var shape = droplets.shape;
            shape.shapeType = ParticleSystemShapeType.Donut;
            shape.radius = 3.2f;
            shape.donutRadius = 0.7f;
            var velocity = droplets.velocityOverLifetime;
            velocity.enabled = true;
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.45f, 1.5f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            particleObject.GetComponent<ParticleSystemRenderer>().sharedMaterial = ThuyTinhWaterSpells.ShieldWaterMaterial;
        }

        void AnimateDome()
        {
            if (dome == null) return;
            float appear = 1f - Mathf.Pow(1f - Mathf.Clamp01(elapsed / 0.22f), 3f);
            float breathe = 1f + Mathf.Sin(elapsed * 7.5f) * 0.035f;
            dome.localScale = new Vector3(appear * breathe, appear, appear * breathe);
            dome.Rotate(Vector3.up, 16f * Time.deltaTime, Space.Self);

            for (int currentIndex = 0; currentIndex < shieldCurrents.Count; currentIndex++)
            {
                float direction = currentIndex % 2 == 0 ? 1f : -1f;
                shieldCurrents[currentIndex].transform.Rotate(Vector3.up, direction * (34f + currentIndex * 13f) * Time.deltaTime, Space.Self);
            }

        }

        void AnimateGroundVortex()
        {
            for (int rippleIndex = 0; rippleIndex < waterRipples.Count; rippleIndex++)
            {
                LineRenderer ripple = waterRipples[rippleIndex];
                float normalized = rippleIndex / (float)Mathf.Max(1, waterRipples.Count - 1);
                float baseRadius = Mathf.Lerp(0.85f, spell.radius, normalized);
                float direction = rippleIndex % 2 == 0 ? 1f : -1f;
                for (int point = 0; point < ripple.positionCount; point++)
                {
                    float t = point / (float)(ripple.positionCount - 1);
                    float angle = t * Mathf.PI * 2f + elapsed * direction * (1.1f + rippleIndex * 0.08f);
                    float brokenSurface = Mathf.Sin(angle * 3f + elapsed * 4f + rippleIndex) * Mathf.Lerp(0.06f, 0.15f, normalized);
                    brokenSurface += Mathf.Sin(angle * 7f - elapsed * 2.8f) * Mathf.Lerp(0.015f, 0.05f, normalized);
                    float pulse = Mathf.Sin(elapsed * 3.2f - rippleIndex * 0.65f) * Mathf.Lerp(0.07f, 0.14f, normalized);
                    float radius = baseRadius + brokenSurface + pulse;
                    float crest = Mathf.Sin(angle * 4f + elapsed * 5f) * Mathf.Lerp(0.025f, 0.075f, normalized);
                    ripple.SetPosition(point, new Vector3(Mathf.Cos(angle) * radius, crest, Mathf.Sin(angle) * radius));
                }
            }
        }

        void Damage(bool explosion)
        {
            foreach (Health target in SpellEffects.Targets(Physics.OverlapSphere(caster.transform.position + Vector3.up, spell.radius, ~0, QueryTriggerInteraction.Ignore), caster.Health))
            {
                if (explosion && !explosionHit.Add(target)) continue;
                float amount = explosion ? spell.damage : spell.tickDamage;
                var info = new DamageInfo(amount, caster.gameObject, target.transform.position, explosion);
                if (target.TakeDamage(info))
                {
                    Vector3 away = (target.transform.position - caster.transform.position).normalized;
                    target.GetComponentInParent<IHitReceiver>()?.OnHit(info, explosion ? away * 5f + Vector3.up * 2f : Vector3.zero);
                    if (!explosion)
                    {
                        WaterRootEffect root = target.GetComponent<WaterRootEffect>();
                        if (root == null) root = target.gameObject.AddComponent<WaterRootEffect>();
                        root.Refresh(spell.tickInterval + 0.1f);
                    }
                }
            }
        }
    }

    // Refreshed by every damage tick while an enemy stands in the water field.
    // LateUpdate wins over AI movement without disabling combat scripts or gravity.
    public sealed class WaterRootEffect : MonoBehaviour
    {
        Vector3 anchor;
        float rootedUntil;

        public void Refresh(float duration)
        {
            if (Time.time >= rootedUntil) anchor = transform.position;
            rootedUntil = Mathf.Max(rootedUntil, Time.time + duration);
        }

        void LateUpdate()
        {
            if (Time.time >= rootedUntil)
            {
                Destroy(this);
                return;
            }

            Vector3 current = transform.position;
            transform.position = new Vector3(anchor.x, current.y, anchor.z);
        }
    }

    public sealed class WaterTowerEffect : MonoBehaviour
    {
        const float SlashRootHeight = -0.1f;
        PlayerController caster;
        SpellData spell;
        bool stopped;

        public int SlashesFired { get; private set; }
        public bool Finished => SlashesFired >= ThuyTinhWaterSpells.MaximumWaterSlashes;

        public void Init(PlayerController owner, SpellData data)
        {
            caster = owner;
            spell = data;
            transform.SetPositionAndRotation(owner.transform.position, owner.transform.rotation);
        }

        void Update()
        {
            if (stopped || caster == null || spell == null) return;
            transform.SetPositionAndRotation(caster.transform.position, caster.transform.rotation);
            if (Finished) Destroy(gameObject, 0.45f);
        }

        // Called by the player animation timeline at the exact contact frame.
        public void FireNextSlash()
        {
            if (stopped || caster == null || spell == null || Finished) return;
            SpawnSlash(SlashesFired);
            SlashesFired++;
        }

        void SpawnSlash(int index)
        {
            float side = index % 2 == 0 ? -1f : 1f;
            if (spell.effectPrefab != null)
            {
                GameObject slash = Instantiate(spell.effectPrefab);
                slash.name = $"Horizontal Water Wave Slash {index + 1}";
                WaveProjectile projectile = slash.GetComponent<WaveProjectile>();
                if (projectile != null)
                {
                    projectile.Init(caster, spell);
                    float roll = side * 30f;
                    slash.transform.rotation = caster.transform.rotation * Quaternion.Euler(0f, 0f, roll);
                    slash.transform.position += caster.transform.right * side * 0.3f + Vector3.up * SlashRootHeight;
                }
                else
                {
                    Destroy(slash);
                    SpawnFallbackSlash(index, side);
                }
            }
            else
            {
                SpawnFallbackSlash(index, side);
            }
            caster.Shake(0.24f);
        }

        void SpawnFallbackSlash(int index, float side)
        {
            Vector3 origin = caster.transform.position + caster.transform.right * side * 0.65f +
                             Vector3.up * 0.4f + caster.transform.forward * 0.8f;
            var slash = new GameObject($"Water Slash {index + 1}");
            slash.transform.SetPositionAndRotation(origin, caster.transform.rotation * Quaternion.Euler(0f, 0f, side * 30f));
            slash.AddComponent<WaterSlashProjectile>().Init(caster, spell, side);
        }

        public void StopChannel(bool completed)
        {
            stopped = true;
            if (!completed) Destroy(gameObject);
        }

        public void SetPhase(VanThuyTramPhase _) { }
    }

    public sealed class WaterSlashProjectile : MonoBehaviour
    {
        PlayerController caster;
        SpellData spell;
        Vector3 previousPosition;
        float travelled;
        float elapsed;
        readonly HashSet<Health> hitTargets = new();
        readonly List<LineRenderer> layers = new();
        readonly List<Color> layerStartColors = new();
        readonly List<Color> layerEndColors = new();
        Light centreFlash;

        public void Init(PlayerController owner, SpellData data, float side)
        {
            caster = owner;
            spell = data;
            previousPosition = transform.position;
            CreateArc("Outer Glow", 0.42f, new Color(0.01f, 0.18f, 1f, 0.24f), 1.22f, side);
            CreateArc("Water Ribbon", 0.24f, new Color(0.02f, 0.58f, 1f, 0.86f), 1.08f, side);
            CreateArc("White Crest", 0.085f, new Color(0.82f, 1f, 1f, 1f), 1f, side);

            centreFlash = gameObject.AddComponent<Light>();
            centreFlash.type = LightType.Point;
            centreFlash.color = new Color(0.2f, 0.8f, 1f);
            centreFlash.range = 5.5f;
            centreFlash.intensity = 3.2f;
        }

        void CreateArc(string objectName, float width, Color color, float scale, float side)
        {
            LineRenderer line = new GameObject(objectName).AddComponent<LineRenderer>();
            line.transform.SetParent(transform, false);
            line.useWorldSpace = false;
            line.positionCount = 25;
            line.widthMultiplier = width;
            line.numCapVertices = 4;
            line.numCornerVertices = 3;
            line.textureMode = LineTextureMode.Stretch;
            line.sharedMaterial = ThuyTinhWaterSpells.EnvironmentWaterMaterial;
            Color endColor = new Color(color.r, color.g, color.b, 0.05f);
            line.startColor = color;
            line.endColor = endColor;
            line.widthCurve = new AnimationCurve(
                new Keyframe(0f, 0.02f),
                new Keyframe(0.12f, 0.45f),
                new Keyframe(0.5f, 1f),
                new Keyframe(0.88f, 0.45f),
                new Keyframe(1f, 0.02f));
            for (int i = 0; i < line.positionCount; i++)
            {
                float t = i / (float)(line.positionCount - 1);
                float angle = Mathf.Lerp(-132f, 58f, t) * Mathf.Deg2Rad;
                float x = Mathf.Cos(angle) * 2.7f * scale;
                float y = Mathf.Sin(angle) * 1.18f * scale;
                float z = side * Mathf.Sin(t * Mathf.PI) * 0.34f;
                line.SetPosition(i, new Vector3(x, y, z));
            }
            layers.Add(line);
            layerStartColors.Add(color);
            layerEndColors.Add(endColor);
        }

        void Update()
        {
            if (caster == null || spell == null) { Destroy(gameObject); return; }
            elapsed += Time.deltaTime;
            float step = Mathf.Max(10f, spell.speed * 1.15f) * Time.deltaTime;
            previousPosition = transform.position;
            transform.position += transform.forward * step;
            travelled += step;
            float alpha = 1f - Mathf.Clamp01(elapsed / 0.75f);
            float expansion = 1f + Mathf.Sin(Mathf.Clamp01(elapsed / 0.22f) * Mathf.PI * 0.5f) * 0.35f;
            transform.localScale = new Vector3(expansion, expansion, 1f);
            if (centreFlash != null)
                centreFlash.intensity = 3.2f * Mathf.Pow(alpha, 2f);
            for (int i = 0; i < layers.Count; i++)
            {
                LineRenderer layer = layers[i];
                Color start = layerStartColors[i];
                Color end = layerEndColors[i];
                start.a = layerStartColors[i].a * alpha;
                end.a = layerEndColors[i].a * alpha;
                layer.startColor = start;
                layer.endColor = end;
            }
            HitAlongPath(previousPosition, transform.position);
            if (travelled >= spell.range || elapsed >= 0.8f) Destroy(gameObject);
        }

        void HitAlongPath(Vector3 from, Vector3 to)
        {
            foreach (Health target in SpellEffects.Targets(Physics.OverlapCapsule(from, to, 1.45f, ~0, QueryTriggerInteraction.Ignore), caster.Health))
            {
                if (!hitTargets.Add(target)) continue;
                var info = new DamageInfo(20f, caster.gameObject, target.transform.position, true);
                if (!target.TakeDamage(info)) continue;
                target.GetComponentInParent<IHitReceiver>()?.OnHit(info, transform.forward * 4.5f + Vector3.up * 0.7f);
                caster.HitStop(0.035f);
            }
        }
    }

}
