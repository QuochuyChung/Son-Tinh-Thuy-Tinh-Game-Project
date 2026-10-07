using System;
using System.Collections.Generic;
using UnityEngine;

namespace SonTinhThuyTinh.UI.Map
{
    // The illustrated world map (docs/reference/world_map.webp) and the places named on it.
    [CreateAssetMenu(menuName = "Son Tinh Thuy Tinh/World Map", fileName = "WorldMap")]
    public class WorldMapData : ScriptableObject
    {
        [Serializable]
        public struct Place
        {
            public string label;
            [Tooltip("Position on the picture as a fraction of its size, measured from the top-left corner.")]
            public Vector2 position;
        }

        [SerializeField] Texture2D texture;
        [SerializeField] Place[] places;

        public Texture2D Texture => texture;
        public IReadOnlyList<Place> Places => places;

        // Width / height of the picture.
        public float Aspect => texture != null ? (float)texture.width / texture.height : 1f;
    }
}
