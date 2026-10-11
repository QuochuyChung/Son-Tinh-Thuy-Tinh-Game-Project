using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static SonTinhThuyTinh.EditorTools.SonTinhMapBuilder;

namespace SonTinhThuyTinh.EditorTools
{
    // The open sea of Map_ThuyTinh (docs/progress.md 9.26): since 10/10 the east side of the marsh is no longer a wall but a lagoon
    // about 250 x 180 m, ringed by limestone karst stacks like Ha Long Bay. Out in it lie
    //  - a sea road of wooden piers: the causeway at z 108 -> the harbour (moored boats) -> the fishing village island -> the lighthouse
    //    hill -> the sea shrine island, ~250 m of boardwalk with colliders over a shelf of shallow water;
    //  - sandbars, a shipwreck, coral in the shallows, anchored junks, a village gate over the pier;
    //  - and the terrain itself: basins and shoals (SeaFloor), karst towers (KarstTop) and sand (Paint).
    // The height field functions live here; GroundHeight in ThuyTinhMapBuilder.cs calls them.
    public static partial class ThuyTinhMapBuilder
    {
        const float SeaWallX = 282f;                      // x where the cliffs close the lagoon on the east
        const float PierDeck = WaterY + 0.95f;            // boards, a hand above the causeway (0.75)
        const float HarbourZ = 108f;                      // where the sea road leaves the causeway

        struct SeaIsland { public string Name; public Vector2 Centre; public float Radius, Top, Skirt; }
        struct Karst { public Vector2 C; public float Radius, Height, Seed; }

        static SeaIsland[] seaIslands;
        
        static Karst[] karsts;
        static readonly List<(Vector2 centre, float radius)> seaBlockers = new();

        // ---------------------------------------------------------------- layout

        // The open water window on the east side of the path: closed (a wall) at the start and at the end of the map.
        static float EastOpen(float z) => Smooth(18f, 48f, z) * (1f - Smooth(226f, 256f, z));

        static SeaIsland[] SeaIslands => seaIslands ??= MakeSeaIslands();

        static SeaIsland[] MakeSeaIslands()
        {
            Vector2 g = Bay(1);                        // the Sunken Grotto islet, the sea road's landmark of reference
            Vector2 village = g + new Vector2(66f, -16f);
            Vector2 lighthouse = village + new Vector2(62f, 40f);
            Vector2 shrine = lighthouse + new Vector2(58f, -62f);
            return new[]
            {
                new SeaIsland { Name = "FishingVillage", Centre = village, Radius = 15f, Top = 1.0f, Skirt = 11f },
                new SeaIsland { Name = "Lighthouse", Centre = lighthouse, Radius = 9f, Top = 5.0f, Skirt = 30f },
                new SeaIsland { Name = "SeaShrine", Centre = shrine, Radius = 17f, Top = 1.2f, Skirt = 12f },
                new SeaIsland { Name = "Sandbar_Wreck", Centre = new Vector2(118f, 62f), Radius = 6f, Top = 0.35f, Skirt = 7f },
                new SeaIsland { Name = "Sandbar_Tree", Centre = new Vector2(172f, 205f), Radius = 7f, Top = 0.4f, Skirt = 8f },
                new SeaIsland { Name = "Sandbar_Reeds", Centre = new Vector2(150f, 105f), Radius = 5f, Top = 0.3f, Skirt = 7f },
            };
        }

        static Vector2 HarbourCentre => new(PathX(HarbourZ) + 40f, HarbourZ + 4f);

        // ---- the story route (docs/ke-hoach-thuy-tinh-su-kien.md): causeway -> harbour (pirates) -> fishing village -> lighthouse (water
        // ghosts, the lamp) -> Shark Bay (Mập 9 Vây) -> the crocodile's island on the causeway -> flooded shrine (Rùa 9 Chân) -> gate.
        // The island of the shrine (SeaIslands[2]) is a side branch from the lighthouse.

        // Shark Bay: a round pool in the north-west of the lagoon, walled in by a ring of karst stacks with two gaps: the entrance on the
        // lighthouse side and the way out on the crocodile's island side (shut by a net until the shark is beaten, ThuyTinhSharkEvent).
        const float PoolRadius = 19f, PoolRingRadius = 25f, PoolDepth = 1.5f;
        static Vector2 PoolCentre => new(78f, 192f);
        static Vector2 PoolEntranceDir => (SeaIslands[1].Centre - PoolCentre).normalized;
        static Vector2 PoolExitDir => (Stop(1) - PoolCentre).normalized;
        // the rim points where the piers arrive and leave (on the shelf just outside the ring's gaps)
        static Vector2 PoolEntrance => PoolCentre + PoolEntranceDir * (PoolRingRadius + 3f);
        static Vector2 PoolExit => PoolCentre + PoolExitDir * (PoolRingRadius + 3f);
        // where the pier from the pool reaches the crocodile's island (its east shore; the boulder ring is open there)
        static Vector2 CrocodileLanding => Stop(1) + (PoolCentre - Stop(1)).normalized * (IslandRadius + 1.2f);

        static Vector2[] seaSegmentPoints;

        // The straight segments of the sea road (a, b pairs); the shelf of shallow water and the karst keep clear of them.
        static Vector2[] SeaSegments => seaSegmentPoints ??= new[]
        {
            new Vector2(PathX(HarbourZ) + 2f, HarbourZ), HarbourCentre,
            HarbourCentre, SeaIslands[0].Centre,
            SeaIslands[0].Centre, SeaIslands[1].Centre,
            SeaIslands[1].Centre, SeaIslands[2].Centre,           // the side branch to the shrine island
            SeaIslands[1].Centre, PoolEntrance,
            PoolExit, CrocodileLanding,
        };

        static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        static float DistanceToRoute(Vector2 p)
        {
            Vector2[] r = SeaSegments;
            float best = float.MaxValue;
            for (int i = 0; i + 1 < r.Length; i += 2) best = Mathf.Min(best, SegmentDistance(p, r[i], r[i + 1]));
            return best;
        }

        // ---------------------------------------------------------------- height field

