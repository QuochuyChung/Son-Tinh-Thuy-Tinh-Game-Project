using UnityEngine;

namespace SonTinhThuyTinh.Environment
{
    // Ambient rain. The emitters live on one root that trails the camera on XZ, so a
    // fixed, modest particle budget always lands in front of the player instead of the
    // rain being left behind the moment they run forward.
    //
    // Intensity is a single 0..1 dial rather than a per-system rate: quest and battle
    // code can fade a downpour in or out (FinalBattleArenaManager has a "rain intensifies"
    // phase) without having to know how many ParticleSystems a rain prefab happens to hold.
    public class RainController : MonoBehaviour
    {
        [Tooltip("What the rain trails; the main camera when left empty.")]
        [SerializeField] Transform follow;

        [Tooltip("Seconds it takes to close most of the gap to the follow target. Higher snaps, lower glides.")]
        [SerializeField] float followSharpness = 14f;

        [Tooltip("Extra metres the emitter is pushed forward along the camera's view, so the rain is not all behind you.")]
        [SerializeField] float lookahead = 4f;

        [Range(0f, 2f)]
        [Tooltip("Scales every emitter's rate. 0 = dry, 1 = the density the prefab was authored at, above 1 = a heavier fall (FinalBattle's storm).")]
        [SerializeField] float intensity = 1f;

        [Tooltip("Emitters to drive. Filled from this hierarchy when left empty.")]
        [SerializeField] ParticleSystem[] emitters;

        [Tooltip("Emitters whose rate stays at its authored value, so raising intensity only\n" +
                 "thickens the falling rain. Ground sprites sit right under the camera and are the\n" +
                 "most expensive thing here to overdraw, so their density is held constant by\n" +
                 "design rather than being tied to the storm.")]
        [SerializeField] ParticleSystem[] fixedRateEmitters;

        // Smoothed copy of the follow point; easing this instead of the root keeps the
        // curtain from popping when the player whips the camera around.
        Vector3 smoothedFollow;

        // Rate each emitter was authored at, captured once. Intensity scales from this
        // rather than writing an absolute value: in this Unity version
        // EmissionModule.rateOverTimeMultiplier shares storage with rateOverTime, so
        // assigning it REPLACES the authored rate instead of multiplying it - intensity 1
        // would silently turn a 3200/s downpour into 1 drop per second.
        float[] baseRates;

        void Awake()
        {
            if (follow == null && Camera.main != null) follow = Camera.main.transform;
            if (emitters == null || emitters.Length == 0) emitters = GetComponentsInChildren<ParticleSystem>(true);
            CaptureBaseRates();
            if (follow != null) smoothedFollow = follow.position;
            SetIntensity(intensity);
        }

        void CaptureBaseRates()
        {
            if (emitters == null) return;
            baseRates = new float[emitters.Length];
            for (int i = 0; i < emitters.Length; i++)
                if (emitters[i] != null)
                    baseRates[i] = emitters[i].emission.rateOverTime.constant;
        }

        void LateUpdate()
        {
            if (follow == null) return;

            // The root keeps its authored height (ground level); only XZ is driven, so the
            // splashes stay on the floor even when the camera tilts up at the sky.
            Vector3 forward = follow.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();

            Vector3 wanted = follow.position + forward * lookahead;
            smoothedFollow = Vector3.Lerp(smoothedFollow, wanted, 1f - Mathf.Exp(-followSharpness * Time.deltaTime));
            transform.position = new Vector3(smoothedFollow.x, transform.position.y, smoothedFollow.z);
        }

        public void SetIntensity(float value)
        {
            intensity = Mathf.Clamp(value, 0f, 2f);
            if (emitters == null || baseRates == null) return;

            for (int i = 0; i < emitters.Length; i++)
            {
                var ps = emitters[i];
                if (ps == null) continue;

                var emission = ps.emission;
                if (!IsFixedRate(ps)) emission.rateOverTime = baseRates[i] * intensity;

                // Stopping outright matters for the 0 case: an emission rate of 0 still
                // leaves the last second of drops hanging in the air.
                bool on = intensity > 0.001f;
                if (ps.isPlaying != on)
                {
                    if (on) ps.Play(true);
                    else ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }

        bool IsFixedRate(ParticleSystem ps)
        {
            if (fixedRateEmitters == null) return false;
            for (int i = 0; i < fixedRateEmitters.Length; i++)
                if (fixedRateEmitters[i] == ps) return true;
            return false;
        }

        public float Intensity => intensity;
    }
}
