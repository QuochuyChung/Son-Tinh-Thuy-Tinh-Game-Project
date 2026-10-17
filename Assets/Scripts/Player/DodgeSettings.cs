using System;
using UnityEngine;

namespace SonTinhThuyTinh.Player
{
    [Serializable]
    public class DodgeSettings
    {
        public float distance = 4f;
        public float duration = 0.6f;
        public float staminaCost = 25f;
        [Tooltip("Playback speed of the dodge clip (so a long roll clip fits the dodge duration).")]
        public float animationSpeed = 1f;
        [Tooltip("Seconds into the dodge (start, end) during which the player cannot be hit.")]
        public Vector2 invulnerableWindow = new(0.05f, 0.4f);
        [Tooltip("Fraction of the distance covered (0-1) over normalized time (0-1). Fast start, slow finish.")]
        public AnimationCurve displacement = new(new Keyframe(0f, 0f, 2.2f, 2.2f), new Keyframe(1f, 1f, 0f, 0f));

        [Header("Cancel-out (recovering early)")]
        [Tooltip("Normalized time (0-1) from which the roll can be cut short by the next action. The invulnerable part always plays out; " +
                 "this only gives back the recovery. 0 = always cancellable, 1 = never (the old full-commit roll).")]
        // Normalised point in the roll at which the next action may cut it short. The roll runs 0.75s, so 0.867 leaves only the last
        // 0.1s of recovery skippable - enough to chain out of a roll without letting it be cancelled almost on contact.
        [Range(0f, 1f)] public float cancelFrom = 0.867f;

        [Header("After-images")]
        [Tooltip("Metres travelled between fading copies of the character left behind by the roll. 0 = no copies.")]
        [Min(0f)] public float afterImageSpacing = 2.2f;
    }
}
