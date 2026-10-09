using SonTinhThuyTinh.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SonTinhThuyTinh.UI
{
    // The boss bar at the top of the screen: name, a long health bar with a slow orange trail after each hit (like GameplayHUD), a
    // mark at half health where phase 2 starts, and two phase dots under the bar. Both dots are lit from the start; when the bar
    // depletes to the phase-2 threshold the first dot turns gray (color only — never hidden) and phase 2 begins.
    // Layout made by Assets/Editor/FinalBattleBossBarBuilder.cs; the boss calls Bind / Show / SetPhase2.
    public class BossHealthBar : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;
        [SerializeField] Image fill;
        [SerializeField] Image trail;
        [SerializeField] TMP_Text nameLabel;
        [SerializeField] TMP_Text phaseLabel;
        [Tooltip("Dot for phase 1. Turns gray (stays visible) once phase 2 starts.")]
        [SerializeField] Image phaseDot1;
        [Tooltip("Dot for phase 2. Stays lit through phase 2.")]
        [SerializeField] Image phaseDot2;
        [SerializeField] Color dotLitColor = new(0.95f, 0.85f, 0.35f);
        [SerializeField] Color dotDoneColor = new(0.35f, 0.35f, 0.35f);
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
            // Phase 1 done: its dot dims but stays on screen; phase 2's dot stays lit.
            if (phaseDot1 != null) phaseDot1.color = on ? dotDoneColor : dotLitColor;
            if (phaseDot2 != null) phaseDot2.color = dotLitColor;
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
