using SonTinhThuyTinh.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace SonTinhThuyTinh.UI
{
    // A small health bar floating over an enemy's head (the Ninja): a world-space canvas that always faces the camera, with a fill that
    // eases down after each hit and a pale trail that follows a moment later. Hidden until Show(true) (the fight starts) and after death.
    public class WorldHealthBar : MonoBehaviour
    {
        [SerializeField] Health health;
        [SerializeField] CanvasGroup group;
        [SerializeField] Image fill;
        [SerializeField] Image trail;
        [SerializeField] float smoothing = 12f;
        [SerializeField] float trailDelay = 0.4f;
        [SerializeField] float trailDrainPerSecond = 0.5f;

        float shown = 1f, trailShown = 1f, trailHold;
        bool visible;

        public void Show(bool on)
        {
            visible = on;
            if (on && health != null) shown = trailShown = health.Normalized;
        }

        void Awake()
        {
            if (group != null) group.alpha = 0f;
        }

        void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam != null) transform.rotation = cam.transform.rotation;
            float dt = Time.deltaTime;
            if (group != null) group.alpha = Mathf.MoveTowards(group.alpha, visible && health != null && !health.IsDead ? 1f : 0f, 4f * dt);
            if (health == null) return;

            float target = health.Normalized;
            shown = Mathf.Lerp(shown, target, 1f - Mathf.Exp(-smoothing * dt));
            if (target >= trailShown) { trailShown = target; trailHold = trailDelay; }
            else if ((trailHold -= dt) <= 0f) trailShown = Mathf.Max(target, trailShown - trailDrainPerSecond * dt);
            if (fill != null) fill.fillAmount = shown;
            if (trail != null) trail.fillAmount = trailShown;
        }
    }
}
