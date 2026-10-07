using SonTinhThuyTinh.Core;
using UnityEngine;

namespace SonTinhThuyTinh.Player.States
{
    // Knocked down for good (health reached zero): plays the death clip once and stays on its last pose, controls are off.
    // PlayerController.Revive() brings the character back (used by the editor test keys until a real game-over flow exists).
    public sealed class PlayerDeathState : IState
    {
        static readonly int DeathHash = Animator.StringToHash("Death");

        readonly PlayerController player;

        public PlayerDeathState(PlayerController player) => this.player = player;

        public void Enter()
        {
            player.SetSpeed(0f);
            player.SetTrail(false);
            player.SetAnimSpeed(1f);
            player.InputReader.ClearBuffered();
            player.Animator.CrossFadeInFixedTime(DeathHash, 0.1f);
        }

        // Gravity only, so a character knocked down in mid-air still lands.
        public void Tick(float deltaTime) => player.Move(Vector3.zero, deltaTime);

        public void Exit() { }
    }
}
