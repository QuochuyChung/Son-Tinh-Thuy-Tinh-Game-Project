using SonTinhThuyTinh.Core;
using UnityEngine;

namespace SonTinhThuyTinh.Player.States
{
    public sealed class PlayerLocomotionState : IState
    {
        static readonly int LocomotionHash = Animator.StringToHash("Locomotion");

        readonly PlayerController player;

        public PlayerLocomotionState(PlayerController player) => this.player = player;

        public void Enter()
        {
            player.SetAnimSpeed(1f);
            player.Animator.CrossFadeInFixedTime(LocomotionHash, 0.15f);
        }

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

            // Space: jump up; Space while sprinting: running jump. Next to a ledge it climbs instead (characters that can).
            if (input.ConsumeJump() && player.IsGrounded)
            {
                if (player.TryClimb()) return;
                player.ChangeState(player.IsSprinting ? player.RunJumpState : player.JumpUpState);
                return;
            }

            if (TryFight(input)) return;

            player.Locomote(input.Move, deltaTime);
        }

        // Attacks and spells, for characters that have a move set. Without one the presses are simply thrown away.
        bool TryFight(PlayerInputReader input)
        {
            bool light = input.ConsumeLight(), heavy = input.ConsumeHeavy();
            bool[] spell = { input.ConsumeSpell(0), input.ConsumeSpell(1), input.ConsumeSpell(2) };
            if (!player.HasCombat) return false;

            for (int i = 0; i < spell.Length; i++)
                if (spell[i] && player.TryCast(i)) return true;
            if (heavy && player.TryAttack(player.MoveSet.heavy, -1)) return true;
            if (light && player.MoveSet.lightCombo != null && player.MoveSet.lightCombo.Length > 0 && player.TryAttack(player.MoveSet.lightCombo[0], 0)) return true;
            return false;
        }

        public void Exit() { }
    }
}
