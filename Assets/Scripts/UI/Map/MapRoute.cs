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

        public IReadOnlyList<GiftPin> GiftPins => giftPins;

        // Where the picture shows a player standing at `world`, and the clockwise angle (from "up" on the picture) the arrow points at
        // for a player facing `yaw` degrees (up on the picture is world +z, so the two angles are the same).
        public bool TryLocate(Vector3 world, float yaw, out Vector2 map, out float arrowAngle)
        {
            map = mapReference + new Vector2((world.x - worldReference.x) * mapPerMeter.x, -(world.z - worldReference.y) * mapPerMeter.y);
            map = new Vector2(Mathf.Clamp01(map.x), Mathf.Clamp01(map.y));
            arrowAngle = yaw;
            return true;
        }
    }
}
