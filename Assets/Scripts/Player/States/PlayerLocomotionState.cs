using SonTinhThuyTinh.Audio;
using SonTinhThuyTinh.Combat.Environment;
using SonTinhThuyTinh.Combat.Skills;
using SonTinhThuyTinh.Core;
using UnityEngine;

namespace SonTinhThuyTinh.Player.States
{
    public sealed class PlayerLocomotionState : IState
    {
        static readonly int LocomotionHash = Animator.StringToHash("Locomotion");

        readonly PlayerController player;

        public PlayerLocomotionState(PlayerController player) => this.player = player;

        public void Enter() => player.Animator.CrossFadeInFixedTime(LocomotionHash, 0.15f);

        public void Tick(float deltaTime)
        {
            SkillSlot skill = player.InputReader.ConsumeSkill();
            if (skill != SkillSlot.None && player.Skills != null && player.Skills.TryCast(skill))
            {
                if (skill == SkillSlot.Ult && EnvironmentDirector.Instance != null)
                {
                    EnvironmentDirector.Instance.RegisterUlt();
                    BattleAudio.Instance?.PlayUltStinger(EnvironmentDirector.Instance.PlayerFaction);
                }
                player.ChangeState(player.CastState);
                return;
            }

            if (player.InputReader.ConsumeDodge() && player.Stamina.TryConsume(player.Dodge.staminaCost))
            {
                player.ChangeState(player.DodgeState);
                return;
            }

            player.Locomote(player.InputReader.Move, deltaTime);
        }

        public void Exit() { }
    }
}
