using SonTinhThuyTinh.Core;
using SonTinhThuyTinh.Combat;
using UnityEngine;

namespace SonTinhThuyTinh.Player.States
{
    // Both jumps. `running` = started while sprinting (Space + Shift): keeps the sprint momentum, leaves the ground at once and plays the
    // running-jump clip. Otherwise the standing "jump up": a short crouch first, then up, with only part of the speed carried.
    // Height comes from JumpSettings, the arc from the player's gravity.
    public sealed class PlayerJumpState : IState
    {
        static readonly int JumpUpHash = Animator.StringToHash("JumpUp");
        static readonly int RunJumpHash = Animator.StringToHash("RunJump");
        static readonly int RightWaterSlashHash = Animator.StringToHash("SpellWaveRight");
        const float MaxAirTime = 4f;
        const float CrouchBrake = 12f;   // m/s^2, how fast the character stops during the crouch of a standing jump
        const float AerialSlashImpactTime = 0.38f;
        const float AerialSlashDuration = 0.72f;

        readonly PlayerController player;
        readonly bool running;
        Vector3 velocity;
        float elapsed;
        float takeoffAt;
        bool launched;
        bool aerialSlashActive;
        bool aerialSlashFired;
        float aerialSlashElapsed;

        bool UsesProjectileCombat => player.MoveSet != null &&
                                     player.MoveSet.lightCombo != null &&
                                     player.MoveSet.lightCombo.Length > 0 &&
                                     player.MoveSet.lightCombo[0] is ProjectileAttackData;

        public PlayerJumpState(PlayerController player, bool running)
        {
            this.player = player;
            this.running = running;
        }

        public void Enter()
        {
            JumpSettings settings = player.JumpSettings;
            elapsed = 0f;
            launched = false;
            aerialSlashActive = false;
            aerialSlashFired = false;
            aerialSlashElapsed = 0f;
            player.StopUpperBodyAttack();
            player.SetTrail(false);

            float speed = running ? Mathf.Max(player.CurrentSpeed, player.RunSpeed) : player.CurrentSpeed * settings.standingMomentum;
            velocity = player.transform.forward * speed;
            takeoffAt = running ? 0f : settings.standingTakeoffDelay;

            player.Animator.CrossFadeInFixedTime(running ? RunJumpHash : JumpUpHash, settings.blendIn, -1, running ? 0f : settings.standingClipOffset);
            if (takeoffAt <= 0f) Launch();
        }

        void Launch()
        {
            launched = true;
            takeoffAt = elapsed;
            player.Jump(running ? player.JumpSettings.runningHeight : player.JumpSettings.standingHeight);
        }

        public void Tick(float deltaTime)
        {
            JumpSettings settings = player.JumpSettings;
            elapsed += deltaTime;
            if (!launched && elapsed >= takeoffAt) Launch();

            if (!launched)
            {
                // still crouching on the ground: settle to a stop
                velocity = Vector3.MoveTowards(velocity, Vector3.zero, CrouchBrake * deltaTime);
                player.Move(velocity * deltaTime, deltaTime);
                return;
            }

            UpdateAerialWaterSlash(deltaTime);

            // Projectile characters keep their magic attacks in the air instead of switching to the legacy sword jump attack.
            bool light = player.InputReader.ConsumeLight();
            bool heavy = player.InputReader.ConsumeHeavy();
            if (player.HasCombat && (light || heavy))
            {
                if (heavy && UsesProjectileCombat && !aerialSlashActive)
                {
                    BeginAerialWaterSlash();
                }
                else if (!aerialSlashActive)
                {
                    AttackData aerialAttack = player.MoveSet.jumpAttack;
                    if (UsesProjectileCombat)
                        aerialAttack = player.MoveSet.lightCombo[0];
                    if (aerialAttack != null && player.TryAttack(aerialAttack, -1)) return;
                }
            }

            // steer with the stick; let go and the horizontal speed slowly bleeds off
            Vector3 wish = player.CameraRelative(player.InputReader.Move);
            if (wish.sqrMagnitude > 0.01f)
            {
                float top = running ? player.SprintSpeed : settings.standingAirSpeed;
                velocity = Vector3.MoveTowards(velocity, wish * top, settings.airAcceleration * deltaTime);
            }
            else
            {
                velocity = Vector3.MoveTowards(velocity, Vector3.zero, settings.airDrag * deltaTime);
            }

            if (velocity.sqrMagnitude > 0.25f) player.FaceTowards(velocity, 540f * deltaTime);
            player.Move(velocity * deltaTime, deltaTime);

            if ((elapsed - takeoffAt >= settings.minAirTime && player.IsGrounded) || elapsed > MaxAirTime)
                player.ChangeState(player.LocomotionState);
        }

        public void Exit()
        {
            aerialSlashActive = false;
            player.StopUpperBodyAttack();
            player.SetAnimSpeed(1f);
            // carry the speed into the run so landing a running jump does not stop the character dead
            player.SetSpeed(velocity.magnitude);
            player.InputReader.ClearBuffered();
        }

        void BeginAerialWaterSlash()
        {
            aerialSlashActive = true;
            aerialSlashFired = false;
            aerialSlashElapsed = 0f;
            player.FaceAim(540f);
            player.SetAnimSpeed(0.98f);
            player.Animator.CrossFadeInFixedTime(RightWaterSlashHash, 0.06f, 0, 0f);
        }

        void UpdateAerialWaterSlash(float deltaTime)
        {
            if (!aerialSlashActive) return;

            aerialSlashElapsed += deltaTime;
            if (!aerialSlashFired && aerialSlashElapsed >= AerialSlashImpactTime)
            {
                aerialSlashFired = true;
                SpellData slashSpell = player.MoveSet.spells != null && player.MoveSet.spells.Length > 2
                    ? player.MoveSet.spells[2]
                    : null;
                ThuyTinhWaterSpells.CastSingleWaterSlash(player, slashSpell, 1f);
            }

            if (aerialSlashElapsed < AerialSlashDuration) return;
            aerialSlashActive = false;
            player.SetAnimSpeed(1f);
            player.Animator.CrossFadeInFixedTime(running ? RunJumpHash : JumpUpHash, 0.08f, 0,
                running ? 0.35f : player.JumpSettings.standingClipOffset);
        }
    }
}
