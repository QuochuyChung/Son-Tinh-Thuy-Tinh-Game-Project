using SonTinhThuyTinh.Combat;
using SonTinhThuyTinh.Core;
using UnityEngine;

namespace SonTinhThuyTinh.Player.States
{
    public sealed class PlayerChannelSpellState : IState
    {
        public const float MaximumDuration = 3.75f;
        const float SlashCycleDuration = 0.69f;
        const float SlashImpactTime = 0.38f;
        const float FinalFollowThrough = 0.25f;
        static readonly int LeftSlashHash = Animator.StringToHash("SpellWaveLeft");
        static readonly int RightSlashHash = Animator.StringToHash("SpellWaveRight");

        readonly PlayerController player;
        SpellData spell;
        int slot;
        float elapsed;
        float swingStartedAt;
        int swingsStarted;
        int currentAnimationHash;
        bool impactFired;
        WaterTowerEffect effect;
        VanThuyTramPhase phase = VanThuyTramPhase.Idle;
        bool cancelRequested;
        bool cleaned;

        public VanThuyTramPhase Phase => phase;

        public PlayerChannelSpellState(PlayerController player) => this.player = player;

        public void Begin(SpellData data, int spellSlot)
        {
            spell = data;
            slot = spellSlot;
        }

        public void Enter()
        {
            elapsed = 0f;
            cancelRequested = false;
            cleaned = false;
            swingStartedAt = 0f;
            swingsStarted = 1;
            impactFired = false;
            phase = VanThuyTramPhase.Idle;
            player.SetSpeed(0f);
            player.StopUpperBodyAttack();
            player.SetTrail(false);
            player.InputReader.ClearBuffered();
            player.SetAnimSpeed(spell.animSpeed);
            currentAnimationHash = LeftSlashHash;
            player.Animator.CrossFadeInFixedTime(currentAnimationHash, 0.08f, 0);
            player.Health.Damaged += OnCasterDamaged;
            effect = ThuyTinhWaterSpells.BeginTower(player, spell);
            SetPhase(VanThuyTramPhase.Slashing);
        }

        public void Tick(float deltaTime)
        {
            if (ShouldCancel())
            {
                SetPhase(VanThuyTramPhase.Cancelled);
                player.ChangeState(player.Health.IsDead ? player.DeathState : player.LocomotionState);
                return;
            }

            elapsed += deltaTime;
            KeepCastingAnimationAlive();
            player.FaceInput(720f * deltaTime);
            player.Move(Vector3.zero, deltaTime);

            if (!impactFired && elapsed >= swingStartedAt + SlashImpactTime)
            {
                effect?.FireNextSlash();
                impactFired = true;
            }

            if (impactFired && swingsStarted < ThuyTinhWaterSpells.MaximumWaterSlashes && elapsed >= swingStartedAt + SlashCycleDuration)
            {
                swingStartedAt += SlashCycleDuration;
                swingsStarted++;
                impactFired = false;
                currentAnimationHash = swingsStarted % 2 == 1 ? LeftSlashHash : RightSlashHash;
                player.Animator.CrossFadeInFixedTime(currentAnimationHash, 0.06f, 0, 0f);
            }

            bool finishedFollowThrough = effect != null && effect.Finished &&
                                         elapsed >= swingStartedAt + SlashImpactTime + FinalFollowThrough;
            if (finishedFollowThrough || elapsed >= MaximumDuration)
            {
                SetPhase(VanThuyTramPhase.Finishing);
                player.ChangeState(player.LocomotionState);
            }
        }

        public void Exit() => Cleanup();

        public void RequestCancel()
        {
            cancelRequested = true;
            effect?.StopChannel(false);
            if (phase != VanThuyTramPhase.Idle && phase != VanThuyTramPhase.Cooldown)
                SetPhase(VanThuyTramPhase.Cancelled);
        }

        public void ForceCleanup()
        {
            cancelRequested = true;
            SetPhase(VanThuyTramPhase.Cancelled);
            Cleanup();
        }

        bool ShouldCancel()
        {
            return cancelRequested || player == null || player.Health == null || player.Health.IsDead ||
                   !player.InputReader.HasGameplayFocus || !player.InputReader.SpellHeld(slot);
        }

        void OnCasterDamaged(DamageInfo _) => RequestCancel();

        void KeepCastingAnimationAlive()
        {
            AnimatorStateInfo info = player.Animator.GetCurrentAnimatorStateInfo(0);
            if (info.shortNameHash == currentAnimationHash && info.normalizedTime >= 1f)
                player.Animator.Play(currentAnimationHash, 0, info.normalizedTime % 1f);
        }

        void SetPhase(VanThuyTramPhase next)
        {
            if (phase == next) return;
            phase = next;
            if (next == VanThuyTramPhase.Slashing)
                player.Animator.Play(currentAnimationHash, 0, 0f);
            effect?.SetPhase(next);
        }

        void Cleanup()
        {
            if (cleaned) return;
            cleaned = true;
            if (player != null && player.Health != null)
                player.Health.Damaged -= OnCasterDamaged;
            bool completed = effect != null && effect.Finished;
            if (!completed) phase = VanThuyTramPhase.Cancelled;
            effect?.StopChannel(completed);
            effect = null;
            player.SetAnimSpeed(1f);
            player.StopUpperBodyAttack();
            player.SetTrail(false);
            player.InputReader.ClearBuffered();
            player.StartSpellCooldown(slot, spell != null ? spell.cooldown : 0f);
            phase = VanThuyTramPhase.Cooldown;
        }
    }
}
