using System;
using System.Collections.Generic;
using SonTinhThuyTinh.Quest;
using UnityEngine;

namespace SonTinhThuyTinh.UI.Map
{
    // Ties the ground of one gameplay scene to the world map picture. The picture is a top-down view of the real scenes (captured by
    // Assets/Editor/WorldMapComposer.cs), so the link is a plain shift + scale: world (x, z) -> picture position. One world point and
    // the picture position it lands on (as fractions of the picture, from the top-left corner) plus the fraction one metre covers
    // are enough. World +x is right on the picture and world +z is up. Set by MapHudBuilder.
    public class MapRoute : MonoBehaviour
    {
        [Serializable]
        public struct GiftPin
        {
            public GiftItem gift;
            public Vector2 map;
        }

        [SerializeField] Vector2 worldReference;
        [SerializeField] Vector2 mapReference;
        [Tooltip("Fraction of the picture width (x) and height (y) that one metre covers.")]
        [SerializeField] Vector2 mapPerMeter = new(0.002f, 0.002f);
        [SerializeField] GiftPin[] giftPins;

        readonly List<Vector2> roadNodes = new();
        readonly List<List<int>> roadLinks = new();

        public IReadOnlyList<GiftPin> GiftPins => giftPins;

        void Awake() => BuildRoadGraph();

        public bool TryGetGiftPin(GiftItem gift, out Vector2 position)
        {
            if (giftPins != null)
            {
                foreach (GiftPin pin in giftPins)
                {
                    if (pin.gift != gift) continue;
                    position = pin.map;
                    return true;
                }
            }

            position = default;
            return false;
        }

        public bool TryGetGiftWorld(GiftItem gift, out Vector3 position)
        {
            if (TryGetGiftPin(gift, out Vector2 mapPosition))
            {
                Vector2 world = MapToWorld(mapPosition);
                position = new Vector3(world.x, 0f, world.y);
                return true;
            }

            position = default;
            return false;
        }

        // Finds the shortest route through the actual roads used by the map builders. The returned points are map fractions.
        public bool TryBuildRoadPath(Vector3 from, Vector3 to, List<Vector2> mapPath)
        {
            mapPath.Clear();
            if (roadNodes.Count == 0) BuildRoadGraph();
            if (roadNodes.Count == 0) return false;

            Vector2 start = new(from.x, from.z);
            Vector2 goal = new(to.x, to.z);
            int startNode = ClosestNode(start);
            int goalNode = ClosestNode(goal);
            int count = roadNodes.Count;
            var distance = new float[count];
            var previous = new int[count];
            var visited = new bool[count];
            for (int i = 0; i < count; i++) { distance[i] = float.PositiveInfinity; previous[i] = -1; }
            distance[startNode] = 0f;

            for (int step = 0; step < count; step++)
            {
                int current = -1;
                float best = float.PositiveInfinity;
                for (int i = 0; i < count; i++)
                    if (!visited[i] && distance[i] < best) { best = distance[i]; current = i; }
                if (current < 0 || current == goalNode) break;
                visited[current] = true;
                foreach (int neighbour in roadLinks[current])
                {
                    float candidate = distance[current] + Vector2.Distance(roadNodes[current], roadNodes[neighbour]);
                    if (candidate >= distance[neighbour]) continue;
                    distance[neighbour] = candidate;
                    previous[neighbour] = current;
                }
            }

            var reverse = new List<int>();
            for (int node = goalNode; node >= 0; node = previous[node])
            {
                reverse.Add(node);
                if (node == startNode) break;
            }
            if (reverse[reverse.Count - 1] != startNode) return false;

            mapPath.Add(WorldToMap(start));
            for (int i = reverse.Count - 1; i >= 0; i--) mapPath.Add(WorldToMap(roadNodes[reverse[i]]));
            mapPath.Add(WorldToMap(goal));
            return true;
        }

        // Where the picture shows a player standing at `world`, and the clockwise angle (from "up" on the picture) the arrow points at
        // for a player facing `yaw` degrees (up on the picture is world +z, so the two angles are the same).
        public bool TryLocate(Vector3 world, float yaw, out Vector2 map, out float arrowAngle)
        {
            map = WorldToMap(new Vector2(world.x, world.z));
            arrowAngle = yaw;
            return true;
        }

        Vector2 WorldToMap(Vector2 world)
        {
            Vector2 result = mapReference + new Vector2((world.x - worldReference.x) * mapPerMeter.x, -(world.y - worldReference.y) * mapPerMeter.y);
            return new Vector2(Mathf.Clamp01(result.x), Mathf.Clamp01(result.y));
        }

        Vector2 MapToWorld(Vector2 map) => new(
            worldReference.x + (map.x - mapReference.x) / mapPerMeter.x,
            worldReference.y - (map.y - mapReference.y) / mapPerMeter.y);

