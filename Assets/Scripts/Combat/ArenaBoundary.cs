using SonTinhThuyTinh.Player;
using UnityEngine;

namespace SonTinhThuyTinh.Combat
{
    // Keeps the player inside a circle while a fight is on (the crocodile's island, the rooster's stone ring). The fight's director calls
    // Engage / Release. The limit is invisible unless `showWall` is on: then the edge is a glowing curtain that fades in and out.
    // The curtain is two ring meshes built at runtime, a see-through wall and a bright band where it meets the ground; their material
    // (URP Unlit, transparent, double-sided) comes from Assets/Editor/ArenaBoundaryBuilder.cs and is tinted with `color`.
    public class ArenaBoundary : MonoBehaviour
    {
        [SerializeField] Transform center;
        [SerializeField] float radius = 12f;
        [SerializeField] float wallHeight = 3.5f;
        [Tooltip("The curtain starts this far below the centre's height (uneven ground).")]
        [SerializeField] float wallDrop = 0.8f;
        [SerializeField] Color color = new(0.45f, 0.85f, 1f, 0.6f);
        [SerializeField] Material wallMaterial;
        [Tooltip("Show the edge as a coloured curtain (off: the limit is invisible).")]
        [SerializeField] bool showWall;

        PlayerController player;
        CharacterController body;
        bool engaged;
        float alpha;
        MeshRenderer wall, rim;
        Material wallMat, rimMat;

        public bool Engaged => engaged;
        public float Radius => radius;

        public void Engage() => engaged = true;
        public void Release() => engaged = false;

        void Awake() { if (showWall) BuildWall(); }

        void OnDestroy()
        {
            if (wallMat != null) Destroy(wallMat);
            if (rimMat != null) Destroy(rimMat);
        }

        void LateUpdate()
        {
            alpha = Mathf.MoveTowards(alpha, engaged ? 1f : 0f, Time.deltaTime * 1.5f);
            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 2.2f);
            Tint(wall, wallMat, color.a * 0.3f * alpha * pulse);   // light: the camera often looks through it
            Tint(rim, rimMat, Mathf.Min(1f, color.a * 1.3f) * alpha);
            if (!engaged || center == null) return;
            FindPlayer();
            if (player == null) return;

            // past the edge: a small step (walking, dashing into it) is pushed back; a big one (knocked far, rocks in the way) is put
            // straight back on the ground just inside the circle
            Vector3 p = player.transform.position;
            Vector3 off = p - center.position; off.y = 0f;
            float limit = radius - 0.4f;
            float over = off.magnitude - limit;
            if (over <= 0f) return;
            Vector3 target = center.position + off.normalized * limit;
            target.y = p.y;
            if (over < 0.75f && body != null && body.enabled) { body.Move(target - p); return; }
            target.y = GroundAt(target, p.y);
            player.SetBodyEnabled(false);
            player.transform.position = target;
            player.SetBodyEnabled(true);
        }

        // the highest solid ground under a point (not the player, not triggers), else the centre's height
        float GroundAt(Vector3 at, float fallback)
        {
            float best = float.MinValue;
            foreach (var hit in Physics.RaycastAll(new Vector3(at.x, center.position.y + 30f, at.z), Vector3.down, 60f, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.collider.transform.IsChildOf(player.transform) && hit.point.y > best) best = hit.point.y;
            return best > float.MinValue ? best + 0.1f : Mathf.Max(fallback, center.position.y + 0.1f);
        }

        void FindPlayer()
        {
            if (player != null && player.isActiveAndEnabled) return;
            player = null; body = null;
            foreach (PlayerController c in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
                if (c.isActiveAndEnabled) { player = c; body = c.GetComponent<CharacterController>(); break; }
        }

        void Tint(MeshRenderer r, Material m, float a)
        {
            if (r == null) return;
            r.enabled = a > 0.005f;
            if (m == null) return;
            Color c = color; c.a = a;
            m.SetColor("_BaseColor", c);
        }

        void BuildWall()
        {
            if (center == null) return;
            wall = Ring("Curtain", -wallDrop, wallHeight, out wallMat);
            rim = Ring("Rim", -0.2f, 0.18f, out rimMat);
        }

        // A ring of quads (open top and bottom) round the centre, from `bottom` to `top` metres relative to the centre's height.
        MeshRenderer Ring(string name, float bottom, float top, out Material material)
        {
            const int segments = 72;
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(center.position, Quaternion.identity);
            var vertices = new Vector3[(segments + 1) * 2];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                var d = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radius;
                vertices[i * 2] = d + Vector3.up * bottom;
                vertices[i * 2 + 1] = d + Vector3.up * top;
                uvs[i * 2] = new Vector2(i / (float)segments, 0f);
                uvs[i * 2 + 1] = new Vector2(i / (float)segments, 1f);
                if (i == segments) break;
                int t = i * 6, v = i * 2;
                triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
                triangles[t + 3] = v + 2; triangles[t + 4] = v + 1; triangles[t + 5] = v + 3;
            }
            var mesh = new Mesh { name = "Arena" + name, vertices = vertices, uv = uvs, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.enabled = false;
            material = wallMaterial != null ? new Material(wallMaterial) : null;
            if (material != null) { material.SetTexture("_BaseMap", Texture2D.whiteTexture); r.sharedMaterial = material; }
            return r;
        }

        void OnDrawGizmosSelected()
        {
            if (center == null) return;
            Gizmos.color = color;
            Gizmos.DrawWireSphere(center.position, radius);
        }
    }
}
