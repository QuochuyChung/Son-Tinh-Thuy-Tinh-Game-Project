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
        [Tooltip("Seconds into the dodge (start, end) during which the player cannot be hit.")]
        public Vector2 invulnerableWindow = new(0.05f, 0.4f);
        [Tooltip("Fraction of the distance covered (0-1) over normalized time (0-1). Fast start, slow finish.")]
        public AnimationCurve displacement = new(new Keyframe(0f, 0f, 2.2f, 2.2f), new Keyframe(1f, 1f, 0f, 0f));
    }
}
