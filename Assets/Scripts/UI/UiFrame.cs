using UnityEngine;
using UnityEngine.UI;

namespace SonTinhThuyTinh.UI
{
    // The gold-framed, dark-blue panel look of the health bars (GameplayHudBuilder), but with rounded corners, for plain panels
    // (the gift checklist, its banner). The layers are made at run time from one generated rounded sprite, so no scene has to be rebuilt:
    // dark outline, gold band, thin dark gap, then the track. They go in behind the panel's own children (its text).
    public static class UiFrame
    {
        const string Marker = "Frame_Track";
        static Sprite rounded;

        public static Sprite Rounded()
        {
            if (rounded != null) return rounded;
            const int size = 64, radius = 20;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "UiFrame_Rounded" };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // distance outside the rounded rectangle, soft one-pixel edge
                    float dx = Mathf.Max(radius - (x + 0.5f), (x + 0.5f) - (size - radius), 0f);
                    float dy = Mathf.Max(radius - (y + 0.5f), (y + 0.5f) - (size - radius), 0f);
                    float outside = Mathf.Sqrt(dx * dx + dy * dy) - radius;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - outside)));
                }
            tex.Apply(false, true);
            rounded = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            return rounded;
        }

        public static Gradient Gold() => G((0f, new Color(0.42f, 0.24f, 0.05f)), (0.35f, new Color(0.76f, 0.52f, 0.14f)), (0.7f, new Color(1f, 0.84f, 0.40f)), (1f, new Color(1f, 0.95f, 0.70f)));

        // trackBottom / trackTop: the dark gradient inside the gold band (bars: 0.03,0.05,0.12 -> 0.10,0.14,0.28).
        public static void Apply(RectTransform panel, Color trackBottom, Color trackTop, float cornerScale = 0.7f)
        {
            if (panel == null || panel.Find(Marker) != null) return;
            if (panel.TryGetComponent(out Image own)) { own.sprite = null; own.color = Color.clear; }

            // behind the text: inserted first, outermost layer first
            Layer(panel, "Frame_Outline", 6f, new Color(0f, 0f, 0f, 0.85f), null, 0, cornerScale);
            Layer(panel, "Frame_Gold", 4.5f, Color.white, Gold(), 1, cornerScale);
            Layer(panel, "Frame_Gap", 1.5f, new Color(0.16f, 0.09f, 0.02f), null, 2, cornerScale);
            Layer(panel, Marker, 0f, Color.white, G((0f, trackBottom), (1f, trackTop)), 3, cornerScale);
        }

        static void Layer(RectTransform panel, string name, float grow, Color color, Gradient gradient, int index, float cornerScale)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(panel, false);
            go.transform.SetSiblingIndex(index);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(-grow, -grow); rect.offsetMax = new Vector2(grow, grow);
            var image = go.GetComponent<Image>();
            image.sprite = Rounded();
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f / cornerScale;
            image.color = color;
            image.raycastTarget = false;
            if (gradient != null) go.AddComponent<UIGradient>().Gradient = gradient;
        }

        static Gradient G(params (float t, Color color)[] keys)
        {
            var colorKeys = new GradientColorKey[keys.Length];
            var alphaKeys = new GradientAlphaKey[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                colorKeys[i] = new GradientColorKey(new Color(keys[i].color.r, keys[i].color.g, keys[i].color.b), keys[i].t);
                alphaKeys[i] = new GradientAlphaKey(keys[i].color.a, keys[i].t);
            }
            var gradient = new Gradient();
            gradient.SetKeys(colorKeys, alphaKeys);
            return gradient;
        }
    }
}
