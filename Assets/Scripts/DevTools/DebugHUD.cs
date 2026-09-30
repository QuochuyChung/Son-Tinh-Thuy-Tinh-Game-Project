using SonTinhThuyTinh.Combat.Environment;
using SonTinhThuyTinh.Combat.Skills;
using SonTinhThuyTinh.Player;
using UnityEngine;

namespace SonTinhThuyTinh.DevTools
{
    public class DebugHUD : MonoBehaviour
    {
        [SerializeField] PlayerController player;

        GUIStyle style;

        void OnGUI()
        {
            if (player == null) return;
            style ??= new GUIStyle(GUI.skin.label) { fontSize = 18 };

            var stamina = player.Stamina;
            string exhausted = stamina.IsExhausted ? " (KIỆT SỨC)" : "";

            GUILayout.BeginArea(new Rect(16, 16, 460, 300), GUI.skin.box);
            GUILayout.Label($"State: {player.CurrentStateName}", style);
            GUILayout.Label($"Speed: {player.CurrentSpeed:F2} m/s", style);
            GUILayout.Label($"Stamina: {stamina.Current:F0}/{stamina.Max:F0}{exhausted}", style);
            GUILayout.Label($"I-frame: {(player.IsInvulnerable ? "ON" : "off")}", style);

            var env = EnvironmentDirector.Instance;
            if (env != null)
            {
                string side = env.Level > 0 ? "Đất" : env.Level < 0 ? "Nước" : "Trung lập";
                int chainCount = env.Chain != null ? env.Chain.Count : 0;
                GUILayout.Label(
                    $"Env: {env.Level:+0;-0;0} ({side})  chuỗi {chainCount}/{ComboChainTracker.HitsPerStep}  " +
                    $"speed ×{env.GetSpeedMultiplier(player.Faction):F2}  stam ×{env.GetStaminaRegenMultiplier(player.Faction):F2}",
                    style);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("-1 bậc", GUILayout.Height(26))) env.AdjustLevel(-1);
                if (GUILayout.Button("+1 bậc", GUILayout.Height(26))) env.AdjustLevel(1);
                GUILayout.EndHorizontal();
            }

            var skills = player.Skills;
            if (skills != null)
            {
                GUILayout.Label(
                    $"CD: E {skills.CooldownRemaining(SkillSlot.E1):F1}s  " +
                    $"R {skills.CooldownRemaining(SkillSlot.E2):F1}s  " +
                    $"F {skills.CooldownRemaining(SkillSlot.Ult):F1}s",
                    style);
                skills.IgnoreCooldowns = GUILayout.Toggle(skills.IgnoreCooldowns, "Bơm CD (bỏ cooldown)", style);
            }
            GUILayout.EndArea();
        }
    }
}
