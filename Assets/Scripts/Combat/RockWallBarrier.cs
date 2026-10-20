using System.Collections.Generic;
using SonTinhThuyTinh.Player;
using UnityEngine;

namespace SonTinhThuyTinh.Combat
{
    // Sơn Tinh's X, Tường đá: a wall of rock (width x height metres) slams up a short way in front of him, stands `lifetime` seconds and
    // sinks again. It is solid, and every water attack of the other side that touches it (Sóng đẩy, the water slashes, the water orbs)
    // bursts on it and is gone at once: nothing goes through and nothing behind it is hurt. A wave that misses the wall still hits as usual.
    // Water attacks call RockWallBarrier.Blocks with their own hit box before they hurt anything.
    public class RockWallBarrier : MonoBehaviour, ISpellEffect
    {
        static readonly List<RockWallBarrier> active = new();
        static readonly Collider[] overlap = new Collider[32];

        [SerializeField] GameObject wallModel;
        [SerializeField] GameObject dustPrefab;
        [SerializeField] float width = 4f;
        [SerializeField] float height = 3f;
        [SerializeField] float thickness = 1f;
        [Tooltip("Metres from Sơn Tinh to the middle of the wall.")]
        [SerializeField] float distance = 2.2f;
        [Tooltip("Seconds the wall stands (rising included, sinking not).")]
        [SerializeField] float lifetime = 2f;
        [SerializeField] float riseTime = 0.2f;
        [SerializeField] float sinkTime = 0.35f;

        PlayerController owner;
        Transform model;
        BoxCollider body;
        Vector3 groundPosition;
        float elapsed;
        float shakeUntil;

        public PlayerController Owner => owner;

        public void Init(PlayerController caster, SpellData spell)
        {
            owner = caster;
            Vector3 forward = Vector3.ProjectOnPlane(caster.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            groundPosition = caster.transform.position + forward * distance;
            groundPosition.y = RockSkillUtil.GroundY(groundPosition, caster.transform.position.y);
            transform.SetPositionAndRotation(groundPosition + Vector3.down * height, Quaternion.LookRotation(forward));

            if (wallModel != null)
            {
                // the model is a low dry-stone wall (about 4 x 1.6 m): stacked in courses, each one turned round, up to the height
                model = new GameObject("Stones").transform;
                model.SetParent(transform, false);
                float y = 0f;
                for (int course = 0; y < height - 0.05f && course < 6; course++)
                {
                    Transform piece = Instantiate(wallModel, model).transform;
                    piece.localRotation = Quaternion.Euler(0f, course % 2 == 0 ? 0f : 180f, 0f);
                    piece.localPosition = Vector3.zero;
                    RockSkillUtil.FitTo(piece, new Vector3(width, 0f, 0f));
                    Bounds b = RockSkillUtil.LocalBounds(piece);
                    float scaleY = Mathf.Min(1f, (height - y) / Mathf.Max(b.size.y * 0.92f, 0.01f));
                    if (scaleY < 0.6f) scaleY = (height - y) / b.size.y;   // a thin last course: squash it rather than leave a gap
                    piece.localScale = Vector3.Scale(piece.localScale, new Vector3(1f, scaleY, 1f));
                    b = RockSkillUtil.LocalBounds(piece);
                    piece.localPosition = new Vector3(-b.center.x, y - b.min.y, -b.center.z);
                    y += b.size.y * 0.92f;   // courses overlap a little: no light between them
                    foreach (Collider c in piece.GetComponentsInChildren<Collider>()) Destroy(c);
                }
            }

            body = gameObject.AddComponent<BoxCollider>();
            body.size = new Vector3(width, height, thickness);
            body.center = new Vector3(0f, height * 0.5f, 0f);
            body.enabled = false;
            active.Add(this);
            RockSkillUtil.Dust(dustPrefab, groundPosition, 1.1f);
            caster.Shake(spell != null ? spell.shake : 0.4f);
        }

        void OnDestroy() => active.Remove(this);

        bool Standing => body != null && body.enabled;

        void Update()
        {
            elapsed += Time.deltaTime;
            float rise;
            if (elapsed < riseTime)
            {
                float t = elapsed / riseTime;
                rise = 1f - (1f - t) * (1f - t);
            }
            else if (elapsed < lifetime) rise = 1f;
            else
            {
                float t = Mathf.Clamp01((elapsed - lifetime) / sinkTime);
                rise = 1f - t * t;
                if (t >= 1f) { Destroy(gameObject); return; }
            }
            Vector3 jitter = Time.time < shakeUntil ? Random.insideUnitSphere * 0.06f : Vector3.zero;
            transform.position = groundPosition + Vector3.down * (height * (1f - rise)) + jitter;
            if (body != null) body.enabled = rise > 0.5f;
        }

        // A water attack hit the wall: a splash where it struck and a little shudder of the stone.
        public void Absorb(Vector3 point)
        {
            shakeUntil = Time.time + 0.18f;
            var splash = new GameObject("Water Burst On Rock Wall");
            splash.transform.SetPositionAndRotation(point, transform.rotation);
            splash.AddComponent<WaterWaveImpact>().Init();
        }

        // True when a box (centre, half size, rotation) of an attack by `attacker` touches a standing wall of the other side. `wall` is that
        // wall, `point` the place on it nearest the box.
        public static bool Blocks(Vector3 centre, Vector3 halfSize, Quaternion rotation, PlayerController attacker, out RockWallBarrier wall, out Vector3 point)
        {
            wall = null;
            point = centre;
            if (active.Count == 0) return false;
            int count = Physics.OverlapBoxNonAlloc(centre, halfSize, overlap, rotation, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var w = overlap[i].GetComponent<RockWallBarrier>();
                if (w == null || !w.Standing || w.owner == attacker) continue;
                wall = w;
                point = overlap[i].ClosestPoint(centre);
                return true;
            }
            return false;
        }

        public static bool Blocks(Vector3 centre, float radius, PlayerController attacker, out RockWallBarrier wall, out Vector3 point)
            => Blocks(centre, Vector3.one * radius, Quaternion.identity, attacker, out wall, out point);
    }
}
