using System.Collections;
using UnityEngine;

namespace SonTinhThuyTinh.Combat.Skills
{
    [RequireComponent(typeof(Renderer))]
    public sealed class AfterImageGhost : MonoBehaviour
    {
        [Tooltip("Bóng tồn tại bấy nhiêu giây trước khi mờ đi.")]
        [SerializeField, Min(0.05f)] float lifetime = 0.35f;
        [Tooltip("Bóng mờ dần trong bấy nhiêu giây cuối.")]
        [SerializeField, Min(0.05f)] float fadeSeconds = 0.25f;
        [Tooltip("Độ trong của bóng (0 = hết thấy, 1 = đặc).")]
        [SerializeField, Range(0f, 1f)] float peakAlpha = 0.4f;

        public float Lifetime
        {
            get => lifetime;
            set => lifetime = Mathf.Max(0.05f, value);
        }

        Material material;
        Color baseColor;

        void Awake()
        {
            Renderer body = GetComponent<Renderer>();
            material = body.material;
            baseColor = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : material.color;
        }

        void Start() => StartCoroutine(FadeRoutine());

        IEnumerator FadeRoutine()
        {
            float hold = lifetime - fadeSeconds;
            if (hold > 0f) yield return new WaitForSeconds(hold);

            float elapsed = 0f;
            while (elapsed < fadeSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fadeSeconds);
                SetAlpha(Mathf.Lerp(peakAlpha, 0f, t));
                yield return null;
            }
            Destroy(gameObject);
        }

        void SetAlpha(float alpha)
        {
            if (material == null) return;
            Color color = baseColor;
            color.a = alpha;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            else material.color = color;
        }

        void OnDestroy()
        {
            if (material != null) Destroy(material);
        }
    }
}
