using UnityEngine;

namespace SonTinhThuyTinh.Environment
{
    // Moves RenderSettings.fogDensity between a calm value and a storm value over time, so
    // fog can close in on a scene without a script hard-coding the numbers every frame.
    //
    // This writes a global, so it is deliberately opt-in: only one controller should live
    // in a scene, and it will fight SeaLampBeacon (which also drives fogDensity during the
    // sea quest) if both are ever loaded at once.
    public class FogController : MonoBehaviour
    {
        [Tooltip("Density when the scene is calm. Read from RenderSettings on Awake when left at 0, so the authored value is the starting point.")]
        [SerializeField] float calmDensity;

        [Tooltip("Density at the peak of a swell.")]
        [SerializeField] float peakDensity = 0.006f;

        [Tooltip("Seconds for one full calm -> peak -> calm cycle.")]
        [SerializeField] float period = 45f;

        [Tooltip("How much of the swell is used. 0 pins the fog at the calm value, 1 lets it reach peak.")]
        [Range(0f, 1f)]
        [SerializeField] float amount = 1f;

        [Tooltip("Drop this to hand the fog back to whatever else is driving it (a quest, another controller).")]
        [SerializeField] bool animate = true;

        float seed;

        void Awake()
        {
            if (calmDensity <= 0f) calmDensity = RenderSettings.fogDensity;
            seed = Random.value * 100f;
            RenderSettings.fog = true;
        }

        void Update()
        {
            if (!animate || period <= 0f) return;

            // Perlin rather than a sine so successive swells never line up into a pulse
            // the player can time. Only the X axis advances; the seed picks a lane.
            float t = Mathf.PerlinNoise(seed, Time.time / period);
            RenderSettings.fogDensity = Mathf.Lerp(calmDensity, peakDensity, t * amount);
        }

        public void SetAmount(float value)
        {
            amount = Mathf.Clamp01(value);
        }

        public void SetAnimating(bool value)
        {
            animate = value;
        }
    }
}
