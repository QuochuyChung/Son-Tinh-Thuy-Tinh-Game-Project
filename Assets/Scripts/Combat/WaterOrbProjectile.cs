using System.Collections.Generic;
using SonTinhThuyTinh.Player;
using UnityEngine;

namespace SonTinhThuyTinh.Combat
{
    public class WaterOrbProjectile : MonoBehaviour
    {
        ProjectileAttackData data;
        PlayerController caster;
        Vector3 startPosition;
        readonly HashSet<Health> hit = new();

        public void Init(PlayerController owner, ProjectileAttackData attack)
        {
            caster = owner;
            data = attack;

            Transform ownerTransform = owner.transform;
            transform.SetPositionAndRotation(
                ownerTransform.TransformPoint(attack.projectileSpawnOffset),
                ownerTransform.rotation);
            transform.localScale *= attack.projectileRadius;
            startPosition = transform.position;

            TintWaterVisuals();
            Destroy(gameObject, attack.projectileRange / Mathf.Max(attack.projectileSpeed, 0.01f) + 1f);
        }

        void Update()
        {
            if (data == null || caster == null) return;

            float distance = data.projectileSpeed * Time.deltaTime;
            transform.position += transform.forward * distance;

            Collider[] contacts = Physics.OverlapSphere(
                transform.position,
                data.projectileRadius,
                ~0,
                QueryTriggerInteraction.Ignore);

            foreach (Health target in SpellEffects.Targets(contacts, caster.Health))
            {
                if (!hit.Add(target)) continue;

                var info = new DamageInfo(data.damage, caster.gameObject, target.transform.position, data.heavy);
                if (!target.TakeDamage(info)) continue;

                Vector3 launch = transform.forward * data.knockback + Vector3.up * data.knockUp;
                target.GetComponentInParent<IHitReceiver>()?.OnHit(info, launch);
                caster.HitStop(data.hitStop);
                caster.Shake(data.shake);
                Destroy(gameObject);
                return;
            }

            if (Vector3.Distance(startPosition, transform.position) >= data.projectileRange)
                Destroy(gameObject);
        }

        void TintWaterVisuals()
        {
            // Keep all three basic attacks in the same water palette as Skill Z
            // and the environment waterfalls. The shared material avoids creating
            // a new material instance for every projectile.
            Color deepWater = new(0.03f, 0.30f, 0.46f, 1f);
            Color surfaceWater = new(0.62f, 0.84f, 0.97f, 1f);
            Material environmentWater = ThuyTinhWaterSpells.EnvironmentWaterMaterial;

            foreach (Renderer waterRenderer in GetComponentsInChildren<Renderer>(true))
            {
                waterRenderer.sharedMaterial = environmentWater;
                waterRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                waterRenderer.receiveShadows = false;
            }

            foreach (ParticleSystem particles in GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = particles.main;
                main.startColor = new ParticleSystem.MinMaxGradient(deepWater, surfaceWater);
            }

            foreach (Light glow in GetComponentsInChildren<Light>(true))
                glow.color = surfaceWater;
        }
    }
}
