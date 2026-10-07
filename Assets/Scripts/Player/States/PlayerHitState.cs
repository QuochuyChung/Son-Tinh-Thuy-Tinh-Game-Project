using SonTinhThuyTinh.Combat;
using SonTinhThuyTinh.Core;
using UnityEngine;

namespace SonTinhThuyTinh.Player.States
{
    // Got hit: the Impact clip plays (faster for a flinch, slower for a stagger), the character is pushed back a little and cannot act or be
    // hit again until it is over. Started by PlayerController when Health.Damaged fires.
    public sealed class PlayerHitState : IState
    {
        static readonly int HitHash = Animator.StringToHash("Hit");

        readonly PlayerController player;
        bool heavy;
        Vector3 pushDirection;
        float elapsed;
        float duration;

        public PlayerHitState(PlayerController player) => this.player = player;

        // `from` is where the blow came from; the push goes away from it.
        public void Begin(bool stagger, Vector3 from)
        {
            heavy = stagger;
            pushDirection = player.transform.position - from;
            pushDirection.y = 0f;
            pushDirection = pushDirection.sqrMagnitude > 0.01f ? pushDirection.normalized : -player.transform.forward;
        }

        public void Enter()
        {
            MoveSet set = player.MoveSet;
            elapsed = 0f;
            duration = heavy ? set.hitHeavyDuration : set.hitLightDuration;
            player.SetSpeed(0f);
            player.SetAnimSpeed(heavy ? set.hitHeavySpeed : set.hitLightSpeed);
            player.SetTrail(false);
            player.IsInvulnerable = true;
            player.InputReader.ClearBuffered();
            player.Animator.CrossFadeInFixedTime(HitHash, 0.04f);
        }

        public void Tick(float deltaTime)
        {
            elapsed += deltaTime;
            float push = elapsed < 0.3f ? (heavy ? 5f : 2.5f) * (1f - elapsed / 0.3f) : 0f;
            player.Move(pushDirection * (push * deltaTime), deltaTime);
            if (elapsed >= duration) player.ChangeState(player.LocomotionState);
        }

        public void Exit()
        {
            player.IsInvulnerable = false;
            player.SetAnimSpeed(1f);
            player.InputReader.ClearBuffered();
        }
    }
}
