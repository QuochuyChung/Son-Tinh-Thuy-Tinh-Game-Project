using System.Collections.Generic;
using SonTinhThuyTinh.Environment;
using UnityEditor;
using UnityEngine;

namespace SonTinhThuyTinh.EditorTools
{
    // The mountain trail of Map_SonTinh: a ledge that climbs the west wall of the valley behind the village, then a canyon on the
    // plateau above with rock steps, a broken bridge over a pit, a clearing for the beasts and a summit where the runaway horse waits
    // (the horse quest, docs/progress.md 9.23). Everything is a height-field modifier on top of SonTinhMapBuilder.BaseHeight, so the
    // terrain, the paint and the props all see the same trail.
    //
    // Route (west of the valley, +z is up the map): ledge up the wall -> canyon start -> rock steps -> landing -> pit with stub planks and
    // three stone pillars -> landing -> beast clearing -> narrow climb -> summit.
    public static class SonTinhMountainBranch
    {
        struct Node
        {
            public Vector2 p;      // x, z
            public float y;        // trail height
            public float halfW;    // half width of the walkable trail
            public float canyon;   // 0 = ledge on the open wall, 1 = sunken between cliffs
            public int steps;      // > 0: the segment to the next node is a flight of rock steps
        }

        // node indices that other code and the quest builders refer to
        public const int ILedgeStart = 0, ICanyonStart = 5, ISteps = 7, IBeforePit = 9, IAfterPit = 10, IClearing = 11, ISummit = 13;
        public const float CliffHeight = 12f, PitDepth = 15f, PitEdge = 0.7f, PitStart = 7f, PitEnd = 23f;

        static Node[] nodes;
        static float[] arc;       // arc[i] = length of the trail from node 0 to node i
        static Vector2 min, max;  // bounds of the area the trail can change

        public static void Reset() => nodes = null;

        static Node N(float x, float z, float y, float halfW, float canyon, int steps = 0) =>
            new() { p = new Vector2(x, z), y = y, halfW = halfW, canyon = canyon, steps = steps };

        static void Init()
        {
            if (nodes != null) return;
            var list = new List<Node>();

            // The ledge: climbs the wall at the foot of the village. Its x is found where the bare wall reaches the wanted height,
            // so the cut into the hill stays shallow.
            float[] zs = { 70f, 74f, 90f, 106f, 122f, 136f };   // starts at z = 70, north of the village houses (the Hearth Lodge reaches z = 65)
            float[] ys = { 0f, 8f, 13f, 18.5f, 24f, 29f };
            float x = SonTinhMapBuilder.PathX(zs[0]) - 7f;
            ys[0] = SonTinhMapBuilder.BaseHeight(x, zs[0]) + 0.1f;
            list.Add(N(x, zs[0], ys[0], 3.3f, 0f));
            for (int i = 1; i < zs.Length; i++)
            {
                float lim = SonTinhMapBuilder.PathX(zs[i]) - 80f;
                float cx = x - 6f;
                while (cx > lim && SonTinhMapBuilder.BaseHeight(cx, zs[i]) < ys[i] - 0.3f) cx -= 0.5f;
                x = cx;
                list.Add(N(x, zs[i], ys[i], 3.3f, i == zs.Length - 1 ? 0.5f : 0f));
            }

            // The canyon on the plateau, relative to the end of the ledge.
            Node m = list[list.Count - 1];
            void Add(float dx, float dz, float dy, float halfW, float canyon, int steps = 0)
            {
                m = N(m.p.x + dx, m.p.y + dz, m.y + dy, halfW, canyon, steps);
                list.Add(m);
            }
            Add(-30f, 6f, 0.5f, 4f, 1f);              // 6  canyon start (checkpoint)
            Add(-28f, -4f, 0.5f, 4f, 1f);             // 7  foot of the rock steps
            list[list.Count - 1] = With(list[list.Count - 1], steps: 4);
            Add(-30f, 6f, 5f, 4f, 1f);                // 8  top of the rock steps
            Add(-16f, 2f, 0.3f, 4f, 1f);              // 9  landing before the pit (checkpoint)
            list[list.Count - 1] = With(list[list.Count - 1], steps: 0);
            Add(-30f, 4f, 0f, 4f, 1f);                // 10 landing after the pit (checkpoint)
            Add(-24f, 6f, 0.3f, 13f, 1f);             // 11 clearing for the beasts (checkpoint)
            Add(-26f, 12f, 1.5f, 4.5f, 1f);           // 12 narrow climb
            Add(-22f, 14f, 2.5f, 9f, 1f);             // 13 summit

            nodes = list.ToArray();
            arc = new float[nodes.Length];
            min = new Vector2(float.MaxValue, float.MaxValue);
            max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < nodes.Length; i++)
            {
                if (i > 0) arc[i] = arc[i - 1] + Vector2.Distance(nodes[i - 1].p, nodes[i].p);
                min = Vector2.Min(min, nodes[i].p);
                max = Vector2.Max(max, nodes[i].p);
            }
            min -= Vector2.one * 52f;
            max += Vector2.one * 52f;
        }

