using UnityEngine;

namespace SonTinhThuyTinh.Combat.Skills
{
    public readonly struct SkillContext
    {
        public readonly Transform Caster;
        public readonly SkillDefinition Skill;

        public SkillContext(Transform caster, SkillDefinition skill)
        {
            Caster = caster;
            Skill = skill;
        }
    }

    public abstract class SkillEffect : MonoBehaviour
    {
        public abstract void Play(in SkillContext context);
    }
}
