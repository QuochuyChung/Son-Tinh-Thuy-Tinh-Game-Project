using UnityEngine;

namespace SonTinhThuyTinh.Environment
{
    // Keeps the many-finned shark swimming around its gift spot on the river bank.
    [DisallowMultipleComponent]
    public sealed class ManyFinnedSharkSwimmer : MonoBehaviour
    {
        [SerializeField] Transform visual;
        [SerializeField] Material sharkMaterial;
        [SerializeField, Min(0.1f)] float sharkScale = 4.5f;
        [SerializeField, Min(0.1f)] float swimSpeed = 2.4f;
        [SerializeField, Min(1f)] float patrolRadiusX = 2.5f;
        [SerializeField, Min(1f)] float patrolRadiusZ = 3.5f;
        [SerializeField, Min(1f)] float turnSpeed = 120f;
        [SerializeField, Min(0f)] float verticalBob = 0.05f;
        [SerializeField] bool followGround = true;
        [SerializeField, Min(0f)] float groundClearance = 0.65f;
        [SerializeField] float initialPhase = 25f;

        Animator animator;
        Vector3 patrolCenter;
        float phase;

        void Awake()
        {
            ApplyVisual();
            animator = GetComponentInChildren<Animator>(true);
            if (animator != null) animator.applyRootMotion = false;
        }

        void Start()
        {
            patrolCenter = transform.position;
            phase = initialPhase * Mathf.Deg2Rad;
            PlaySwimAnimation();
            MoveAlongRoute(0f, true);
        }

        void OnEnable()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            PlaySwimAnimation();
        }

        void Update()
        {
            float averageRadius = Mathf.Max(1f, (patrolRadiusX + patrolRadiusZ) * 0.5f);
            phase = Mathf.Repeat(phase + swimSpeed / averageRadius * Time.deltaTime, Mathf.PI * 2f);
            MoveAlongRoute(Time.deltaTime, false);
        }

        void MoveAlongRoute(float deltaTime, bool snapRotation)
        {
            Vector3 destination = patrolCenter + new Vector3(
                Mathf.Cos(phase) * patrolRadiusX,
                Mathf.Sin(phase * 2f) * verticalBob,
                Mathf.Sin(phase) * patrolRadiusZ);

            if (followGround)
            {
                Vector3 rayOrigin = new Vector3(destination.x, patrolCenter.y + 10f, destination.z);
                if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 30f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    destination.y = hit.point.y + groundClearance
                        + Mathf.Sin(phase * 2f) * verticalBob;
                }
            }

            Vector3 tangent = new Vector3(
                -Mathf.Sin(phase) * patrolRadiusX,
                0f,
                Mathf.Cos(phase) * patrolRadiusZ).normalized;

            if (tangent.sqrMagnitude > 0.0001f)
            {
                Quaternion facing = Quaternion.LookRotation(tangent, Vector3.up);
                transform.rotation = snapRotation
                    ? facing
                    : Quaternion.RotateTowards(transform.rotation, facing, turnSpeed * deltaTime);
            }

            transform.position = destination;
        }

        void PlaySwimAnimation()
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            animator.CrossFade("Shark_Swim", 0.12f);
        }

        void ApplyVisual()
        {
            if (visual == null && transform.childCount > 0) visual = transform.GetChild(0);
            if (visual == null) return;

            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
            visual.localScale = Vector3.one * sharkScale;

            if (sharkMaterial == null) return;
            foreach (Renderer modelRenderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = modelRenderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = sharkMaterial;
                modelRenderer.sharedMaterials = materials;
            }
        }
    }
}
