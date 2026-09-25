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