        static Node With(Node n, int steps) { n.steps = steps; return n; }

        // ---------------------------------------------------------------- height field

        static void Query(float x, float z, out int seg, out float t, out float db)
        {
            seg = 0; t = 0f; db = float.MaxValue;
            var q = new Vector2(x, z);
            for (int i = 0; i < nodes.Length - 1; i++)
            {
                Vector2 a = nodes[i].p, ab = nodes[i + 1].p - a;
                float tt = Mathf.Clamp01(Vector2.Dot(q - a, ab) / ab.sqrMagnitude);
                float d = (a + ab * tt - q).magnitude;
                if (d < db) { db = d; seg = i; t = tt; }
            }
        }

        static float SegLength(int seg) => arc[seg + 1] - arc[seg];

        // Trail height at (seg, t): linear between the nodes, quantised on a flight of steps. Without the pit.
        static float NominalY(int seg, float t)
        {
            Node a = nodes[seg], b = nodes[seg + 1];
            if (a.steps <= 0) return Mathf.Lerp(a.y, b.y, t);
            float u = Mathf.Min(t * a.steps, a.steps - 0.0001f), k = Mathf.Floor(u);
            return a.y + (b.y - a.y) * (k + SonTinhMapBuilder.Smooth(0f, 0.2f, u - k)) / a.steps;
        }

        // How deep the floor of the trail is sunk at this point (the pit between the broken bridge ends).
        static float Pit(int seg, float t)
        {
            if (seg != IBeforePit) return 0f;
            float m = t * SegLength(seg);
            return PitDepth * SonTinhMapBuilder.Smooth(PitStart - PitEdge, PitStart + PitEdge, m) * (1f - SonTinhMapBuilder.Smooth(PitEnd - PitEdge, PitEnd + PitEdge, m));
        }

        internal static float Apply(float x, float z, float h)
        {
            Init();
            if (x < min.x || x > max.x || z < min.y || z > max.y) return h;
            Query(x, z, out int seg, out float t, out float db);

            Node a = nodes[seg], b = nodes[seg + 1];
            float halfW = Mathf.Lerp(a.halfW, b.halfW, t), canyon = Mathf.Lerp(a.canyon, b.canyon, t);
            float nominal = NominalY(seg, t), floor = nominal - Pit(seg, t);

            // between cliffs: the ground beside the trail rises steeply, then eases back to the plateau
            float cliff = nominal + CliffHeight * Mathf.Pow(Mathf.Clamp01((db - halfW) / 10f), 1.2f);
            float outer = (1f - SonTinhMapBuilder.Smooth(halfW + 16f, halfW + 30f, db)) * canyon;
            h = Mathf.Lerp(h, cliff, outer);

            // the walkable trail itself
            float inner = 1f - SonTinhMapBuilder.Smooth(halfW, halfW + 5f, db);
            return Mathf.Lerp(h, floor, inner);
        }

