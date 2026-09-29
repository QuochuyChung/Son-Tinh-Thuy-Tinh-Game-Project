using UnityEngine;

namespace SonTinhThuyTinh.Combat.Skills
{
    public class SkillCaster : MonoBehaviour
    {
        [SerializeField] SkillDefinition e1;
        [SerializeField] SkillDefinition e2;
        [SerializeField] SkillDefinition ult;

        readonly float[] readyAt = new float[3];

        public SkillDefinition LastCast { get; private set; }
        public bool IgnoreCooldowns { get; set; }

        public SkillDefinition Get(SkillSlot slot) => slot switch
        {
            SkillSlot.E1 => e1,
            SkillSlot.E2 => e2,
            SkillSlot.Ult => ult,
            _ => null
        };

        public bool TryCast(SkillSlot slot)
        {
            if (!SkillGate.IsOpen || slot == SkillSlot.None) return false;

            SkillDefinition skill = Get(slot);
            if (skill == null) return false;
            if (!IgnoreCooldowns && Time.time < readyAt[(int)slot]) return false;

            if (skill.cooldown > 0f) readyAt[(int)slot] = Time.time + skill.cooldown;
            LastCast = skill;
            return true;
        }

        public void Execute(SkillDefinition skill, Transform caster)
        {
            if (skill == null || skill.prefab == null || caster == null) return;
            GameObject instance = Object.Instantiate(skill.prefab);
            SkillEffect effect = instance.GetComponent<SkillEffect>();
            if (effect != null) effect.Play(new SkillContext(caster, skill));
            else Object.Destroy(instance);
        }

        public float CooldownRemaining(SkillSlot slot)
        {
            if (slot == SkillSlot.None || IgnoreCooldowns) return 0f;
            return Mathf.Max(0f, readyAt[(int)slot] - Time.time);
        }
    }
}
