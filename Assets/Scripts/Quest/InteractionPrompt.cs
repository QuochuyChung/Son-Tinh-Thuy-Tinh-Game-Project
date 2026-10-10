using SonTinhThuyTinh.Dialogue;
using SonTinhThuyTinh.Flow;
using SonTinhThuyTinh.Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SonTinhThuyTinh.Quest
{
    // "Nhấn E để …" under the player: the nearest Interactable in range, used with E (keyboard) or D-pad up (gamepad).
    // Pressing E with nothing in range does nothing. Off while a dialogue runs, the game is paused, the map is open or a scene loads.
    public class InteractionPrompt : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;
        [SerializeField] TMP_Text label;
        [SerializeField] DialogueRunner dialogue;

        InputAction use;
        PlayerController player;
        Interactable current;

        void Awake()
        {
            use = new InputAction("Interact", InputActionType.Button);
            use.AddBinding("<Keyboard>/e");
            use.AddBinding("<Gamepad>/dpad/up");
            group.alpha = 0f;
        }

        void OnEnable() => use.Enable();
        void OnDisable() => use.Disable();
        void OnDestroy() => use.Dispose();

        // Time.timeScale 0 = the pause menu (or a conversation) has frozen the game
        bool Blocked => (dialogue != null && dialogue.IsPlaying) || QuestDialogue.IsPlaying || Time.timeScale == 0f
                        || Time.unscaledTime - QuestDialogue.EndedAt < 0.25f
                        || UI.Map.MapHUD.IsOpen || SceneLoader.IsLoading;

        void Update()
        {
            if (player == null) player = FindAnyObjectByType<PlayerController>();
            current = Blocked || player == null ? null : Nearest(player.transform.position);

            group.alpha = current != null ? 1f : 0f;
            if (current == null) return;
            label.text = current.Prompt;
            if (use.WasPressedThisFrame()) current.Interact();
        }

        static Interactable Nearest(Vector3 from)
        {
            Interactable best = null; float bestDistance = float.MaxValue;
            foreach (Interactable item in Interactable.All)
            {
                Vector3 d = item.transform.position - from; d.y = 0f;
                float distance = d.magnitude;
                if (distance <= item.Radius && distance < bestDistance) { best = item; bestDistance = distance; }
            }
            return best;
        }
    }
}