        // 1 on the trail (soil colour), 0 off it.
        internal static float TrailMask(float x, float z)
        {
            Init();
            if (x < min.x || x > max.x || z < min.y || z > max.y) return 0f;
            Query(x, z, out int seg, out float t, out float db);
            float halfW = Mathf.Lerp(nodes[seg].halfW, nodes[seg + 1].halfW, t);
            return 1f - SonTinhMapBuilder.Smooth(halfW - 0.8f, halfW + 1.2f, db);
        }

        // True on the trail and, in the canyon, on the cliffs around it: no trees, rocks or flowers there.
        internal static bool NearTrail(float x, float z, float extra)
        {
            Init();
            if (x < min.x || x > max.x || z < min.y || z > max.y) return false;
            Query(x, z, out int seg, out float t, out float db);
            float halfW = Mathf.Lerp(nodes[seg].halfW, nodes[seg + 1].halfW, t), canyon = Mathf.Lerp(nodes[seg].canyon, nodes[seg + 1].canyon, t);
            return db < halfW + 2f + extra + canyon * 12f;
        }

        // ---------------------------------------------------------------- build

        static Vector3 Ground(Vector2 p) => new(p.x, SonTinhMapBuilder.GroundHeight(p.x, p.y), p.y);

        // Point, tangent and half width at arc length s.
        static Vector2 PointAt(float s, out Vector2 tangent, out float halfW, out int seg, out float t)
        {
            s = Mathf.Clamp(s, 0f, arc[arc.Length - 1] - 0.01f);
            seg = 0;
            while (seg < nodes.Length - 2 && arc[seg + 1] < s) seg++;
            t = (s - arc[seg]) / SegLength(seg);
            tangent = (nodes[seg + 1].p - nodes[seg].p).normalized;
            halfW = Mathf.Lerp(nodes[seg].halfW, nodes[seg + 1].halfW, t);
            return Vector2.Lerp(nodes[seg].p, nodes[seg + 1].p, t);
        }

        // Walks the centre line on the real terrain and reports the steepest part and the side drops at the ledge nodes (for checking a build).
        public static string Report(Terrain terrain)
        {
            Init();
            var sb = new System.Text.StringBuilder();
            float maxSlope = 0f;
            string worst = "";
            Vector2 prev = nodes[0].p;
            float prevY = terrain.SampleHeight(new Vector3(prev.x, 0f, prev.y));
            for (int i = 0; i < nodes.Length - 1; i++)
            {
                float len = Vector2.Distance(nodes[i].p, nodes[i + 1].p);
                for (float s = 1f; s <= len; s += 1f)
                {
                    Vector2 p = Vector2.Lerp(nodes[i].p, nodes[i + 1].p, s / len);
                    float y = terrain.SampleHeight(new Vector3(p.x, 0f, p.y));
                    float slope = Mathf.Abs(y - prevY) / Vector2.Distance(prev, p);
                    if (slope > maxSlope && !(i == IBeforePit && s > PitStart - 2f && s < PitEnd + 2f)) { maxSlope = slope; worst = $"seg {i} s={s:F0} at ({p.x:F0},{p.y:F0}) y={y:F1}"; }
                    prev = p; prevY = y;
                }
                sb.AppendLine($"node {i}: ({nodes[i].p.x:F0},{nodes[i].p.y:F0}) ground y={terrain.SampleHeight(new Vector3(nodes[i].p.x, 0f, nodes[i].p.y)):F1} trail y={nodes[i].y:F1}");
            }
            sb.AppendLine($"steepest centre-line slope outside the pit: {maxSlope:F2} ({Mathf.Atan(maxSlope) * Mathf.Rad2Deg:F0} deg) {worst}");
            for (int i = 0; i <= ICanyonStart; i++)
            {
                Vector2 p = nodes[i].p, d = (nodes[Mathf.Min(i + 1, nodes.Length - 1)].p - p).normalized, lat = new(-d.y, d.x);
                float c = terrain.SampleHeight(new Vector3(p.x, 0f, p.y));
                float a = terrain.SampleHeight(new Vector3(p.x + lat.x * 4f, 0f, p.y + lat.y * 4f)) - c;
                float b = terrain.SampleHeight(new Vector3(p.x - lat.x * 4f, 0f, p.y - lat.y * 4f)) - c;
                sb.AppendLine($"ledge node {i}: height of ground 4 m to the left {a:F1}, to the right {b:F1}");
            }
            return sb.ToString();
        }