        // Basins and shoals of the open sea: 1 to 2.3 m of water over a sandy floor, a few sandbars, a shallow shelf under the sea road.
        static float SeaFloor(float x, float z, float r1, float r2)
        {
            float n = Mathf.PerlinNoise(x * 0.016f + 11f, z * 0.016f + 91f);
            float rough = Mathf.PerlinNoise(x * 0.07f + 5f, z * 0.07f + 33f) - 0.5f;
            // always over DeepWaterGuard's limit (1.9 m): out of the shelf the open sea cannot be waded (the terrain cannot go below y = 0)
            float depth = Mathf.Lerp(2.4f, 2.1f, Smooth(0.30f, 0.55f, n));
            float shoalOk = Smooth(48f, 64f, r1) * Smooth(26f, 38f, r2)   // no sandbars round the crocodile's island or the shrine
                          * Smooth(PoolRingRadius + 8f, PoolRingRadius + 24f, Vector2.Distance(new Vector2(x, z), PoolCentre));   // nor at Shark Bay
            float floor = WaterY - depth + 1.6f * Smooth(0.58f, 0.74f, n) * shoalOk + 0.3f * rough;
            float shelf = 1f - Smooth(9f, 16f, DistanceToRoute(new Vector2(x, z)));
            return Mathf.Lerp(floor, WaterY - 0.85f, shelf);
        }

        static Karst[] Karsts => karsts ??= MakeKarsts();

