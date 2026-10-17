using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SonTinhThuyTinh.Cutscene
{
    // The screen furniture of a cutscene, built at runtime (no assets): black letterbox bars that slide in, fades to a colour, a flash
    // (lightning), a name card (big name + a line under it) and short pop-up words. Everything runs in unscaled time, so it keeps working
    // through slow motion. The bars and cards sit under the dialogue box (sorting order below `dialogueOrder`), fades and flashes over it.
    public class CinematicFx : MonoBehaviour
    {
        RectTransform top, bottom;
        Image fade, flash;
        CanvasGroup cardGroup, popGroup;
        TMP_Text cardBig, cardSmall, popText;
        RectTransform cardRect, popRect;
        float barSize;

        public static CinematicFx Create(TMP_FontAsset font, int dialogueOrder)
        {
            var go = new GameObject("CinematicFx");
            var fx = go.AddComponent<CinematicFx>();
            fx.Build(font, dialogueOrder);
            return fx;
        }

        static Canvas MakeCanvas(Transform parent, string name, int order)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        static Image MakeImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color; img.raycastTarget = false;
            return img;
        }

        static TMP_Text MakeText(Transform parent, string name, TMP_FontAsset font, float size, Color color, FontStyles style, float spacing)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size; t.color = color; t.fontStyle = style; t.characterSpacing = spacing;
            t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false; t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        void Build(TMP_FontAsset font, int dialogueOrder)
        {
            barSize = 130f;
            var below = MakeCanvas(transform, "Below", dialogueOrder - 1);
            var above = MakeCanvas(transform, "Above", dialogueOrder + 5);

            top = Bar(below.transform, "BarTop", true);
            bottom = Bar(below.transform, "BarBottom", false);

            var card = new GameObject("NameCard", typeof(RectTransform), typeof(CanvasGroup));
            card.transform.SetParent(below.transform, false);
            cardRect = (RectTransform)card.transform;
            cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f); cardRect.sizeDelta = new Vector2(1600f, 300f);
            cardRect.anchoredPosition = new Vector2(0f, 20f);
            cardGroup = card.GetComponent<CanvasGroup>(); cardGroup.alpha = 0f;
            var band = MakeImage(card.transform, "Band", new Color(0f, 0f, 0f, 0.42f));
            var bandRt = (RectTransform)band.transform;
            bandRt.anchorMin = new Vector2(0f, 0.5f); bandRt.anchorMax = new Vector2(1f, 0.5f); bandRt.sizeDelta = new Vector2(1200f, 280f);
            bandRt.anchoredPosition = new Vector2(0f, 0f);
            cardBig = MakeText(card.transform, "Big", font, 120f, new Color(0.98f, 0.92f, 0.78f), FontStyles.Bold, 14f);
            cardSmall = MakeText(card.transform, "Small", font, 44f, new Color(0.62f, 0.86f, 1f), FontStyles.Italic, 6f);
            Place((RectTransform)cardBig.transform, 40f, 160f);
            Place((RectTransform)cardSmall.transform, -70f, 60f);

            var pop = new GameObject("Pop", typeof(RectTransform), typeof(CanvasGroup));
            pop.transform.SetParent(below.transform, false);
            popRect = (RectTransform)pop.transform;
            popRect.anchorMin = popRect.anchorMax = new Vector2(0.5f, 0.5f); popRect.sizeDelta = new Vector2(1600f, 200f);
            popRect.anchoredPosition = new Vector2(0f, 0f);
            popGroup = pop.GetComponent<CanvasGroup>(); popGroup.alpha = 0f;
            popText = MakeText(pop.transform, "Text", font, 96f, new Color(1f, 0.9f, 0.6f), FontStyles.Bold, 10f);
            Place((RectTransform)popText.transform, 0f, 200f);

            fade = MakeImage(above.transform, "Fade", new Color(0f, 0f, 0f, 1f));
            Stretch((RectTransform)fade.transform);
            flash = MakeImage(above.transform, "Flash", new Color(1f, 1f, 1f, 0f));
            Stretch((RectTransform)flash.transform);
        }

        RectTransform Bar(Transform parent, string name, bool isTop)
        {
            var img = MakeImage(parent, name, Color.black);
            var rt = (RectTransform)img.transform;
            rt.anchorMin = new Vector2(0f, isTop ? 1f : 0f); rt.anchorMax = new Vector2(1f, isTop ? 1f : 0f); rt.pivot = new Vector2(0.5f, isTop ? 1f : 0f);
            rt.sizeDelta = new Vector2(0f, barSize);
            rt.anchoredPosition = new Vector2(0f, isTop ? barSize : -barSize);   // hidden off screen
            return rt;
        }

        static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }

        static void Place(RectTransform rt, float y, float height)
        {
            rt.anchorMin = new Vector2(0f, 0.5f); rt.anchorMax = new Vector2(1f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, y); rt.sizeDelta = new Vector2(0f, height);
        }

        // ---------------------------------------------------------------- effects

        public IEnumerator Letterbox(bool on, float seconds = 0.8f)
        {
            float from = top.anchoredPosition.y, toTop = on ? 0f : barSize, toBottom = on ? 0f : -barSize, fromB = bottom.anchoredPosition.y;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / seconds);
                top.anchoredPosition = new Vector2(0f, Mathf.Lerp(from, toTop, k));
                bottom.anchoredPosition = new Vector2(0f, Mathf.Lerp(fromB, toBottom, k));
                yield return null;
            }
            top.anchoredPosition = new Vector2(0f, toTop); bottom.anchoredPosition = new Vector2(0f, toBottom);
        }

        public void LetterboxNow(bool on)
        {
            top.anchoredPosition = new Vector2(0f, on ? 0f : barSize);
            bottom.anchoredPosition = new Vector2(0f, on ? 0f : -barSize);
        }

        // Fade the screen cover to `alpha` of `color` (0 = clear, 1 = black).
        public IEnumerator Fade(float alpha, float seconds = 1f, Color? color = null)
        {
            Color c = color ?? new Color(0f, 0f, 0f);
            float from = fade.color.a;
            fade.color = new Color(c.r, c.g, c.b, from);
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                fade.color = new Color(c.r, c.g, c.b, Mathf.Lerp(from, alpha, t / seconds));
                yield return null;
            }
            fade.color = new Color(c.r, c.g, c.b, alpha);
        }

        public void FadeNow(float alpha) => fade.color = new Color(fade.color.r, fade.color.g, fade.color.b, alpha);

        // A bright flash that dies away (lightning, an impact).
        Coroutine flashing;
        public void Flash(Color color, float seconds = 0.35f, float strength = 0.8f)
        {
            if (flashing != null) StopCoroutine(flashing);
            flashing = StartCoroutine(FlashRoutine(color, seconds, strength));
        }

        IEnumerator FlashRoutine(Color color, float seconds, float strength)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                float k = 1f - t / seconds;
                flash.color = new Color(color.r, color.g, color.b, strength * k * k);
                yield return null;
            }
            flash.color = new Color(color.r, color.g, color.b, 0f);
        }

        // Big name + a line under it: fades and drifts in, holds, fades out. Returns when it is gone.
        public IEnumerator NameCard(string big, string small, float hold = 2.2f, float fadeSeconds = 0.6f)
        {
            cardBig.text = big; cardSmall.text = small;
            cardGroup.alpha = 0f;
            for (float t = 0f; t < fadeSeconds; t += Time.unscaledDeltaTime)
            {
                float k = t / fadeSeconds;
                cardGroup.alpha = k; cardRect.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, k);
                yield return null;
            }
            cardGroup.alpha = 1f; cardRect.localScale = Vector3.one;
            for (float t = 0f; t < hold; t += Time.unscaledDeltaTime) { cardRect.localScale = Vector3.one * (1f + 0.01f * t); yield return null; }
            for (float t = 0f; t < fadeSeconds; t += Time.unscaledDeltaTime) { cardGroup.alpha = 1f - t / fadeSeconds; yield return null; }
            cardGroup.alpha = 0f;
        }

        public void ShowName(string big, string small, float hold = 2.2f) => StartCoroutine(NameCard(big, small, hold));

        // A word that snaps in large and settles back, then fades.
        public IEnumerator Pop(string text, float hold = 1.2f, Color? color = null)
        {
            popText.text = text; popText.color = color ?? new Color(1f, 0.9f, 0.6f);
            popGroup.alpha = 0f;
            for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.25f;
                popGroup.alpha = k; popRect.localScale = Vector3.one * Mathf.Lerp(1.5f, 1f, 1f - (1f - k) * (1f - k));
                yield return null;
            }
            popGroup.alpha = 1f; popRect.localScale = Vector3.one;
            yield return new WaitForSecondsRealtime(hold);
            for (float t = 0f; t < 0.4f; t += Time.unscaledDeltaTime) { popGroup.alpha = 1f - t / 0.4f; yield return null; }
            popGroup.alpha = 0f;
        }

        CanvasGroup epilogueGroup;

        // The epilogue card: a dark veil with a title, a few lines and a prompt, fading in; it stays until the scene ends.
        public IEnumerator Epilogue(string title, string body, string prompt, float fadeSeconds = 1f)
        {
            if (epilogueGroup == null)
            {
                var canvas = MakeCanvas(transform, "Epilogue", 500);
                epilogueGroup = canvas.gameObject.AddComponent<CanvasGroup>();
                var dim = MakeImage(canvas.transform, "Dim", new Color(0f, 0f, 0f, 0.9f));
                Stretch((RectTransform)dim.transform);
                var font = cardBig.font;
                var t = MakeText(canvas.transform, "Title", font, 96f, new Color(0.98f, 0.92f, 0.78f), FontStyles.Bold, 12f);
                Place((RectTransform)t.transform, 150f, 140f); t.text = title.ToUpper();
                var b = MakeText(canvas.transform, "Body", font, 38f, Color.white, FontStyles.Italic, 2f);
                Place((RectTransform)b.transform, 10f, 200f); b.textWrappingMode = TextWrappingModes.Normal; b.text = body;
                var p = MakeText(canvas.transform, "Prompt", font, 28f, new Color(1f, 1f, 1f, 0.7f), FontStyles.Normal, 2f);
                Place((RectTransform)p.transform, -170f, 50f); p.text = prompt;
            }
            epilogueGroup.alpha = 0f;
            for (float t = 0f; t < fadeSeconds; t += Time.unscaledDeltaTime) { epilogueGroup.alpha = t / fadeSeconds; yield return null; }
            epilogueGroup.alpha = 1f;
        }

        public void HideAll()
        {
            StopAllCoroutines();
            cardGroup.alpha = 0f; popGroup.alpha = 0f;
            flash.color = new Color(1f, 1f, 1f, 0f);
        }
    }
}
