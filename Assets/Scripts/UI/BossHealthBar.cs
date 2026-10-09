using SonTinhThuyTinh.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SonTinhThuyTinh.UI
{
    // The boss bar at the top of the screen: name, a long health bar with a slow orange trail after each hit (like GameplayHUD) and a
    // mark at half health where phase 2 starts. Layout made by Assets/Editor/SauChinDuoiBuilder.cs; the boss calls Bind / Show / SetPhase2.
    public class BossHealthBar : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;
        [SerializeField] Image fill;
        [SerializeField] Image trail;
        [SerializeField] TMP_Text nameLabel;
        [SerializeField] TMP_Text phaseLabel;
        [SerializeField] Color phase1Color = new(0.80f, 0.16f, 0.12f);
        [SerializeField] Color phase2Color = new(0.55f, 0.10f, 0.55f);
        [SerializeField] float fillSmoothing = 10f;
        [SerializeField] float trailDelay = 0.45f;
        [SerializeField] float trailDrainPerSecond = 0.3f;
        [SerializeField] float fadeSpeed = 3f;

        Health health;
        float shown = 1f, trailShown = 1f, trailHold, targetAlpha;

        public void Bind(Health target, string displayName)
        {
            health = target;
            if (nameLabel != null) nameLabel.text = displayName;
            shown = trailShown = health.Normalized;
            SetPhase2(false);
            Apply();
        }

        public void Show(bool on)
        {
            targetAlpha = on ? 1f : 0f;
            if (on && health != null) shown = trailShown = health.Normalized;
        }

        public void SetPhase2(bool on)
        {
            if (fill != null) fill.color = on ? phase2Color : phase1Color;
            if (phaseLabel != null) phaseLabel.text = on ? "Cuồng nộ" : "";
        }

        void Awake()
        {
            if (group != null) group.alpha = 0f;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (group != null) group.alpha = Mathf.MoveTowards(group.alpha, targetAlpha, fadeSpeed * dt);
            if (health == null) return;

            float target = health.Normalized;
            shown = Mathf.Lerp(shown, target, 1f - Mathf.Exp(-fillSmoothing * dt));
            if (target >= trailShown) { trailShown = target; trailHold = trailDelay; }
            else if ((trailHold -= dt) <= 0f) trailShown = Mathf.Max(target, trailShown - trailDrainPerSecond * dt);
            Apply();
        }

        void Apply()
        {
            if (fill != null) fill.fillAmount = shown;
            if (trail != null) trail.fillAmount = trailShown;
        }
    }
}
