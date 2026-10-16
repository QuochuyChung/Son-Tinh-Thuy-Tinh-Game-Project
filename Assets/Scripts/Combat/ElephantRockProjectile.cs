using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace SonTinhThuyTinh.Combat
{
    // Procedural projectile for Voi Chín Ngà's trunk throw. It first rises from
    // the ground to the trunk, waits there, then follows a deterministic arc to
    // the telegraphed point so the attack remains fair and dodgeable.
    [DisallowMultipleComponent]
    public sealed class ElephantRockProjectile : MonoBehaviour
    {
        static Material rockMaterial;
        static Material trailMaterial;

        Transform owner;
        Vector3 target;
        Vector3 carryOffset;
        Vector3 launchPosition;
        float scoopDuration;
        float flightDuration;
        float arcHeight;
        float elapsed;
        Action<Vector3> onImpact;
        bool launched;
        bool finished;

        public static GameObject Create(Transform thrower, Vector3 impactTarget,
            Vector3 localCarryOffset, float scoopTime, float travelTime,
            float height, Action<Vector3> impactCallback)
        {
            GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.name = "VoiChinNga_TrunkRock_Runtime";
            Collider primitiveCollider = rock.GetComponent<Collider>();
            if (primitiveCollider != null) Destroy(primitiveCollider);

            rock.transform.localScale = new Vector3(1.35f, 1.05f, 1.2f) * 0.15f;
            rock.transform.rotation = Quaternion.Euler(18f, 31f, 11f);

            MeshRenderer renderer = rock.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = GetRockMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;

            TrailRenderer trail = rock.AddComponent<TrailRenderer>();
            trail.time = 0.22f;
            trail.startWidth = 0.38f;
            trail.endWidth = 0f;
            trail.minVertexDistance = 0.08f;
            trail.startColor = new Color(0.95f, 0.48f, 0.12f, 0.7f);
            trail.endColor = new Color(0.25f, 0.1f, 0.03f, 0f);
            trail.sharedMaterial = GetTrailMaterial();
            trail.emitting = false;

            ElephantRockProjectile projectile = rock.AddComponent<ElephantRockProjectile>();
            projectile.owner = thrower;
            projectile.target = impactTarget;
            projectile.carryOffset = localCarryOffset;
            projectile.scoopDuration = Mathf.Max(0.1f, scoopTime);
            projectile.flightDuration = Mathf.Max(0.1f, travelTime);
            projectile.arcHeight = Mathf.Max(0.5f, height);
            projectile.onImpact = impactCallback;
            rock.transform.position = projectile.GroundScoopPoint;
            return rock;
        }

        Vector3 GroundScoopPoint => owner != null
            ? owner.TransformPoint(new Vector3(0f, 0.2f, 3.1f) + carryOffset)
            : transform.position;

        Vector3 TrunkHoldPoint => owner != null
            ? owner.TransformPoint(new Vector3(0f, 5.3f, 3.3f) + carryOffset)
            : transform.position;

        void Update()
        {
            if (finished) return;
            elapsed += Time.deltaTime;

            if (!launched)
            {
                float amount = Smooth01(Mathf.Clamp01(elapsed / scoopDuration));
                Vector3 start = GroundScoopPoint;
                Vector3 end = TrunkHoldPoint;
                transform.position = Vector3.Lerp(start, end, amount) +
                    Vector3.up * (Mathf.Sin(amount * Mathf.PI) * 0.65f);
                transform.localScale = new Vector3(1.35f, 1.05f, 1.2f) * Mathf.Lerp(0.15f, 0.95f, amount);
                transform.Rotate(new Vector3(85f, 125f, 65f) * Time.deltaTime, Space.Self);
                return;
            }

            float flightAmount = Mathf.Clamp01(elapsed / flightDuration);
            float eased = 1f - (1f - flightAmount) * (1f - flightAmount);
            transform.position = Vector3.Lerp(launchPosition, target, eased) +
                Vector3.up * (4f * arcHeight * flightAmount * (1f - flightAmount));
            transform.Rotate(new Vector3(420f, 560f, 310f) * Time.deltaTime, Space.Self);

            if (flightAmount >= 1f) Impact();
        }

        public void Launch()
        {
            if (launched || finished) return;
            launched = true;
            elapsed = 0f;
            launchPosition = transform.position;
            TrailRenderer trail = GetComponent<TrailRenderer>();
            if (trail != null) trail.emitting = true;
        }

        void Impact()
        {
            if (finished) return;
            finished = true;
            onImpact?.Invoke(target);
            Destroy(gameObject);
        }

        static Material GetRockMaterial()
        {
            if (rockMaterial != null) return rockMaterial;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            rockMaterial = new Material(shader)
            {
                name = "VoiChinNga_ThrownRock_Runtime",
                hideFlags = HideFlags.DontSave
            };
            SetColor(rockMaterial, new Color(0.24f, 0.15f, 0.075f, 1f));
            if (rockMaterial.HasProperty("_Smoothness")) rockMaterial.SetFloat("_Smoothness", 0.06f);
            return rockMaterial;
        }

        static Material GetTrailMaterial()
        {
            if (trailMaterial != null) return trailMaterial;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Unlit/Color");
            trailMaterial = new Material(shader)
            {
                name = "VoiChinNga_RockTrail_Runtime",
                hideFlags = HideFlags.DontSave,
                renderQueue = (int)RenderQueue.Transparent
            };
            SetColor(trailMaterial, Color.white);
            return trailMaterial;
        }

        static void SetColor(Material material, Color color)
        {
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        }

        static float Smooth01(float value) => value * value * (3f - 2f * value);
    }
}
