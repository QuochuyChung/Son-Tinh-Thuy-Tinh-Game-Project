using UnityEngine;

namespace SonTinhThuyTinh.Combat
{
    // One attack: which animator state to play and everything that happens during it. Times are in seconds of real time from the start
    // of the attack (already divided by the animation speed). Made by Assets/Editor/CombatSetupBuilder.cs from the measured clips.
    [CreateAssetMenu(menuName = "Son Tinh Thuy Tinh/Attack", fileName = "Attack")]
    public class AttackData : ScriptableObject
    {
        [Header("Animation")]
        [Tooltip("State in AC_Humanoid_Base that plays the clip.")]
        public string stateName;
        [Tooltip("Playback speed of the clip (the AnimSpeed parameter).")]
        public float animSpeed = 1f;
        [Tooltip("How long the attack lasts before the character is free again.")]
        public float duration = 1f;

        [Header("Timing")]
        public float hitStart = 0.3f;
        public float hitEnd = 0.5f;
        [Tooltip("From this moment the next light attack, a dodge or a spell can cancel the rest of the animation.")]
        public float comboStart = 0.6f;

        [Header("Cost and damage")]
        public float staminaCost = 8f;
        public float damage = 10f;
        public bool heavy;
        [Tooltip("Not interrupted by getting hit.")]
        public bool superArmor;

        [Header("Hit volume (local to the character: x right, y up, z forward)")]
        public Vector3 hitCenter = new(0f, 1f, 1.4f);
        public Vector3 hitSize = new(2.4f, 2f, 2.4f);
        [Tooltip("Hit everything within a sphere of radius Hit Size X around the character instead (the spin).")]
        public bool hitAround;

        [Header("Effect on the target")]
        public float knockback = 3f;
        public float knockUp;

        [Header("Travel")]
        [Tooltip("The character is carried this far forward between Lunge Start and Lunge End (the clips are played in place).")]
        public float lungeDistance;
        public float lungeStart;
        public float lungeEnd;

        [Header("Feedback")]
        public float hitStop = 0.05f;
        public float shake = 0.25f;
        [Tooltip("Optional effect spawned in front of the character at Swing Effect Time (the fan of water of the spin).")]
        public GameObject swingEffect;
        public float swingEffectTime;
        public float swingEffectScale = 1f;
    }
}
