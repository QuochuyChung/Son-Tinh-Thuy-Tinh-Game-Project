using SonTinhThuyTinh.Combat;
using SonTinhThuyTinh.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SonTinhThuyTinh.DevTools
{
    public class DebugHUD : MonoBehaviour
    {
        [SerializeField] PlayerController player;
        [Tooltip("Damage dealt to the player when pressing K, for testing health and dodge i-frames.")]
        [SerializeField] float testDamage = 10f;

        GUIStyle style;

        public void Bind(PlayerController target) => player = target;

        void Update()
        {
            if (player == null || Keyboard.current == null) return;

            if (Keyboard.current.kKey.wasPressedThisFrame)
                player.Health.TakeDamage(new DamageInfo(testDamage, gameObject));
        }

        void OnGUI()
        {
            if (player == null) return;
            style ??= new GUIStyle(GUI.skin.label) { fontSize = 18 };

            var stamina = player.Stamina;
            var health = player.Health;
            string exhausted = stamina.IsExhausted ? " (KIỆT SỨC)" : "";
            string dead = health.IsDead ? " (CHẾT)" : "";

            GUILayout.BeginArea(new Rect(16, 16, 420, 210), GUI.skin.box);
            GUILayout.Label($"State: {player.CurrentStateName}", style);
            GUILayout.Label($"Speed: {player.CurrentSpeed:F2} m/s", style);
            GUILayout.Label($"Health: {health.Current:F0}/{health.Max:F0}{dead}", style);
            GUILayout.Label($"Stamina: {stamina.Current:F0}/{stamina.Max:F0}{exhausted}", style);
            GUILayout.Label($"I-frame: {(player.IsInvulnerable ? "ON" : "off")}", style);
            GUILayout.Label($"[K] trừ {testDamage:F0} máu để test", style);
            GUILayout.EndArea();
        }
    }
}
