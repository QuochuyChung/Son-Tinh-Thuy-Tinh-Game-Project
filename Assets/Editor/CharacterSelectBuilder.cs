using System.Collections.Generic;
using System.IO;
using SonTinhThuyTinh.Characters;
using SonTinhThuyTinh.UI;
using SonTinhThuyTinh.UI.CharacterSelect;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace SonTinhThuyTinh.EditorTools
{
    // Rebuilds the CharacterSelect scene after docs/reference/ui_character_select_ref.webp (layout only, nothing copied from that game):
    // split screen, big 3D busts, stacked outlined names, P1 / CPU tags, three skill rows, a key hint bar, roster cards with an orange frame.
    // Everything is plain Unity UI built with the helpers of GameplayHudBuilder, plus generated sprites in Assets/Art/UI/CharacterSelect.
    public static class CharacterSelectBuilder
    {
        const string Dir = "Assets/Art/UI/CharacterSelect/";
        const string RoundPath = "Assets/Art/UI/Dialogue/ui_round_corner.png";   // 9-slice, border 44 px
        const string CirclePath = Dir + "cs_circle.png";
        const string BackdropPath = Dir + "cs_backdrop.png";
        const string FontPath = "Assets/Art/Fonts/Roboto-Bold SDF.asset";
        const string NameMaterialPath = Dir + "SelectName_Outline.mat";
        const string TagMaterialPath = Dir + "SelectTag_Outline.mat";

        // 3D framing: camera closer than before, characters in the middle of each half, tall enough for the crowns.
        static readonly Vector3 CameraPosition = new(0f, 1.58f, -3.0f);
        const float CameraFov = 30f;
        const float FigureX = 0.72f;
        const float BackdropDistance = 6f;   // from the camera

        static readonly Color Orange = new(1f, 0.56f, 0.12f);
        static readonly Color Slate = new(0.14f, 0.18f, 0.28f, 0.9f);

        static readonly string[][] Glyphs = { new[] { "mountain", "point", "ridges" }, new[] { "wind", "drop", "wave" } };
        static readonly string[][] Moves =
        {
            new[] { "Sức mạnh của núi non", "Núi mọc theo tay chỉ", "Đất dâng thành bãi" },
            new[] { "Gọi gió, gió tới", "Hô mưa, mưa về", "Sóng nước nghe lệnh" },
        };

        static Sprite round;

        [MenuItem("Tools/Son Tinh Thuy Tinh/Rebuild Character Select in open scene")]
        public static void Rebuild()
        {
            var menu = Object.FindFirstObjectByType<CharacterSelectMenu>(FindObjectsInactive.Include);
            if (menu == null) { Debug.LogError("No CharacterSelectMenu in the open scene."); return; }
            var canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);

            var menuObject = new SerializedObject(menu);
            SerializedProperty options = menuObject.FindProperty("options");
            int count = options.arraySize;
            var characters = new CharacterDefinition[count];
            for (int i = 0; i < count; i++)
                characters[i] = (CharacterDefinition)options.GetArrayElementAtIndex(i).FindPropertyRelative("character").objectReferenceValue;

            round = AssetDatabase.LoadAssetAtPath<Sprite>(RoundPath);
            Sprite circle = GameplayHudBuilder.EnsureSprite(CirclePath, 256, 256, (u, v) => Mathf.Clamp01(126.5f - Vector2.Distance(new Vector2(u * 256f, v * 256f), new Vector2(128f, 128f)) + 0.5f));
            var glyphSprites = new Dictionary<string, Sprite>();
            foreach (string[] set in Glyphs)
                foreach (string name in set)
                    glyphSprites[name] = BuildGlyph(name);
            Sprite backdrop = BuildBackdrop();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            Material nameMaterial = EnsureMaterial(NameMaterialPath, font, 0.4f, new Color(0.05f, 0.03f, 0.02f), 0.12f);
            Material tagMaterial = EnsureMaterial(TagMaterialPath, font, 0.4f, Color.white, 0.12f);

            ArrangeScene(characters, backdrop);

            for (int i = canvas.transform.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(canvas.transform.GetChild(i).gameObject);

            var buttons = new Button[count];
            var panels = new CharacterSelectPanel[count];
            for (int i = 0; i < count; i++)
            {
                FillMoves(characters[i], i);
                panels[i] = BuildHalf(canvas.transform, characters[i].name, i, count, glyphSprites, Glyphs[Mathf.Min(i, Glyphs.Length - 1)], circle, font, nameMaterial, tagMaterial, out buttons[i]);
            }
            BuildCentre(canvas.transform, font, nameMaterial);
            ConnectNavigation(buttons);

            for (int i = 0; i < count; i++)
            {
                SerializedProperty option = options.GetArrayElementAtIndex(i);
                option.FindPropertyRelative("button").objectReferenceValue = buttons[i];
                option.FindPropertyRelative("panel").objectReferenceValue = panels[i];
            }
            menuObject.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
        }

        // ---------------------------------------------------------------- 3D scene

        static void ArrangeScene(CharacterDefinition[] characters, Sprite backdrop)
        {
            Camera camera = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
            camera.transform.SetPositionAndRotation(CameraPosition, Quaternion.identity);
            camera.fieldOfView = CameraFov;

            foreach (string hidden in new[] { "Stage", "Pedestal_SonTinh", "Pedestal_ThuyTinh" })
            {
                GameObject go = GameObject.Find(hidden);
                if (go != null) go.SetActive(false);
            }

            var previews = Object.FindObjectsByType<CharacterPreview>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (CharacterPreview preview in previews)
            {
                Vector3 p = preview.transform.position;
                preview.transform.position = new Vector3(Mathf.Sign(p.x) * FigureX, p.y, p.z);
                var so = new SerializedObject(preview);
                so.FindProperty("dimmedIntensity").floatValue = 22f;
                so.ApplyModifiedProperties();
            }

            // Bright backdrop and softer, brighter lighting than the old dark stage.
            GameObject keyLight = GameObject.Find("Key Light");
            if (keyLight != null) keyLight.GetComponent<Light>().intensity = 2.0f;
            RenderSettings.ambientLight = new Color(0.55f, 0.52f, 0.48f);

            GameObject backdropObject = GameObject.Find("Backdrop");
            if (backdropObject == null) backdropObject = new GameObject("Backdrop", typeof(SpriteRenderer));
            var renderer = backdropObject.GetComponent<SpriteRenderer>();
            renderer.sprite = backdrop;
            float height = 2f * BackdropDistance * Mathf.Tan(CameraFov * 0.5f * Mathf.Deg2Rad);
            float scale = height * 1.06f / (backdrop.rect.height / backdrop.pixelsPerUnit);
            backdropObject.transform.SetPositionAndRotation(CameraPosition + Vector3.forward * BackdropDistance, Quaternion.identity);
            backdropObject.transform.localScale = new Vector3(scale, scale, 1f);
        }

        static void FillMoves(CharacterDefinition character, int index)
        {
            var so = new SerializedObject(character);
            SerializedProperty moves = so.FindProperty("signatureMoves");
            if (moves.arraySize > 0) return;   // already written (or edited by hand)
            string[] lines = Moves[Mathf.Min(index, Moves.Length - 1)];
            moves.arraySize = lines.Length;
            for (int i = 0; i < lines.Length; i++) moves.GetArrayElementAtIndex(i).stringValue = lines[i];
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(character);
        }

        // ---------------------------------------------------------------- UI

        // The panel is anchored to its share of the screen width (0..0.5, 0.5..1 for two characters), whatever the window aspect, so its dimmer
        // always lines up with the split of the 3D scene (the camera is centred and the figures sit at +-FigureX).
        // The texts and rows live in a fixed 960 x 1080 frame ("Content") centred in the half and anchored to the bottom.
        static CharacterSelectPanel BuildHalf(Transform canvas, string key, int index, int count, Dictionary<string, Sprite> glyphSprites, string[] glyphNames, Sprite circle,
            TMP_FontAsset font, Material nameMaterial, Material tagMaterial, out Button button)
        {
            RectTransform root = GameplayHudBuilder.NewRect("Panel_" + key, canvas);
            root.anchorMin = new Vector2((float)index / count, 0f);
            root.anchorMax = new Vector2((index + 1f) / count, 1f);
            root.offsetMin = root.offsetMax = Vector2.zero;
            var panel = root.gameObject.AddComponent<CharacterSelectPanel>();

            Image dimmer = GameplayHudBuilder.NewImage("Dimmer", root, new Color(0f, 0f, 0f, 0f), null);
            GameplayHudBuilder.Stretch(dimmer.rectTransform, 0f);

            // Hit area for the mouse and the navigation target for keyboard / gamepad (no visuals).
            RectTransform hit = GameplayHudBuilder.NewRect("Button", root);
            hit.anchorMin = Vector2.zero;
            hit.anchorMax = new Vector2(1f, 0f);
            hit.pivot = Vector2.zero;
            hit.offsetMin = new Vector2(0f, 70f);
            hit.offsetMax = new Vector2(0f, 880f);
            Image hitImage = hit.gameObject.AddComponent<Image>();
            hitImage.color = new Color(0f, 0f, 0f, 0f);
            button = hit.gameObject.AddComponent<Button>();
            button.targetGraphic = hitImage;
            button.transition = Selectable.Transition.None;
            hit.gameObject.AddComponent<SelectOnHover>();

            RectTransform content = GameplayHudBuilder.NewRect("Content", root);
            content.anchorMin = content.anchorMax = new Vector2(0.5f, 0f);
            content.pivot = new Vector2(0.5f, 0f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(960f, 1080f);
            var group = content.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            // roster card (portrait + orange frame) and description box at top
            Image frame = Slice(GameplayHudBuilder.NewImage("RosterFrame", content, new Color(Orange.r, Orange.g, Orange.b, 0f), round), 22f);
            SetRect(frame.rectTransform, 38f, 874f, 154f, 154f);
            Image card = Slice(GameplayHudBuilder.NewImage("RosterCard", content, new Color(0.07f, 0.09f, 0.14f, 0.92f), round), 18f);
            SetRect(card.rectTransform, 48f, 884f, 134f, 134f);
            card.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            Image portrait = GameplayHudBuilder.NewImage("Portrait", card.transform, Color.white, null);
            portrait.rectTransform.anchorMin = portrait.rectTransform.anchorMax = portrait.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            portrait.rectTransform.anchoredPosition = new Vector2(0f, -8f);
            portrait.rectTransform.sizeDelta = new Vector2(168f, 168f);
            portrait.preserveAspect = true;

            // Naruto Storm "Strike Back" / Support Slots Next to Main Portrait
            TMP_Text strikeBackText = Text(content, "StrikeBackHeader", "STRIKE BACK", 18f, new Color(0.9f, 0.9f, 0.95f, 0.8f), font, FontStyles.Bold, TextAlignmentOptions.Center, null);
            SetRect(strikeBackText.rectTransform, 204f, 995f, 120f, 24f);

            for (int s = 0; s < 2; s++)
            {
                Image subCard = Slice(GameplayHudBuilder.NewImage($"SupportSlot_{s}", content, new Color(0.12f, 0.15f, 0.22f, 0.85f), round), 12f);
                SetRect(subCard.rectTransform, 204f + s * 58f, 934f, 52f, 56f);
                Image subIcon = GameplayHudBuilder.NewImage("Icon", subCard.transform, new Color(0.4f, 0.45f, 0.55f, 0.4f), circle);
                SetRect(subIcon.rectTransform, 10f, 12f, 32f, 32f);
            }

            Image descriptionBox = Slice(GameplayHudBuilder.NewImage("DescriptionBox", content, new Color(0.05f, 0.07f, 0.12f, 0.65f), round), 16f);
            SetRect(descriptionBox.rectTransform, 332f, 892f, 570f, 124f);
            TMP_Text description = Text(descriptionBox.transform, "Description", "", 25f, Color.white, font, FontStyles.Italic, TextAlignmentOptions.MidlineLeft, null);
            description.rectTransform.anchorMin = Vector2.zero; description.rectTransform.anchorMax = Vector2.one;
            description.rectTransform.offsetMin = new Vector2(22f, 8f); description.rectTransform.offsetMax = new Vector2(-22f, -8f);

            // Naruto Storm Style Character Roster Grid on Outer Edge (3 columns x 4 rows)
            float gridXOffset = (index == 0) ? 20f : 740f;
            Transform gridGroup = GameplayHudBuilder.NewRect("RosterGrid", content);
            SetRect(gridGroup.GetComponent<RectTransform>(), gridXOffset, 380f, 200f, 480f);
            for (int r = 0; r < 4; r++)
            {
                for (int c = 0; c < 3; c++)
                {
                    int slotIdx = r * 3 + c;
                    Image gridSlot = Slice(GameplayHudBuilder.NewImage($"Slot_{slotIdx}", gridGroup, new Color(0.08f, 0.10f, 0.16f, 0.85f), round), 10f);
                    SetRect(gridSlot.rectTransform, c * 64f, (3 - r) * 64f, 58f, 58f);

                    // Active character highlight
                    if ((index == 0 && slotIdx == 0) || (index == 1 && slotIdx == 1))
                    {
                        Image activeBorder = Slice(GameplayHudBuilder.NewImage("ActiveGlow", gridSlot.transform, Orange, round), 10f);
                        GameplayHudBuilder.Stretch(activeBorder.rectTransform, -3f);
                        activeBorder.gameObject.AddComponent<UIGradient>().Gradient = GameplayHudBuilder.Gold();
                    }
                    else
                    {
                        Image slotBorder = Slice(GameplayHudBuilder.NewImage("Border", gridSlot.transform, new Color(0.3f, 0.35f, 0.45f, 0.5f), round), 10f);
                        GameplayHudBuilder.Stretch(slotBorder.rectTransform, -1f);
                    }
                }
            }

            // tag, stacked name, subtitle
            TMP_Text tag = Text(content, "Tag", "P1", 108f, Color.white, font, FontStyles.Bold | FontStyles.Italic, TextAlignmentOptions.MidlineRight, tagMaterial);
            tag.textWrappingMode = TextWrappingModes.NoWrap;
            SetRect(tag.rectTransform, 30f, 392f, 300f, 130f);
            TMP_Text nameTop = Text(content, "NameTop", "", 118f, Color.white, font, FontStyles.Bold | FontStyles.Italic, TextAlignmentOptions.BottomLeft, nameMaterial);
            SetRect(nameTop.rectTransform, 330f, 436f, 620f, 134f);
            TMP_Text nameBottom = Text(content, "NameBottom", "", 118f, Color.white, font, FontStyles.Bold | FontStyles.Italic, TextAlignmentOptions.BottomLeft, nameMaterial);
            SetRect(nameBottom.rectTransform, 424f, 344f, 520f, 134f);
            TMP_Text subtitle = Text(content, "Subtitle", "", 36f, Color.white, font, FontStyles.Bold | FontStyles.Italic, TextAlignmentOptions.BottomLeft, nameMaterial);
            SetRect(subtitle.rectTransform, 340f, 288f, 600f, 46f);

            // three skill rows (Capsule / Pill shape with metallic glow like Naruto Storm 4)
            var moves = new TMP_Text[3];
            for (int i = 0; i < 3; i++)
            {
                Image row = Slice(GameplayHudBuilder.NewImage($"Move_{i}", content, new Color(0.08f, 0.11f, 0.18f, 0.92f), round), 30f);
                SetRect(row.rectTransform, 140f, 78f + (2 - i) * 68f, 680f, 58f);

                // Top highlight line for 3D metallic feel
                Image topGlow = Slice(GameplayHudBuilder.NewImage("Highlight", row.transform, new Color(1f, 1f, 1f, 0.15f), round), 15f);
                SetRect(topGlow.rectTransform, 10f, 40f, 660f, 12f);

                // Ninja scroll badge on left
                Image ring = GameplayHudBuilder.NewImage("IconRing", row.transform, Color.white, circle);
                SetRect(ring.rectTransform, 6f, 4f, 50f, 50f);
                ring.gameObject.AddComponent<UIGradient>().Gradient = GameplayHudBuilder.Gold();
                Image disc = GameplayHudBuilder.NewImage("IconDisc", ring.transform, new Color(0.06f, 0.08f, 0.14f), circle);
                SetRect(disc.rectTransform, 3f, 3f, 44f, 44f);
                Image glyph = GameplayHudBuilder.NewImage("Glyph", disc.transform, new Color(1f, 0.88f, 0.55f), glyphSprites[glyphNames[i]]);
                SetRect(glyph.rectTransform, 7f, 7f, 30f, 30f);

                moves[i] = Text(row.transform, "Label", "", 30f, new Color(0.96f, 0.97f, 1f), font, FontStyles.Bold, TextAlignmentOptions.Center, null);
                moves[i].rectTransform.anchorMin = Vector2.zero; moves[i].rectTransform.anchorMax = Vector2.one;
                moves[i].rectTransform.offsetMin = new Vector2(65f, 2f); moves[i].rectTransform.offsetMax = new Vector2(-20f, -2f);
            }

            var so = new SerializedObject(panel);
            so.FindProperty("content").objectReferenceValue = group;
            so.FindProperty("dimmer").objectReferenceValue = dimmer;
            so.FindProperty("tag").objectReferenceValue = tag;
            so.FindProperty("nameTop").objectReferenceValue = nameTop;
            so.FindProperty("nameBottom").objectReferenceValue = nameBottom;
            so.FindProperty("subtitle").objectReferenceValue = subtitle;
            so.FindProperty("description").objectReferenceValue = description;
            so.FindProperty("rosterPortrait").objectReferenceValue = portrait;
            so.FindProperty("rosterFrame").objectReferenceValue = frame;
            SerializedProperty list = so.FindProperty("moves");
            list.arraySize = moves.Length;
            for (int i = 0; i < moves.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = moves[i];
            so.ApplyModifiedProperties();
            return panel;
        }

        static void BuildCentre(Transform canvas, TMP_FontAsset font, Material nameMaterial)
        {
            // everything here is centred horizontally on the screen, the same line the halves and the 3D scene split on
            Image divider = GameplayHudBuilder.NewImage("Divider", canvas, new Color(1f, 1f, 1f, 0.35f), null);
            SetRectCentred(divider.rectTransform, 0f, 70f, 2f, 940f);

            TMP_Text title = Text(canvas, "Title", "CHỌN NHÂN VẬT", 32f, Color.white, font, FontStyles.Bold, TextAlignmentOptions.Center, nameMaterial);
            title.characterSpacing = 10f;
            SetRectCentred(title.rectTransform, 0f, 1024f, 800f, 48f);

            Image bar = GameplayHudBuilder.NewImage("HintBar", canvas, new Color(0f, 0f, 0f, 0.58f), null);
            bar.rectTransform.anchorMin = Vector2.zero;
            bar.rectTransform.anchorMax = new Vector2(1f, 0f);
            bar.rectTransform.pivot = new Vector2(0.5f, 0f);
            bar.rectTransform.offsetMin = Vector2.zero;
            bar.rectTransform.offsetMax = new Vector2(0f, 58f);

            // key caps + labels, centred as a group
            string[,] hints = { { "< >", "Chọn nhân vật" }, { "Enter", "Xác nhận" }, { "Esc", "Quay lại" } };
            const float capHeight = 40f, gap = 14f, spacing = 56f;
            var capWidths = new float[3]; var labelWidths = new float[3];
            float total = 0f;
            for (int i = 0; i < 3; i++)
            {
                capWidths[i] = Mathf.Max(capHeight, hints[i, 0].Length * 18f + 30f);
                var probe = Text(bar.transform, "Probe", hints[i, 1], 30f, Color.white, font, FontStyles.Bold, TextAlignmentOptions.Left, null);
                labelWidths[i] = probe.GetPreferredValues(hints[i, 1]).x + 6f;
                Object.DestroyImmediate(probe.gameObject);
                total += capWidths[i] + gap + labelWidths[i] + (i < 2 ? spacing : 0f);
            }
            RectTransform keys = GameplayHudBuilder.NewRect("Keys", bar.transform);
            keys.anchorMin = keys.anchorMax = new Vector2(0.5f, 0f);
            keys.pivot = new Vector2(0.5f, 0f);
            keys.anchoredPosition = Vector2.zero;
            keys.sizeDelta = new Vector2(total, 58f);

            float x = 0f;
            for (int i = 0; i < 3; i++)
            {
                Image cap = Slice(GameplayHudBuilder.NewImage("KeyCap_" + hints[i, 0], keys, new Color(0.93f, 0.94f, 0.97f), round), 10f);
                SetRect(cap.rectTransform, x, 9f, capWidths[i], capHeight);
                TMP_Text keyText = Text(cap.transform, "Key", hints[i, 0], 26f, new Color(0.10f, 0.11f, 0.16f), font, FontStyles.Bold, TextAlignmentOptions.Center, null);
                keyText.rectTransform.anchorMin = Vector2.zero; keyText.rectTransform.anchorMax = Vector2.one;
                keyText.rectTransform.offsetMin = keyText.rectTransform.offsetMax = Vector2.zero;
                x += capWidths[i] + gap;

                TMP_Text label = Text(keys, "Label_" + hints[i, 1], hints[i, 1], 30f, Color.white, font, FontStyles.Bold, TextAlignmentOptions.Left, null);
                SetRect(label.rectTransform, x, 7f, labelWidths[i], 44f);
                x += labelWidths[i] + spacing;
            }
        }

        static void ConnectNavigation(Button[] buttons)
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                var navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnLeft = i > 0 ? buttons[i - 1] : null,
                    selectOnRight = i < buttons.Length - 1 ? buttons[i + 1] : null,
                };
                buttons[i].navigation = navigation;
            }
        }

        // ---------------------------------------------------------------- small helpers

        static void SetRect(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }

        // Bottom-centre anchored: x is the offset of the rect's centre from the middle of the screen, y the distance of its bottom edge from the screen bottom.
        static void SetRectCentred(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }

        static Image Slice(Image image, float radius)
        {
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 44f / radius;   // the sprite's border is 44 px; this makes the corner `radius` canvas pixels
            return image;
        }

        static TMP_Text Text(Transform parent, string name, string text, float size, Color color, TMP_FontAsset font, FontStyles style, TextAlignmentOptions alignment, Material material)
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
            label.fontStyle = style;
            label.alignment = alignment;
            label.color = color;
            label.raycastTarget = false;
            return label;
        }

        // Own material (so other texts using the font are untouched): thick outline plus a soft drop shadow.
        static Material EnsureMaterial(string path, TMP_FontAsset font, float outlineWidth, Color outlineColor, float faceDilate)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                material = new Material(font.material) { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(material, path);
            }
            material.EnableKeyword("OUTLINE_ON");
            material.SetFloat("_FaceDilate", faceDilate);
            material.SetFloat("_OutlineWidth", outlineWidth);
            material.SetColor("_OutlineColor", outlineColor);
            material.EnableKeyword("UNDERLAY_ON");
            material.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.55f));
            material.SetFloat("_UnderlayOffsetX", 0.7f);
            material.SetFloat("_UnderlayOffsetY", -0.7f);
            material.SetFloat("_UnderlayDilate", 0.3f);
            material.SetFloat("_UnderlaySoftness", 0.25f);
            EditorUtility.SetDirty(material);
            return material;
        }

        // ---------------------------------------------------------------- generated art

        // Smoothstep between two edges (Mathf.SmoothStep takes a 0..1 parameter, not edges).
        static float Edge(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        // Gold background on the left (earth, Sơn Tinh), cool blue on the right (water, Thủy Tinh): soft glow behind each head,
        // faint concentric rings like a bronze drum, brush-like streaks, dark edges.
        static Sprite BuildBackdrop()
        {
            const int W = 1536, H = 864;
            var pixels = new Color[W * H];
            float[] rings = { 0.20f, 0.28f, 0.36f, 0.46f, 0.58f };
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = (x + 0.5f) / W, v = (y + 0.5f) / H;
                    bool left = u < 0.5f;
                    float lu = (left ? u : u - 0.5f) / 0.5f;
                    Color top = left ? new Color(1f, 0.92f, 0.66f) : new Color(0.72f, 0.90f, 0.94f);
                    Color bottom = left ? new Color(0.90f, 0.68f, 0.36f) : new Color(0.30f, 0.58f, 0.76f);
                    Color glow = left ? new Color(1f, 0.97f, 0.80f) : new Color(0.90f, 0.98f, 0.99f);
                    Color dark = left ? new Color(0.35f, 0.20f, 0.08f) : new Color(0.04f, 0.12f, 0.22f);

                    Color c = Color.Lerp(bottom, top, Mathf.SmoothStep(0f, 1f, v));
                    float dx = (lu - 0.5f) * (0.5f * W) / H, dy = v - 0.60f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    c = Color.Lerp(c, glow, Mathf.Exp(-r * r / 0.10f) * 0.85f);

                    float ring = 0f;
                    foreach (float radius in rings) ring = Mathf.Max(ring, 1f - Edge(0f, 0.0035f, Mathf.Abs(r - radius)));
                    c = Color.Lerp(c, dark, ring * 0.22f);

                    float streak = Mathf.PerlinNoise(x * 0.004f + (left ? 0f : 50f), y * 0.045f + x * 0.006f);
                    c *= 0.94f + 0.12f * streak;

                    float edge = Mathf.Max(Mathf.Abs(lu - 0.5f) * 2f, Mathf.Abs(v - 0.5f) * 2f);
                    c = Color.Lerp(c, dark, Mathf.Pow(edge, 3.5f) * 0.24f);
                    c = Color.Lerp(c, dark, (1f - Edge(0f, 0.012f, Mathf.Abs(u - 0.5f))) * 0.6f);
                    c.a = 1f;
                    pixels[y * W + x] = c;
                }

            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();
            Directory.CreateDirectory(Dir);
            File.WriteAllBytes(BackdropPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(BackdropPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(BackdropPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(BackdropPath);
        }

        static Sprite BuildGlyph(string name)
        {
            string path = Dir + "cs_glyph_" + name + ".png";
            if (File.Exists(path)) AssetDatabase.DeleteAsset(path);   // regenerate, so the shapes below can be tuned
            return GameplayHudBuilder.EnsureSprite(path, 128, 128, (u, v) => Mathf.Clamp01(0.5f - GlyphDistance(name, new Vector2(u * 128f, v * 128f))));
        }

        // Signed distance (pixels, negative inside) to the glyph, y up, 128 x 128.
        static float GlyphDistance(string name, Vector2 p)
        {
            switch (name)
            {
                case "mountain":
                    return Mathf.Min(Triangle(p, new Vector2(6, 22), new Vector2(48, 98), new Vector2(90, 22)), Triangle(p, new Vector2(50, 22), new Vector2(90, 72), new Vector2(124, 22)));
                case "point":
                    return Mathf.Min(Triangle(p, new Vector2(64, 116), new Vector2(18, 58), new Vector2(110, 58)), Box(p, new Vector2(64, 40), new Vector2(15, 34)));
                case "ridges":
                    return Mathf.Min(Box(p, new Vector2(64, 28), new Vector2(54, 11), 9f), Mathf.Min(Box(p, new Vector2(64, 62), new Vector2(40, 11), 9f), Box(p, new Vector2(64, 96), new Vector2(26, 11), 9f)));
                case "drop":
                    return Mathf.Min(Vector2.Distance(p, new Vector2(64, 44)) - 34f, Triangle(p, new Vector2(64, 120), new Vector2(34, 58), new Vector2(94, 58)));
                case "wave":
                    return Mathf.Min(Polyline(p, Sine(10f, 118f, 90f, 15f, 18f)), Polyline(p, Sine(10f, 118f, 44f, 15f, 18f))) - 6.5f;
                case "wind":
                    return Polyline(p, Spiral(new Vector2(60, 64), 6f, 46f, 2.1f)) - 6.5f;
                default:
                    return 1000f;
            }
        }

        static float Box(Vector2 p, Vector2 centre, Vector2 half, float rounding = 0f)
        {
            Vector2 q = new Vector2(Mathf.Abs(p.x - centre.x), Mathf.Abs(p.y - centre.y)) - half + Vector2.one * rounding;
            return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - rounding;
        }

        static float Triangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            Vector2 e0 = b - a, e1 = c - b, e2 = a - c, v0 = p - a, v1 = p - b, v2 = p - c;
            Vector2 pq0 = v0 - e0 * Mathf.Clamp01(Vector2.Dot(v0, e0) / Vector2.Dot(e0, e0));
            Vector2 pq1 = v1 - e1 * Mathf.Clamp01(Vector2.Dot(v1, e1) / Vector2.Dot(e1, e1));
            Vector2 pq2 = v2 - e2 * Mathf.Clamp01(Vector2.Dot(v2, e2) / Vector2.Dot(e2, e2));
            float s = Mathf.Sign(e0.x * e2.y - e0.y * e2.x);
            Vector2 d0 = new(Vector2.Dot(pq0, pq0), s * (v0.x * e0.y - v0.y * e0.x));
            Vector2 d1 = new(Vector2.Dot(pq1, pq1), s * (v1.x * e1.y - v1.y * e1.x));
            Vector2 d2 = new(Vector2.Dot(pq2, pq2), s * (v2.x * e2.y - v2.y * e2.x));
            Vector2 d = d0.x < d1.x ? d0 : d1;
            if (d2.x < d.x) d = d2;
            return -Mathf.Sqrt(d.x) * Mathf.Sign(d.y);
        }

        static float Polyline(Vector2 p, Vector2[] points)
        {
            float best = float.MaxValue;
            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector2 a = points[i], ab = points[i + 1] - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
                best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
            }
            return best;
        }

        static Vector2[] Sine(float x0, float x1, float y, float amplitude, float wavelength)
        {
            var points = new Vector2[61];
            for (int i = 0; i < points.Length; i++)
            {
                float x = Mathf.Lerp(x0, x1, i / 60f);
                points[i] = new Vector2(x, y + amplitude * Mathf.Sin((x - x0) / wavelength * Mathf.PI));
            }
            return points;
        }

        static Vector2[] Spiral(Vector2 centre, float startRadius, float endRadius, float turns)
        {
            var points = new Vector2[121];
            for (int i = 0; i < points.Length; i++)
            {
                float t = i / 120f, angle = t * turns * 2f * Mathf.PI;
                points[i] = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Mathf.Lerp(startRadius, endRadius, t);
            }
            return points;
        }
    }
}
