using SonTinhThuyTinh.Core;
using UnityEngine;

namespace SonTinhThuyTinh.Player.States
{
    public sealed class PlayerDodgeState : IState
    {
        static readonly int DodgeHash = Animator.StringToHash("Dodge");

        readonly PlayerController player;
        Vector3 direction;
        float elapsed;
        float coveredDistance;

        public PlayerDodgeState(PlayerController player) => this.player = player;

        public void Enter()
        {
            elapsed = 0f;
            coveredDistance = 0f;

            Vector3 inputDirection = player.CameraRelative(player.InputReader.Move);
            direction = inputDirection.sqrMagnitude > 0.01f ? inputDirection.normalized : player.transform.forward;
            player.FaceTowards(direction, 360f);
            player.Animator.CrossFadeInFixedTime(DodgeHash, 0.05f);
        }

        public void Tick(float deltaTime)
        {
            DodgeSettings settings = player.Dodge;
            elapsed += deltaTime;

            float t = Mathf.Clamp01(elapsed / settings.duration);
            float targetDistance = settings.displacement.Evaluate(t) * settings.distance;
            player.Move(direction * (targetDistance - coveredDistance), deltaTime);
            coveredDistance = targetDistance;

            player.IsInvulnerable = elapsed >= settings.invulnerableWindow.x && elapsed <= settings.invulnerableWindow.y;

            if (elapsed >= settings.duration)
                player.ChangeState(player.LocomotionState);
        }

        public void Exit()
        {
            player.IsInvulnerable = false;
            player.MatchSpeedToInput();
        }
    }
}
