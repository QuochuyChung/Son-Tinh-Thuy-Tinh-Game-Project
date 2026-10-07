using System;
using System.Collections.Generic;
using UnityEngine;

namespace SonTinhThuyTinh.Characters
{
    // Gentle secondary motion for outfit parts (cape, skirt panels) that tools/fit_garment.py gives bone chains:
    // bones named <prefix><column>_<segment>, e.g. Cape_2_1 or Skirt_5_0. The Animator never writes these bones, so after it
    // has run each frame every chain is simulated as particles on springs: the tips lag behind the character's movement and are
    // pulled back to the animated pose. Standing still (or turning on the spot) leaves the outfit exactly as modelled.
    // The spring is a real spring-damper in seconds (frequency / damping ratio, sub-stepped), so the look does not depend on the
    // frame rate: a "pull per frame" version wobbled at ~16 Hz at 350 fps and looked like a vibrating machine.
    // (Unity Cloth was tried first; on meshes made of many loose pieces it collapsed them into strips.)
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public class OutfitSpringBones : MonoBehaviour
    {
        [Serializable]
        public class Group
        {
            public string name = "Cape";
            [Tooltip("Bones are named <prefix><column>_<segment>, e.g. Cape_2_1.")]
            public string bonePrefix = "Cape_";
            [Tooltip("Spring frequency in Hz: how fast the outfit is pulled back to its animated pose. THE swing size knob: higher = tighter, smaller and quicker sway, lower = looser and lazier.")]
            [Range(0.5f, 12f)] public float frequency = 5f;
            [Tooltip("1 = settles without overshoot, below 1 = swings back and forth a few times before it settles, 0 = never settles.")]
            [Range(0f, 2f)] public float dampingRatio = 0.7f;
            [Tooltip("Air drag per second: makes the outfit trail behind a moving character (trail at constant speed = drag x speed / (2 pi x frequency)^2). 0 = it only reacts to speeding up, slowing down and the gait.")]
            [Range(0f, 20f)] public float drag = 5f;
            [Tooltip("Extra downward pull in m/s2. The pose already hangs, so keep it small.")]
            public float gravity = 1f;
            [Tooltip("Degrees a segment may swing away from its animated direction: the hard cap on the sway.")]
            public float maxAngle = 8f;
            [Tooltip("How far a segment may swing towards the body, as the sine of an angle. Keeps the outfit off the back and legs.")]
            [Range(0f, 1f)] public float inwardLimit = 0.06f;
        }

        [SerializeField] List<Group> groups = new() { new Group() };
        [Tooltip("Turning the character (character select, spinning on the spot) rotates the outfit with it instead of flinging it. Only moving makes it lag.")]
        [SerializeField] bool ignoreRootRotation = true;
        [Tooltip("Moving further than this in one frame counts as a teleport (spawn, respawn): the outfit snaps to its rest pose.")]
        [SerializeField] float teleportDistance = 2f;
        [Tooltip("Longest frame time the simulation takes in one step, so a hitch (loading, GC) cannot fling the outfit.")]
        [SerializeField] float maxStep = 0.05f;

        const float MaxSubStep = 1f / 90f;
        const float MinStep = 1f / 80f;

        class Segment
        {
            public Transform bone;
            public Quaternion restLocalRotation;
            public Vector3 restDirLocal;     // direction from this bone's head to its tip, in the bone's own space
            public float length;             // world length of the segment
            public Vector3 tip;              // simulated tip (= head of the next segment)
            public Vector3 velocity;         // m/s, relative to the character's turning
            public Vector3 restTipPrev;      // where the animated pose wanted the tip last frame (moves with the turning too)
            public bool hasPrev;
        }

        class Chain
        {
            public Group group;
            public List<Segment> segments = new();
            public Vector3 outwardLocal;     // horizontal direction away from the body, in this object's space
        }

        readonly List<Chain> chains = new();
        Vector3 lastPosition;
        Quaternion lastRotation;
        bool needsReset = true;
        float accumulated;

        void Awake() => BuildChains();

        void OnEnable()
        {
            needsReset = true;
            accumulated = 0f;
        }

        void BuildChains()
        {
            chains.Clear();
            foreach (Group group in groups)
            {
                var columns = new SortedDictionary<int, SortedDictionary<int, Transform>>();
                foreach (Transform t in GetComponentsInChildren<Transform>(true))
                {
                    if (!t.name.StartsWith(group.bonePrefix)) continue;
                    string[] parts = t.name.Substring(group.bonePrefix.Length).Split('_');
                    if (parts.Length != 2 || !int.TryParse(parts[0], out int column) || !int.TryParse(parts[1], out int level)) continue;
                    if (!columns.TryGetValue(column, out var levels)) columns[column] = levels = new SortedDictionary<int, Transform>();
                    levels[level] = t;
                }

                foreach (var levels in columns.Values)
                {
                    var bones = new List<Transform>(levels.Values);
                    var chain = new Chain { group = group };
                    for (int i = 0; i < bones.Count; i++)
                    {
                        var seg = new Segment { bone = bones[i], restLocalRotation = bones[i].localRotation };
                        if (i + 1 < bones.Count)
                        {
                            Vector3 toChild = bones[i + 1].position - bones[i].position;
                            seg.length = toChild.magnitude;
                            seg.restDirLocal = bones[i].InverseTransformDirection(toChild.normalized);
                        }
                        else if (chain.segments.Count > 0)
                        {
                            // The last bone has no child to measure: it continues straight on from the one above.
                            Segment above = chain.segments[chain.segments.Count - 1];
                            seg.length = above.length;
                            seg.restDirLocal = Quaternion.Inverse(seg.restLocalRotation) * above.restDirLocal;
                        }
                        else { seg.length = 0.1f; seg.restDirLocal = Vector3.down; }
                        chain.segments.Add(seg);
                    }
                    if (chain.segments.Count == 0) continue;
                    Vector3 away = Vector3.ProjectOnPlane(chain.segments[0].bone.position - transform.position, transform.up);
                    chain.outwardLocal = transform.InverseTransformDirection(away.sqrMagnitude > 1e-8f ? away.normalized : -transform.forward);
                    chains.Add(chain);
                }
            }
        }

        void ResetChains()
        {
            foreach (var chain in chains)
            {
                foreach (var seg in chain.segments) seg.bone.localRotation = seg.restLocalRotation;
                for (int i = 0; i < chain.segments.Count; i++)
                {
                    Segment seg = chain.segments[i];
                    seg.tip = i + 1 < chain.segments.Count ? chain.segments[i + 1].bone.position : seg.bone.position + seg.bone.TransformDirection(seg.restDirLocal) * seg.length;
                    seg.velocity = Vector3.zero;
                    seg.hasPrev = false;
                }
            }
            lastPosition = transform.position;
            lastRotation = transform.rotation;
            needsReset = false;
        }

        void LateUpdate()
        {
            if (chains.Count == 0) return;
            // Fast frame rates (the editor runs at hundreds of fps) are collected into steps of at least MinStep: the length and
            // swing limits are applied once per step, so stepping at 350 fps would damp the sway ~5x more than at 60 fps.
            accumulated += Time.deltaTime;
            if (accumulated < MinStep) return;
            float dt = Mathf.Min(accumulated, maxStep);
            accumulated = 0f;
            Vector3 position = transform.position; Quaternion rotation = transform.rotation;
            if (needsReset || (position - lastPosition).sqrMagnitude > teleportDistance * teleportDistance) ResetChains();
            else if (ignoreRootRotation)
            {
                // carry the particles round with the character's turn, about where it stood last frame, so only translation causes lag
                Quaternion carry = rotation * Quaternion.Inverse(lastRotation);
                foreach (var chain in chains)
                    foreach (var seg in chain.segments)
                    {
                        seg.tip = lastPosition + carry * (seg.tip - lastPosition);
                        seg.velocity = carry * seg.velocity;
                        seg.restTipPrev = lastPosition + carry * (seg.restTipPrev - lastPosition);
                    }
            }
            lastPosition = position; lastRotation = rotation;

            int steps = Mathf.Clamp(Mathf.CeilToInt(dt / MaxSubStep), 1, 8);   // small steps keep the spring stable and independent of the frame rate
            float h = dt / steps;
            foreach (var chain in chains)
            {
                Group g = chain.group;
                float omega = 2f * Mathf.PI * g.frequency;
                float spring = omega * omega, damper = 2f * g.dampingRatio * omega;
                Vector3 gravity = Vector3.down * g.gravity;
                float cosMax = Mathf.Cos(g.maxAngle * Mathf.Deg2Rad);
                Vector3 outward = transform.TransformDirection(chain.outwardLocal);

                Quaternion parentRotation = chain.segments[0].bone.parent.rotation;   // animated parent (Spine2 / Hips)
                Vector3 head = chain.segments[0].bone.position;                       // follows the animated parent
                foreach (Segment seg in chain.segments)
                {
                    Quaternion restRotation = parentRotation * seg.restLocalRotation;
                    Vector3 restDir = restRotation * seg.restDirLocal;
                    Vector3 restTip = head + restDir * seg.length;

                    if (!seg.hasPrev) { seg.restTipPrev = restTip; seg.hasPrev = true; }
                    Vector3 restVelocity = (restTip - seg.restTipPrev) / dt;          // how fast the animated pose itself moves (gait, walking)

                    // spring towards the animated tip, damped relative to its own movement, plus a little air drag so it trails when moving
                    Vector3 next = seg.tip, velocity = seg.velocity;
                    for (int s = 1; s <= steps; s++)
                    {
                        Vector3 target = Vector3.Lerp(seg.restTipPrev, restTip, (float)s / steps);
                        Vector3 accel = (target - next) * spring - (velocity - restVelocity) * damper - velocity * g.drag + gravity;
                        velocity += accel * h;
                        next += velocity * h;
                    }

                    Vector3 dir = next - head;
                    dir = dir.sqrMagnitude > 1e-10f ? dir.normalized : restDir;
                    if (Vector3.Dot(dir, restDir) < cosMax)                           // swing cap around the animated direction
                    {
                        float angle = Vector3.Angle(dir, restDir);
                        dir = Vector3.Slerp(restDir, dir, g.maxAngle / angle);
                    }
                    float outDir = Vector3.Dot(dir, outward), restOut = Vector3.Dot(restDir, outward);
                    if (outDir < restOut - g.inwardLimit)                             // do not swing into the body
                        dir = (dir + outward * (restOut - g.inwardLimit - outDir)).normalized;

                    Vector3 newTip = head + dir * seg.length;
                    seg.velocity = velocity + (newTip - next) / dt;                   // the cap / length limits take their share of the speed
                    seg.tip = newTip;
                    seg.restTipPrev = restTip;
                    Quaternion rot = Quaternion.FromToRotation(restDir, dir) * restRotation;
                    seg.bone.rotation = rot;
                    parentRotation = rot;
                    head = seg.tip;
                }
            }
        }
    }
}
