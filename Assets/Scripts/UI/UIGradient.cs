using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SonTinhThuyTinh.UI
{
    // Vertical colour gradient over a UI graphic (bottom = 0, top = 1), multiplied with the graphic's own colour.
    // Lets the HUD bars and the gold frames be plain white rectangles instead of hand-painted sprites.
    // A plain quad (Simple / Filled image) is split into one band per gradient key, so every key of the gradient shows,
    // not only the two end colours. Other meshes (e.g. Sliced) only get the colour at each vertex's height.
    [AddComponentMenu("UI/Effects/Vertical Gradient")]
    public class UIGradient : BaseMeshEffect
    {
        [SerializeField] Gradient gradient = new();

        public Gradient Gradient
        {
            get => gradient;
            set
            {
                gradient = value;
                if (graphic != null) graphic.SetVerticesDirty();
            }
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;

            if (vh.currentVertCount == 4) SplitQuad(vh);
            else ColourByHeight(vh);
        }

        void ColourByHeight(VertexHelper vh)
        {
            float minY = float.MaxValue, maxY = float.MinValue;
            var vertex = new UIVertex();
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                minY = Mathf.Min(minY, vertex.position.y);
                maxY = Mathf.Max(maxY, vertex.position.y);
            }

            float height = Mathf.Max(maxY - minY, 0.0001f);
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                vertex.color *= gradient.Evaluate((vertex.position.y - minY) / height);
                vh.SetUIVertex(vertex, i);
            }
        }

        void SplitQuad(VertexHelper vh)
        {
            var corners = new UIVertex[4];
            for (int i = 0; i < 4; i++) vh.PopulateUIVertex(ref corners[i], i);
            System.Array.Sort(corners, (a, b) => a.position.y.CompareTo(b.position.y));
            UIVertex bottomLeft = corners[0], bottomRight = corners[1], topLeft = corners[2], topRight = corners[3];
            if (bottomLeft.position.x > bottomRight.position.x) (bottomLeft, bottomRight) = (bottomRight, bottomLeft);
            if (topLeft.position.x > topRight.position.x) (topLeft, topRight) = (topRight, topLeft);

            var rows = new List<float> { 0f, 1f };
            foreach (GradientColorKey key in gradient.colorKeys) rows.Add(key.time);
            foreach (GradientAlphaKey key in gradient.alphaKeys) rows.Add(key.time);
            rows.Sort();

            vh.Clear();
            float previous = -1f;
            int rowCount = 0;
            foreach (float t in rows)
            {
                if (t - previous < 0.001f) continue;
                previous = t;
                Color tint = gradient.Evaluate(t);
                vh.AddVert(Blend(bottomLeft, topLeft, t, tint));
                vh.AddVert(Blend(bottomRight, topRight, t, tint));
                if (rowCount > 0)
                {
                    int i = (rowCount - 1) * 2;
                    vh.AddTriangle(i, i + 2, i + 1);
                    vh.AddTriangle(i + 1, i + 2, i + 3);
                }
                rowCount++;
            }
        }

        static UIVertex Blend(UIVertex bottom, UIVertex top, float t, Color tint)
        {
            UIVertex v = bottom;
            v.position = Vector3.Lerp(bottom.position, top.position, t);
            v.uv0 = Vector4.Lerp(bottom.uv0, top.uv0, t);
            v.color = (Color32)((Color)Color.Lerp(bottom.color, top.color, t) * tint);
            return v;
        }
    }
}
