using System.Collections.Generic;
using System.IO;
using SonTinhThuyTinh.Player;
using SonTinhThuyTinh.Quest;
using SonTinhThuyTinh.UI.Map;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace SonTinhThuyTinh.EditorTools
{
    // Builds the map UI of a gameplay scene (docs/progress.md 9.9): a map circle in the bottom-right corner and the full illustrated
    // map (Assets/Art/UI/Map/world_map.jpg, saved from docs/reference/world_map.webp) with a "you are here" arrow. The map builders call
    // Build with the route of their scene. Everything is plain Unity UI plus three generated sprites.
    public static class MapHudBuilder
    {
        const string Dir = "Assets/Art/UI/Map/";
        const string MapTexturePath = Dir + "world_map.jpg";
        const string DataPath = "Assets/Data/Map/WorldMap.asset";
        const string FontPath = "Assets/Art/Fonts/Roboto-Bold SDF.asset";
        const string OutlineMaterialPath = "Assets/Art/UI/HUD/HudName_Outline.mat";
        const string RoundPath = "Assets/Art/UI/Dialogue/ui_round_corner.png";

        // Rebuilds the route object and the map UI in the open scene. `place` puts this scene's ground on the picture, `pins` are the
        // gift stops in world (x, z). The names printed on the picture come from the WorldMapData asset (written by WorldMapComposer).
        public static MapHUD Build(string title, Color accent, WorldMapLayout.Frame place, (GiftItem gift, Vector2 world)[] pins)
        {
            WorldMapData data = EnsureData();
            Sprite circle = GameplayHudBuilder.EnsureSprite(Dir + "map_circle.png", 256, 256, (u, v) => Mathf.Clamp01(126.5f - Vector2.Distance(new Vector2(u * 256f, v * 256f), new Vector2(128f, 128f)) + 0.5f));
            Sprite ring = GameplayHudBuilder.EnsureSprite(Dir + "map_ring.png", 256, 256, (u, v) =>
            {
                float d = Vector2.Distance(new Vector2(u * 256f, v * 256f), new Vector2(128f, 128f));
                return Mathf.Clamp01(Mathf.Min(126.5f - d, d - 112.5f) + 0.5f);
            });
            Sprite arrow = GameplayHudBuilder.EnsureSprite(Dir + "map_arrow.png", 128, 128, ArrowAlpha);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            var outline = AssetDatabase.LoadAssetAtPath<Material>(OutlineMaterialPath);
            var round = AssetDatabase.LoadAssetAtPath<Sprite>(RoundPath);

            foreach (string old in new[] { "MapCanvas", "MapRoute" })
            {
                GameObject existing = GameObject.Find(old);
                if (existing != null) Undo.DestroyObjectImmediate(existing);
            }

            // the route, with the gift stops shown as pins on the full map
            var routeObject = new GameObject("MapRoute");
            var route = routeObject.AddComponent<MapRoute>();
            var routeSo = new SerializedObject(route);
            routeSo.FindProperty("worldReference").vector2Value = place.WorldReference;
            routeSo.FindProperty("mapReference").vector2Value = place.ToFraction(place.WorldReference);
            routeSo.FindProperty("mapPerMeter").vector2Value = place.MapPerMeter;
            SerializedProperty pinList = routeSo.FindProperty("giftPins");
            pinList.arraySize = pins.Length;
            for (int i = 0; i < pins.Length; i++)
            {
                SerializedProperty p = pinList.GetArrayElementAtIndex(i);
                p.FindPropertyRelative("gift").objectReferenceValue = pins[i].gift;
                p.FindPropertyRelative("map").vector2Value = place.ToFraction(pins[i].world);
            }
            routeSo.ApplyModifiedProperties();

            // canvas
            var canvasObject = new GameObject("MapCanvas", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(canvasObject, "Build map HUD");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            var hud = canvasObject.AddComponent<MapHUD>();

            // ---------------------------------------------------------------- the map circle
            RectTransform minimap = GameplayHudBuilder.NewRect("Minimap", canvasObject.transform);
            minimap.anchorMin = minimap.anchorMax = new Vector2(1f, 0f);
            minimap.pivot = new Vector2(1f, 0f);
            minimap.anchoredPosition = new Vector2(-56f, 56f);
            minimap.sizeDelta = new Vector2(300f, 300f);
            var hit = minimap.gameObject.AddComponent<Image>();
            hit.sprite = circle;
            hit.color = new Color(0f, 0f, 0f, 0f);
            var button = minimap.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;

            Image shadow = GameplayHudBuilder.NewImage("Shadow", minimap, new Color(0f, 0f, 0f, 0.45f), circle);
            GameplayHudBuilder.Stretch(shadow.rectTransform, 8f);
            Image mask = GameplayHudBuilder.NewImage("Mask", minimap, Color.white, circle);
            GameplayHudBuilder.Stretch(mask.rectTransform, -13f);
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            var picture = NewRawImage("Picture", mask.transform, data.Texture);
            GameplayHudBuilder.Stretch(picture.rectTransform, 0f);
            picture.uvRect = new Rect(0.4f, 0.1f, 0.2f, 0.23f);
            RectTransform minimapArrow = BuildArrow("Arrow", mask.transform, arrow, accent, 38f);

            Image frame = GameplayHudBuilder.NewImage("Ring", minimap, new Color(0.96f, 0.80f, 0.36f), ring);
            GameplayHudBuilder.Stretch(frame.rectTransform, 0f);
            Image frameDark = GameplayHudBuilder.NewImage("RingInner", minimap, new Color(0.18f, 0.12f, 0.06f, 0.9f), ring);
            GameplayHudBuilder.Stretch(frameDark.rectTransform, -11f);

            // key cap with "M" on the left of the circle
            Image key = GameplayHudBuilder.NewImage("KeyCap", minimap, new Color(0.93f, 0.94f, 0.97f), round);
            key.type = Image.Type.Sliced;
            key.pixelsPerUnitMultiplier = 44f / 10f;
            key.rectTransform.anchorMin = key.rectTransform.anchorMax = Vector2.zero;
            key.rectTransform.pivot = new Vector2(1f, 0f);
            key.rectTransform.anchoredPosition = new Vector2(-4f, 22f);
            key.rectTransform.sizeDelta = new Vector2(46f, 40f);
            TMP_Text keyText = Label("Key", key.transform, "M", 26f, new Color(0.10f, 0.11f, 0.16f), font, null, TextAlignmentOptions.Center);
            GameplayHudBuilder.Stretch(keyText.rectTransform, 0f);

            // ---------------------------------------------------------------- the full map
            RectTransform panelRect = GameplayHudBuilder.NewRect("MapPanel", canvasObject.transform);
            GameplayHudBuilder.Stretch(panelRect, 0f);
            var group = panelRect.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;   // closed; MapHUD fades it in
            group.interactable = false;
            group.blocksRaycasts = false;

            Image dim = GameplayHudBuilder.NewImage("Dim", panelRect, new Color(0.02f, 0.03f, 0.05f, 0.86f), null);
            GameplayHudBuilder.Stretch(dim.rectTransform, 0f);
            dim.raycastTarget = true;
            var closeButton = dim.gameObject.AddComponent<Button>();
            closeButton.targetGraphic = dim;
            closeButton.transition = Selectable.Transition.None;

            TMP_Text heading = Label("Title", panelRect, "BẢN ĐỒ", 54f, Color.white, font, outline, TextAlignmentOptions.Top);
            heading.rectTransform.anchorMin = new Vector2(0f, 1f);
            heading.rectTransform.anchorMax = new Vector2(1f, 1f);
            heading.rectTransform.pivot = new Vector2(0.5f, 1f);
            heading.rectTransform.anchoredPosition = new Vector2(0f, -22f);
            heading.rectTransform.sizeDelta = new Vector2(0f, 70f);
            heading.characterSpacing = 12f;
            TMP_Text subtitle = Label("Subtitle", panelRect, title, 30f, accent, font, outline, TextAlignmentOptions.Top);
            subtitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            subtitle.rectTransform.anchorMax = new Vector2(1f, 1f);
            subtitle.rectTransform.pivot = new Vector2(0.5f, 1f);
            subtitle.rectTransform.anchoredPosition = new Vector2(0f, -86f);
            subtitle.rectTransform.sizeDelta = new Vector2(0f, 44f);

            RectTransform safe = GameplayHudBuilder.NewRect("Safe", panelRect);
            GameplayHudBuilder.Stretch(safe, 0f);
            safe.offsetMin = new Vector2(80f, 90f);
            safe.offsetMax = new Vector2(-80f, -140f);
            RectTransform frameRect = GameplayHudBuilder.NewRect("Frame", safe);
            frameRect.anchorMin = frameRect.anchorMax = new Vector2(0.5f, 0.5f);
            var fitter = frameRect.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = data.Aspect;

            Image border = GameplayHudBuilder.NewImage("Border", frameRect, new Color(0.96f, 0.80f, 0.36f), null);
            GameplayHudBuilder.Stretch(border.rectTransform, 7f);
            RawImage full = NewRawImage("Picture", frameRect, data.Texture);
            GameplayHudBuilder.Stretch(full.rectTransform, 0f);

            // place names: a small gold dot on the spot with the name hanging under it
            for (int i = 0; i < data.Places.Count; i++)
            {
                Vector2 f = data.Places[i].position;
                Image dot = GameplayHudBuilder.NewImage("PlaceDot_" + i, frameRect, new Color(1f, 0.86f, 0.35f), circle);
                dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = new Vector2(f.x, 1f - f.y);
                dot.rectTransform.sizeDelta = new Vector2(14f, 14f);
                Image dotRing = GameplayHudBuilder.NewImage("Edge", dot.transform, new Color(0.1f, 0.07f, 0.03f, 0.9f), ring);
                GameplayHudBuilder.Stretch(dotRing.rectTransform, -2f);

                TMP_Text placeName = Label("Place_" + i, frameRect, data.Places[i].label, 22f, new Color(1f, 0.93f, 0.7f), font, outline, TextAlignmentOptions.Top);
                placeName.rectTransform.anchorMin = placeName.rectTransform.anchorMax = new Vector2(f.x, 1f - f.y);
                placeName.rectTransform.pivot = new Vector2(0.5f, 1f);
                placeName.rectTransform.anchoredPosition = new Vector2(0f, -10f);
                placeName.rectTransform.sizeDelta = new Vector2(320f, 36f);
            }

            var pinImages = new Image[pins.Length];
            for (int i = 0; i < pins.Length; i++)
            {
                Image pinRing = GameplayHudBuilder.NewImage("Pin_" + i, frameRect, new Color(0.2f, 0.2f, 0.2f, 0.9f), circle);
                pinRing.rectTransform.sizeDelta = new Vector2(30f, 30f);
                Image pinCore = GameplayHudBuilder.NewImage("Edge", pinRing.transform, new Color(0.05f, 0.04f, 0.03f, 0.9f), ring);
                GameplayHudBuilder.Stretch(pinCore.rectTransform, 3f);
                pinImages[i] = pinRing;
            }
            RectTransform fullArrow = BuildArrow("You", frameRect, arrow, accent, 58f);
            fullArrow.anchorMin = fullArrow.anchorMax = new Vector2(0.5f, 0.5f);

            TMP_Text legend = Label("Legend", panelRect, "Mũi tên: bạn đang ở đây    Chấm vàng: sính lễ đã nhận    Chấm xám: chưa lấy    M / Esc / bấm chuột: đóng bản đồ",
                24f, new Color(0.85f, 0.88f, 0.92f), font, null, TextAlignmentOptions.Center);
            legend.rectTransform.anchorMin = new Vector2(0f, 0f);
            legend.rectTransform.anchorMax = new Vector2(1f, 0f);
            legend.rectTransform.pivot = new Vector2(0.5f, 0f);
            legend.rectTransform.anchoredPosition = new Vector2(0f, 28f);
            legend.rectTransform.sizeDelta = new Vector2(0f, 40f);

            // ---------------------------------------------------------------- wiring
            var so = new SerializedObject(hud);
            so.FindProperty("map").objectReferenceValue = data;
            so.FindProperty("route").objectReferenceValue = route;
            so.FindProperty("minimapImage").objectReferenceValue = picture;
            so.FindProperty("minimapArrow").objectReferenceValue = minimapArrow;
            so.FindProperty("minimapButton").objectReferenceValue = button;
            so.FindProperty("minimapZoom").floatValue = 0.2f;
            so.FindProperty("panel").objectReferenceValue = group;
            so.FindProperty("closeButton").objectReferenceValue = closeButton;
            so.FindProperty("fullArrow").objectReferenceValue = fullArrow;
            SerializedProperty pinArray = so.FindProperty("giftPins");
            pinArray.arraySize = pinImages.Length;
            for (int i = 0; i < pinImages.Length; i++) pinArray.GetArrayElementAtIndex(i).objectReferenceValue = pinImages[i];
            so.ApplyModifiedProperties();

            var spawner = Object.FindFirstObjectByType<PlayerSpawner>();
            if (spawner != null)
            {
                var spawnerSo = new SerializedObject(spawner);
                spawnerSo.FindProperty("mapHud").objectReferenceValue = hud;
                spawnerSo.ApplyModifiedProperties();
            }
            EditorSceneManager.MarkSceneDirty(canvasObject.scene);
            return hud;
        }

        // ---------------------------------------------------------------- data and generated sprites

        public const string PicturePath = MapTexturePath;

        // The picture is generated by WorldMapComposer (a top-down capture of the scenes); this just makes sure it imports the way the
        // UI wants it: no mipmaps, clamped, up to 2048 px.
        public static void ConfigurePicture()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(MapTexturePath);
            if (importer != null && (importer.mipmapEnabled || importer.wrapMode != TextureWrapMode.Clamp || importer.maxTextureSize < 2048 || importer.npotScale != TextureImporterNPOTScale.None))
            {
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 2048;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
        }

        // The WorldMapData asset: the picture plus the names printed on it. Names are written by WorldMapComposer (SetPlaces).
        public static WorldMapData EnsureData()
        {
            ConfigurePicture();
            var data = AssetDatabase.LoadAssetAtPath<WorldMapData>(DataPath);
            if (data == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(DataPath));
                data = ScriptableObject.CreateInstance<WorldMapData>();
                AssetDatabase.CreateAsset(data, DataPath);
            }
            var so = new SerializedObject(data);
            so.FindProperty("texture").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(MapTexturePath);
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(data);
            return data;
        }

        // Names of places and where they are on the picture (fractions from the top-left corner).
        public static void SetPlaces(IList<(string label, Vector2 position)> places)
        {
            WorldMapData data = EnsureData();
            var so = new SerializedObject(data);
            SerializedProperty list = so.FindProperty("places");
            list.arraySize = places.Count;
            for (int i = 0; i < places.Count; i++)
            {
                SerializedProperty p = list.GetArrayElementAtIndex(i);
                p.FindPropertyRelative("label").stringValue = places[i].label;
                p.FindPropertyRelative("position").vector2Value = places[i].position;
            }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
        }

        // Chevron pointing up (a tip, two wings and a notch), anti-aliased by supersampling.
        static float ArrowAlpha(float u, float v)
        {
            Vector2 tip = new(0.5f, 0.94f), left = new(0.16f, 0.08f), notch = new(0.5f, 0.30f), right = new(0.84f, 0.08f);
            const int n = 4;
            int inside = 0;
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    var p = new Vector2(u + (i + 0.5f - n * 0.5f) / (n * 128f), v + (j + 0.5f - n * 0.5f) / (n * 128f));
                    if (InTriangle(p, tip, left, notch) || InTriangle(p, tip, notch, right)) inside++;
                }
            return inside / (float)(n * n);
        }

        static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Sign(p, a, b), d2 = Sign(p, b, c), d3 = Sign(p, c, a);
            bool negative = d1 < 0f || d2 < 0f || d3 < 0f, positive = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(negative && positive);
        }

        static float Sign(Vector2 p1, Vector2 p2, Vector2 p3) => (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);

        // ---------------------------------------------------------------- small helpers

        static RawImage NewRawImage(string name, Transform parent, Texture texture)
        {
            RectTransform rect = GameplayHudBuilder.NewRect(name, parent);
            var image = rect.gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
            return image;
        }

        // An arrow with a dark outline (a bigger black copy behind the coloured one). Rotate the returned transform.
        static RectTransform BuildArrow(string name, Transform parent, Sprite arrow, Color color, float size)
        {
            RectTransform root = GameplayHudBuilder.NewRect(name, parent);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(size, size);
            Image back = GameplayHudBuilder.NewImage("Outline", root, new Color(0f, 0f, 0f, 0.85f), arrow);
            GameplayHudBuilder.Stretch(back.rectTransform, size * 0.16f);
            Image fill = GameplayHudBuilder.NewImage("Fill", root, color, arrow);
            GameplayHudBuilder.Stretch(fill.rectTransform, 0f);
            return root;
        }

        static TMP_Text Label(string name, Transform parent, string text, float size, Color color, TMP_FontAsset font, Material material, TextAlignmentOptions alignment)
        {
            RectTransform rect = GameplayHudBuilder.NewRect(name, parent);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null)
            {
                label.font = font;
                if (material != null) label.fontSharedMaterial = material;
            }
            label.text = text;
            label.fontSize = size;
            label.fontStyle = FontStyles.Bold;
            label.alignment = alignment;
            label.color = color;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            return label;
        }
    }
}
