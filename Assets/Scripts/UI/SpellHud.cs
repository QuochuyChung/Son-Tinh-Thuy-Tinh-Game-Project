using SonTinhThuyTinh.Combat;
using SonTinhThuyTinh.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SonTinhThuyTinh.UI
{
    // The three spell slots (U / I / O) at the bottom, between the health bars and the map circle, with a clock-wipe for the cooldown. Built in code when a player with spells is
    // spawned (PlayerSpawner), so no scene needs editing.
    public class SpellHud : MonoBehaviour
    {
        const float Slot = 92f, Gap = 34f;

        PlayerController player;
        SpellData[] spells;
        Image[] cooldown;
        TMP_Text[] timers;
        Image[] frames;

        public static SpellHud Create(PlayerController owner)
        {
            if (owner == null || owner.MoveSet == null || owner.MoveSet.spells == null || owner.MoveSet.spells.Length == 0) return null;
            SpellHud existing = FindFirstObjectByType<SpellHud>(FindObjectsInactive.Include);
            if (existing != null)
            {
                existing.gameObject.SetActive(true);
                return existing;
            }
            var go = new GameObject("SpellHud", typeof(RectTransform));
            var hud = go.AddComponent<SpellHud>();
            hud.Build(owner);
            return hud;
        }

        static Sprite disc;

        static Sprite Disc()
        {
            if (disc != null) return disc;
            const int size = 96;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(size * 0.5f - d)));
                }
            tex.Apply();
            disc = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return disc;
        }

        void Build(PlayerController owner)
        {
            player = owner;
            spells = owner.MoveSet.spells;
            TMP_FontAsset font = owner.MoveSet.hudFont;

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 45;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            string[] keys = { "U", "I", "O" };
            cooldown = new Image[spells.Length];
            timers = new TMP_Text[spells.Length];
            frames = new Image[spells.Length];
            float total = spells.Length * Slot + (spells.Length - 1) * Gap;
            for (int i = 0; i < spells.Length; i++)
            {
                SpellData spell = spells[i];
                var slot = NewRect("Slot" + i, transform, new Vector2(0.66f, 0f), new Vector2(Slot, Slot));
                slot.anchoredPosition = new Vector2(-total * 0.5f + Slot * 0.5f + i * (Slot + Gap), 112f);

                frames[i] = Child(slot, "Ring", spell.color, Slot, Disc());
                Image inner = Child(slot, "Back", new Color(0.04f, 0.07f, 0.12f, 0.92f), Slot - 10f, Disc());
                Image glow = Child(slot, "Glow", new Color(spell.color.r, spell.color.g, spell.color.b, 0.55f), Slot - 30f, Disc());
                cooldown[i] = Child(slot, "Cooldown", new Color(0f, 0f, 0f, 0.72f), Slot - 10f, Disc());
                cooldown[i].type = Image.Type.Filled;
                cooldown[i].fillMethod = Image.FillMethod.Radial360;
                cooldown[i].fillOrigin = 2;
                cooldown[i].fillClockwise = false;
                cooldown[i].fillAmount = 0f;

                Label(slot, "Key", keys[i], 38f, Color.white, font, new Vector2(0f, 6f), Slot);
                timers[i] = Label(slot, "Timer", "", 30f, new Color(1f, 0.95f, 0.8f), font, new Vector2(0f, 6f), Slot);
                Label(slot, "Name", spell.displayName, 22f, Color.white, font, new Vector2(0f, -Slot * 0.5f - 18f), Slot + Gap);
                inner.raycastTarget = false;
                glow.raycastTarget = false;
            }
        }

        static RectTransform NewRect(string name, Transform parent, Vector2 anchor, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            return rect;
        }

        static Image Child(RectTransform parent, string name, Color color, float size, Sprite sprite)
        {
            var image = NewRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(size, size)).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static TMP_Text Label(RectTransform parent, string name, string text, float size, Color color, TMP_FontAsset font, Vector2 offset, float width)
        {
            RectTransform rect = NewRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(width, 40f));
            rect.anchoredPosition = offset;
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) label.font = font;
            label.text = text;
            label.fontSize = size;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = color;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            return label;
        }

        void Update()
        {
            if (player == null) { Destroy(gameObject); return; }
            for (int i = 0; i < spells.Length; i++)
            {
                float left = player.SpellCooldownLeft(i);
                cooldown[i].fillAmount = spells[i].cooldown > 0f ? left / spells[i].cooldown : 0f;
                timers[i].text = left > 0.05f ? Mathf.CeilToInt(left).ToString() : "";
                frames[i].color = left > 0.05f ? new Color(spells[i].color.r, spells[i].color.g, spells[i].color.b, 0.35f) : spells[i].color;
            }
        }
    }
}
