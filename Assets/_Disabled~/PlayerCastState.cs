using SonTinhThuyTinh.Combat.Skills;
using SonTinhThuyTinh.Core;

namespace SonTinhThuyTinh.Player.States
{
    public sealed class PlayerCastState : IState
    {
        const float FallbackDuration = 0.6f;

        readonly PlayerController player;
        float elapsed;
        float duration;

        public PlayerCastState(PlayerController player) => this.player = player;

        public void Enter()
        {
            elapsed = 0f;
            SkillDefinition skill = player.Skills != null ? player.Skills.LastCast : null;
            duration = skill != null ? skill.castDuration : FallbackDuration;
            player.HaltLocomotion();
            if (skill != null && player.Skills != null) player.Skills.Execute(skill, player.transform);
        }

        public void Tick(float deltaTime)
        {
            elapsed += deltaTime;
            if (elapsed >= duration) player.ChangeState(player.LocomotionState);
        }

        public void Exit() { }
    }
}
