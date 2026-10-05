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
            PlayerInputReader input = player.InputReader;

            if (input.ConsumeDodge() && player.Stamina.TryConsume(player.Dodge.staminaCost))
            {
                player.ChangeState(player.DodgeState);
                return;
            }

            // C while sprinting: slide. (A press while not sprinting is thrown away.)
            if (input.ConsumeSlide() && player.CanSlide && (player.Slide.staminaCost <= 0f || player.Stamina.TryConsume(player.Slide.staminaCost)))
            {
                player.ChangeState(player.SlideState);
                return;
            }

            // Space: jump up; Space while sprinting: running jump.
            if (input.ConsumeJump() && player.IsGrounded)
            {
                player.ChangeState(player.IsSprinting ? player.RunJumpState : player.JumpUpState);
                return;
            }

            player.Locomote(input.Move, deltaTime);
        }

        public void Exit() { }
    }
}
