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

        [Header("Portrait (optional)")]
        [Tooltip("Flame emblem drawn behind the boss's head, like the player's HUD.")]
        [SerializeField] Sprite emblemSprite;
        [SerializeField] float portraitSize = 170f;

        Health health;
        float shown = 1f, trailShown = 1f, trailHold, targetAlpha;

        public void Bind(Health target, string displayName, Sprite portrait = null)
        {
            health = target;
            if (nameLabel != null) nameLabel.text = displayName;
            ShowPortrait(portrait);
            shown = trailShown = health.Normalized;
            SetPhase2(false);
            Apply();
        }

        // The boss's head on a fire-bird emblem at the left end of the bar (the same look as the player's, bottom-left of the screen).
        void ShowPortrait(Sprite portrait)
        {
            Transform old = transform.Find("BossPortrait");
            if (old != null) Destroy(old.gameObject);
            if (portrait == null) return;

            var root = new GameObject("BossPortrait", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            root.transform.SetAsFirstSibling();
            var rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = rootRect.anchorMax = new Vector2(0f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.sizeDelta = Vector2.one * portraitSize;
            rootRect.anchoredPosition = new Vector2(-portraitSize * 0.35f, -portraitSize * 0.28f);

            if (emblemSprite != null) Picture(rootRect, "Emblem", emblemSprite, portraitSize * 1.5f);
            Picture(rootRect, "Face", portrait, portraitSize);
        }

        static void Picture(RectTransform parent, string name, Sprite sprite, float size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.one * size;
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
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
