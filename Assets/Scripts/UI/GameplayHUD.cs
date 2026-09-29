using SonTinhThuyTinh.Player;
using UnityEngine;
using UnityEngine.UI;

namespace SonTinhThuyTinh.UI
{
    // The HUD players actually see: health + stamina bars. DebugHUD (text dump) stays separate for dev testing.
    public class GameplayHUD : MonoBehaviour
    {
        [SerializeField] Image healthFill;
        [SerializeField] Image staminaFill;
        [Tooltip("Tints the health bar as it drops, e.g. green -> red.")]
        [SerializeField] Gradient healthColor;
        [SerializeField] float fillSmoothing = 10f;

        PlayerController player;
        float displayedHealth = 1f;
        float displayedStamina = 1f;

        public void Bind(PlayerController target)
        {
            player = target;
            displayedHealth = player.Health.Normalized;
            displayedStamina = player.Stamina.Current / player.Stamina.Max;
            Apply();
        }

        void Update()
        {
            if (player == null) return;

            float targetHealth = player.Health.Normalized;
            float targetStamina = player.Stamina.Current / player.Stamina.Max;
            float t = 1f - Mathf.Exp(-fillSmoothing * Time.unscaledDeltaTime);

            // Health eases toward its target so a big hit reads as a drain, not a snap; stamina follows exactly (it already ticks smoothly).
            displayedHealth = Mathf.Lerp(displayedHealth, targetHealth, t);
            displayedStamina = targetStamina;
            Apply();
        }

        void Apply()
        {
            healthFill.fillAmount = displayedHealth;
            healthFill.color = healthColor.Evaluate(displayedHealth);
            staminaFill.fillAmount = displayedStamina;
        }
    }
}