        // Limestone stacks: an inner scatter in the lagoon, a denser taller ring on the east, and low reef rocks. Fixed seed.
        static Karst[] MakeKarsts()
        {
            var rnd = new System.Random(20261010);
            float F(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            var list = new List<Karst>();

            bool Free(Vector2 p, float r, float spacing)
            {
                if (GroundCore(p.x, p.y) > WaterY - 0.25f) return false;
                if (p.x - PathX(p.y) < 46f + r) return false;
                foreach (SeaIsland isl in SeaIslands)
                    if (Vector2.Distance(p, isl.Centre) < isl.Radius + isl.Skirt * 0.7f + r + 6f) return false;
                if (DistanceToRoute(p) < r + 12f) return false;
                if (Vector2.Distance(p, PoolCentre) < PoolRingRadius + r + 10f) return false;
                if (Vector2.Distance(p, Stop(1)) < 52f + r || Vector2.Distance(p, Stop(2)) < 40f + r) return false;
                for (int i = 0; i < Bays.Length; i++)
                    if (Vector2.Distance(p, Bay(i)) < Bays[i].z + 20f + r) return false;
                foreach (Karst k in list)
                    if (Vector2.Distance(p, k.C) < (r + k.Radius) * spacing) return false;
                return true;
            }

            void Add(float xMin, float xMax, float zMin, float zMax, int count, float rMin, float rMax, float hMin, float hMax, float spacing)
            {
                for (int tries = 0, made = 0; tries < 8000 && made < count; tries++)
                {
                    float z = F(zMin, zMax), x = F(Mathf.Max(xMin, PathX(z) + 62f), xMax), r = F(rMin, rMax);
                    if (x >= xMax - 1f || !Free(new Vector2(x, z), r, spacing)) continue;
                    list.Add(new Karst { C = new Vector2(x, z), Radius = r, Height = F(hMin, hMax), Seed = F(0f, 200f) });
                    made++;
                }
            }

            // the wall of Shark Bay: stacks side by side round the pool, two gaps (the entrance and the way out)
            for (int i = 0; i < 18; i++)
            {
                float a = i * 20f * Mathf.Deg2Rad;
                Vector2 dir = new(Mathf.Sin(a), Mathf.Cos(a));
                if (Vector2.Angle(dir, PoolEntranceDir) < 17f || Vector2.Angle(dir, PoolExitDir) < 13f) continue;
                list.Add(new Karst { C = PoolCentre + dir * (PoolRingRadius + F(-1f, 1f)), Radius = F(4.6f, 6.2f), Height = F(15f, 26f), Seed = F(0f, 200f) });
            }
            Add(110f, 250f, 40f, 235f, 30, 5f, 11f, 12f, 32f, 0.9f);     // the lagoon
            Add(252f, 300f, 30f, 250f, 32, 7f, 12f, 24f, 40f, 0.75f);    // the eastern ring
            Add(110f, 250f, 40f, 235f, 45, 1.6f, 3.2f, 2.2f, 5f, 1f);    // reef rocks
            return list.ToArray();
        }

        // Raises h under the karst stacks (steep-sided, ragged tops, a little wobble in the outline).
        static float KarstTop(float x, float z, float h)
        {
            foreach (Karst k in Karsts)
            {
                float dx = x - k.C.x, dz = z - k.C.y, reach = k.Radius * 1.35f;
                if (dx > reach || dx < -reach || dz > reach || dz < -reach) continue;
                float wobble = 1f + 0.22f * (Mathf.PerlinNoise(x * 0.12f + k.Seed, z * 0.12f + k.Seed * 1.7f) - 0.5f);
                float r = Mathf.Sqrt(dx * dx + dz * dz) / (k.Radius * wobble);
                if (r >= 1f) continue;
                float add = k.Height * Mathf.Pow(1f - Smooth(0.45f, 1f, r), 0.75f) * (0.92f + 0.16f * Mathf.PerlinNoise(x * 0.5f + k.Seed, z * 0.5f));
                if (add > 0.05f) h = Mathf.Max(h, WaterY - 1.2f + add);
            }
            return h;
        }

        // How far the walls of an islet bay step back (0 far from the bays, 1 right at them).
        static float BayOpen(float x, float z)
        {
            var p = new Vector2(x, z);
            float open = 0f;
            for (int i = 0; i < Bays.Length; i++) open = Mathf.Max(open, 1f - Smooth(Bays[i].z + 4f, Bays[i].z + 18f, Vector2.Distance(p, Bay(i))));
            return open;
        }

        static float GroundSlope(float x, float z)
        {
            float dx = GroundHeight(x + 1f, z) - GroundHeight(x - 1f, z), dz = GroundHeight(x, z + 1f) - GroundHeight(x, z - 1f);
            return Mathf.Sqrt(dx * dx + dz * dz) * 0.5f;
        }

        // ---------------------------------------------------------------- building the sea

        static Material Emissive(string name, Color color, float intensity)
        {
            Material m = MapDecor.Lit(name, color, 0f, 0.4f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * intensity);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(m);
            return m;
        }

        static void BuildSea(Transform root)
        {
            seaBlockers.Clear();
            Transform sea = Group(root, "Sea");
            Vector2 harbour = HarbourCentre;
            SeaIsland village = SeaIslands[0], lighthouse = SeaIslands[1], shrine = SeaIslands[2];

            // piers: causeway -> harbour -> village -> lighthouse hill ... lighthouse hill -> shrine
            Transform piers = Group(sea, "Piers");
            Vector2 villageIn = Landing(village, harbour), villageOut = Landing(village, lighthouse.Centre);
            Vector2 lightIn = Landing(lighthouse, village.Centre), lightOut = Landing(lighthouse, shrine.Centre);
            Vector2 shrineIn = Landing(shrine, lighthouse.Centre);
            Pier(piers, "Pier_Causeway", new[] { SeaSegments[0], harbour }, 2.8f);
            // lighthouse -> Shark Bay (it stops outside the ring's entrance), and from the bay's way out to the crocodile's island
            Vector2 lightToPool = Landing(lighthouse, PoolEntrance);
            Pier(piers, "Pier_ToSharkBay", Curve(lightToPool, PoolEntrance, -1f), 2.6f);
            Pier(piers, "Pier_FromSharkBay", Curve(PoolExit, CrocodileLanding, 1f), 2.6f);
            Pier(piers, "Pier_ToVillage", Curve(harbour + new Vector2(4.5f, 0f), villageIn, 1f), 2.6f);
            List<Vector2> toLight = Curve(villageOut, lightIn, -1f);
            Pier(piers, "Pier_ToLighthouse", toLight, 2.6f);
            Pier(piers, "Pier_ToShrine", Curve(lightOut, shrineIn, 1f), 2.6f);

            BuildHarbour(sea, harbour);
            BuildFishingVillage(sea, village, harbour, lighthouse.Centre);
            BuildLighthouse(sea, lighthouse);
            BuildSeaShrine(sea, shrine);
            BuildSharkBay(sea);
            BuildSandbars(sea);
            BuildSeaGate(sea, toLight);
            BuildCoral(sea);
            BuildAnchoredBoats(sea);
            SeaVegetation(sea);

            // pale lights drifting over the lagoon
            foreach (SeaIsland isl in SeaIslands)
                MapDecor.Fireflies(sea, new Vector3(isl.Centre.x, WaterY + 2f, isl.Centre.y), new Vector3(34f, 3f, 34f), new Color(0.6f, 0.95f, 1f));
        }

        // The point on an island's shore, towards `toward`, where the ground has dropped to the boards' height.
        static Vector2 Landing(SeaIsland isl, Vector2 toward)
        {
            Vector2 dir = (toward - isl.Centre).normalized;
            float last = 0f;
            for (float t = 0f; t < isl.Radius + isl.Skirt + 4f; t += 0.5f)
            {
                Vector2 p = isl.Centre + dir * t;
                if (GroundHeight(p.x, p.y) < PierDeck - 0.08f) break;
                last = t;
            }
            return isl.Centre + dir * Mathf.Max(0f, last - 0.3f);
        }

        // A gently winding line between two points (two interior points pushed sideways), so the piers are not ruler-straight.
        static List<Vector2> Curve(Vector2 a, Vector2 b, float sign)
        {
            Vector2 d = b - a, side = new Vector2(-d.y, d.x).normalized;
            float off = Mathf.Min(7f, d.magnitude * 0.1f) * sign;
            return new List<Vector2> { a, a + d * 0.33f + side * off, a + d * 0.66f - side * off * 0.6f, b };
        }

        // Boards on posts with a hand rail, a box collider under each straight stretch, a torch every ~30 m.
        static void Pier(Transform parent, string name, IList<Vector2> pts, float width)
        {
            Transform root = MapDecor.Empty(name, parent, Vector3.zero, 0f);
            Material plank = MapDecor.Lit("Mat_Plank", new Color(0.45f, 0.31f, 0.18f), 0f, 0.1f);
            Material wood = MapDecor.Lit("Mat_Wood", new Color(0.33f, 0.22f, 0.12f), 0f, 0.15f);
            float sinceTorch = 24f;
            int torchSide = 1;
            for (int i = 0; i < pts.Count - 1; i++)
            {
                Vector2 a = pts[i], b = pts[i + 1], d = b - a;
                float length = d.magnitude;
                if (length < 0.2f) continue;
                Vector2 dir = d / length, side = new(-dir.y, dir.x);
                Quaternion rotation = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y));

                var deck = new GameObject("Deck").transform;
                deck.SetParent(root, false);
                deck.SetPositionAndRotation(new Vector3((a.x + b.x) * 0.5f, PierDeck - 0.125f, (a.y + b.y) * 0.5f), rotation);
                deck.gameObject.AddComponent<BoxCollider>().size = new Vector3(width, 0.25f, length + 0.5f);

                int boards = Mathf.Max(1, Mathf.CeilToInt(length / 0.62f));
                for (int k = 0; k < boards; k++)
                {
                    Vector2 p = a + d * ((k + 0.5f) / boards);
                    MapDecor.Part(PrimitiveType.Cube, "Board", root, new Vector3(p.x, PierDeck - 0.05f, p.y), new Vector3(width, 0.1f, length / boards * 0.92f), rotation, plank);
                }
                foreach (float sign in new[] { -1f, 1f })
                {
                    Vector2 q0 = a + side * (sign * (width * 0.5f + 0.08f)), q1 = b + side * (sign * (width * 0.5f + 0.08f));
                    Vector3 from = new(q0.x, PierDeck + 0.95f, q0.y), to = new(q1.x, PierDeck + 0.95f, q1.y);
                    MapDecor.Part(PrimitiveType.Cube, "Rail", root, (from + to) * 0.5f, new Vector3(0.07f, 0.07f, length), rotation, wood);
                    for (float t = 0f; t <= length + 0.01f; t += 4f)
                    {
                        Vector2 q = a + dir * Mathf.Min(t, length) + side * (sign * (width * 0.5f + 0.08f));
                        float bottom = GroundHeight(q.x, q.y) - 0.4f, top = PierDeck + 1.0f;
                        if (bottom > top - 0.4f) continue;
                        MapDecor.Part(PrimitiveType.Cylinder, "Post", root, new Vector3(q.x, (top + bottom) * 0.5f, q.y), new Vector3(0.2f, (top - bottom) * 0.5f, 0.2f), Quaternion.identity, wood);
                    }
                }
                sinceTorch += length;
                if (sinceTorch >= 30f)
                {
                    sinceTorch = 0f;
                    Vector2 m = (a + b) * 0.5f + side * (torchSide * (width * 0.5f + 0.5f));
                    MapDecor.Torch(root, new Vector3(m.x, PierDeck - 0.05f, m.y));
                    torchSide = -torchSide;
                }
            }
        }

