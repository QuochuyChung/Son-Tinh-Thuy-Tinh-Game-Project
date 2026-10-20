using System.Collections.Generic;
using SonTinhThuyTinh.Player;
using UnityEngine;

namespace SonTinhThuyTinh.Combat
{
    // Sơn Tinh's X, Tường đá: a wall of rock (width x height metres) slams up a short way in front of him, stands `lifetime` seconds and
    // sinks again. Whoever stands where it comes up is hurt and thrown back (spell.damage, knockback, knockUp). It is solid, and every
    // attack of the other side that reaches it bursts on it and is gone at once: Sóng đẩy, the water slashes, the water orbs of the
    // light and heavy attacks; a melee blow does not reach through it either. Nothing behind it is hurt; what misses the wall still hits.
    // Attacks call RockWallBarrier.Blocks / Between with their own hit volume before they hurt anything.
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
        SpellData spell;
        bool struck;
        readonly HashSet<Health> hit = new();
        Transform model;
        BoxCollider body;
        Vector3 groundPosition;
        float elapsed;
        float shakeUntil;

        public PlayerController Owner => owner;

        public void Init(PlayerController caster, SpellData data)
        {
            owner = caster;
            spell = data;
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
            caster.Shake(data != null ? data.shake : 0.4f);
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
            if (!struck && rise > 0.3f && elapsed < lifetime) StrikeWhereItRises();
        }

        // Coming up, the wall hits whoever stands on its line (each one once): hurt and thrown away from Sơn Tinh.
        void StrikeWhereItRises()
        {
            if (owner == null || spell == null || spell.damage <= 0f) { struck = true; return; }
            if (elapsed >= riseTime) struck = true;   // keeps checking while it rises, then stops
            Vector3 centre = groundPosition + Vector3.up * (height * 0.5f);
            Vector3 half = new Vector3(width * 0.5f + 0.3f, height * 0.5f, thickness * 0.5f + 0.6f);
            int count = Physics.OverlapBoxNonAlloc(centre, half, overlap, transform.rotation, ~0, QueryTriggerInteraction.Ignore);
            var found = new Collider[count];
            System.Array.Copy(overlap, found, count);
            foreach (Health target in SpellEffects.Targets(found, owner.Health))
            {
                if (!hit.Add(target)) continue;
                Vector3 push = transform.forward * spell.knockback + Vector3.up * spell.knockUp;
                if (RockSkillUtil.Strike(owner, spell, target, target.transform.position + Vector3.up, push, false))
                    RockSkillUtil.Dust(dustPrefab, target.transform.position, 0.6f);
            }
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

        // The same for a ball of `radius` that moved from `from` to `to` this frame (a fast orb cannot jump through the stone).
        public static bool Blocks(Vector3 from, Vector3 to, float radius, PlayerController attacker, out RockWallBarrier wall, out Vector3 point)
        {
            wall = null;
            point = to;
            if (active.Count == 0) return false;
            int count = Physics.OverlapCapsuleNonAlloc(from, to, radius, overlap, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var w = overlap[i].GetComponent<RockWallBarrier>();
                if (w == null || !w.Standing || w.owner == attacker) continue;
                wall = w;
                point = overlap[i].ClosestPoint(from);
                return true;
            }
            return false;
        }

        // A standing wall of the other side on the straight line between an attacker and its target (a blow cannot reach through it).
        public static bool Between(Vector3 from, Vector3 to, PlayerController attacker)
        {
            if (active.Count == 0) return false;
            foreach (RockWallBarrier w in active)
                if (w != null && w.Standing && w.owner != attacker && w.body.Raycast(new Ray(from, to - from), out _, Vector3.Distance(from, to)))
                    return true;
            return false;
        }
    }
}
