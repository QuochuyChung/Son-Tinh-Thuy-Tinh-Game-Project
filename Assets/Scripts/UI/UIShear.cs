using UnityEngine;
using UnityEngine.UI;

namespace SonTinhThuyTinh.UI
{
    // Slants a UI graphic into a parallelogram (the angled ends of the HUD bars) by moving every vertex sideways in proportion
    // to its height. Works on Simple, Sliced and Filled images, so a Filled fill keeps an angled edge while it drains.
    // Give every layer of one bar the same vertical centre and the same Shear so they stay aligned.
    [AddComponentMenu("UI/Effects/Shear")]
    public class UIShear : BaseMeshEffect
    {
        [Tooltip("Sideways shift per unit of height: 0.4 is about 22 degrees. Positive leans the top to the right.")]
        [SerializeField] float shear = 0.4f;

        public float Shear
        {
            get => shear;
            set
            {
                shear = value;
                if (graphic != null) graphic.SetVerticesDirty();
            }
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;

            float centerY = ((RectTransform)transform).rect.center.y;
            var vertex = new UIVertex();
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                vertex.position.x += (vertex.position.y - centerY) * shear;
                vh.SetUIVertex(vertex, i);
            }
        }
    }
}
