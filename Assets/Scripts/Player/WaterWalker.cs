using UnityEngine;

namespace SonTinhThuyTinh.Player
{
    // Thủy Tinh, the water god, walks on water (WaterSurface gives him a floor there) and the water answers him:
    //  - rings spread from his feet: one every second or so while he stands, faster and bigger while he walks and runs;
    //  - running kicks up spray from his feet;
    //  - landing on the water (jump, fall) throws a big ring and a burst of droplets.
    // If he ever ends up under the surface (knocked in before the floor switched on) he is lifted back on top.
    // The two particle systems are made here; their materials (URP Particles Unlit, ring / droplet textures) come from
    // Assets/Editor/WaterWalkBuilder.cs, which also puts this on the Thủy Tinh player prefabs.
    [RequireComponent(typeof(PlayerController))]
    public class WaterWalker : MonoBehaviour
    {
        [SerializeField] Material rippleMaterial;
        [SerializeField] Material sprayMaterial;
        [SerializeField] Color rippleColor = new(0.85f, 0.97f, 1f, 0.75f);
        [SerializeField] Color sprayColor = new(0.9f, 0.98f, 1f, 0.9f);

        PlayerController player;
        CharacterController body;
        ParticleSystem ripples, spray;
        Vector3 lastPosition;
        float nextRipple, airborneSince = -1f, fallSpeed;
        bool wasOnWater;

        public bool OnWater { get; private set; }

        void Awake()
        {
            player = GetComponent<PlayerController>();
            body = GetComponent<CharacterController>();
            ripples = MakeSystem("WaterRipples", rippleMaterial, true);
            spray = MakeSystem("WaterSpray", sprayMaterial, false);
            lastPosition = transform.position;
        }

        void LateUpdate()
        {
            Vector3 p = transform.position;
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector3 step = p - lastPosition;
            float speed = new Vector2(step.x, step.z).magnitude / dt;
            float vertical = step.y / dt;
            lastPosition = p;

            bool overWater = WaterSurface.Over(p, out float level);
            // under the surface (fell in before the floor was there): back on top
            if (overWater && p.y < level - 0.25f && !player.Health.IsDead)
            {
                player.SetBodyEnabled(false);
                transform.position = new Vector3(p.x, level + 0.05f, p.z);
                player.SetBodyEnabled(true);
                lastPosition = transform.position;
                p = transform.position;
            }

            bool grounded = body != null && body.isGrounded;
            OnWater = overWater && Mathf.Abs(p.y - level) < 0.35f && grounded;
            if (!grounded) { if (airborneSince < 0f) airborneSince = Time.time; fallSpeed = Mathf.Min(fallSpeed, vertical); }

            if (OnWater)
            {
                Vector3 feet = new(p.x, level + 0.03f, p.z);
                if (!wasOnWater && airborneSince >= 0f && Time.time - airborneSince > 0.25f)
                    Landing(feet, Mathf.Clamp01(-fallSpeed / 12f));
                if (Time.time >= nextRipple)
                {
                    float run = Mathf.Clamp01(speed / 8f);
                    Ripple(feet - step.normalized * 0.2f, Mathf.Lerp(1.6f, 3.2f, run), Mathf.Lerp(1.6f, 1.1f, run), speed > 0.3f ? 1f : 0.55f);
                    nextRipple = Time.time + (speed > 0.3f ? Mathf.Lerp(0.32f, 0.12f, run) : 1.1f);
                }
                if (speed > 3f) Spray(feet, step / dt, Mathf.CeilToInt(speed * 0.6f * dt * 10f));
            }
            if (grounded) { airborneSince = -1f; fallSpeed = 0f; }
            wasOnWater = OnWater;
        }

        void Ripple(Vector3 at, float size, float life, float strength)
        {
            if (ripples == null) return;
            Color c = rippleColor; c.a *= strength;
            ripples.Emit(new ParticleSystem.EmitParams
            {
                position = at, startSize = size, startLifetime = life, startColor = c, velocity = Vector3.zero,
                rotation = Random.Range(0f, 360f),
            }, 1);
        }

        void Spray(Vector3 at, Vector3 velocity, int count)
        {
            if (spray == null) return;
            Vector3 back = -new Vector3(velocity.x, 0f, velocity.z).normalized;
            for (int i = 0; i < count; i++)
            {
                Vector3 v = back * Random.Range(0.6f, 1.6f) + Vector3.up * Random.Range(1.2f, 2.6f) + Random.insideUnitSphere * 0.6f;
                spray.Emit(new ParticleSystem.EmitParams
                {
                    position = at + Random.insideUnitSphere * 0.25f, velocity = v, startSize = Random.Range(0.06f, 0.16f),
                    startLifetime = Random.Range(0.35f, 0.6f), startColor = sprayColor,
                }, 1);
            }
        }

        void Landing(Vector3 at, float force)
        {
            Ripple(at, Mathf.Lerp(3f, 6f, force), 1.4f, 1f);
            Ripple(at, Mathf.Lerp(1.5f, 3f, force), 1.0f, 0.8f);
            if (spray == null) return;
            int n = Mathf.RoundToInt(Mathf.Lerp(14f, 40f, force));
            for (int i = 0; i < n; i++)
            {
                Vector2 d = Random.insideUnitCircle.normalized;
                spray.Emit(new ParticleSystem.EmitParams
                {
                    position = at + new Vector3(d.x, 0f, d.y) * 0.3f,
                    velocity = new Vector3(d.x, 0f, d.y) * Random.Range(1f, 3f) + Vector3.up * Random.Range(2f, 4.5f) * (0.6f + force),
                    startSize = Random.Range(0.08f, 0.2f), startLifetime = Random.Range(0.5f, 0.9f), startColor = sprayColor,
                }, 1);
            }
        }

        // Ripples: flat rings on the water that grow and fade. Spray: droplets thrown up that fall back.
        ParticleSystem MakeSystem(string name, Material material, bool flat)
        {
            if (material == null) return null;
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = flat ? 64 : 400;
            main.gravityModifier = flat ? 0f : 1.6f;
            main.startSpeed = 0f;
            var emission = ps.emission; emission.enabled = false;
            var shape = ps.shape; shape.enabled = false;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = flat ? new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.25f, 0f, 2f), new Keyframe(1f, 1f, 0f, 0f)))
                             : new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.4f));
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         flat ? new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(0f, 1f) }
                              : new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.7f), new GradientAlphaKey(0f, 1f) });
            color.color = fade;

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = flat ? ParticleSystemRenderMode.HorizontalBillboard : ParticleSystemRenderMode.Billboard;
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortingFudge = flat ? 10f : 0f;
            ps.Play();
            return ps;
        }
    }
}
