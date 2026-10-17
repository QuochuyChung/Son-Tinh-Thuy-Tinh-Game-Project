using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SonTinhThuyTinh.EditorTools
{
    // The bridge model's side rails are only meshes: characters walked through them. WoodenBridgeSegment.prefab gets two solid rail walls (child
    // objects, so they survive BridgeModelSwap removing the root's deck collider on the piers, whose deck colliders are their own "Deck" boxes).
    // Every copy of the segment in the maps picks them up from the prefab. Safe to run again.
    // Menu: Tools > Son Tinh Thuy Tinh > New Props > Bridge Rails Solid.
    public static class BridgeRails
    {
        const string SegmentPath = "Assets/Prefabs/Props/WoodenBridgeSegment.prefab";

        [MenuItem("Tools/Son Tinh Thuy Tinh/New Props/Bridge Rails Solid")]
        public static void Menu() => Debug.Log("BridgeRails: " + Apply());

        public static string Apply()
        {
            var root = PrefabUtility.LoadPrefabContents(SegmentPath);
            try
            {
                foreach (var old in root.transform.Cast<Transform>().Where(c => c.name.StartsWith("RailWall")).ToList()) Object.DestroyImmediate(old.gameObject);
                var deck = root.GetComponent<BoxCollider>();
                if (deck == null) return "no deck collider on the segment";
                // the rails are the model's highest parts at both long edges (mesh: x about +-0.6, up to 0.6 m)
                float halfX = deck.size.x * 0.5f, length = deck.size.z, top = 0.6f, floor = deck.center.y + deck.size.y * 0.5f;
                foreach (float side in new[] { -1f, 1f })
                {
                    var wall = new GameObject(side < 0 ? "RailWallL" : "RailWallR").transform;
                    wall.SetParent(root.transform, false);
                    var box = wall.gameObject.AddComponent<BoxCollider>();
                    box.size = new Vector3(0.16f, top - floor, length);
                    box.center = new Vector3(side * (halfX - 0.04f), floor + (top - floor) * 0.5f, 0f);
                }
                PrefabUtility.SaveAsPrefabAsset(root, SegmentPath);
                return "rail walls added to WoodenBridgeSegment (deck half width " + halfX.ToString("F2") + ", rail height " + top + ")";
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
