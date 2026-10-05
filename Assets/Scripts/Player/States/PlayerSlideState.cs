using SonTinhThuyTinh.Core;
using UnityEngine;

namespace SonTinhThuyTinh.Player.States
{
    // Slide along the facing direction (C while sprinting): a fast glide that eases out, no steering.
    public sealed class PlayerSlideState : IState
    {
        static readonly int SlideHash = Animator.StringToHash("Slide");

        readonly PlayerController player;
        Vector3 direction;
        float elapsed;
        float coveredDistance;

        public PlayerSlideState(PlayerController player) => this.player = player;

        public void Enter()
        {
            elapsed = 0f;
            coveredDistance = 0f;
            direction = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;
            player.Animator.CrossFadeInFixedTime(SlideHash, 0.08f);
        }

        public void Tick(float deltaTime)
        {
            SlideSettings settings = player.Slide;
            elapsed += deltaTime;

            float t = Mathf.Clamp01(elapsed / settings.duration);
            float targetDistance = settings.displacement.Evaluate(t) * settings.distance;
            player.Move(direction * (targetDistance - coveredDistance), deltaTime);
            coveredDistance = targetDistance;

            if (elapsed >= settings.duration)
                player.ChangeState(player.LocomotionState);
        }

        public void Exit()
        {
            player.SlideEnded();
            player.SetSpeed(Mathf.Min(player.RunSpeed * player.Slide.exitSpeedFraction, player.TopSpeedFor(player.InputReader.Move)));
            player.InputReader.ClearBuffered();
        }
    }
}
