using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SonTinhThuyTinh.EditorTools
{
    // Keeps what stands in a map from standing inside something else. Everything is ranked; the lower rank is moved away from the higher:
    //     structure (bridge boards and rails, piers, houses, the palace, the throne, ...)  >  giant tree  >  prop (banner, torch, drum)
    //     >  rock  >  tree  >  bush
    //  - the mover goes to the nearest free spot (a ring search up to 10 m, never into water unless it is a boat or a prop standing on boards),
    //    or is deleted when there is none;
    //  - a tree's footprint is its real trunk (the low part of its mesh), a bush's / rock's / prop's its renderer bounds, a boat's its hull;
    //  - torches and banners on a pier or bridge that stand over the water (a brazier's legs on the very edge) are brought back over the boards;
    //  - Sơn Tinh's banners on the road are moved to its side; what floats above the ground after the terrain was levelled is put back on it.
    // Menu: Tools > Son Tinh Thuy Tinh > Scenery > Clean Up Open Scene. FixScene(path) opens, fixes and saves a scene (batch).
    public static class SceneryCleanup
    {
        static readonly string[] SkipStructure = { "Mountain", "Water", "Sea", "Terrain", "Ground", "Sky", "Wall_", "EdgeWall", "Waterfall", "Cloud", "Fog", "Plane", "Quad", "Puddle", "Ripple", "Mist", "Floor", "Slab", "Arena", "Platform", "Dais", "Plateau" };

        enum Rank { Plant = -1, Bush = 0, Tree = 1, Rock = 2, Prop = 3, GiantTree = 4 }

        class Item
        {
            public Transform root; public string name; public Rank rank; public Bounds fp; public bool boat;
        }

        [MenuItem("Tools/Son Tinh Thuy Tinh/Scenery/Clean Up Open Scene")]
        public static void CleanOpenScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            string report = Clean(scene.name);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("SceneryCleanup " + scene.name + ": " + report);
        }

        public static string FixScene(string path)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            string report = Clean(scene.name);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return scene.name + ": " + report;
        }

        public static Vector3? DebugAt; public static string DebugLog = "";

        static string Norm(string n)
        {
            var s = new System.Text.StringBuilder();
            foreach (char c in n) if (!char.IsDigit(c)) s.Append(c);
            return s.ToString().Replace("(Clone)", "");
        }

        static bool IsNatureName(string name, string container)
        {
            string[] nature = { "Tree", "Bush", "Rock", "Stone", "Plant", "Flower", "Grass", "Fern", "Mushroom", "Cliff", "Fir", "Coral", "Lotus", "Reed", "Sapling" };
            return nature.Any(k => name.Contains(k) || container.Contains(k)) || container == "Undergrowth" || container == "Dressing";
        }

        static Transform UnitRoot(Transform t)
        {
            var u = t;
            while (u.parent != null && u.parent.parent != null && u.parent.childCount <= 6) u = u.parent;
            return u;
        }

        static Bounds RendererBounds(Transform root)
        {
            var rs = root.GetComponentsInChildren<Renderer>().Where(r => !(r is ParticleSystemRenderer) && r.enabled).ToArray();
            if (rs.Length == 0) return new Bounds(root.position, Vector3.zero);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        static float GroundY(Terrain t, float x, float z) => t == null ? 0f : t.SampleHeight(new Vector3(x, 0f, z)) + t.transform.position.y;

        // the low part of a tree's mesh: its trunk
        static Bounds Trunk(Transform root)
        {
            var mf = root.GetComponentsInChildren<MeshFilter>().FirstOrDefault(m => m.sharedMesh != null);
            Vector3 p = root.position;
            if (mf == null) return new Bounds(new Vector3(p.x, p.y + 1.2f, p.z), new Vector3(0.9f, 2.4f, 0.9f));
            var verts = mf.sharedMesh.vertices; var m2w = mf.transform.localToWorldMatrix;
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var v in verts)
            {
                var w = m2w.MultiplyPoint3x4(v);
                if (w.y > p.y + 1.4f || w.y < p.y + 0.5f) continue;
                if (w.x < minX) minX = w.x; if (w.x > maxX) maxX = w.x; if (w.z < minZ) minZ = w.z; if (w.z > maxZ) maxZ = w.z;
            }
            if (minX > maxX) return new Bounds(new Vector3(p.x, p.y + 1.2f, p.z), new Vector3(0.9f, 2.4f, 0.9f));
            return new Bounds(new Vector3((minX + maxX) * 0.5f, p.y + 1.1f, (minZ + maxZ) * 0.5f), new Vector3(Mathf.Max(maxX - minX, 0.4f), 2.8f, Mathf.Max(maxZ - minZ, 0.4f)));
        }

        static Bounds Shrink(Bounds b, float f, float yFrom, float yTo)
        {
            float cy = (yFrom + yTo) * 0.5f;
            return new Bounds(new Vector3(b.center.x, cy, b.center.z), new Vector3(b.size.x * f, Mathf.Max(0.1f, yTo - yFrom), b.size.z * f));
        }

        public static string Clean(string sceneName)
        {
            var terrain = Terrain.activeTerrain;
            Physics.SyncTransforms();
            var units = new HashSet<Transform>();
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (r.GetComponentInParent<Canvas>() != null) continue;
                units.Add(UnitRoot(r.transform));
            }
            var items = new List<Item>(); var structures = new List<Bounds>(); var boards = new List<Bounds>();
            float waterY = float.NegativeInfinity;
            foreach (var u in units)
            {
                string name = Norm(u.name), container = u.parent != null ? Norm(u.parent.name) : "";
                if (name == "Water" || name == "Sea") { waterY = Mathf.Max(waterY, RendererBounds(u).max.y); continue; }
                if (SkipStructure.Any(k => name.StartsWith(k) || container.StartsWith(k))) continue;
                var b = RendererBounds(u);
                if (b.size == Vector3.zero) continue;
                float baseY = u.position.y;
                bool boat = name == "Boat" || name == "Shipwreck";
                bool prop = name == "Banner" || name == "Torch" || name == "BronzeDrum";
                if (prop)
                {
                    items.Add(new Item { root = u, name = name, rank = Rank.Prop, fp = Shrink(b, 0.85f, baseY + 0.3f, baseY + 2.4f) });
                    continue;
                }
                if (boat)
                {
                    var hull = u.Find("Longboat");
                    var hb = hull != null ? RendererBounds(hull) : b;
                    items.Add(new Item { root = u, name = name, rank = Rank.Bush, boat = true, fp = Shrink(hb, 0.92f, hb.min.y + 0.25f, hb.min.y + 1.2f) });
                    continue;
                }
                if (IsNatureName(name, container))
                {
                    bool tree = name.Contains("Tree") || name.Contains("Fir") || container.Contains("Tree");
                    bool bush = !tree && (name.Contains("Bush") || container.Contains("Bush"));
                    bool rock = !tree && !bush && (name.Contains("Rock") || name.Contains("Stone") || name.Contains("Cliff") || container.Contains("Rock") || container.Contains("Stone"));
                    if (tree)
                    {
                        var tb = Trunk(u);
                        items.Add(new Item { root = u, name = name, rank = Mathf.Max(tb.size.x, tb.size.z) > 2.2f ? Rank.GiantTree : Rank.Tree, fp = tb });
                    }
                    else if (name.Contains("Plant") || name.Contains("Grass") || name.Contains("Flower") || name.Contains("Fern") || name.Contains("Reed") || name.Contains("Lotus") || name.Contains("Mushroom") || container == "Undergrowth" && !rock)
                        items.Add(new Item { root = u, name = name, rank = Rank.Plant, fp = Shrink(b, 0.8f, b.min.y + 0.05f, b.max.y) });
                    else if (bush) items.Add(new Item { root = u, name = name, rank = Rank.Bush, fp = Shrink(b, 0.7f, baseY - 0.2f, baseY + 1.5f) });
                    else if (rock && b.size.y > 0.3f) items.Add(new Item { root = u, name = name, rank = Rank.Rock, fp = Shrink(b, 0.8f, b.min.y - 0.1f, b.max.y) });
                    continue;
                }
                // a structure: its renderers one by one (a bridge is many boards)
                foreach (var rr in u.GetComponentsInChildren<MeshRenderer>())
                {
                    var rb = rr.bounds;
                    if (rb.size.y < 0.05f && rb.size.x < 0.3f && rb.size.z < 0.3f) continue;
                    if (rb.size.x > 120f || rb.size.z > 120f) continue;
                    structures.Add(rb);
                    if (Norm(rr.name).Contains("Board") || Norm(rr.name).Contains("Plank")) boards.Add(rb);
                }
            }

            bool Hits(Item it, Bounds fp, out Vector3 from)
            {
                from = Vector3.zero;
                foreach (var s in structures)
                {
                    var e = s; e.Expand(0.1f);
                    if (!e.Intersects(fp)) continue;
                    if (it.rank == Rank.Prop ? fp.min.y > s.max.y - 0.04f : fp.min.y > s.max.y + 0.6f) continue;   // a prop may stand on it; a tree, rock or bush may not
                    from = s.center; return true;
                }
                if (it.rank == Rank.GiantTree) return false;
                foreach (var o in items)
                {
                    if (o.root == it.root || o.rank <= it.rank) continue;
                    if (it.boat && o.boat) continue;
                    if (o.rank == Rank.Prop && o.boat == it.boat && it.rank == Rank.Prop) continue;
                    if (o.fp.Intersects(fp)) { from = o.fp.center; return true; }
                }
                return false;
            }

            if (DebugAt.HasValue)
                foreach (var it in items.Where(i => (i.root.position - DebugAt.Value).magnitude < 1.5f))
                    DebugLog += "item " + it.name + " rank " + it.rank + " fp " + it.fp.center + " " + it.fp.size + " hits=" + Hits(it, it.fp, out var dbgFrom) + " structuresNear=" + structures.Count(sb => sb.Intersects(new Bounds(it.fp.center, it.fp.size + Vector3.one * 2f))) + System.Environment.NewLine;
            int moved = 0, removed = 0, anchored = 0;
            int plants = 0;
            foreach (var it in items.Where(i => i.rank == Rank.Plant).ToList())
            {
                bool hit = items.Any(o => o.rank == Rank.Prop && o.fp.Intersects(it.fp)) || boards.Any(bd => { var e = bd; e.Expand(0.1f); return e.Intersects(it.fp) && it.fp.min.y < bd.max.y - 0.04f; });
                if (hit) { Object.DestroyImmediate(it.root.gameObject); it.root = null; plants++; }
            }
            items.RemoveAll(i => i.rank == Rank.Plant);
            foreach (var it in items.OrderByDescending(i => (int)i.rank))
            {
                Vector3 basePos = it.root.position;
                var fp0 = it.fp;
                if (!Hits(it, fp0, out var from)) continue;
                Vector3 dir = new Vector3(basePos.x - from.x, 0f, basePos.z - from.z);
                if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
                dir.Normalize();
                float oldGround = GroundY(terrain, basePos.x, basePos.z), lift = basePos.y - oldGround;
                bool done = false;
                for (float dist = 0.6f; dist <= 10f && !done; dist += 0.6f)
                    for (int k = 0; k < 10 && !done; k++)
                    {
                        float ang = (k % 2 == 0 ? 1 : -1) * (k / 2) * 36f;
                        Vector3 p = basePos + Quaternion.Euler(0f, ang, 0f) * dir * dist;
                        float gy = GroundY(terrain, p.x, p.z);
                        bool onWater = gy < waterY + 0.1f;
                        if (it.boat && onWater != (oldGround < waterY + 0.1f)) continue;   // a boat stays in the water (a beached one on the beach)
                        if (onWater && !it.boat && it.rank != Rank.Prop) continue;
                        if (it.rank == Rank.Prop && !it.boat && onWater) continue;
                        float dy = it.boat || onWater ? 0f : gy - oldGround;       // boats stay at the water level; the rest follow the ground
                        var fp = fp0; fp.center = new Vector3(p.x, fp0.center.y + dy, p.z);
                        if (it.boat) fp.center = new Vector3(p.x, fp0.center.y, p.z);
                        if (Hits(it, fp, out _)) continue;
                        Undo.RecordObject(it.root, "scenery");
                        it.root.position = new Vector3(p.x, it.boat ? basePos.y : gy + lift, p.z);
                        it.fp = fp; moved++; done = true;
                    }
                if (!done && it.rank != Rank.Prop) { Object.DestroyImmediate(it.root.gameObject); it.root = null; removed++; }
            }
            // banners and torches that stand in a clump: one of them stays
            int thinned = 0;
            foreach (string kind in new[] { "Banner", "Torch" })
            {
                float gap = kind == "Banner" ? 3.5f : 1.3f;
                var list = items.Where(i => i.root != null && i.rank == Rank.Prop && !i.boat && i.name == kind).OrderBy(i => i.root.position.z).ThenBy(i => i.root.position.x).ToList();
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].root == null) continue;
                    for (int j = i + 1; j < list.Count; j++)
                        if (list[j].root != null && (list[i].root.position - list[j].root.position).sqrMagnitude < gap * gap)
                        { Object.DestroyImmediate(list[j].root.gameObject); list[j].root = null; thinned++; }
                }
            }
            // torches and banners over the water: back onto the boards
            foreach (var it in items.Where(i => i.root != null && i.rank == Rank.Prop && !i.boat))
            {
                Vector3 p = it.root.position;
                if (GroundY(terrain, p.x, p.z) >= waterY) continue;            // on land
                Bounds? best = null; float bestD = 4f;
                foreach (var bb in boards)
                {
                    if (Mathf.Abs(bb.max.y - p.y) > 0.5f) continue;
                    float dx = Mathf.Max(bb.min.x - p.x, 0f, p.x - bb.max.x), dz = Mathf.Max(bb.min.z - p.z, 0f, p.z - bb.max.z);
                    float d = Mathf.Sqrt(dx * dx + dz * dz);
                    if (d < bestD) { bestD = d; best = bb; }
                }
                if (best == null) continue;
                var board = best.Value;
                // is the torch's footprint (0.3 m) on the boards of this stretch? the boards of one pier line up: the union of the boards within 3 m
                float inX0 = float.MaxValue, inX1 = float.MinValue, inZ0 = float.MaxValue, inZ1 = float.MinValue;
                foreach (var bb in boards)
                    if (Mathf.Abs(bb.max.y - board.max.y) < 0.05f && Vector2.Distance(new Vector2(bb.center.x, bb.center.z), new Vector2(board.center.x, board.center.z)) < 6f)
                    { inX0 = Mathf.Min(inX0, bb.min.x); inX1 = Mathf.Max(inX1, bb.max.x); inZ0 = Mathf.Min(inZ0, bb.min.z); inZ1 = Mathf.Max(inZ1, bb.max.z); }
                const float margin = 0.45f;
                float nx = Mathf.Clamp(p.x, inX0 + margin, Mathf.Max(inX0 + margin, inX1 - margin)), nz = Mathf.Clamp(p.z, inZ0 + margin, Mathf.Max(inZ0 + margin, inZ1 - margin));
                if (Mathf.Abs(nx - p.x) > 0.01f || Mathf.Abs(nz - p.z) > 0.01f)
                {
                    Undo.RecordObject(it.root, "anchor");
                    it.root.position = new Vector3(nx, p.y, nz);
                    anchored++;
                }
            }

            int banners = FixBanners(sceneName, structures, terrain);
            return $"{thinned} clumped banners/torches removed, {plants} plants removed, {moved} moved, {removed} removed, {anchored} torches/banners brought over the boards, {banners} Sơn Tinh banners moved off the road";
        }

        // The torches that stand on piers and walkways over the water (a brazier's legs hang over the edge): deleted. The deck of the harbour
        // and the islands keep theirs.
        [MenuItem("Tools/Son Tinh Thuy Tinh/Scenery/Remove Pier Torches (open scene)")]
        public static void RemovePierTorchesMenu()
        {
            var scene = EditorSceneManager.GetActiveScene();
            Debug.Log("SceneryCleanup: " + RemovePierTorches() + " pier torches removed from " + scene.name);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        public static int RemovePierTorches()
        {
            int n = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).ToList())
            {
                if (t == null || t.name != "Torch" || t.parent == null) continue;
                string parent = t.parent.name;
                if (parent.StartsWith("Pier") || parent.StartsWith("PlankBridge") || parent.StartsWith("PlankWalk") || parent.StartsWith("Causeway"))
                { Object.DestroyImmediate(t.gameObject); n++; }
            }
            return n;
        }

        // Sơn Tinh's banners off the road (the road is about 6 m wide) and the mountain trail.
        static int FixBanners(string sceneName, List<Bounds> structures, Terrain terrain)
        {
            if (!sceneName.Contains("SonTinh")) return 0;
            int n = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.name != "Banner" || t.GetComponentsInChildren<Renderer>().Length == 0) continue;
                Vector3 p = t.position;
                float OnRoad(float x, float z) => Mathf.Abs(x - SonTinhMapBuilder.PathX(z));
                bool Bad(float x, float z) => OnRoad(x, z) < 4.6f || SonTinhMountainBranch.NearTrail(x, z, 1.5f);
                if (p.z < -12f || p.z > 300f || !Bad(p.x, p.z)) continue;
                float side = p.x >= SonTinhMapBuilder.PathX(p.z) ? 1f : -1f;
                bool ok = false;
                for (float lateral = 6.2f; lateral <= 14f && !ok; lateral += 1.2f)
                    foreach (float s in new[] { side, -side })
                    {
                        float x = SonTinhMapBuilder.PathX(p.z) + s * lateral;
                        if (Bad(x, p.z)) continue;
                        var fp = new Bounds(new Vector3(x, p.y + 2f, p.z), new Vector3(1f, 4f, 1f));
                        if (structures.Any(b => b.Intersects(fp))) continue;
                        Undo.RecordObject(t, "banner");
                        t.position = new Vector3(x, GroundY(terrain, x, p.z), p.z);
                        n++; ok = true; break;
                    }
                if (!ok) { Object.DestroyImmediate(t.gameObject); n++; }
            }
            return n;
        }

        // What the levelling left floating (Map_HungVuong): trees, rocks, bushes, torches, banners in the plaza strip go back on the ground.
        public static string SnapPlaza(Rect area, float groundY)
        {
            var terrain = Terrain.activeTerrain;
            int n = 0;
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (r is ParticleSystemRenderer) continue;
                var u = UnitRoot(r.transform);
                if (u == null || !u.gameObject.scene.IsValid()) continue;
                string name = Norm(u.name), container = u.parent != null ? Norm(u.parent.name) : "";
                bool prop = IsNatureName(name, container) || name == "Banner" || name == "Torch" || name == "BronzeDrum";
                if (!prop) continue;
                Vector3 p = u.position;
                if (!area.Contains(new Vector2(p.x, p.z))) continue;
                float gy = GroundY(terrain, p.x, p.z);
                if (p.y - gy > 0.4f && Mathf.Abs(gy - groundY) < 0.5f)
                {
                    Undo.RecordObject(u, "snap");
                    u.position = new Vector3(p.x, gy, p.z);
                    n++;
                }
            }
            return $"{n} props put back on the ground";
        }
    }
}
