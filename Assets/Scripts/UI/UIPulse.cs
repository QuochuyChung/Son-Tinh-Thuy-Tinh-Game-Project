using UnityEngine;

namespace SonTinhThuyTinh.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class UIPulse : MonoBehaviour
    {
        [SerializeField] float speed = 4f;
        [SerializeField] float minAlpha = 0.25f;

        CanvasGroup group;

        void Awake() => group = GetComponent<CanvasGroup>();

        void Update() => group.alpha = Mathf.Lerp(minAlpha, 1f, (Mathf.Sin(Time.unscaledTime * speed) + 1f) * 0.5f);
    }
}
