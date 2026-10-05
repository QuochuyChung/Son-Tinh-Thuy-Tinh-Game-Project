using System;
using UnityEngine;

namespace SonTinhThuyTinh.Player
{
    // Tuning for the two jumps: the standing "jump up" (Space) and the running jump (Space while sprinting).
    // The numbers match the Mixamo clips (docs/progress.md 9.11): JumpUp crouches for ~0.5 s, leaves the ground and ends at the top of
    // the jump; RunJump leaves the ground at once, peaks at ~0.35 s and lands at ~0.65 s.
    [Serializable]
    public class JumpSettings
    {
        [Tooltip("Height (m) of a jump started from a stand or a normal run. Together with gravity this sets the air time: 0.9 m = 0.6 s.")]
        public float standingHeight = 0.9f;
        [Tooltip("Height (m) of a jump started while sprinting. 1 m = 0.63 s of air, about the length of the running-jump clip.")]
        public float runningHeight = 1f;
        [Tooltip("Where (seconds) the standing jump clip starts, skipping the slow first part of its crouch so the jump answers quickly.")]
        public float standingClipOffset = 0.2f;
        [Tooltip("Seconds between pressing Space and leaving the ground for the standing jump (the rest of the crouch, until the clip's take-off at 0.5 s).")]
        public float standingTakeoffDelay = 0.3f;
        [Tooltip("Fraction of the current speed a standing jump keeps (0 = straight up).")]
        [Range(0f, 1f)] public float standingMomentum = 0.6f;
        [Tooltip("Top horizontal speed (m/s) a standing jump can be steered to.")]
        public float standingAirSpeed = 4.5f;
        [Tooltip("How fast (m/s^2) the horizontal velocity follows the stick in the air.")]
        public float airAcceleration = 10f;
        [Tooltip("How fast (m/s^2) the horizontal velocity bleeds off when the stick is released in the air.")]
        public float airDrag = 2.5f;
        [Tooltip("Ground contact right after take-off is ignored for this long (seconds).")]
        public float minAirTime = 0.15f;
        [Tooltip("Seconds the clip fades in when the jump starts.")]
        public float blendIn = 0.08f;
    }
}
