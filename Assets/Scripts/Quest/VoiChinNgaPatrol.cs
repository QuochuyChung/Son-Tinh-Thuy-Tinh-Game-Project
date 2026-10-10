using UnityEngine;

namespace SonTinhThuyTinh.Quest
{
    // Cho voi chín ngà tuần tra trên mặt đất quanh điểm sính lễ.
    [DisallowMultipleComponent]
    public class VoiChinNgaPatrol : MonoBehaviour
    {
        const string ModelName = "VoiChinNga_Model";

        [SerializeField] Transform visual;
        [SerializeField] Material elephantMaterial;
        [SerializeField, Min(1f)] float elephantScale = 14f;
        [SerializeField, Min(1f)] float patrolDistance = 8f;
        [SerializeField, Min(0.1f)] float moveSpeed = 1.35f;
        [SerializeField, Min(1f)] float turnSpeed = 100f;
        [SerializeField] float groundOffset = -0.04f;

        Animator animator;
        Terrain terrain;
        Vector3 patrolCenter;
        int direction = 1;

        void Start()
        {
            terrain = FindFirstObjectByType<Terrain>();
            SnapToGround();
            ApplyElephantVisual();
            patrolCenter = transform.position;

            animator = GetComponentInChildren<Animator>(true);
            if (animator != null) animator.CrossFade("Walk_Forward", 0.12f);
        }

        void Update()
        {
            Vector3 target = patrolCenter + Vector3.forward * (direction * patrolDistance);
            Vector3 delta = target - transform.position;
            delta.y = 0f;

            if (delta.sqrMagnitude < 0.2f)
            {
                direction = -direction;
                target = patrolCenter + Vector3.forward * (direction * patrolDistance);
                delta = target - transform.position;
                delta.y = 0f;
            }

            if (delta.sqrMagnitude > 0.0001f)
            {
                Quaternion facing = Quaternion.LookRotation(delta.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, turnSpeed * Time.deltaTime);
            }

            Vector3 next = Vector3.MoveTowards(transform.position,
                new Vector3(target.x, transform.position.y, target.z), moveSpeed * Time.deltaTime);
            next.y = GroundHeight(next);
            transform.position = next;
        }

        void SnapToGround()
        {
            Vector3 position = transform.position;
            position.y = GroundHeight(position);
            transform.position = position;
        }

        void ApplyElephantVisual()
        {
            if (visual == null) visual = transform.Find("Visual");
            if (visual == null) return;

            Transform model = visual.Find(ModelName);
            if (model == null && visual.childCount > 0) model = visual.GetChild(0);
            if (model == null) return;

            model.localScale = Vector3.one * elephantScale;
            visual.localPosition = Vector3.zero;

            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer modelRenderer in renderers)
            {
                if (elephantMaterial == null) continue;

                Material[] materials = modelRenderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = elephantMaterial;
                modelRenderer.sharedMaterials = materials;
            }

            // Căn điểm thấp nhất của model sát mặt đất, không phụ thuộc pivot của FBX.
            if (renderers.Length == 0) return;
            float lowestPoint = renderers[0].bounds.min.y;
            for (int i = 1; i < renderers.Length; i++)
                lowestPoint = Mathf.Min(lowestPoint, renderers[i].bounds.min.y);

            visual.position += Vector3.up * (transform.position.y - lowestPoint);
        }

        float GroundHeight(Vector3 position)
        {
            if (terrain == null) return transform.position.y;
            return terrain.SampleHeight(position) + terrain.transform.position.y + groundOffset;
        }
    }
}
