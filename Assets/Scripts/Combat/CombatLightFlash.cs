using UnityEngine;

namespace SonTinhThuyTinh.Combat
{
    // Flashes a point light when an attack or spell impact triggers and smoothly fades out.
    public class CombatLightFlash : MonoBehaviour
    {
        public Light targetLight;
        public float duration = 0.28f;
        public float peakIntensity = 4.5f;

        float timer;

        void Awake()
        {
            if (targetLight == null) targetLight = GetComponent<Light>();
            if (targetLight != null) targetLight.intensity = peakIntensity;
        }

        void Update()
        {
            if (targetLight == null) return;
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / duration);
            targetLight.intensity = Mathf.Lerp(peakIntensity, 0f, t * t);
            if (t >= 1f)
            {
                targetLight.enabled = false;
                Destroy(this);
            }
        }

        public static void Attach(GameObject root, Color color, float range = 8f, float intensity = 4.5f, float duration = 0.28f, Vector3 offset = default)
        {
            var lightGo = new GameObject("VFX_FlashLight");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = offset;
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            var flash = lightGo.AddComponent<CombatLightFlash>();
            flash.targetLight = l;
            flash.duration = duration;
            flash.peakIntensity = intensity;
        }
    }
}
