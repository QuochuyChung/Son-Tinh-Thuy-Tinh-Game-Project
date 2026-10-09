using System;
using SonTinhThuyTinh.CameraSystem;
using SonTinhThuyTinh.Dialogue;
using SonTinhThuyTinh.Player;
using UnityEngine;

namespace SonTinhThuyTinh.Quest
{
    // Plays a conversation in the middle of gameplay: the game is frozen (like the pause menu) so Space / click advance the text without
    // making the character jump or attack, and the presses are dropped from the input buffer when the game resumes.
    public static class QuestDialogue
    {
        public static bool IsPlaying { get; private set; }
        // E both advances the text and opens a conversation: the press that ends one must not open the next one
        public static float EndedAt { get; private set; } = -10f;

        public static void Play(DialogueRunner runner, DialogueSequence sequence, Action finished = null)
        {
            if (runner == null || sequence == null || IsPlaying) { finished?.Invoke(); return; }
            IsPlaying = true;
            Time.timeScale = 0f;
            var cameraInput = UnityEngine.Object.FindAnyObjectByType<ThirdPersonCameraInput>();
            if (cameraInput != null) cameraInput.enabled = false;

            runner.Play(sequence, () =>
            {
                Time.timeScale = 1f;
                if (cameraInput != null) cameraInput.enabled = true;
                var input = UnityEngine.Object.FindAnyObjectByType<PlayerInputReader>();
                if (input != null) input.ClearBuffered();
                IsPlaying = false;
                EndedAt = Time.unscaledTime;
                finished?.Invoke();
            });
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() { IsPlaying = false; EndedAt = -10f; }
    }
}
