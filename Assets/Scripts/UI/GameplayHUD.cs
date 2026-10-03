using SonTinhThuyTinh.Characters;
using SonTinhThuyTinh.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SonTinhThuyTinh.UI
{
    // The HUD players actually see: portrait, name, health + stamina bars. DebugHUD (text dump) stays separate for dev testing.
    // Layout is built by Assets/Editor/GameplayHudBuilder.cs (reference: docs/reference/ui_hud_ref.webp).
    public class GameplayHUD : MonoBehaviour
    {
        [SerializeField] Image healthFill;
        [SerializeField] Image staminaFill;
        [Tooltip("Optional. Sits behind the health fill and drains slowly after a hit, so the lost chunk stays visible for a moment.")]
        [SerializeField] Image healthTrail;
        [Tooltip("Tints the health fill as it drops, e.g. green -> orange -> red. The fill is a white-to-grey gradient, so this colour is its whole hue.")]
        [SerializeField] Gradient healthColor;
        [SerializeField] float fillSmoothing = 10f;
        [Tooltip("Seconds the trail waits after a hit before it starts draining.")]
        [SerializeField] float trailDelay = 0.45f;
        [SerializeField] float trailDrainPerSecond = 0.35f;

        [Header("Character (optional)")]
        [SerializeField] Image portrait;
        [SerializeField] TMP_Text[] nameLabels;

        PlayerController player;
        float displayedHealth = 1f;
        float displayedStamina = 1f;
        float displayedTrail = 1f;
        float trailHold;

        public void Bind(PlayerController target, CharacterDefinition character = null)
        {
            player = target;
            displayedHealth = displayedTrail = player.Health.Normalized;
            displayedStamina = player.Stamina.Current / player.Stamina.Max;

            if (character != null)
            {
                if (portrait != null)
                {
                    portrait.sprite = character.Portrait;
                    portrait.enabled = character.Portrait != null;
                }
                foreach (TMP_Text label in nameLabels)
                    if (label != null) label.text = character.DisplayName;
            }

            Apply();
        }

        void Update()
        {
            if (player == null) return;

            float targetHealth = player.Health.Normalized;
            float targetStamina = player.Stamina.Current / player.Stamina.Max;
            float dt = Time.unscaledDeltaTime;
            float t = 1f - Mathf.Exp(-fillSmoothing * dt);

            // Health eases toward its target so a big hit reads as a drain, not a snap; stamina follows exactly (it already ticks smoothly).
            displayedHealth = Mathf.Lerp(displayedHealth, targetHealth, t);
            displayedStamina = targetStamina;

            if (targetHealth >= displayedTrail)
            {
                displayedTrail = targetHealth;
                trailHold = trailDelay;
            }
            else if ((trailHold -= dt) <= 0f)
            {
                displayedTrail = Mathf.Max(targetHealth, displayedTrail - trailDrainPerSecond * dt);
            }

            Apply();
        }

        void Apply()
        {
            healthFill.fillAmount = displayedHealth;
            healthFill.color = healthColor.Evaluate(displayedHealth);
            staminaFill.fillAmount = displayedStamina;
            if (healthTrail != null) healthTrail.fillAmount = displayedTrail;
        }
    }
}
