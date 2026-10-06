using UnityEngine;

namespace SonTinhThuyTinh.Combat
{
    // Animates a 3D crescent slash arc: smoothly expands forward, sweeps rotation,
    // and fades out over a fraction of a second.
    public class SlashArcRunner : MonoBehaviour
    {
        public float duration = 0.22f;
        public Vector3 startScale = new Vector3(0.5f, 0.5f, 0.5f);
        public Vector3 endScale = new Vector3(1.2f, 1.2f, 1.2f);
        public float sweepAngle = 50f;
        public Renderer meshRenderer;

        float timer;
        Material materialInstance;
        Quaternion baseRot;

        void Awake()
        {
            baseRot = transform.localRotation;
            if (meshRenderer == null) meshRenderer = GetComponentInChildren<Renderer>();
            if (meshRenderer != null) materialInstance = meshRenderer.material;
            transform.localScale = startScale;
        }

        void Update()
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / duration);
            transform.localScale = Vector3.Lerp(startScale, endScale, Mathf.Sqrt(t));
            transform.localRotation = baseRot * Quaternion.Euler(0f, Mathf.Lerp(-sweepAngle * 0.5f, sweepAngle * 0.5f, t), 0f);

            if (materialInstance != null)
            {
                float alpha = Mathf.Sin(t * Mathf.PI);
                if (materialInstance.HasProperty("_BaseColor"))
                {
                    Color c = materialInstance.GetColor("_BaseColor");
                    c.a = alpha;
                    materialInstance.SetColor("_BaseColor", c);
                }
                else if (materialInstance.HasProperty("_TintColor"))
                {
                    Color c = materialInstance.GetColor("_TintColor");
                    c.a = alpha * 0.6f;
                    materialInstance.SetColor("_TintColor", c);
                }
            }

            if (t >= 1f) Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (materialInstance != null) Destroy(materialInstance);
        }
    }
}
