using UnityEngine;

namespace SonTinhThuyTinh.Environment
{
    // Makes a torch or fire light flicker a little. Intensity wanders around the value set in the Light.
    [RequireComponent(typeof(Light))]
    public class LightFlicker : MonoBehaviour
    {
        [Range(0f, 1f)]
        [SerializeField] float amount = 0.25f;
        [SerializeField] float speed = 7f;

        Light target;
        float baseIntensity;
        float seed;

        void Awake()
        {
            target = GetComponent<Light>();
            baseIntensity = target.intensity;
            seed = Random.value * 100f;
        }

        void Update()
        {
            float noise = Mathf.PerlinNoise(Time.time * speed, seed);
            target.intensity = baseIntensity * (1f - amount + amount * 2f * noise);
        }
    }
}
