using System.Collections.Generic;
using UnityEngine;

namespace SonTinhThuyTinh.Player
{
    // A water plane that a player with WaterWalker (Thủy Tinh) can stand and run on. It adds a thin invisible floor (BoxCollider, layer
    // Ignore Raycast so aiming and ground checks of other systems miss it) at the water's height over the plane's area, switched on only
    // while the active player has WaterWalker: anyone else sinks as before. Over dry land the terrain is higher than the floor, so it
    // never gets in the way there. Added to Map_ThuyTinh's "Water" by Assets/Editor/WaterWalkBuilder.cs.
    [RequireComponent(typeof(Renderer))]
    public class WaterSurface : MonoBehaviour
    {
        static readonly List<WaterSurface> all = new();

        BoxCollider floor;
        Bounds area;
        Terrain terrain;
        float nextCheck;

        public float Level => area.max.y;

        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        void Awake()
        {
            area = GetComponent<Renderer>().bounds;
            terrain = FindFirstObjectByType<Terrain>();
            var go = new GameObject("WalkableWater") { layer = 2 };   // Ignore Raycast
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(new Vector3(area.center.x, Level, area.center.z), Quaternion.identity);
            go.transform.localScale = new Vector3(1f / transform.lossyScale.x, 1f / transform.lossyScale.y, 1f / transform.lossyScale.z);
            floor = go.AddComponent<BoxCollider>();
            floor.size = new Vector3(area.size.x, 0.4f, area.size.z);
            floor.center = new Vector3(0f, -0.2f, 0f);   // top face exactly at the water's surface
            floor.enabled = false;
        }

        void Update()
        {
            if (Time.time < nextCheck) return;
            nextCheck = Time.time + 0.25f;
            bool walker = false;
            foreach (PlayerController p in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
                if (p.isActiveAndEnabled) { walker = p.GetComponent<WaterWalker>() != null; break; }
            if (floor.enabled != walker) floor.enabled = walker;
        }

        // True when the point is over open water (inside the plane, the ground under it below the surface); level = the water height.
        public static bool Over(Vector3 point, out float level)
        {
            foreach (var s in all)
            {
                if (point.x < s.area.min.x || point.x > s.area.max.x || point.z < s.area.min.z || point.z > s.area.max.z) continue;
                float ground = s.terrain != null ? s.terrain.SampleHeight(point) + s.terrain.transform.position.y : float.MinValue;
                if (ground > s.Level - 0.05f) continue;
                level = s.Level;
                return true;
            }
            level = 0f;
            return false;
        }
    }
}