        void BuildRoadGraph()
        {
            roadNodes.Clear();
            roadLinks.Clear();
            bool sonTinh = mapReference.x < 0.5f;

            int previous = -1;
            for (float z = 0f; z <= 295f; z += 8f)
            {
                int node = AddNode(new Vector2(MainPathX(z, sonTinh), z));
                if (previous >= 0) Connect(previous, node);
                previous = node;
            }

            if (sonTinh) AddSonTinhMountainRoad();
            else AddThuyTinhSeaRoad();
        }

        void AddSonTinhMountainRoad()
        {
            // All fourteen centre-line nodes used by SonTinhMountainBranch. Joining only the named checkpoints cuts diagonally
            // through the forest, especially on the long ledge between LedgeStart and CanyonStart.
            Vector2[] points =
            {
                new(3.1387f, 70f), new(-28.8613f, 74f), new(-34.8613f, 90f), new(-40.8613f, 106f),
                new(-46.8613f, 122f), new(-49.8613f, 136f), new(-79.8613f, 142f), new(-107.8613f, 138f),
                new(-137.8613f, 144f), new(-153.8613f, 146f), new(-183.8613f, 150f), new(-207.8613f, 156f),
                new(-233.8613f, 168f), new(-255.8613f, 182f)
            };

            string[] markerNames = { "Anchor_LedgeStart", "Anchor_CanyonStart", "Anchor_Steps", "Anchor_BeforePit", "Anchor_AfterPit", "Anchor_BeastClearing", "Anchor_Summit" };
            int[] markerIndices = { 0, 5, 7, 9, 10, 11, 13 };
            for (int i = 0; i < markerNames.Length; i++)
            {
                GameObject marker = GameObject.Find(markerNames[i]);
                if (marker != null) points[markerIndices[i]] = new Vector2(marker.transform.position.x, marker.transform.position.z);
            }

            int last = ClosestNode(points[0]);
            foreach (Vector2 point in points)
            {
                int node = AddNode(point);
                Connect(last, node);
                last = node;
            }
        }

        void AddThuyTinhSeaRoad()
        {
            Vector2 G(float z, float lateral = 0f) => new(MainPathX(z, false) + lateral, z);
            Vector2 grotto = G(135f, 32f);
            Vector2 harbour = new(MainPathX(108f, false) + 40f, 112f);
            Vector2 village = grotto + new Vector2(66f, -16f);
            Vector2 lighthouse = village + new Vector2(62f, 40f);
            Vector2 shrine = lighthouse + new Vector2(58f, -62f);
            Vector2 pool = new(78f, 192f);
            Vector2 crocodile = new(MainPathX(175f, false), 175f);
            Vector2 poolEntrance = pool + (lighthouse - pool).normalized * 28f;
            Vector2 poolExit = pool + (crocodile - pool).normalized * 28f;
            Vector2 crocodileLanding = crocodile + (pool - crocodile).normalized * 17.2f;

            int harbourNode = AddNode(harbour);
            Connect(ClosestNode(G(108f)), harbourNode);
            int villageNode = AddNode(village); Connect(harbourNode, villageNode);
            int lighthouseNode = AddNode(lighthouse); Connect(villageNode, lighthouseNode);
            int shrineNode = AddNode(shrine); Connect(lighthouseNode, shrineNode);
            int entranceNode = AddNode(poolEntrance); Connect(lighthouseNode, entranceNode);
            int poolNode = AddNode(pool); Connect(entranceNode, poolNode);
            int exitNode = AddNode(poolExit); Connect(poolNode, exitNode);
            int landingNode = AddNode(crocodileLanding); Connect(exitNode, landingNode);
            Connect(landingNode, ClosestNode(crocodile));
        }

        int AddNode(Vector2 point)
        {
            roadNodes.Add(point);
            roadLinks.Add(new List<int>());
            return roadNodes.Count - 1;
        }

        void Connect(int a, int b)
        {
            if (!roadLinks[a].Contains(b)) roadLinks[a].Add(b);
            if (!roadLinks[b].Contains(a)) roadLinks[b].Add(a);
        }

        int ClosestNode(Vector2 point)
        {
            int closest = 0;
            float best = float.PositiveInfinity;
            for (int i = 0; i < roadNodes.Count; i++)
            {
                float distance = (roadNodes[i] - point).sqrMagnitude;
                if (distance >= best) continue;
                best = distance;
                closest = i;
            }
            return closest;
        }

        static float MainPathX(float z, bool sonTinh)
        {
            float t = Mathf.Clamp01(z / 50f);
            float ramp = t * t * (3f - 2f * t);
            return sonTinh
                ? ramp * (15f * Mathf.Sin(z * 0.021f) + 6f * Mathf.Sin(z * 0.058f + 1.3f))
                : ramp * (13f * Mathf.Sin(z * 0.02f + 0.6f) + 7f * Mathf.Sin(z * 0.055f + 2.1f));
        }
    }
}
