using SonTinhThuyTinh.Combat;
using UnityEngine;

namespace SonTinhThuyTinh.Player
{
    // What is climbable in front of the character: a near-vertical wall (two probes at knee and waist height hit it), a flat top at the right height
    // above it, and room for the whole body up there. Used when Space is pressed (PlayerController.TryClimb).
    public static class LedgeProbe
    {
        public struct Ledge
        {
            public Vector3 Normal;       // horizontal, out of the wall towards the character
            public Vector3 WallPoint;    // on the wall face, at the character's height
            public float TopY;           // height of the flat top
            public float Height;         // TopY above the feet
        }

        static readonly Collider[] Overlaps = new Collider[8];

        public static bool TryFind(Transform body, float radius, float height, ClimbSettings settings, Vector3 direction, out Ledge ledge)
        {
            ledge = default;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f) return false;
            direction.Normalize();

            Vector3 feet = body.position;
            float distance = radius + settings.reach;
            if (!Wall(body, feet + Vector3.up * 0.3f, direction, distance, out RaycastHit low)) return false;
            if (!Wall(body, feet + Vector3.up * Mathf.Max(settings.minHeight - 0.1f, 0.4f), direction, distance, out RaycastHit high)) return false;
            if (high.collider != low.collider && Mathf.Abs(high.distance - low.distance) > 0.3f) return false;

            Vector3 normal = low.normal;
            normal.y = 0f;
            if (normal.sqrMagnitude < 0.01f) return false;
            normal.Normalize();
            if (Vector3.Dot(normal, -direction) < 0.35f) return false;   // a wall seen at a shallow angle is not climbed

            // the flat top: from above, a little way behind the wall face
            float probeTop = settings.maxHeight + 0.2f;
            Vector3 above = low.point - normal * 0.2f;
            above.y = feet.y + probeTop;
            if (!TopOf(body, above, probeTop + 0.1f, out RaycastHit top)) return false;
            float climbHeight = top.point.y - feet.y;
            if (climbHeight < settings.minHeight || climbHeight > settings.maxHeight) return false;

            // where the character ends up: the same ground a bit further on, and free of anything in the way
            float onto = settings.forward.Evaluate(1f) - settings.wallDistance + settings.landingExtra;
            Vector3 landing = low.point - normal * onto;
            landing.y = top.point.y + 0.6f;
            if (!TopOf(body, landing, 1.2f, out RaycastHit ground) || Mathf.Abs(ground.point.y - top.point.y) > 0.25f) return false;
            Vector3 bottom = ground.point + Vector3.up * (radius + 0.05f);
            Vector3 topSphere = ground.point + Vector3.up * (height - radius);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, topSphere, radius * 0.9f, Overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (!Overlaps[i].transform.IsChildOf(body)) return false;

            ledge = new Ledge { Normal = normal, WallPoint = low.point, TopY = top.point.y, Height = climbHeight };
            return true;
        }

        // The nearest thing in the way that is a wall (not a character, not the character itself).
        static bool Wall(Transform body, Vector3 origin, Vector3 direction, float distance, out RaycastHit best)
        {
            best = default;
            float nearest = float.MaxValue;
            foreach (RaycastHit hit in Physics.RaycastAll(origin, direction, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(body) || hit.distance >= nearest) continue;
                nearest = hit.distance;
                best = hit;
            }
            if (nearest == float.MaxValue) return false;
            return Mathf.Abs(best.normal.y) < 0.4f && !IsCharacter(best.collider);
        }

        // The first flat surface under a point, looking straight down.
        static bool TopOf(Transform body, Vector3 origin, float distance, out RaycastHit best)
        {
            best = default;
            float nearest = float.MaxValue;
            foreach (RaycastHit hit in Physics.RaycastAll(origin, Vector3.down, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(body) || hit.distance >= nearest) continue;
                nearest = hit.distance;
                best = hit;
            }
            return nearest != float.MaxValue && best.normal.y >= 0.7f && !IsCharacter(best.collider);
        }

        static bool IsCharacter(Collider collider) => collider.GetComponentInParent<Health>() != null || collider.GetComponentInParent<CharacterController>() != null;
    }
}
