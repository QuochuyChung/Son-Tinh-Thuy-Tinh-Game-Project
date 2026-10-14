using UnityEngine;

namespace SonTinhThuyTinh.Environment
{
    // Low-lying mist. Same trailing behaviour as RainController, but the layer keeps its
    // own height and only the opacity is dialled, because mist is read as a change in
    // contrast rather than a change in coverage: fading the particles in place reads as
    // fog thickening, while spawning more of them reads as new fog arriving.
    //
    // Unlike RainController this one can breathe on its own (driftIn), which is what gives
    // a still scene a slow pulse of depth without any quest code driving it.
    public class MistController : MonoBehaviour
    {
        [Tooltip("What the mist trails; the main camera when left empty.")]
        [SerializeField] Transform follow;

        [Tooltip("Seconds it takes to close most of the gap to the follow target.")]
        [SerializeField] float followSharpness = 6f;

        [Range(0f, 1f)]
        [Tooltip("Base opacity of the layer, before any breathing.")]
        [SerializeField] float opacity = 1f;

        [Tooltip("Wander amplitude and speed for the slow in/out breathing. 0 = steady.")]
        [Range(0f, 1f)]
        [SerializeField] float driftIn = 0.25f;

        [SerializeField] float driftSpeed = 0.08f;

        [Tooltip("Emitters to drive. Filled from this hierarchy when left empty.")]
        [SerializeField] ParticleSystem[] emitters;

        float seed;
        Vector3 smoothedFollow;

        // Rate each emitter was authored at. Intensity scales from this rather than writing
        // an absolute value - see RainController, rateOverTimeMultiplier shares storage with
        // rateOverTime here, so assigning it would replace the authored rate outright.
        float[] baseRates;

        void Awake()
        {
            if (follow == null && Camera.main != null) follow = Camera.main.transform;
            if (emitters == null || emitters.Length == 0) emitters = GetComponentsInChildren<ParticleSystem>(true);
            if (emitters != null)
            {
                baseRates = new float[emitters.Length];
                for (int i = 0; i < emitters.Length; i++)
                    if (emitters[i] != null)
                        baseRates[i] = emitters[i].emission.rateOverTime.constant;
            }
            if (follow != null) smoothedFollow = follow.position;
            seed = Random.value * 100f;
            Apply(opacity);
        }

        void LateUpdate()
        {
            if (follow != null)
            {
                smoothedFollow = Vector3.Lerp(smoothedFollow, follow.position,
                    1f - Mathf.Exp(-followSharpness * Time.deltaTime));
                transform.position = new Vector3(smoothedFollow.x, transform.position.y, smoothedFollow.z);
            }

            if (driftIn <= 0f) return;

            // Perlin rather than a sine so the breathing never falls into a visible beat.
            float wander = Mathf.PerlinNoise(seed, Time.time * driftSpeed);
            Apply(Mathf.Clamp01(opacity * (1f - driftIn) + opacity * driftIn * 2f * wander));
        }

        void Apply(float value)
        {
            if (emitters == null || baseRates == null) return;

            for (int i = 0; i < emitters.Length; i++)
            {
                var ps = emitters[i];
                if (ps == null) continue;
                // EmissionModule is a struct wrapper: copy it to a local first, otherwise
                // the compiler refuses the chained assignment (CS1612).
                var emission = ps.emission;
                emission.rateOverTime = baseRates[i] * value;
            }
        }

        public void SetOpacity(float value)
        {
            opacity = Mathf.Clamp01(value);
            Apply(opacity);
        }
    }
}
