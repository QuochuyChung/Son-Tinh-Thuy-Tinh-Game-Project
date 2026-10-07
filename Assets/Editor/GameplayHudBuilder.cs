using System.Collections.Generic;
using System.IO;
using SonTinhThuyTinh.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace SonTinhThuyTinh.EditorTools
{
    // Rebuilds the player HUD in the open scene, bottom-left like before, laid out after docs/reference/ui_hud_ref.webp:
    // a head cut-out on a flame emblem, the name in big outlined letters, and a long slanted block of two gold-framed bars
    // (health on top, stamina underneath). All graphics are our own: white rectangles + UIGradient (colour) + UIShear (slant),
    // two generated sprites and the portraits from HudPortraitRenderer. Nothing is copied from the reference game.
    public static class GameplayHudBuilder
    {
        const string WhitePath = "Assets/Art/UI/HUD/hud_white.png";
        const string EmblemPath = "Assets/Art/UI/HUD/hud_fire_bird_emblem.png";
        const string EmblemSourcePath = "ArtSource/Concept/hud_fire_bird_silhouette.png";   // black bird on white, relative to the project folder
        const string FontPath = "Assets/Art/Fonts/Roboto-Bold SDF.asset";
        const string NameMaterialPath = "Assets/Art/UI/HUD/HudName_Outline.mat";

        // Slant of the bar block: sideways shift per unit of height (about 35 degrees, as in the reference).
        const float Shear = 0.7f;
        const float BarLeft = 150f, BarLength = 680f;
        const float StaminaBottom = 30f, StaminaHeight = 14f;
        const float HealthBottom = 55f, HealthHeight = 20f;

        static Sprite white;

        [MenuItem("Tools/Son Tinh Thuy Tinh/Rebuild Gameplay HUD in open scene")]
        public static void Rebuild()
        {
            var hud = Object.FindFirstObjectByType<GameplayHUD>(FindObjectsInactive.Include);
            if (hud == null) { Debug.LogError("No GameplayHUD in the open scene."); return; }

            white = EnsureSprite(WhitePath, 8, 8, (u, v) => 1f);
            Sprite emblem = BuildEmblemSprite(EmblemPath);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            Transform canvas = hud.transform;
            for (int i = canvas.childCount - 1; i >= 0; i--)
                if (canvas.GetChild(i).name == "Bars") Undo.DestroyObjectImmediate(canvas.GetChild(i).gameObject);

            // Same corner and margin as the old bars: bottom-left, 32 px in.
            RectTransform root = NewRect("Bars", canvas);
            root.anchorMin = root.anchorMax = Vector2.zero;
            root.pivot = Vector2.zero;
            root.anchoredPosition = new Vector2(32f, 32f);
            root.sizeDelta = new Vector2(900f, 210f);

            BuildEmblem(root, new Vector2(138f, 168f), 336f, emblem);

            // Each bar is shifted right by its height above the stamina bar times the slant, so the left and right ends of the
            // two bars line up on one slanted edge, like one block.
            float staminaCenter = StaminaBottom + StaminaHeight * 0.5f, healthCenter = HealthBottom + HealthHeight * 0.5f;
            BarParts stamina = BuildBar(root, "Stamina", new Vector2(BarLeft, StaminaBottom), new Vector2(BarLength, StaminaHeight), new Color(0.22f, 0.66f, 1f), false, true);
            BarParts health = BuildBar(root, "Health", new Vector2(BarLeft + (healthCenter - staminaCenter) * Shear, HealthBottom), new Vector2(BarLength, HealthHeight), new Color(0.27f, 0.88f, 0.23f), true, false);

            TMP_Text nameLabel = BuildLabel(root, new Vector2(212f, HealthBottom + HealthHeight + 6f), font);
            Image portrait = BuildPortrait(root, new Vector2(100f, 118f));

            var so = new SerializedObject(hud);
            so.FindProperty("healthFill").objectReferenceValue = health.Fill;
            so.FindProperty("staminaFill").objectReferenceValue = stamina.Fill;
            so.FindProperty("healthTrail").objectReferenceValue = health.Trail;
            so.FindProperty("portrait").objectReferenceValue = portrait;
            SerializedProperty names = so.FindProperty("nameLabels");
            names.arraySize = 1;
            names.GetArrayElementAtIndex(0).objectReferenceValue = nameLabel;
            so.FindProperty("healthColor").gradientValue = HealthGradient();
            so.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
        }

        struct BarParts { public Image Fill, Trail; }

        static BarParts BuildBar(Transform parent, string name, Vector2 pos, Vector2 size, Color fillColor, bool withTrail, bool withEndCap)
        {
            RectTransform bar = NewRect(name + "_Bar", parent);
            bar.anchorMin = bar.anchorMax = Vector2.zero;
            bar.pivot = Vector2.zero;
            bar.anchoredPosition = pos;
            bar.sizeDelta = size;

            // Frame, outside in: dark outline, gold band, thin dark line, then the track.
            Layer(bar, "Outline", 6f, new Color(0f, 0f, 0f, 0.85f), null);
            Layer(bar, "Gold", 4.5f, Color.white, Gold());
            Layer(bar, "GoldGap", 1.5f, new Color(0.16f, 0.09f, 0.02f), null);
            Layer(bar, "Track", 0f, Color.white, G((0f, new Color(0.03f, 0.05f, 0.12f)), (1f, new Color(0.10f, 0.14f, 0.28f))));

            Image trail = null;
            if (withTrail)
            {
                trail = Layer(bar, "Trail", 0f, new Color(1f, 0.40f, 0.18f), G((0f, new Color(0.6f, 0.6f, 0.6f)), (1f, Color.white)));
                MakeFilled(trail);
            }

            // Grey-scale bands: the tint (Image.color) gives the hue, so the health colour can change with the health.
            Image fill = Layer(bar, "Fill", 0f, fillColor, G((0f, new Color(0.45f, 0.45f, 0.45f)), (0.3f, new Color(0.62f, 0.62f, 0.62f)), (0.62f, new Color(0.86f, 0.86f, 0.86f)), (1f, Color.white)));
            MakeFilled(fill);

            // Light band just under the top edge, as in the reference.
            Layer(bar, "Gloss", 0f, Color.white, G((0f, new Color(1f, 1f, 1f, 0f)), (0.55f, new Color(1f, 1f, 1f, 0f)), (0.64f, new Color(1f, 1f, 1f, 0.42f)), (0.84f, new Color(1f, 1f, 1f, 0.2f)), (1f, new Color(1f, 1f, 1f, 0.05f))));

            if (withEndCap)
            {
                Image cap = Layer(bar, "EndCap", 0f, Color.white, Gold());
                cap.rectTransform.anchorMin = new Vector2(1f, 0f);
                cap.rectTransform.offsetMin = new Vector2(-16f, 1f);
                cap.rectTransform.offsetMax = new Vector2(-7f, -1f);
            }

            return new BarParts { Fill = fill, Trail = trail };
        }

        static void MakeFilled(Image image)
        {
            image.sprite = white;   // a Filled image without a sprite ignores its fill amount
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = 0;
            image.fillAmount = 1f;
        }

        // One full-size layer of a bar; grow > 0 pushes it past the bar's edge (the frame layers). All layers share the same shear.
        static Image Layer(RectTransform bar, string name, float grow, Color color, Gradient gradient)
        {
            Image image = NewImage(name, bar, color, null);
            Stretch(image.rectTransform, grow);
            if (gradient != null) image.gameObject.AddComponent<UIGradient>().Gradient = gradient;
            image.gameObject.AddComponent<UIShear>().Shear = Shear;
            return image;
        }

        internal static Gradient Gold() => G((0f, new Color(0.42f, 0.24f, 0.05f)), (0.35f, new Color(0.76f, 0.52f, 0.14f)), (0.7f, new Color(1f, 0.84f, 0.40f)), (1f, new Color(1f, 0.95f, 0.70f)));

        // Head and shoulders in front of everything (it overlaps the start of the bars, like the reference).
        static Image BuildPortrait(Transform parent, Vector2 center)
        {
            Image portrait = NewImage("Portrait", parent, Color.white, null);
            portrait.rectTransform.anchorMin = portrait.rectTransform.anchorMax = Vector2.zero;
            portrait.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            portrait.rectTransform.anchoredPosition = center;
            portrait.rectTransform.sizeDelta = new Vector2(240f, 240f);
            portrait.preserveAspect = true;
            return portrait;
        }

        // Fire-bird emblem behind the head. The shape comes from the silhouette in ArtSource/Concept (user's reference), the colours
        // are the old flame colours: dark brown outline, gold rim, orange body that glows lighter towards the top.
        static void BuildEmblem(Transform parent, Vector2 center, float size, Sprite emblem)
        {
            Image image = NewImage("FireBirdEmblem", parent, Color.white, emblem);
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = Vector2.zero;
            image.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            image.rectTransform.sizeDelta = new Vector2(size, size);
            image.rectTransform.anchoredPosition = center;
        }

        const int EmblemSize = 1024;

        static Sprite BuildEmblemSprite(string path)
        {
            string source = Path.GetFullPath(Path.Combine(Application.dataPath, "..", EmblemSourcePath));
            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            src.LoadImage(File.ReadAllBytes(source));

            // black on white -> ink amount, then the bounding box of the ink
            int sw = src.width, sh = src.height;
            Color[] sp = src.GetPixels();
            var ink = new float[sw * sh];
            int minX = sw, maxX = 0, minY = sh, maxY = 0;
            for (int y = 0; y < sh; y++)
                for (int x = 0; x < sw; x++)
                {
                    Color c = sp[y * sw + x];
                    float v = Mathf.Clamp01(1f - (c.r + c.g + c.b) / 3f) * c.a;
                    ink[y * sw + x] = v;
                    if (v > 0.5f) { minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y); }
                }
            Object.DestroyImmediate(src);

            // fit the box into the square sprite with a small margin, sampling the ink bilinearly, then sharpen the edge
            float boxSize = Mathf.Max(maxX - minX, maxY - minY) * 1.04f;
            float cx = (minX + maxX) * 0.5f, cy = (minY + maxY) * 0.5f;
            var alpha = new float[EmblemSize * EmblemSize];
            for (int y = 0; y < EmblemSize; y++)
                for (int x = 0; x < EmblemSize; x++)
                {
                    float sx = cx + ((x + 0.5f) / EmblemSize - 0.5f) * boxSize, sy = cy + ((y + 0.5f) / EmblemSize - 0.5f) * boxSize;
                    alpha[y * EmblemSize + x] = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.7f, Sample(ink, sw, sh, sx, sy)));
                }

            float[] depth = DistanceInside(alpha, EmblemSize);
            var pixels = new Color[EmblemSize * EmblemSize];
            for (int y = 0; y < EmblemSize; y++)
                for (int x = 0; x < EmblemSize; x++)
                {
                    int i = y * EmblemSize + x;
                    Color c = Shade(depth[i], y / (float)EmblemSize);
                    c.a = alpha[i];
                    pixels[i] = c;
                }

            var tex = new Texture2D(EmblemSize, EmblemSize, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = EmblemSize;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static float Sample(float[] data, int w, int h, float x, float y)
        {
            x = Mathf.Clamp(x - 0.5f, 0f, w - 1.001f);
            y = Mathf.Clamp(y - 0.5f, 0f, h - 1.001f);
            int x0 = (int)x, y0 = (int)y;
            float fx = x - x0, fy = y - y0;
            return Mathf.Lerp(Mathf.Lerp(data[y0 * w + x0], data[y0 * w + x0 + 1], fx), Mathf.Lerp(data[(y0 + 1) * w + x0], data[(y0 + 1) * w + x0 + 1], fx), fy);
        }

        // Distance (in pixels) from every inside pixel to the nearest outside pixel: two-pass chamfer transform.
        static float[] DistanceInside(float[] alpha, int n)
        {
            const float side = 1f, diagonal = 1.4142f;
            var d = new float[n * n];
            for (int i = 0; i < d.Length; i++) d[i] = alpha[i] > 0.5f ? 1e6f : 0f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int i = y * n + x;
                    if (d[i] == 0f) continue;
                    float best = d[i];
                    if (x > 0) best = Mathf.Min(best, d[i - 1] + side);
                    if (y > 0) best = Mathf.Min(best, d[i - n] + side);
                    if (x > 0 && y > 0) best = Mathf.Min(best, d[i - n - 1] + diagonal);
                    if (x < n - 1 && y > 0) best = Mathf.Min(best, d[i - n + 1] + diagonal);
                    d[i] = best;
                }
            for (int y = n - 1; y >= 0; y--)
                for (int x = n - 1; x >= 0; x--)
                {
                    int i = y * n + x;
                    if (d[i] == 0f) continue;
                    float best = d[i];
                    if (x < n - 1) best = Mathf.Min(best, d[i + 1] + side);
                    if (y < n - 1) best = Mathf.Min(best, d[i + n] + side);
                    if (x < n - 1 && y < n - 1) best = Mathf.Min(best, d[i + n + 1] + diagonal);
                    if (x > 0 && y < n - 1) best = Mathf.Min(best, d[i + n - 1] + diagonal);
                    d[i] = best;
                }
            return d;
        }

        // Dark outline at the edge, then a gold rim, then the orange body: deeper red-orange at the bottom, brighter towards the top.
        static Color Shade(float depth, float height)
        {
            Color body = Color.Lerp(new Color(0.78f, 0.27f, 0.05f), new Color(1f, 0.62f, 0.16f), Mathf.SmoothStep(0f, 1f, height));
            Color c = new(0.28f, 0.10f, 0.02f);
            c = Color.Lerp(c, new Color(1f, 0.80f, 0.38f), Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2.5f, 5f, depth)));
            c = Color.Lerp(c, body, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(8f, 12f, depth)));
            return c;
        }

        static TMP_Text BuildLabel(Transform parent, Vector2 pos, TMP_FontAsset font)
        {
            RectTransform rect = NewRect("Name", parent);
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(520f, 60f);

            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null)
            {
                label.font = font;
                label.fontSharedMaterial = EnsureNameMaterial(font);
            }
            label.text = "Thủy Tinh";
            label.fontSize = 46f;
            label.fontStyle = FontStyles.Bold | FontStyles.Italic;
            label.alignment = TextAlignmentOptions.BottomLeft;
            label.characterSpacing = 1f;
            label.color = Color.white;
            label.raycastTarget = false;
            return label;
        }

        // Thick black outline + soft drop shadow for the name (its own material, so other texts using the font are not affected).
        static Material EnsureNameMaterial(TMP_FontAsset font)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(NameMaterialPath);
            if (material == null)
            {
                material = new Material(font.material) { name = "HudName_Outline" };
                AssetDatabase.CreateAsset(material, NameMaterialPath);
            }
            material.EnableKeyword("OUTLINE_ON");   // the mobile SDF shader draws the outline only with this keyword
            material.SetFloat("_FaceDilate", 0.15f);
            material.SetFloat("_OutlineWidth", 0.32f);
            material.SetColor("_OutlineColor", new Color(0.05f, 0.03f, 0.02f, 1f));
            material.EnableKeyword("UNDERLAY_ON");
            material.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.6f));
            material.SetFloat("_UnderlayOffsetX", 0.7f);
            material.SetFloat("_UnderlayOffsetY", -0.7f);
            material.SetFloat("_UnderlayDilate", 0.3f);
            material.SetFloat("_UnderlaySoftness", 0.25f);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Gradient HealthGradient() => G((0f, new Color(0.91f, 0.21f, 0.17f)), (0.25f, new Color(0.95f, 0.53f, 0.16f)), (0.5f, new Color(0.56f, 0.88f, 0.23f)), (0.7f, new Color(0.27f, 0.88f, 0.23f)), (1f, new Color(0.27f, 0.88f, 0.23f)));

        internal static Gradient G(params (float t, Color color)[] keys)
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

        internal static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Build HUD");
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        internal static Image NewImage(string name, Transform parent, Color color, Sprite sprite)
        {
            RectTransform rect = NewRect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        internal static void Stretch(RectTransform rect, float grow)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(-grow, -grow);
            rect.offsetMax = new Vector2(grow, grow);
        }

        // White sprite whose alpha comes from alpha(u, v) (u, v in 0..1, v = 0 at the bottom). Written once, then reused.
        internal static Sprite EnsureSprite(string path, int width, int height, System.Func<float, float, float> alpha)
        {
            if (!File.Exists(path))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha((x + 0.5f) / width, (y + 0.5f) / height)));
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
