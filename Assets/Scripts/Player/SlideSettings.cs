using System;
using UnityEngine;

namespace SonTinhThuyTinh.Player
{
    // Tuning for the slide (C while sprinting). Distance and timing are the Mixamo "Running Slide" clip's own: 1.53 s, 6.5 m.
    [Serializable]
    public class SlideSettings
    {
        public float distance = 6.5f;
        [Tooltip("Seconds the slide lasts. Match it to the Slide clip length of the characters (1.53 s).")]
        public float duration = 1.53f;
        public float staminaCost = 0f;
        [Tooltip("Seconds after a slide before the next one is allowed.")]
        public float cooldown = 0.5f;
        [Tooltip("Fraction of the run speed the character keeps when the slide ends (it then speeds up or stops with the stick).")]
        [Range(0f, 1f)] public float exitSpeedFraction = 0.5f;
        [Tooltip("Fraction of the distance covered (0-1) over normalized time (0-1). Measured from the clip: fast start, long glide at the end.")]
        public AnimationCurve displacement = DefaultCurve();

        static AnimationCurve DefaultCurve()
        {
            var curve = new AnimationCurve(
                new Keyframe(0f, 0f), new Keyframe(0.065f, 0.10f), new Keyframe(0.27f, 0.36f), new Keyframe(0.47f, 0.58f),
                new Keyframe(0.67f, 0.75f), new Keyframe(0.87f, 0.88f), new Keyframe(1f, 1f));
            for (int i = 0; i < curve.length; i++) curve.SmoothTangents(i, 0f);
            return curve;
        }
    }
}
