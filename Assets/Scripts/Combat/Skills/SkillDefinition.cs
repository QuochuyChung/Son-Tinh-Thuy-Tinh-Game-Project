using UnityEngine;

namespace SonTinhThuyTinh.Combat.Skills
{
    public enum SkillSlot
    {
        None = -1,
        E1 = 0,
        E2 = 1,
        Ult = 2
    }

    [CreateAssetMenu(menuName = "SonTinhThuyTinh/Skill Definition", fileName = "Skill_")]
    public sealed class SkillDefinition : ScriptableObject
    {
        [Tooltip("Stable key used by events/UI (e.g. \"e1\", \"ult\").")]
        public string id = "";

        [Tooltip("Which input slot this skill answers to.")]
        public SkillSlot type = SkillSlot.None;

        [Tooltip("Seconds before this skill can be used again.")]
        [Min(0f)] public float cooldown = 8f;

        [Tooltip("Seconds the character stays locked in the Cast state.")]
        [Min(0f)] public float castDuration = 0.6f;

        [Min(0f)] public float damageMultiplier = 1f;

        [Tooltip("VFX/geometry root spawned on cast — assigned once skill content lands (C6/C7).")]
        public GameObject prefab;
    }
}
