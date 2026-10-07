using System;
using UnityEngine;

namespace SonTinhThuyTinh.Player
{
    // Tuning for climbing onto a ledge (Space next to a wall or an object). The numbers come from the Mixamo "Climbing" clip cut to the part that
    // starts at the wall (docs/progress.md 9.13): it was made for a ledge 1.15 m high, with the body 0.32 m in front of the wall at the start;
    // the curves are what the clip does to the character's root (measured with ClimbMeasureTest) and are scaled to the real ledge.
    [Serializable]
    public class ClimbSettings
    {
        [Tooltip("Off = this character cannot climb (only Thuy Tinh has the climb clip so far).")]
        public bool enabled;
        [Tooltip("Lowest ledge (m above the feet) that is climbed instead of jumped.")]
        public float minHeight = 0.7f;
        [Tooltip("Highest ledge (m) that can be climbed. The clip is stretched up to this, so it looks best near its own 1.15 m.")]
        public float maxHeight = 1.9f;
        [Tooltip("How far beyond the body (m) a wall is looked for when Space is pressed.")]
        public float reach = 0.55f;
        [Tooltip("Height (m) of the ledge the clip was animated for.")]
        public float clipHeight = 1.15f;
        [Tooltip("Where the wall is in the clip: the body starts this far (m) in front of the wall.")]
        public float wallDistance = 0.32f;
        [Tooltip("Length (s) of the clip at normal speed.")]
        public float duration = 2.6f;
        [Tooltip("Playback speed of the clip (2.6 s / 1.15 = 2.3 s).")]
        public float speed = 1.15f;
        [Tooltip("Seconds to step in to the wall and turn to face it at the start.")]
        public float approachTime = 0.2f;
        [Tooltip("Seconds the clip fades in, and the character's pose settles to the hanging height.")]
        public float blendIn = 0.1f;
        [Tooltip("Seconds the stand-up fades back into the normal idle at the top.")]
        public float blendOut = 0.25f;
        [Tooltip("Extra distance (m) onto the ledge so the whole body ends up on it, not on its edge.")]
        public float landingExtra = 0.12f;
        [Tooltip("Seconds after a climb before the next one.")]
        public float cooldown = 0.4f;

        [Tooltip("Metres the body rises over normalized time (0-1 of the clip): hangs on the wall first, then pulls up over the edge.")]
        public AnimationCurve rise = Curve(Rise);
        [Tooltip("Metres the body moves forward over normalized time (0-1 of the clip), counted from where it started.")]
        public AnimationCurve forward = Curve(Forward);

        // seconds into the clip, then rise and forward (m) of the root, every ~0.1 s
        static readonly float[] Times = { 0f, 0.10f, 0.21f, 0.31f, 0.41f, 0.52f, 0.62f, 0.73f, 0.83f, 0.93f, 1.03f, 1.13f, 1.23f, 1.33f, 1.44f, 1.54f, 1.64f, 1.74f, 1.84f, 1.95f, 2.05f, 2.15f, 2.26f, 2.36f, 2.46f, 2.56f, 2.60f };
        static readonly float[] Rise = { 0f, 0.10f, 0.22f, 0.27f, 0.34f, 0.47f, 0.64f, 0.69f, 0.68f, 0.68f, 0.68f, 0.69f, 0.73f, 0.80f, 0.88f, 1.00f, 1.15f, 1.20f, 1.21f, 1.20f, 1.17f, 1.15f, 1.15f, 1.16f, 1.15f, 1.14f, 1.14f };
        static readonly float[] Forward = { 0f, 0.09f, 0.15f, 0.18f, 0.18f, 0.18f, 0.19f, 0.19f, 0.19f, 0.20f, 0.23f, 0.27f, 0.30f, 0.32f, 0.33f, 0.34f, 0.37f, 0.43f, 0.48f, 0.51f, 0.54f, 0.56f, 0.59f, 0.61f, 0.65f, 0.68f, 0.69f };

        static AnimationCurve Curve(float[] values)
        {
            var curve = new AnimationCurve();
            for (int i = 0; i < Times.Length; i++) curve.AddKey(new Keyframe(Times[i] / 2.6f, values[i]));
            for (int i = 0; i < curve.length; i++) curve.SmoothTangents(i, 0f);
            return curve;
        }
    }
}
