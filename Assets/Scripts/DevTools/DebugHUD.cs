using SonTinhThuyTinh.Combat.Boss;
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
        PlayerHealth playerHealth;

        void OnGUI()
        {
            if (player == null) return;
            style ??= new GUIStyle(GUI.skin.label) { fontSize = 18 };

            var stamina = player.Stamina;
            string exhausted = stamina.IsExhausted ? " (KIỆT SỨC)" : "";

            GUILayout.BeginArea(new Rect(16, 16, 470, 470), GUI.skin.box);
            GUILayout.Label($"State: {player.CurrentStateName}", style);
            GUILayout.Label($"Speed: {player.CurrentSpeed:F2} m/s", style);
            GUILayout.Label($"Stamina: {stamina.Current:F0}/{stamina.Max:F0}{exhausted}", style);
            GUILayout.Label($"I-frame: {(player.IsInvulnerable ? "ON" : "off")}", style);

            var ult = UltMeter.Instance;
            if (ult != null)
            {
                string full = ult.IsFull ? "  (ĐẦY)" : "";
                GUILayout.Label($"Thần Lực: {ult.Value:F0}/{ult.Max:F0}{full}", style);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("+25 Thần Lực", GUILayout.Height(26))) ult.Add(25f);
                GUI.enabled = !ult.IsFull;
                if (GUILayout.Button("Bơm đầy", GUILayout.Height(26))) ult.Fill();
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }

            var dodgeDetector = PerfectDodgeDetector.Instance;
            if (dodgeDetector != null)
            {
                string window = dodgeDetector.IsInPerfectWindow ? "  [CỬA SỔ HOÀN HẢO]" : "";
                string timing = dodgeDetector.IsDodging ? dodgeDetector.DodgeElapsed.ToString("F2") + "s" : "off";
                GUILayout.Label($"Né: {timing}{window}", style);
            }

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

            var boss = BossHealth.Instance;
            if (boss != null)
            {
                string ex = boss.IsExhausted ? " (CẠN KIỆT)" : "";
                GUILayout.Label(
                    $"Boss: {boss.Hp:F0}/{boss.MaxHp:F0}  đoạn {boss.CurrentSegment}/{boss.Segments}{ex}",
                    style);
            }

            if (playerHealth == null) playerHealth = FindFirstObjectByType<PlayerHealth>();
            if (playerHealth != null)
                GUILayout.Label($"Player HP: {playerHealth.Hp:F0}/{playerHealth.MaxHp:F0}", style);

            var battle = BattleFlow.Instance;
            if (battle != null)
            {
                string timer = battle.ReclaimActive ? $"  đếm ngược {battle.ReclaimRemaining:F1}s" : "";
                string end = battle.EndReason.Length > 0 ? $"  [{battle.EndReason}]" : "";
                GUILayout.Label($"Battle: {battle.State}{timer}{end}", style);
                GUILayout.BeginHorizontal();
                GUI.enabled = boss != null;
                if (GUILayout.Button("Boss -25%", GUILayout.Height(26)))
                    boss.ApplyDamage(boss.MaxHp * 0.25f);
                GUI.enabled = playerHealth != null;
                if (GUILayout.Button("Player -20", GUILayout.Height(26)))
                    playerHealth.ApplyDamage(20f);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            GUILayout.EndArea();
        }
    }
}
