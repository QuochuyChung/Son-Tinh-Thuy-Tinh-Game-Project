using System.Collections.Generic;
using SonTinhThuyTinh.Combat;
using SonTinhThuyTinh.Core;
using UnityEngine;

namespace SonTinhThuyTinh.Player.States
{
    // One attack or spell, described by an AttackData / SpellData: plays its animator state, carries the character forward, hurts what its hit
    // volume touches during the hit window (each target once), spawns the swing / spell effect and lets the next light attack, a dodge or a
    // spell cancel the end of the animation once the combo window is open. Start it with PlayerController.TryAttack / TryCast.
    public sealed class PlayerAttackState : IState
    {
        readonly PlayerController player;
        readonly HashSet<Health> alreadyHit = new();
        AttackData data;
        int comboIndex;
        float elapsed;
        bool swingSpawned;
        bool effectSpawned;
        bool upperBodyAttack;
        bool queuedLight;

        public PlayerAttackState(PlayerController player) => this.player = player;

        public bool CanBeInterrupted => data == null || !data.superArmor;

        // comboIndex: position in the light combo, or -1 for anything that is not part of it.
        public void Begin(AttackData attack, int index)
        {
            data = attack;
            comboIndex = index;
        }

        public void Enter()
        {
            elapsed = 0f;
            swingSpawned = false;
            effectSpawned = false;
            queuedLight = false;
            alreadyHit.Clear();

            player.FaceAim(360f);   // swing at the locked enemy, else the way the stick points
            player.SetAnimSpeed(data.animSpeed);
            upperBodyAttack = data is ProjectileAttackData && player.PlayUpperBodyAttack(data.stateName);
            if (!upperBodyAttack)
            {
                player.SetSpeed(0f);
                player.Animator.CrossFadeInFixedTime(Animator.StringToHash(data.stateName), 0.06f);
            }
            if (data.stateName == "JumpAttack") player.CancelVertical(1.5f);
        }

        public void Tick(float deltaTime)
        {
            elapsed += deltaTime;

            if (upperBodyAttack)
            {
                player.Locomote(player.InputReader.Move, deltaTime);
                if (player.InputReader.ConsumeLight()) queuedLight = true;
            }
            else
            {
                Vector3 travel = Vector3.zero;
                if (data.lungeDistance > 0f && elapsed >= data.lungeStart && elapsed < data.lungeEnd)
                    travel = player.transform.forward * (data.lungeDistance * deltaTime / Mathf.Max(data.lungeEnd - data.lungeStart, 0.01f));
                player.Move(travel, deltaTime);
            }

            player.SetTrail(elapsed >= data.hitStart - 0.12f && elapsed <= data.hitEnd + 0.1f);

            if (!swingSpawned && data.swingEffect != null && elapsed >= data.swingEffectTime)
            {
                swingSpawned = true;
                Transform t = player.transform;
                GameObject effect = Object.Instantiate(data.swingEffect, t.position + t.forward * 0.4f, t.rotation);
                effect.transform.localScale *= data.swingEffectScale;
                Object.Destroy(effect, 3f);
            }

            if (!effectSpawned && data is SpellData spell && elapsed >= spell.effectTime)
            {
                effectSpawned = true;
                SpellEffects.Cast(player, spell);
            }

            if (!effectSpawned && data is ProjectileAttackData projectile && elapsed >= projectile.projectileTime)
            {
                effectSpawned = true;
                if (projectile.projectilePrefab != null)
                {
                    GameObject orb = Object.Instantiate(projectile.projectilePrefab);
                    orb.GetComponent<WaterOrbProjectile>()?.Init(player, projectile);
                }
            }

            if (data is not ProjectileAttackData && data.damage > 0f && elapsed >= data.hitStart && elapsed <= data.hitEnd) Sweep();

            if (elapsed >= data.comboStart && TryCancel()) return;
            if (elapsed >= data.duration) player.ChangeState(player.LocomotionState);
        }

        public void Exit()
        {
            player.SetTrail(false);
            player.SetAnimSpeed(1f);
            if (upperBodyAttack) player.StopUpperBodyAttack();
        }

        // The combo window: next light attack in the chain, the heavy attack as a finisher, a spell, or a dodge.
        bool TryCancel()
        {
            PlayerInputReader input = player.InputReader;
            MoveSet set = player.MoveSet;

            bool lightRequested = queuedLight || input.ConsumeLight();
            queuedLight = false;
            if (comboIndex >= 0 && set.lightCombo != null && set.lightCombo.Length > 0 && lightRequested)
            {
                int nextIndex = comboIndex + 1;
                if (nextIndex < set.lightCombo.Length && player.TryAttack(set.lightCombo[nextIndex], nextIndex)) return true;
            }
            if (input.ConsumeHeavy() && player.TryAttack(set.heavy, -1)) return true;
            for (int i = 0; i < 3; i++)
                if (input.ConsumeSpell(i) && player.TryCast(i)) return true;
            if (input.ConsumeDodge() && player.Stamina.TryConsume(player.Dodge.staminaCost))
            {
                player.ChangeState(player.DodgeState);
                return true;
            }
            return false;
        }

        // Everything with Health inside the hit volume gets hurt once per attack.
        void Sweep()
        {
            Transform t = player.transform;
            Collider[] found = data.hitAround
                ? Physics.OverlapSphere(t.position + Vector3.up * data.hitCenter.y, data.hitSize.x, ~0, QueryTriggerInteraction.Ignore)
                : Physics.OverlapBox(t.TransformPoint(data.hitCenter), data.hitSize * 0.5f, t.rotation, ~0, QueryTriggerInteraction.Ignore);

            foreach (Collider c in found)
            {
                if (c.transform.IsChildOf(t)) continue;
                Health target = c.GetComponentInParent<Health>();
                if (target == null || target == player.Health || alreadyHit.Contains(target)) continue;
                // Sơn Tinh's rock wall between the two: the blow does not reach
                if (RockWallBarrier.Between(t.position + Vector3.up * 1.1f, target.transform.position + Vector3.up * 1.1f, player)) continue;
                alreadyHit.Add(target);

                Vector3 point = c.ClosestPoint(t.position + Vector3.up);
                Vector3 away = target.transform.position - t.position;
                away.y = 0f;
                away = away.sqrMagnitude > 0.01f ? away.normalized : t.forward;

                var info = new DamageInfo(data.damage, player.gameObject, point, data.heavy, isNormalAttack: true);
                if (!target.TakeDamage(info)) continue;

                target.GetComponentInParent<IHitReceiver>()?.OnHit(info, away * data.knockback + Vector3.up * data.knockUp);
                if (player.MoveSet.hitSplash != null) Object.Destroy(Object.Instantiate(player.MoveSet.hitSplash, point, Quaternion.LookRotation(-away)), 2f);
                player.HitStop(data.hitStop);
                player.Shake(data.shake);
            }
        }
    }
}
