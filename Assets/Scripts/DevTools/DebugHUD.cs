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

            GUILayout.BeginArea(new Rect(16, 16, 420, 150), GUI.skin.box);
            GUILayout.Label($"State: {player.CurrentStateName}", style);
            GUILayout.Label($"Speed: {player.CurrentSpeed:F2} m/s", style);
            GUILayout.Label($"Stamina: {stamina.Current:F0}/{stamina.Max:F0}{exhausted}", style);
            GUILayout.Label($"I-frame: {(player.IsInvulnerable ? "ON" : "off")}", style);
            GUILayout.EndArea();
        }
    }
}