        internal static Vector3 NodePosition(int i)
        {
            Init();
            return new Vector3(nodes[i].p.x, NominalY(Mathf.Min(i, nodes.Length - 2), i >= nodes.Length - 1 ? 1f : 0f), nodes[i].p.y);
        }

        internal static void Build(Transform root)
        {
            Init();
            Transform group = SonTinhMapBuilder.Group(root, "MountainBranch");
            Color ochre = new(0.78f, 0.58f, 0.18f), red = new(0.62f, 0.12f, 0.08f);

            // anchors for the quest builder
            string[] anchorNames = { "Anchor_LedgeStart", null, null, null, null, "Anchor_CanyonStart", null, "Anchor_Steps", null, "Anchor_BeforePit", "Anchor_AfterPit", "Anchor_BeastClearing", null, "Anchor_Summit" };
            for (int i = 0; i < anchorNames.Length; i++)
            {
                if (anchorNames[i] == null) continue;
                var anchor = new GameObject(anchorNames[i]).transform;
                anchor.SetParent(group, false);
                anchor.position = Ground(nodes[i].p) + Vector3.up * 0.05f;
                if (i + 1 < nodes.Length)
                {
                    Vector2 dir = (nodes[i + 1].p - nodes[i].p).normalized;
                    anchor.rotation = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y));
                }
            }

            // torches along the way, alternating sides
            Transform decor = SonTinhMapBuilder.Group(group, "Decor");
            float total = arc[arc.Length - 1];
            int side = 1;
            for (float s = 14f; s < total - 4f; s += 20f)
            {
                Vector2 p = PointAt(s, out Vector2 tg, out float halfW, out int seg, out float t);
                if (seg == IBeforePit && t * SegLength(seg) > PitStart - 3f && t * SegLength(seg) < PitEnd + 3f) continue;
                Vector2 q = p + new Vector2(-tg.y, tg.x) * (side * (halfW - 0.9f));
                MapDecor.Torch(decor, Ground(q));
                side = -side;
            }

            // the broken bridge: two stubs of planks reaching out from the edges and four stone pillars in between
            Transform pit = SonTinhMapBuilder.Group(group, "BrokenBridge");
            {
                Node a = nodes[IBeforePit], b = nodes[IBeforePit + 1];
                float len = SegLength(IBeforePit);
                Vector2 dir = (b.p - a.p) / len, lat = new(-dir.y, dir.x);
                Material plank = MapDecor.Lit("Mat_Plank", new Color(0.45f, 0.31f, 0.18f), 0f, 0.1f);
                Material wood = MapDecor.Lit("Mat_Wood", new Color(0.33f, 0.22f, 0.12f), 0f, 0.15f);
                Material stone = MapDecor.Lit("Mat_BranchStone", new Color(0.74f, 0.66f, 0.50f), 0f, 0.15f);
                float halfW = a.halfW;

                // stubs: walkable, with a rope hanging off the broken end
                foreach (float m in new[] { PitStart + 0.8f, PitEnd - 0.8f })
                {
                    float y = NominalY(IBeforePit, m / len);
                    Vector2 c = a.p + dir * m;
                    var board = MapDecor.Part(PrimitiveType.Cube, "StubPlanks", pit, new Vector3(c.x, y - 0.14f, c.y), new Vector3(halfW * 2f - 0.4f, 0.28f, 1.6f),
                        Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y)), plank, true);
                    board.isStatic = true;
                    foreach (float sd in new[] { -1f, 1f })
                    {
                        Vector2 e = c + lat * (sd * (halfW - 0.5f)) + dir * (m < len * 0.5f ? 0.7f : -0.7f);
                        MapDecor.Part(PrimitiveType.Cylinder, "Post", pit, new Vector3(e.x, y + 0.5f, e.y), new Vector3(0.14f, 0.7f, 0.14f), Quaternion.identity, wood);
                    }
                }

                // pillars: four wide stones in a straight line between the stubs (the free span is 12.8 m: 4 x 2 m stones, ~1 m gaps), their tops
                // only a little below the planks, a light stone colour that stands out from the grey cliffs. A running jump is 1 m high, ~4 m long.
                float[] ms = { 10.56f, 13.52f, 16.48f, 19.44f };
                float[] top = { -0.2f, -0.35f, -0.35f, -0.2f };
                for (int i = 0; i < ms.Length; i++)
                {
                    Vector2 c = a.p + dir * ms[i];
                    float nominal = NominalY(IBeforePit, ms[i] / len);
                    float height = PitDepth + 1.5f + top[i] * -1f;
                    var pillar = MapDecor.Part(PrimitiveType.Cube, "Pillar_" + i, pit, new Vector3(c.x, nominal + top[i] - height * 0.5f, c.y),
                        new Vector3(2f, height, 2f), Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y)), stone, true);
                    pillar.isStatic = true;
                }

                // fall zone in the pit; the player is sent back to the last checkpoint
                var zone = new GameObject("PitFallZone");
                zone.transform.SetParent(pit, false);
                Vector2 mid = a.p + dir * ((PitStart + PitEnd) * 0.5f);
                float nomMid = NominalY(IBeforePit, (PitStart + PitEnd) * 0.5f / len);
                zone.transform.SetPositionAndRotation(new Vector3(mid.x, nomMid - 9f, mid.y), Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y)));
                var box = zone.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(halfW * 2f + 8f, 6f, PitEnd - PitStart + 4f);
                var respawn = new GameObject("FallbackSpawn").transform;
                respawn.SetParent(zone.transform, false);
                respawn.position = Ground(a.p - dir * 2f) + Vector3.up * 0.3f;
                var zoneComponent = zone.AddComponent<FallRespawnZone>();
                var so = new SerializedObject(zoneComponent);
                so.FindProperty("fallback").objectReferenceValue = respawn;
                so.ApplyModifiedProperties();
            }

            // checkpoints: canyon start, before and after the pit, the clearing
            Transform checkpoints = SonTinhMapBuilder.Group(group, "Checkpoints");
            int[] cps = { ICanyonStart + 1, IBeforePit, IAfterPit, IClearing };
            for (int k = 0; k < cps.Length; k++)
            {
                Node n = nodes[cps[k]];
                var go = new GameObject("Checkpoint_" + k);
                go.transform.SetParent(checkpoints, false);
                Vector3 g = Ground(n.p);
                go.transform.position = g + Vector3.up * 2f;
                var box = go.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(n.halfW * 2f + 2f, 6f, 5f);
                Vector2 dir = (nodes[Mathf.Min(cps[k] + 1, nodes.Length - 1)].p - n.p).normalized;
                go.transform.rotation = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y));
                var cp = go.AddComponent<RespawnCheckpoint>();
                var respawn = new GameObject("Spawn").transform;
                respawn.SetParent(go.transform, false);
                respawn.position = g + Vector3.up * 0.3f;
                var so = new SerializedObject(cp);
                so.FindProperty("spawnPoint").objectReferenceValue = respawn;
                so.ApplyModifiedProperties();

                Vector2 lat = new(-dir.y, dir.x);
                MapDecor.Banner(decor, Ground(n.p + lat * (n.halfW - 1.2f)), Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg, k % 2 == 0 ? ochre : red);
            }

            ScatterBranchProps(group);
        }

        // Trees and boulders on the plateau above the canyon, boulders against the cliff feet. Nothing on the trail.
        static void ScatterBranchProps(Transform group)
        {
            GameObject tree = SonTinhMapBuilder.Load("P_fwOF_Tree_M_2.prefab"), rock = SonTinhMapBuilder.Load("P_fwOF_Rock_01.prefab"),
                grass = SonTinhMapBuilder.Load("P_fwOF_Grass_M_1.prefab"), plant = SonTinhMapBuilder.Load("P_fwOF_ForestPlant_B_02.prefab");
            Transform trees = SonTinhMapBuilder.Group(group, "Trees"), rocks = SonTinhMapBuilder.Group(group, "Rocks"), small = SonTinhMapBuilder.Group(group, "Undergrowth");
            float total = arc[arc.Length - 1];
            var placed = new List<Vector2>();

            for (int attempt = 0; attempt < 4000 && placed.Count < 150 && tree != null; attempt++)
            {
                Vector2 c = PointAt(SonTinhMapBuilder.R(0f, total), out Vector2 tg, out float halfW, out _, out _);
                if (SonTinhMapBuilder.R(0f, 1f) < 0.3f && halfW < 9f) continue;
                float away = (SonTinhMapBuilder.R(0f, 1f) < 0.5f ? -1f : 1f) * (halfW + SonTinhMapBuilder.R(19f, 40f));
                Vector2 p = c + new Vector2(-tg.y, tg.x) * away;
                if (NearTrail(p.x, p.y, 3f) || p.x < SonTinhMapBuilder.XMin + 8f) continue;
                if (placed.Exists(o => Vector2.Distance(o, p) < 9f)) continue;
                placed.Add(p);
                SonTinhMapBuilder.Place(tree, Ground(p) - Vector3.up * 0.2f, SonTinhMapBuilder.R(0f, 360f), Vector3.one * SonTinhMapBuilder.R(0.8f, 1.25f), trees, false);
            }

            for (int i = 0; i < 70 && rock != null; i++)
            {
                Vector2 c = PointAt(SonTinhMapBuilder.R(0f, total), out Vector2 tg, out float halfW, out int seg, out float t);
                if (seg == IBeforePit && t * SegLength(seg) > PitStart - 4f && t * SegLength(seg) < PitEnd + 4f) continue;
                float away = (SonTinhMapBuilder.R(0f, 1f) < 0.5f ? -1f : 1f) * (halfW + SonTinhMapBuilder.R(2.5f, 9f));
                Vector2 p = c + new Vector2(-tg.y, tg.x) * away;
                if (NearTrail(p.x, p.y, -1f) && TrailMask(p.x, p.y) > 0.01f) continue;
                float s = SonTinhMapBuilder.R(2.2f, 5.5f);
                SonTinhMapBuilder.Place(rock, Ground(p) - Vector3.up * s * 0.15f, SonTinhMapBuilder.R(0f, 360f), new Vector3(s, s * SonTinhMapBuilder.R(0.7f, 1.2f), s), rocks, false);
            }

            for (int i = 0; i < 500 && grass != null; i++)
            {
                Vector2 c = PointAt(SonTinhMapBuilder.R(0f, total), out Vector2 tg, out float halfW, out int seg, out float t);
                if (seg == IBeforePit && t * SegLength(seg) > PitStart - 2f && t * SegLength(seg) < PitEnd + 2f) continue;
                Vector2 p = c + new Vector2(-tg.y, tg.x) * SonTinhMapBuilder.R(-halfW - 6f, halfW + 6f);
                if (TrailMask(p.x, p.y) > 0.2f) continue;
                GameObject prefab = i % 3 == 0 && plant != null ? plant : grass;
                SonTinhMapBuilder.Place(prefab, Ground(p), SonTinhMapBuilder.R(0f, 360f), Vector3.one * SonTinhMapBuilder.R(3f, 5.5f), small, true);
            }
        }
    }
}