        // ---------------------------------------------------------------- boats

        // A small junk: planked hull, deck, cabin with a thatch roof, mast with a battened sail, a lantern at the stern.
        // `walkable` gives the deck a box collider (moored boats); `wreck` makes it a half-sunk hull with a broken mast.
        static GameObject Boat(Transform parent, Vector3 water, float yaw, float scale, Color sailColor, bool walkable, bool wreck = false)
        {
            Transform root = MapDecor.Empty(wreck ? "Shipwreck" : "Boat", parent, water, yaw);
            root.localScale = Vector3.one * scale;
            Material hull = MapDecor.Lit("Mat_BoatHull", new Color(0.30f, 0.19f, 0.10f), 0f, 0.2f);
            Material inner = MapDecor.Lit("Mat_BoatDeck", new Color(0.52f, 0.38f, 0.22f), 0f, 0.15f);
            Material wood = MapDecor.Lit("Mat_Wood", new Color(0.33f, 0.22f, 0.12f), 0f, 0.15f);
            Material thatch = MapDecor.Lit("Mat_Thatch", new Color(0.50f, 0.40f, 0.20f), 0f, 0.05f, true);

            MapDecor.Part(PrimitiveType.Cube, "Keel", root, new Vector3(0f, -0.15f, 0f), new Vector3(1.5f, 0.3f, 6.4f), Quaternion.identity, hull);
            foreach (float s in new[] { -1f, 1f })
            {
                MapDecor.Part(PrimitiveType.Cube, "Side", root, new Vector3(s * 1.0f, 0.4f, 0f), new Vector3(0.2f, 1.2f, 6.8f), Quaternion.Euler(0f, 0f, -s * 17f), hull);
                MapDecor.Part(PrimitiveType.Cube, "Gunwale", root, new Vector3(s * 1.3f, 0.98f, 0f), new Vector3(0.14f, 0.12f, 6.8f), Quaternion.identity, wood);
            }
            MapDecor.Part(PrimitiveType.Cube, "Floor", root, new Vector3(0f, 0.2f, 0f), new Vector3(1.9f, 0.1f, 6.2f), Quaternion.identity, inner);
            MapDecor.Part(PrimitiveType.Cube, "Bow", root, new Vector3(0f, 0.75f, 3.45f), new Vector3(1.7f, 0.16f, 1.7f), Quaternion.Euler(-35f, 0f, 0f), hull);
            MapDecor.Part(PrimitiveType.Cube, "Stern", root, new Vector3(0f, 0.65f, -3.4f), new Vector3(1.7f, 0.16f, 1.2f), Quaternion.Euler(28f, 0f, 0f), hull);

            if (wreck)
            {
                MapDecor.Part(PrimitiveType.Cylinder, "BrokenMast", root, new Vector3(0.5f, 1.9f, 0.9f), new Vector3(0.14f, 1.5f, 0.14f), Quaternion.Euler(0f, 0f, 24f), wood);
                MapDecor.Part(PrimitiveType.Cube, "Rib", root, new Vector3(0f, 0.9f, -1.2f), new Vector3(2.1f, 0.12f, 0.12f), Quaternion.identity, wood);
                MapDecor.Part(PrimitiveType.Cube, "Rib", root, new Vector3(0f, 0.9f, 1.6f), new Vector3(2.1f, 0.12f, 0.12f), Quaternion.identity, wood);
            }
            else
            {
                MapDecor.Part(PrimitiveType.Cylinder, "Mast", root, new Vector3(0f, 2.8f, 0.9f), new Vector3(0.14f, 2.8f, 0.14f), Quaternion.identity, wood);
                Transform sail = MapDecor.Empty("Sail", root, Vector3.zero, 0f);
                sail.localPosition = new Vector3(0f, 0f, 0.9f);
                sail.localRotation = Quaternion.Euler(0f, 8f, 0f);
                MapDecor.Part(PrimitiveType.Quad, "Cloth", sail, new Vector3(0f, 3.4f, -1.6f), new Vector3(3.2f, 4.2f, 1f), Quaternion.Euler(0f, 90f, 0f),
                    MapDecor.Lit("Mat_Sail_" + ColorUtility.ToHtmlStringRGB(sailColor), sailColor, 0f, 0.1f, true));
                foreach (float y in new[] { 1.9f, 2.9f, 3.9f, 4.9f })
                    MapDecor.Part(PrimitiveType.Cube, "Batten", sail, new Vector3(0f, y, -1.6f), new Vector3(0.05f, 0.07f, 3.2f), Quaternion.identity, wood);
                MapDecor.Part(PrimitiveType.Cylinder, "Boom", sail, new Vector3(0f, 1.35f, -1.6f), new Vector3(0.06f, 1.7f, 0.06f), Quaternion.Euler(90f, 0f, 0f), wood);

                MapDecor.Part(PrimitiveType.Cube, "Cabin", root, new Vector3(0f, 0.95f, -2.0f), new Vector3(1.5f, 1.0f, 1.8f), Quaternion.identity, inner);
                MapDecor.Part(PrimitiveType.Cube, "CabinRoof", root, new Vector3(0f, 1.5f, -2.0f), new Vector3(1.9f, 0.1f, 2.3f), Quaternion.identity, thatch);
                MapDecor.Part(PrimitiveType.Sphere, "Lantern", root, new Vector3(0f, 1.55f, -3.35f), Vector3.one * 0.24f, Quaternion.identity, Emissive("Mat_Lantern", new Color(1f, 0.78f, 0.35f), 2.2f));
            }

            if (walkable)
            {
                var box = root.gameObject.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 0.25f, 0f);
                box.size = new Vector3(2.1f, 0.5f, 6.8f);
            }
            return root.gameObject;
        }

        // ---------------------------------------------------------------- places

        // A square deck on posts at the end of the first pier with three junks tied alongside, crates, barrels and a banner.
        static void BuildHarbour(Transform sea, Vector2 c)
        {
            Transform group = Group(sea, "Harbour");
            Material plank = MapDecor.Lit("Mat_Plank", new Color(0.45f, 0.31f, 0.18f), 0f, 0.1f);
            Material wood = MapDecor.Lit("Mat_Wood", new Color(0.33f, 0.22f, 0.12f), 0f, 0.15f);
            const float size = 13f;
            var deck = new GameObject("Deck").transform;
            deck.SetParent(group, false);
            deck.position = new Vector3(c.x, PierDeck - 0.125f, c.y);
            deck.gameObject.AddComponent<BoxCollider>().size = new Vector3(size, 0.25f, size);
            for (int k = 0; k < 15; k++)
                MapDecor.Part(PrimitiveType.Cube, "Board", group, new Vector3(c.x, PierDeck - 0.05f, c.y - size * 0.5f + (k + 0.5f) * size / 15f), new Vector3(size, 0.1f, size / 15f * 0.92f), Quaternion.identity, plank);
            foreach (float sx in new[] { -1f, 0f, 1f })
                foreach (float sz in new[] { -1f, 0f, 1f })
                {
                    float x = c.x + sx * (size * 0.5f - 0.3f), z = c.y + sz * (size * 0.5f - 0.3f);
                    float bottom = GroundHeight(x, z) - 0.4f, top = PierDeck + (sx != 0f && sz != 0f ? 1.5f : 0f);
                    MapDecor.Part(PrimitiveType.Cylinder, "Post", group, new Vector3(x, (top + bottom) * 0.5f, z), new Vector3(0.28f, (top - bottom) * 0.5f, 0.28f), Quaternion.identity, wood);
                }
            MapDecor.Banner(group, new Vector3(c.x + 3.6f, PierDeck, c.y + 3.6f), 0f, new Color(0.10f, 0.45f, 0.55f));
            MapDecor.Torch(group, new Vector3(c.x - 3.8f, PierDeck, c.y + 3.8f));
            MapDecor.Torch(group, new Vector3(c.x + 3.8f, PierDeck, c.y - 3.8f));

            // crates and barrels in a corner
            Material crate = MapDecor.Lit("Mat_Crate", new Color(0.50f, 0.36f, 0.20f), 0f, 0.1f);
            MapDecor.Part(PrimitiveType.Cube, "Crate", group, new Vector3(c.x - 3.3f, PierDeck + 0.45f, c.y - 3.4f), new Vector3(0.9f, 0.9f, 0.9f), Quaternion.Euler(0f, 18f, 0f), crate, true);
            MapDecor.Part(PrimitiveType.Cube, "Crate", group, new Vector3(c.x - 2.2f, PierDeck + 0.35f, c.y - 3.6f), new Vector3(0.7f, 0.7f, 0.7f), Quaternion.Euler(0f, -9f, 0f), crate, true);
            MapDecor.Part(PrimitiveType.Cube, "Crate", group, new Vector3(c.x - 3.1f, PierDeck + 1.3f, c.y - 3.3f), new Vector3(0.7f, 0.7f, 0.7f), Quaternion.Euler(0f, 40f, 0f), crate, true);
            MapDecor.Part(PrimitiveType.Cylinder, "Barrel", group, new Vector3(c.x - 3.6f, PierDeck + 0.5f, c.y - 2.3f), new Vector3(0.7f, 0.5f, 0.7f), Quaternion.identity, wood, true);

            // junks moored to the sides; their decks sit a little under the boards
            float deckBelow = 0.3f;   // boat origin so that its deck top is at WaterY + deckBelow
            Boat(group, new Vector3(c.x, WaterY - 0.45f + deckBelow - 0.05f, c.y + size * 0.5f + 1.9f), 4f, 1f, new Color(0.88f, 0.80f, 0.62f), true);
            Boat(group, new Vector3(c.x, WaterY - 0.45f + deckBelow - 0.05f, c.y - size * 0.5f - 1.9f), -6f, 1.05f, new Color(0.78f, 0.28f, 0.22f), true);
            Boat(group, new Vector3(c.x + size * 0.5f + 1.9f, WaterY - 0.45f + deckBelow - 0.05f, c.y + 0.5f), 94f, 0.95f, new Color(0.30f, 0.60f, 0.65f), true);
            seaBlockers.Add((c, 9f));
        }

        // Five stilt houses round a campfire on the village island, nets and drying racks, two boats pulled up on the beach.
        static void BuildFishingVillage(Transform sea, SeaIsland isl, Vector2 towardHarbour, Vector2 towardLighthouse)
        {
            Transform group = Group(sea, "FishingVillage");
            Vector2 c = isl.Centre;
            float angHarbour = Mathf.Atan2(towardHarbour.x - c.x, towardHarbour.y - c.y) * Mathf.Rad2Deg;
            float angLight = Mathf.Atan2(towardLighthouse.x - c.x, towardLighthouse.y - c.y) * Mathf.Rad2Deg;
            Color teal = new(0.10f, 0.45f, 0.55f), pale = new(0.80f, 0.86f, 0.90f);

            int houses = 0;
            for (int slot = 0; slot < 12 && houses < 5; slot++)
            {
                float ang = slot * 30f + 15f;
                if (Mathf.Abs(Mathf.DeltaAngle(ang, angHarbour)) < 38f || Mathf.Abs(Mathf.DeltaAngle(ang, angLight)) < 38f) continue;
                if (houses > 0 && slot % 2 == 1) continue;   // skip every other slot: room between the houses
                float a = ang * Mathf.Deg2Rad;
                Vector2 dir = new(Mathf.Sin(a), Mathf.Cos(a));
                Vector2 p = c + dir * (isl.Radius - 4.5f);
                float y = GroundHeight(p.x, p.y);
                MapDecor.StiltHouse(group, new Vector3(p.x, y + 0.2f, p.y), ang);   // the door (-Z) faces the middle of the island
                seaBlockers.Add((p, 6.5f));
                Vector2 t = p + dir * 0.8f + new Vector2(dir.y, -dir.x) * 4.6f;
                MapDecor.Torch(group, Ground(t.x, t.y));
                houses++;
            }

            MapDecor.Campfire(group, Ground(c.x, c.y));
            seaBlockers.Add((c, 3f));
            MapDecor.Banner(group, Ground(c.x + 3.2f, c.y - 2.2f), 0f, teal);
            MapDecor.Banner(group, Ground(c.x - 3.4f, c.y + 2.6f), 0f, pale);

            // drying racks with fish
            Material wood = MapDecor.Lit("Mat_Wood", new Color(0.33f, 0.22f, 0.12f), 0f, 0.15f);
            Material fish = MapDecor.Lit("Mat_Fish", new Color(0.72f, 0.78f, 0.82f), 0.5f, 0.6f);
            foreach (var (dx, dz, yaw) in new[] { (-2.2f, -3.4f, 20f), (3.8f, 3.8f, -35f) })
            {
                Vector3 b = Ground(c.x + dx, c.y + dz);
                Transform rack = MapDecor.Empty("FishRack", group, b, yaw);
                foreach (float s in new[] { -1f, 1f })
                    MapDecor.Part(PrimitiveType.Cylinder, "Post", rack, new Vector3(s * 1.4f, 0.9f, 0f), new Vector3(0.12f, 0.95f, 0.12f), Quaternion.identity, wood);
                MapDecor.Part(PrimitiveType.Cylinder, "Bar", rack, new Vector3(0f, 1.75f, 0f), new Vector3(0.06f, 1.5f, 0.06f), Quaternion.Euler(0f, 0f, 90f), wood);
                for (int k = 0; k < 7; k++)
                    MapDecor.Part(PrimitiveType.Sphere, "Fish", rack, new Vector3(-1.1f + k * 0.37f, 1.3f, 0f), new Vector3(0.12f, 0.42f, 0.06f), Quaternion.Euler(0f, k * 11f, 0f), fish);
                seaBlockers.Add((new Vector2(b.x, b.z), 2.6f));
            }

            // two boats drawn up on the beach on the far side from the piers
            float away = angHarbour + 180f;
            for (int k = 0; k < 2; k++)
            {
                float a = (away + (k == 0 ? -28f : 28f)) * Mathf.Deg2Rad;
                Vector2 p = c + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * (isl.Radius + 3.5f);
                float y = GroundHeight(p.x, p.y);
                Boat(group, new Vector3(p.x, Mathf.Max(y, WaterY - 0.3f) + 0.15f, p.y), away + (k == 0 ? -28f : 28f) + 90f, 0.9f, k == 0 ? pale : teal, false);
            }
        }

        // A white and teal tower on the hill, a lamp room with a roof and a strong warm light that shows through the mist from everywhere.
        static void BuildLighthouse(Transform sea, SeaIsland isl)
        {
            Transform group = Group(sea, "Lighthouse");
            Vector3 b = Ground(isl.Centre.x, isl.Centre.y);
            Transform root = MapDecor.Empty("LighthouseTower", group, b, 0f);
            Material stone = MapDecor.Lit("Mat_LighthouseStone", new Color(0.62f, 0.60f, 0.56f), 0f, 0.15f);
            Material white = MapDecor.Lit("Mat_LighthouseWhite", new Color(0.90f, 0.90f, 0.86f), 0f, 0.2f);
            Material teal = MapDecor.Lit("Mat_LighthouseTeal", new Color(0.10f, 0.45f, 0.55f), 0f, 0.2f);

            MapDecor.Part(PrimitiveType.Cylinder, "Base", root, new Vector3(0f, 0.7f, 0f), new Vector3(7.4f, 0.7f, 7.4f), Quaternion.identity, stone);
            float y = 1.4f;
            for (int i = 0; i < 5; i++)
            {
                float radius = Mathf.Lerp(2.7f, 1.9f, i / 4f);
                MapDecor.Part(PrimitiveType.Cylinder, "Tower", root, new Vector3(0f, y + 1.3f, 0f), new Vector3(radius * 2f, 1.3f, radius * 2f), Quaternion.identity, i % 2 == 0 ? white : teal);
                y += 2.6f;
            }
            MapDecor.Part(PrimitiveType.Cylinder, "Gallery", root, new Vector3(0f, y + 0.15f, 0f), new Vector3(5.4f, 0.15f, 5.4f), Quaternion.identity, stone);
            MapDecor.Part(PrimitiveType.Cylinder, "LampRoom", root, new Vector3(0f, y + 1.3f, 0f), new Vector3(2.6f, 1.15f, 2.6f), Quaternion.identity, Emissive("Mat_LighthouseLamp", new Color(1f, 0.9f, 0.6f), 2.6f));
            MapDecor.Roof(root, new Vector3(0f, y + 2.5f, 0f), 3.6f, 3.6f, 1.7f, new Color(0.10f, 0.40f, 0.50f));
            MapDecor.Part(PrimitiveType.Sphere, "Finial", root, new Vector3(0f, y + 4.4f, 0f), Vector3.one * 0.4f, Quaternion.identity, MapDecor.Lit("Mat_Gold", new Color(0.93f, 0.72f, 0.20f), 0.8f, 0.6f));

            var lamp = new GameObject("Beacon").transform;
            lamp.SetParent(root, false);
            lamp.localPosition = new Vector3(0f, y + 1.3f, 0f);
            var light = lamp.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.9f, 0.65f);
            light.range = 70f;
            light.intensity = 9f;
            light.shadows = LightShadows.None;

            var collider = root.gameObject.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0f, 7f, 0f);
            collider.radius = 2.8f;
            collider.height = 14f;
            seaBlockers.Add((isl.Centre, 6f));

            // torches on the way up from the sea road
            Vector2 toward = (SeaIslands[0].Centre - isl.Centre).normalized;
            for (int i = 1; i <= 3; i++)
            {
                Vector2 p = isl.Centre + toward * (isl.Radius + i * 5.5f);
                if (GroundHeight(p.x, p.y) > WaterY + 0.5f) MapDecor.Torch(group, Ground(p.x, p.y));
            }
            MapDecor.Banner(group, Ground(isl.Centre.x + 5.2f, isl.Centre.y + 3.2f), 0f, new Color(0.10f, 0.45f, 0.55f));
        }

        // The last island: a stone gateway over glowing crystals, a ring of standing stones, a camp. The end of the sea road.
        static void BuildSeaShrine(Transform sea, SeaIsland isl)
        {
            Transform group = Group(sea, "SeaShrine");
            GameObject rock = Load("P_fwOF_Rock_01.prefab");
            Vector2 c = isl.Centre;
            Vector2 toward = (SeaIslands[1].Centre - c).normalized, side = new(-toward.y, toward.x);
            Color teal = new(0.30f, 0.85f, 0.85f), blue = new(0.45f, 0.62f, 1f), violet = new(0.80f, 0.45f, 1f);

            if (rock != null)
            {
                // the gateway on the far side from the pier: two tall stones and a lintel across them
                Vector2 gate = c - toward * 8.5f;
                float pillarTop = 0f;
                foreach (float s in new[] { -1f, 1f })
                {
                    Vector2 p = gate + side * (s * 3.6f);
                    float top = StandingTop(p.x, p.y, 8.5f);
                    pillarTop = Mathf.Max(pillarTop, top);
                    PlaceWithTop(rock, p.x, p.y, top, 90f + s * 10f, new Vector3(3.2f, 8.5f, 3.2f), group);
                }
                PlaceWithTop(rock, gate.x, gate.y, pillarTop + 0.6f, Mathf.Atan2(side.x, side.y) * Mathf.Rad2Deg, new Vector3(11f, 2.4f, 4.4f), group);
                MapDecor.Crystals(group, Ground(gate.x, gate.y) - Vector3.up * 0.05f, 12, blue, 2.2f);
                MapDecor.Crystals(group, Ground(gate.x + side.x * 3.2f, gate.y + side.y * 3.2f + 2.4f) - Vector3.up * 0.05f, 6, violet, 1.4f);
                MapDecor.Crystals(group, Ground(gate.x - side.x * 3.4f, gate.y - side.y * 3.4f - 2.2f) - Vector3.up * 0.05f, 6, teal, 1.4f);
                seaBlockers.Add((gate, 9f));

                // a ring of standing stones round the middle, open towards the pier
                for (int i = 0; i < 9; i++)
                {
                    float a = (i * 40f + 20f) * Mathf.Deg2Rad;
                    Vector2 dir = new(Mathf.Sin(a), Mathf.Cos(a));
                    if (Vector2.Dot(dir, toward) > 0.55f) continue;
                    Vector2 p = c + dir * (isl.Radius - 3f);
                    PlaceWithTop(rock, p.x, p.y, StandingTop(p.x, p.y, 3.4f), R(0f, 360f), new Vector3(1.8f, 3.4f, 1.8f), group);
                    seaBlockers.Add((p, 2.6f));
                }
            }
            MapDecor.Campfire(group, Ground(c.x + toward.x * 4f, c.y + toward.y * 4f));
            MapDecor.Torch(group, Ground(c.x + toward.x * 9f + side.x * 3.5f, c.y + toward.y * 9f + side.y * 3.5f));
            MapDecor.Torch(group, Ground(c.x + toward.x * 9f - side.x * 3.5f, c.y + toward.y * 9f - side.y * 3.5f));
            MapDecor.Banner(group, Ground(c.x + side.x * 6f, c.y + side.y * 6f), 0f, new Color(0.10f, 0.45f, 0.55f));
            MapDecor.Banner(group, Ground(c.x - side.x * 6f, c.y - side.y * 6f), 0f, new Color(0.80f, 0.86f, 0.90f));
            seaBlockers.Add((c + toward * 4f, 3f));
        }

        // Shark Bay's dressing: torches and a banner at the entrance, glowing crystals at the foot of the wall inside, a few reef rocks.
        // The fight itself is ThuyTinhSharkEvent (built by ThuyTinhSharkEventBuilder).
        static void BuildSharkBay(Transform sea)
        {
            Transform group = Group(sea, "SharkBay");
            Vector2 c = PoolCentre, entrance = PoolEntrance, side = new(-PoolEntranceDir.y, PoolEntranceDir.x);
            foreach (float s in new[] { -1f, 1f })
                MapDecor.Torch(group, new Vector3(entrance.x + side.x * s * 3.2f, PierDeck, entrance.y + side.y * s * 3.2f));
            MapDecor.Banner(group, new Vector3(entrance.x + side.x * 3.2f - PoolEntranceDir.x * 1.2f, PierDeck, entrance.y + side.y * 3.2f - PoolEntranceDir.y * 1.2f), 0f, new Color(0.10f, 0.45f, 0.55f));
            Color[] glow = { new(0.45f, 0.62f, 1f), new(0.30f, 0.85f, 0.85f), new(0.80f, 0.45f, 1f) };
            for (int i = 0; i < 3; i++)
            {
                float a = (60f + i * 100f) * Mathf.Deg2Rad;
                Vector2 dir = new(Mathf.Sin(a), Mathf.Cos(a));
                if (Vector2.Angle(dir, PoolEntranceDir) < 30f || Vector2.Angle(dir, PoolExitDir) < 30f) continue;
                Vector2 p = c + dir * (PoolRadius + 1.5f);
                MapDecor.Crystals(group, new Vector3(p.x, GroundHeight(p.x, p.y) - 0.05f, p.y), 7, glow[i], 1.5f);
            }
            seaBlockers.Add((c, PoolRingRadius + 6f));
        }

        // The wreck on one sandbar, a lone tree and reeds on the others.
        static void BuildSandbars(Transform sea)
        {
            Transform group = Group(sea, "Sandbars");
            SeaIsland wreckBar = SeaIslands[3];
            Vector2 c = wreckBar.Centre;
            Boat(group, new Vector3(c.x, GroundHeight(c.x, c.y) - 0.15f, c.y), 35f, 1.25f, Color.white, false, true).transform.rotation *= Quaternion.Euler(6f, 0f, 14f);
            seaBlockers.Add((c, 5f));
        }

        // A village gate straddling the pier between the village and the lighthouse.
        static void BuildSeaGate(Transform sea, List<Vector2> pier)
        {
            int i = pier.Count / 2;
            Vector2 a = pier[i - 1], b = pier[i], mid = (a + b) * 0.5f, dir = (b - a).normalized;
            MapDecor.Gate(Group(sea, "Gate"), new Vector3(mid.x, WaterY - 0.5f, mid.y), Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg, 5.2f, new Color(0.10f, 0.45f, 0.55f));
        }

        // Anchored junks out in the lagoon (decoration).
        static void BuildAnchoredBoats(Transform sea)
        {
            Transform group = Group(sea, "Boats");
            Color[] sails = { new(0.88f, 0.80f, 0.62f), new(0.78f, 0.28f, 0.22f), new(0.30f, 0.60f, 0.65f), new(0.92f, 0.90f, 0.84f) };
            int made = 0;
            for (int tries = 0; tries < 400 && made < 7; tries++)
            {
                float z = R(60f, 220f), x = R(PathX(z) + 55f, 245f);
                var p = new Vector2(x, z);
                if (GroundHeight(x, z) > WaterY - 0.9f || DistanceToRoute(p) < 14f || Vector2.Distance(p, PoolCentre) < PoolRingRadius + 10f) continue;
                if (GroundHeight(x + 5f, z) > WaterY - 0.5f || GroundHeight(x - 5f, z) > WaterY - 0.5f || GroundHeight(x, z + 5f) > WaterY - 0.5f || GroundHeight(x, z - 5f) > WaterY - 0.5f) continue;
                Boat(group, new Vector3(x, WaterY - 0.4f, z), R(0f, 360f), R(1.1f, 1.5f), sails[made % sails.Length], false);
                made++;
            }
        }

        // Pink, orange, violet and teal coral in the shallows round the islands and under the piers.
        static void BuildCoral(Transform sea)
        {
            Transform group = Group(sea, "Coral");
            Color[] palette = { new(0.95f, 0.45f, 0.55f), new(0.98f, 0.60f, 0.25f), new(0.65f, 0.40f, 0.85f), new(0.30f, 0.80f, 0.75f) };
            var materials = new Material[palette.Length];
            for (int i = 0; i < palette.Length; i++) materials[i] = Emissive("Mat_Coral_" + i, palette[i], 0.35f);

            int made = 0;
            for (int tries = 0; tries < 12000 && made < 90; tries++)
            {
                SeaIsland isl = SeaIslands[Mathf.Min(SeaIslands.Length - 1, (int)R(0f, SeaIslands.Length))];
                float a = R(0f, Mathf.PI * 2f), r = R(isl.Radius * 0.8f, isl.Radius + isl.Skirt * 0.9f);
                Vector2 p = isl.Centre + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * r;
                float y = GroundHeight(p.x, p.y);
                if (y > WaterY - 0.45f || y < WaterY - 1.7f || DistanceToRoute(p) < 3f) continue;

                Material m = materials[made % materials.Length];
                Transform cluster = MapDecor.Empty("CoralCluster", group, new Vector3(p.x, y, p.y), R(0f, 360f));
                MapDecor.Part(PrimitiveType.Sphere, "Mound", cluster, new Vector3(0f, 0.05f, 0f), new Vector3(R(0.9f, 1.5f), R(0.45f, 0.8f), R(0.9f, 1.5f)), Quaternion.identity, m);
                int stems = (int)R(4f, 8f);
                for (int s = 0; s < stems; s++)
                {
                    float sa = s * 360f / stems + R(-20f, 20f), lean = R(12f, 38f), h = R(0.5f, 1.1f);
                    Quaternion q = Quaternion.Euler(0f, sa, 0f) * Quaternion.Euler(lean, 0f, 0f);
                    MapDecor.Part(PrimitiveType.Capsule, "Branch", cluster, Quaternion.Euler(0f, sa, 0f) * new Vector3(0f, 0.25f + h * 0.4f, 0.22f), new Vector3(0.14f, h, 0.14f), q, m);
                }
                made++;
            }
        }

        // Trees, reeds and stones on the islands, kept clear of the houses, the piers and the shrine.
        static void SeaVegetation(Transform sea)
        {
            GameObject tree = Load("P_fwOF_Tree_M_2.prefab"), sapling = Load("P_fwOF_TreeSapling_02B.prefab"), grass = Load("P_fwOF_Grass_M_1.prefab"),
                stone = Load("P_fwOF_Stone_01.prefab"), plant = Load("P_fwOF_ForestPlant_B_02.prefab");
            Transform trees = Group(sea, "IslandTrees"), small = Group(sea, "IslandUndergrowth");

            bool Blocked(Vector2 p)
            {
                if (DistanceToRoute(p) < 3.5f) return true;
                foreach (var (centre, radius) in seaBlockers) if (Vector2.Distance(p, centre) < radius) return true;
                return false;
            }

            foreach (SeaIsland isl in SeaIslands)
            {
                bool big = isl.Radius >= 9f && isl.Name != "SeaShrine";
                int treeCount = isl.Name == "Sandbar_Wreck" ? 0 : isl.Name == "Sandbar_Reeds" ? 0 : isl.Name == "Sandbar_Tree" ? 2 : isl.Name == "Lighthouse" ? 6 : isl.Name == "FishingVillage" ? 7 : 5;
                for (int tries = 0, made = 0; tries < 600 && made < treeCount && tree != null; tries++)
                {
                    // the lighthouse hill keeps its top clear so the tower shows: trees only on the lower slopes
                    float a = R(0f, Mathf.PI * 2f), r = isl.Name == "Lighthouse" ? R(isl.Radius + 9f, isl.Radius + isl.Skirt * 0.45f) : R(isl.Radius * 0.4f, isl.Radius + isl.Skirt * 0.12f);
                    Vector2 p = isl.Centre + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * r;
                    float y = GroundHeight(p.x, p.y);
                    if (y < WaterY + 0.55f || Blocked(p) || GroundSlope(p.x, p.y) > 0.45f) continue;
                    Place(tree, new Vector3(p.x, y - 0.2f, p.y), R(0f, 360f), Vector3.one * R(0.8f, 1.2f), trees, false);
                    seaBlockers.Add((p, 3f));
                    made++;
                }
                int smallCount = big ? 70 : 40;
                for (int tries = 0, made = 0; tries < 1200 && made < smallCount; tries++)
                {
                    float a = R(0f, Mathf.PI * 2f), r = R(isl.Radius * 0.3f, isl.Radius + isl.Skirt * 0.35f);
                    Vector2 p = isl.Centre + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * r;
                    float y = GroundHeight(p.x, p.y);
                    bool shallows = y < WaterY + 0.25f && y > WaterY - 0.8f;
                    if (Blocked(p) || (!shallows && y < WaterY + 0.3f)) continue;
                    GameObject prefab = shallows ? grass : (made % 4 == 0 ? stone : made % 4 == 1 ? sapling : made % 4 == 2 ? plant : grass);
                    if (prefab == null) continue;
                    float scale = prefab == grass ? R(2.4f, 3.6f) : prefab == stone ? R(1.2f, 2.2f) : prefab == sapling ? R(1.6f, 3f) : R(2f, 3.5f);
                    Place(prefab, new Vector3(p.x, y, p.y), R(0f, 360f), Vector3.one * scale, small, true);
                    made++;
                }
            }
        }
    }
}
