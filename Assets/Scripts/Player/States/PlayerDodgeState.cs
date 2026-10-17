using SonTinhThuyTinh.Combat;
using SonTinhThuyTinh.Core;
using UnityEngine;

namespace SonTinhThuyTinh.Player.States
{
    // The dodge roll: turns to the way the stick points, covers its distance on a fast-start curve, and is untouchable for the middle of
    // it. Past the safe part the roll can be cut short by the next action (see TryCancel), so recovery is the player's to skip rather
    // than a wait. While the roll is untouchable it also leaves a trail of fading copies behind it (see EmitGhosts).
    public sealed class PlayerDodgeState : IState
    {
        static readonly int DodgeHash = Animator.StringToHash("Dodge");

        readonly PlayerController player;
        Vector3 direction;
        float elapsed;
        float coveredDistance;
        float sinceGhost;

        public PlayerDodgeState(PlayerController player) => this.player = player;

        public void Enter()
        {
            elapsed = 0f;
            coveredDistance = 0f;
            sinceGhost = 0f;

            Vector3 inputDirection = player.CameraRelative(player.InputReader.Move);
            direction = inputDirection.sqrMagnitude > 0.01f ? inputDirection.normalized : player.transform.forward;
            player.FaceTowards(direction, 360f);
            player.SetAnimSpeed(player.Dodge.animationSpeed);
            player.Animator.CrossFadeInFixedTime(DodgeHash, 0.05f);
        }

        public void Tick(float deltaTime)
        {
            DodgeSettings settings = player.Dodge;
            elapsed += deltaTime;

            float t = Mathf.Clamp01(elapsed / settings.duration);
            float targetDistance = settings.displacement.Evaluate(t) * settings.distance;
            float step = targetDistance - coveredDistance;
            player.Move(direction * step, deltaTime);
            coveredDistance = targetDistance;

            player.IsInvulnerable = WindowIsLive(settings);

            EmitGhosts(settings, step);

            if (elapsed >= settings.duration)
            {
                player.ChangeState(player.LocomotionState);
                return;
            }

            // Recovery is the player's to skip: from cancelFrom on, the next action cuts the roll short instead of waiting it out. The
            // invulnerable part still plays out in full, so this hands back time without handing back safety.
            if (t >= settings.cancelFrom && TryCancel()) return;
        }

        public void Exit()
        {
            player.IsInvulnerable = false;
            player.SetAnimSpeed(1f);
            player.MatchSpeedToInput();
        }

        // Cutting the roll short into whatever comes next. Ordered the same way the attack state's combo window is (PlayerAttackState
        // .TryCancel): spells, then heavy, then light, then another dodge, so a button pressed here behaves the same as one pressed a
        // moment later out of an attack.
        bool TryCancel()
        {
            PlayerInputReader input = player.InputReader;

            if (input.ConsumeJump() && player.IsGrounded)
            {
                // Same choice as leaving the ground out of a run: sprinting into the roll makes it a running jump.
                player.ChangeState(player.IsSprinting ? player.RunJumpState : player.JumpUpState);
                return true;
            }

            if (!player.HasCombat)
            {
                if (input.ConsumeDodge() && player.Stamina.TryConsume(player.Dodge.staminaCost))
                {
                    player.ChangeState(player.DodgeState);   // a second roll chains straight on
                    return true;
                }
                return false;
            }

            MoveSet set = player.MoveSet;
            for (int i = 0; i < 3; i++)
                if (input.ConsumeSpell(i) && player.TryCast(i)) return true;
            if (input.ConsumeHeavy() && player.TryAttack(set.heavy, -1)) return true;
            if (set.lightCombo != null && set.lightCombo.Length > 0 && input.ConsumeLight() && player.TryAttack(set.lightCombo[0], 0)) return true;
            if (input.ConsumeDodge() && player.Stamina.TryConsume(player.Dodge.staminaCost))
            {
                player.ChangeState(player.DodgeState);
                return true;
            }
            return false;
        }

        // True while the roll's untouchable window is running. Kept as a separate read so the trail and the invulnerability can never
        // disagree about which part of the roll is safe.
        bool WindowIsLive(DodgeSettings settings) =>
            elapsed >= settings.invulnerableWindow.x && elapsed <= settings.invulnerableWindow.y;

        // A fading copy every afterImageSpacing metres actually travelled, so the trail follows the roll whatever the frame rate is.
        // Only emitted while the roll is untouchable: the trail marks the part of the dodge that is actually a dodge, and stays quiet
        // through the vulnerable recovery instead of trailing the whole roll.
        void EmitGhosts(DodgeSettings settings, float step)
        {
            if (settings.afterImageSpacing <= 0f || player.AfterImages == null) return;
            if (!WindowIsLive(settings)) return;

            sinceGhost += step;
            while (sinceGhost >= settings.afterImageSpacing)
            {
                sinceGhost -= settings.afterImageSpacing;
                player.AfterImages.Emit();
            }
        }
    }
}
