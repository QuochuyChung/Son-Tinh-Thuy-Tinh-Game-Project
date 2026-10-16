using System.Collections;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SonTinhThuyTinh.Cutscene
{
    // The weather and the magic of a cutscene, built from code at runtime: rain, dust, rocks rising out of the ground (Sơn Tinh), water
    // gathering and spreading round Thủy Tinh's feet, lightning, and the mood of the light (colour / intensity of the sun, fog, sky).
    // The materials come from the scene builder (URP particles: a soft dot for dust and spray, a ring for ripples; the stone of the map).
    public class CineStage : MonoBehaviour
    {
        [SerializeField] Material softDot;
        [SerializeField] Material ring;
        [SerializeField] Material stone;
        [SerializeField] Material puddle;
        [SerializeField] GameObject[] rockPrefabs;
        [SerializeField] GameObject[] mountainPrefabs;   // tall rock models that rise as Sơn Tinh's mountains (origin at their foot); empty = plain blocks
        [SerializeField] Volume volume;
        [SerializeField] Light sun;
        [SerializeField] CinematicFx fx;
        [SerializeField] CinematicCamera cam;

        ParticleSystem rain, dust, spray, ripples, petals;
        readonly List<GameObject> spawned = new();
        Coroutine moodRoutine;

        void Awake()
        {
            // the sky's exposure is changed with the mood: on a copy, never on the asset
            if (RenderSettings.skybox != null) RenderSettings.skybox = new Material(RenderSettings.skybox);
            rain = Make("Rain", softDot, 1500, 0f, false);
            var rm = rain.main; rm.startSpeed = 30f; rm.startLifetime = 0.8f; rm.startSize = 0.055f; rm.startColor = new Color(0.9f, 0.95f, 1f, 0.8f);
            rm.gravityModifier = 0.1f; rm.simulationSpace = ParticleSystemSimulationSpace.World;
            var rs = rain.shape; rs.enabled = true; rs.shapeType = ParticleSystemShapeType.Box; rs.scale = new Vector3(44f, 0.1f, 44f);
            rain.transform.rotation = Quaternion.Euler(80f, 20f, 0f);
            var rr = rain.GetComponent<ParticleSystemRenderer>(); rr.renderMode = ParticleSystemRenderMode.Stretch; rr.velocityScale = 0.07f; rr.lengthScale = 6f;
            var re = rain.emission; re.enabled = true; re.rateOverTime = 0f;
            rain.Play();

            dust = Make("Dust", softDot, 300, 0f, true);
            var dm = dust.main; dm.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.4f); dm.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4f);
            dm.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.8f); dm.startColor = new Color(0.72f, 0.64f, 0.5f, 0.45f); dm.gravityModifier = -0.02f;
            var ds = dust.shape; ds.enabled = true; ds.shapeType = ParticleSystemShapeType.Circle; ds.radius = 1.4f; ds.rotation = new Vector3(-90f, 0f, 0f);
            var dsz = dust.sizeOverLifetime; dsz.enabled = true; dsz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.4f, 1f, 1.3f));
            Fade(dust);

            spray = Make("Spray", softDot, 400, 1.2f, true);
            var sm = spray.main; sm.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f); sm.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
            sm.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f); sm.startColor = new Color(0.85f, 0.97f, 1f, 0.9f);
            var ss = spray.shape; ss.enabled = true; ss.shapeType = ParticleSystemShapeType.Cone; ss.angle = 40f; ss.radius = 0.8f; ss.rotation = new Vector3(-90f, 0f, 0f);
            Fade(spray);

            ripples = Make("Ripples", ring, 40, 0f, true);
            var pm = ripples.main; pm.startSpeed = 0f; pm.startLifetime = 1.8f; pm.startSize = 4f; pm.startColor = new Color(0.85f, 0.97f, 1f, 0.8f);
            var psz = ripples.sizeOverLifetime; psz.enabled = true; psz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.2f, 0f, 3f), new Keyframe(1f, 1f)));
            ripples.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            Fade(ripples);

            petals = Make("Petals", softDot, 600, 0.03f, false);
            var pt = petals.main; pt.startLifetime = 11f; pt.startSpeed = 0f; pt.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
            pt.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.72f, 0.82f, 0.95f), new Color(1f, 0.92f, 0.94f, 0.95f));
            pt.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            var pshape = petals.shape; pshape.enabled = true; pshape.shapeType = ParticleSystemShapeType.Box; pshape.scale = new Vector3(26f, 0.1f, 26f);
            var pn = petals.noise; pn.enabled = true; pn.strength = 1.4f; pn.frequency = 0.35f; pn.scrollSpeed = 0.4f;
            var pe = petals.emission; pe.enabled = true; pe.rateOverTime = 0f;
            Fade(petals);
            petals.Play();
        }

        // Flower petals drifting down over a spot (the feast): rate per second, 0 stops them.
        public void Petals(float rate, Vector3 around)
        {
            petals.transform.position = around + Vector3.up * 13f;
            var e = petals.emission; e.rateOverTime = rate;
        }

        ParticleSystem Make(string name, Material material, int max, float gravity, bool burstOnly)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.playOnAwake = false; main.loop = !burstOnly; main.maxParticles = max; main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission; emission.enabled = !burstOnly;
            var shape = ps.shape; shape.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = material; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            return ps;
        }

        static void Fade(ParticleSystem ps)
        {
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
        }

        // ---------------------------------------------------------------- weather

        public void Rain(float rate, Vector3? around = null)
        {
            if (around.HasValue) rain.transform.position = around.Value + Vector3.up * 22f;
            var e = rain.emission; e.rateOverTime = rate;
        }

        public IEnumerator RainTo(float rate, float seconds, Vector3? around = null)
        {
            if (around.HasValue) rain.transform.position = around.Value + Vector3.up * 22f;
            var e = rain.emission; float from = e.rateOverTime.constant;
            for (float t = 0f; t < seconds; t += Time.deltaTime) { e.rateOverTime = Mathf.Lerp(from, rate, t / seconds); yield return null; }
            e.rateOverTime = rate;
        }

        public void Lightning(float strength = 1f)
        {
            if (fx != null) fx.Flash(new Color(0.85f, 0.92f, 1f), 0.5f, 0.7f * strength);
            if (cam != null) cam.Shake(0.5f * strength, 0.5f);
            if (sun != null) StartCoroutine(SunSpike(strength));
        }

        IEnumerator SunSpike(float strength)
        {
            float baseIntensity = sun.intensity; Color baseColor = sun.color;
            sun.intensity = baseIntensity + 3f * strength; sun.color = new Color(0.8f, 0.9f, 1f);
            yield return new WaitForSecondsRealtime(0.08f);
            sun.intensity = baseIntensity + 0.5f; yield return new WaitForSecondsRealtime(0.07f);
            sun.intensity = baseIntensity + 2f * strength; yield return new WaitForSecondsRealtime(0.1f);
            sun.intensity = baseIntensity; sun.color = baseColor;
        }

        // ---------------------------------------------------------------- mood

        // The light and the air change over `seconds`: sun colour / intensity, fog colour / density, and the sky's tint / exposure if its
        // material has them.
        public IEnumerator Mood(Color sunColor, float sunIntensity, Color fogColor, float fogDensity, float skyExposure, float seconds,
            float gradeExposure = 0.1f, float gradeSaturation = 10f, Color? gradeFilter = null)
        {
            ColorAdjustments grade = null;
            if (volume != null && volume.profile != null) volume.profile.TryGet(out grade);
            float ge0 = grade != null ? grade.postExposure.value : 0f, gs0 = grade != null ? grade.saturation.value : 0f;
            Color gf0 = grade != null ? grade.colorFilter.value : Color.white; Color gf1 = gradeFilter ?? Color.white;
            Color s0 = sun != null ? sun.color : Color.white; float i0 = sun != null ? sun.intensity : 1f;
            Color f0 = RenderSettings.fogColor; float d0 = RenderSettings.fogDensity;
            var sky = RenderSettings.skybox; bool hasExposure = sky != null && sky.HasProperty("_Exposure"); float e0 = hasExposure ? sky.GetFloat("_Exposure") : 1f;
            if (!RenderSettings.fog) { RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared; }
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / seconds);
                if (sun != null) { sun.color = Color.Lerp(s0, sunColor, k); sun.intensity = Mathf.Lerp(i0, sunIntensity, k); }
                RenderSettings.fogColor = Color.Lerp(f0, fogColor, k); RenderSettings.fogDensity = Mathf.Lerp(d0, fogDensity, k);
                if (hasExposure) sky.SetFloat("_Exposure", Mathf.Lerp(e0, skyExposure, k));
                if (grade != null)
                {
                    grade.postExposure.value = Mathf.Lerp(ge0, gradeExposure, k);
                    grade.saturation.value = Mathf.Lerp(gs0, gradeSaturation, k);
                    grade.colorFilter.value = Color.Lerp(gf0, gf1, k);
                }
                yield return null;
            }
            if (grade != null) { grade.postExposure.value = gradeExposure; grade.saturation.value = gradeSaturation; grade.colorFilter.value = gf1; }
            if (sun != null) { sun.color = sunColor; sun.intensity = sunIntensity; }
            RenderSettings.fogColor = fogColor; RenderSettings.fogDensity = fogDensity;
            if (hasExposure) sky.SetFloat("_Exposure", skyExposure);
        }

        // ---------------------------------------------------------------- Sơn Tinh: dust and rising rock

        public void Dust(Vector3 at, float scale = 1f, int count = 40)
        {
            dust.transform.position = at;
            var s = dust.shape; s.radius = 1.4f * scale;
            dust.Emit(count);
        }

        // Boulders (the map's rock prefabs) push up out of the ground and stay until SinkRocks. They form an arc round `center` on the
        // side away from `awayFrom` (a point; null = a full ring) and never come within `clear` metres of it, so they cannot reach
        // another actor.
        public IEnumerator RaiseRocks(Vector3 center, float radius, int count, float seconds = 1.4f, Vector3? awayFrom = null, float clear = 3.5f, bool blocks = false, float arcDegrees = 230f)
        {
            var rocks = new List<(Transform t, Vector3 from, Vector3 to, Quaternion rot, float delay)>();
            var rng = new System.Random(7);
            float centreAngle = 0f, span = 360f;
            if (awayFrom.HasValue)
            {
                Vector3 away = center - awayFrom.Value; away.y = 0f;
                centreAngle = Mathf.Atan2(away.x, away.z) * Mathf.Rad2Deg; span = arcDegrees;
            }
            for (int i = 0; i < count; i++)
            {
                float k = count > 1 ? i / (float)(count - 1) : 0.5f;
                float deg = awayFrom.HasValue ? centreAngle + (k - 0.5f) * span : k * 360f;
                float a = (deg + ((float)rng.NextDouble() - 0.5f) * 8f) * Mathf.Deg2Rad;
                float r = radius * (0.92f + (float)rng.NextDouble() * 0.2f);
                Vector3 spot = center + new Vector3(Mathf.Sin(a) * r, 0f, Mathf.Cos(a) * r);
                if (awayFrom.HasValue)
                {
                    Vector3 d = spot - awayFrom.Value; d.y = 0f;
                    if (d.magnitude < clear) continue;
                }
                GameObject go;
                float h = 3.0f + (float)rng.NextDouble() * 2.6f;   // metres tall
                float mountainScale = 1f;
                if (blocks && mountainPrefabs != null && mountainPrefabs.Length > 0)
                {
                    go = Instantiate(mountainPrefabs[rng.Next(mountainPrefabs.Length)]);
                    h = 8f + (float)rng.NextDouble() * 4f;
                    var mr = go.GetComponentsInChildren<Renderer>(); var mb = mr[0].bounds; foreach (var q in mr) mb.Encapsulate(q.bounds);
                    mountainScale = h / Mathf.Max(mb.size.y, 0.1f);
                    go.transform.localScale = go.transform.localScale * mountainScale;
                }
                else if (blocks)
                {
                    // plain tall rectangular blocks: stand-ins for the mountains (to be swapped for a modelled mountain)
                    h = 8f + (float)rng.NextDouble() * 4f;
                    go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.transform.localScale = new Vector3(4.5f + (float)rng.NextDouble() * 2f, h, 4f + (float)rng.NextDouble() * 2f);
                    var rend = go.GetComponent<Renderer>();
                    if (stone != null) rend.sharedMaterial = stone;
                    else { var m = new Material(Shader.Find("Universal Render Pipeline/Lit")); m.color = new Color(0.45f, 0.45f, 0.48f); rend.sharedMaterial = m; }
                }
                else if (rockPrefabs != null && rockPrefabs.Length > 0)
                {
                    go = Instantiate(rockPrefabs[rng.Next(rockPrefabs.Length)]);
                    go.transform.localScale = new Vector3(1.8f + (float)rng.NextDouble() * 1.2f, h, 1.8f + (float)rng.NextDouble() * 1.2f);
                }
                else
                {
                    go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.transform.localScale = new Vector3(2.0f, h, 1.8f);
                    if (stone != null) go.GetComponent<Renderer>().sharedMaterial = stone;
                }
                foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
                go.name = "StageRock";
                Vector3 to = new Vector3(spot.x, center.y - 0.6f + (blocks && !(mountainPrefabs != null && mountainPrefabs.Length > 0) ? h * 0.5f : 0f), spot.z);
                go.transform.position = to + Vector3.down * (h + 1.5f);
                go.transform.rotation = blocks ? Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f) : Quaternion.Euler(((float)rng.NextDouble() - 0.5f) * 14f, (float)rng.NextDouble() * 360f, ((float)rng.NextDouble() - 0.5f) * 14f);
                if (blocks) { float foot = (mountainPrefabs != null && mountainPrefabs.Length > 0 ? 4f * mountainScale : Mathf.Max(go.transform.localScale.x, go.transform.localScale.z)) * 1.45f + 3f; HideScenery(new Bounds(new Vector3(to.x, center.y + h * 0.5f, to.z), new Vector3(foot, h + 4f, foot))); }
                spawned.Add(go);
                rocks.Add((go.transform, go.transform.position, to, go.transform.rotation, (float)rng.NextDouble() * 0.5f));
            }
            for (float t = 0f; t < seconds + 0.5f; t += Time.deltaTime)
            {
                foreach (var r in rocks)
                {
                    if (r.t == null) continue;
                    float k = Mathf.Clamp01((t - r.delay) / seconds);
                    float e = 1f - (1f - k) * (1f - k) * (1f - k);
                    r.t.position = Vector3.Lerp(r.from, r.to, e);
                    float wobble = (1f - k) * 10f;
                    r.t.rotation = r.rot * Quaternion.Euler(Mathf.Sin(t * 9f + r.delay * 7f) * wobble, 0f, Mathf.Cos(t * 8f) * wobble);
                    if (k > 0f && k < 0.1f && Random.value < 0.12f) Dust(r.to + Vector3.up * 0.2f, 0.5f, 6);
                }
                yield return null;
            }
        }

        // Trees, rocks, torches, banners... of the map that stand where a stage block rises are put away (so nothing is crossed by a
        // block) and come back when the blocks have sunk.
        readonly List<GameObject> hiddenScenery = new();
        static readonly string[] SceneryNames = { "Tree", "Bush", "Rock", "Stone", "Plant", "Flower", "Grass", "Fern", "Mushroom", "Torch", "Banner", "Drum", "Crystal", "Fence", "Barricade" };

        void HideScenery(Bounds area)
        {
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (r is ParticleSystemRenderer || !r.enabled || !r.bounds.Intersects(area)) continue;
                if (r.GetComponentInParent<ActorRig>() != null || r.GetComponentInParent<Canvas>() != null) continue;
                var u = r.transform;
                while (u.parent != null && u.parent.parent != null && u.parent.childCount <= 6) u = u.parent;
                if (!u.gameObject.activeSelf || u.name.StartsWith("Stage")) continue;
                string n = u.name, par = u.parent != null ? u.parent.name : "";
                bool scenery = SceneryNames.Any(k => n.Contains(k) || par.Contains(k));
                if (!scenery || n.Contains("Palace") || n.Contains("Throne")) continue;
                u.gameObject.SetActive(false);
                hiddenScenery.Add(u.gameObject);
            }
        }

        void RestoreScenery()
        {
            foreach (var go in hiddenScenery) if (go != null) go.SetActive(true);
            hiddenScenery.Clear();
        }

        // The blocks and the scenery put away for them are back at once (a skipped scene must not leave them standing).
        public void ClearRocksNow()
        {
            foreach (var go in spawned.Where(g => g != null && g.name == "StageRock").ToList()) { spawned.Remove(go); Destroy(go); }
            RestoreScenery();
        }

        // The rocks sink back into the ground and are removed.
        public IEnumerator SinkRocks(float seconds = 1.4f)
        {
            var list = new List<(Transform t, Vector3 from)>();
            foreach (var go in spawned) if (go != null && go.name == "StageRock") list.Add((go.transform, go.transform.position));
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = t / seconds;
                foreach (var r in list) if (r.t != null) r.t.position = r.from + Vector3.down * (k * k * 16f);
                yield return null;
            }
            foreach (var r in list) if (r.t != null) { spawned.Remove(r.t.gameObject); Destroy(r.t.gameObject); }
            RestoreScenery();
        }

        // ---------------------------------------------------------------- Thủy Tinh: water gathers

        // Thủy Tinh calls the water: a pool spreads under him, rings run out of it one after another, a vortex of droplets and
        // streaks of wind spiral up round his body, a crown of spray leaps at the start, and rain pours down on the spot.
        public IEnumerator SummonWater(Vector3 at, float radius = 4.5f, float seconds = 3f)
        {
            GameObject pool = null;
            if (puddle != null)
            {
                pool = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(pool.GetComponent<Collider>());
                pool.name = "StagePuddle";
                pool.transform.SetPositionAndRotation(at + Vector3.up * 0.04f, Quaternion.Euler(90f, 0f, 0f));
                pool.transform.localScale = Vector3.one * 0.1f;
                pool.GetComponent<Renderer>().sharedMaterial = puddle;
                pool.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                spawned.Add(pool);
            }
            var vortex = MakeVortex(at, "WaterVortex", 0.14f, 0.18f, 260f, 4.8f, 3.2f, ParticleSystemRenderMode.Billboard, new Color(0.7f, 0.92f, 1f, 0.9f), 1.5f);
            var wind = MakeVortex(at + Vector3.up * 0.4f, "WaterWind", 0.05f, 0.06f, 90f, 6.5f, 2.2f, ParticleSystemRenderMode.Stretch, new Color(0.92f, 0.98f, 1f, 0.55f), 1.1f);
            var rain = MakeRainColumn(at);
            spray.transform.position = at;
            spray.Emit(90);
            ripples.transform.position = at + Vector3.up * 0.06f;
            float nextRing = 0f; int ring = 0;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, t / 1.4f));
                if (pool != null) pool.transform.localScale = Vector3.one * Mathf.Lerp(0.1f, radius * 2.2f, k) * (1f + 0.03f * Mathf.Sin(t * 5f));
                if (t >= nextRing)
                {
                    // rings of three sizes, one behind the other, each thin and bright
                    float size = radius * (1.0f + 0.55f * (ring % 3));
                    ripples.Emit(new ParticleSystem.EmitParams { position = at + Vector3.up * 0.07f, startSize = size, startLifetime = 1.8f }, 1);
                    ring++; nextRing = t + 0.28f;
                }
                if (Random.value < 0.12f) spray.Emit(new ParticleSystem.EmitParams { position = at + Random.insideUnitSphere * radius * 0.6f, velocity = Vector3.up * Random.Range(2f, 4.5f) }, 3);
                yield return null;
            }
            foreach (var ps in new[] { vortex, wind, rain }) { var e = ps.emission; e.enabled = false; Destroy(ps.gameObject, 3f); }
        }

        // Droplets (or streaks) circling a point and rising: the spiral of the storm.
        ParticleSystem MakeVortex(Vector3 at, string name, float sizeMin, float sizeMax, float rate, float orbit, float rise, ParticleSystemRenderMode mode, Color color, float radius)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = at;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = true; main.playOnAwake = false; main.maxParticles = 700;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.5f); main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax); main.startColor = color; main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = rate;
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = radius; sh.radiusThickness = 0.35f; sh.rotation = new Vector3(-90f, 0f, 0f);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            // all three linear and all three orbital curves must be in the same mode, or Unity throws
            vel.x = new ParticleSystem.MinMaxCurve(0f); vel.y = new ParticleSystem.MinMaxCurve(rise * 0.8f); vel.z = new ParticleSystem.MinMaxCurve(0f);
            vel.orbitalX = new ParticleSystem.MinMaxCurve(0f); vel.orbitalY = new ParticleSystem.MinMaxCurve(orbit); vel.orbitalZ = new ParticleSystem.MinMaxCurve(0f);
            vel.radial = new ParticleSystem.MinMaxCurve(-0.4f);
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.7f, 1f, 0.2f));
            Fade(ps);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = softDot; r.renderMode = mode; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (mode == ParticleSystemRenderMode.Stretch) { r.velocityScale = 0.12f; r.lengthScale = 3f; }
            ps.Play();
            return ps;
        }

        // A dense column of rain pouring on one spot.
        ParticleSystem MakeRainColumn(Vector3 at)
        {
            var go = new GameObject("WaterRainColumn");
            go.transform.SetParent(transform, false);
            go.transform.position = at + Vector3.up * 9f;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = true; main.playOnAwake = false; main.maxParticles = 900;
            main.startLifetime = 0.55f; main.startSpeed = 0f; main.startSize = 0.07f; main.startColor = new Color(0.85f, 0.95f, 1f, 0.85f);
            main.gravityModifier = 0f; main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 700f;
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 3.4f; sh.rotation = new Vector3(90f, 0f, 0f);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0f); vel.y = new ParticleSystem.MinMaxCurve(-17f); vel.z = new ParticleSystem.MinMaxCurve(0f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = softDot; r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.09f; r.lengthScale = 5f;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        // The ring keeps running slowly under a standing figure.
        public void Ripple(Vector3 at, float size = 3f)
        {
            ripples.Emit(new ParticleSystem.EmitParams { position = at + Vector3.up * 0.06f, startSize = size }, 1);
        }

        public void ClearSpawned()
        {
            foreach (var go in spawned) if (go != null) Destroy(go);
            spawned.Clear();
        }
    }
}
