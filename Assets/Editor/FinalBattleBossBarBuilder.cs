using System.Linq;
using SonTinhThuyTinh.Combat;
using SonTinhThuyTinh.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UImage = UnityEngine.UI.Image;

namespace SonTinhThuyTinh.EditorTools
{
    // Boss health bar for Map_FinalBattle: a top-center bar (name, phase label, frame/back/trail/fill, half mark) plus the two
    // phase dots under it. Both dots start lit; BossArenaDuel flips dot 1 to gray at 50% HP through BossHealthBar.SetPhase2.
    // Rebuild-safe (destroys the old canvas first):  menu Tools > Son Tinh Thuy Tinh > Build Final Battle Boss Bar, or
    // unity command eval "return SonTinhThuyTinh.EditorTools.FinalBattleBossBarBuilder.Build();"
    public static class FinalBattleBossBarBuilder
    {
        const string ScenePath = "Assets/Scenes/Map_FinalBattle.unity";
        const string CanvasName = "BossBarCanvas";
        const string WhiteSprite = "Assets/Art/UI/HUD/hud_white.png";
        const string CircleSprite = "Assets/Art/UI/Map/map_circle.png";

        [MenuItem("Tools/Son Tinh Thuy Tinh/Build Final Battle Boss Bar")]
        public static void BuildMenu() => Debug.Log(Build());

        public static string Build()
        {
            var active = EditorSceneManager.GetActiveScene();
            if (active.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return "aborted: unsaved changes in " + active.name;
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var scene = EditorSceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == CanvasName) Object.DestroyImmediate(root);

            var bar = BuildBar();

            var duel = Object.FindFirstObjectByType<BossArenaDuel>(FindObjectsInactive.Include);
            if (duel == null) return "error: BossArenaDuel not found in " + scene.name;
            var so = new SerializedObject(duel);
            var barProp = so.FindProperty("bar");
            if (barProp == null) return "error: BossArenaDuel has no serialized 'bar' field";
            barProp.objectReferenceValue = bar;
            so.FindProperty("phase2Entered").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return "built BossBarCanvas + wired BossArenaDuel.bar in " + scene.path;
        }

        static BossHealthBar BuildBar()
        {
            var white = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSprite);
            var circle = AssetDatabase.LoadAssetAtPath<Sprite>(CircleSprite);
            if (white == null) return null;
            var font = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(t => t.font != null)?.font;

            var canvasGo = new GameObject(CanvasName, typeof(Canvas), typeof(CanvasScaler));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 4;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = 0.5f;

            var barGo = new GameObject("BossBar", typeof(RectTransform), typeof(CanvasGroup));
            barGo.transform.SetParent(canvasGo.transform, false);
            var rt = (RectTransform)barGo.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f); rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -70f); rt.sizeDelta = new Vector2(920f, 26f);

            UImage Img(string name, Color color, Vector2 inset, bool filled)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(UImage));
                go.transform.SetParent(barGo.transform, false);
                var r = (RectTransform)go.transform;
                r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = -inset; r.offsetMax = inset;
                var img = go.GetComponent<UImage>(); img.sprite = white; img.color = color; img.raycastTarget = false;
                if (filled) { img.type = UImage.Type.Filled; img.fillMethod = UImage.FillMethod.Horizontal; img.fillOrigin = 0; img.fillAmount = 1f; }
                return img;
            }
            Img("Frame", new Color(0.85f, 0.68f, 0.30f), new Vector2(4f, 4f), false);
            Img("Back", new Color(0.05f, 0.06f, 0.08f, 0.9f), Vector2.zero, false);
            var trail = Img("Trail", new Color(1f, 0.62f, 0.2f), Vector2.zero, true);
            var fill = Img("Fill", new Color(0.8f, 0.16f, 0.12f), Vector2.zero, true);
            var mark = Img("HalfMark", new Color(1f, 0.95f, 0.8f, 0.9f), Vector2.zero, false);
            var mr = mark.rectTransform;
            mr.anchorMin = new Vector2(0.5f, 0f); mr.anchorMax = new Vector2(0.5f, 1f);
            mr.offsetMin = new Vector2(-1.5f, -4f); mr.offsetMax = new Vector2(1.5f, 4f);

            TMP_Text Label(string name, string text, TextAlignmentOptions align, Vector2 anchor, float size)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
                go.transform.SetParent(barGo.transform, false);
                var r = (RectTransform)go.transform;
                r.anchorMin = r.anchorMax = anchor; r.pivot = new Vector2(anchor.x, 0f);
                r.anchoredPosition = new Vector2(0f, 8f); r.sizeDelta = new Vector2(600f, 44f);
                var t = go.GetComponent<TextMeshProUGUI>();
                if (font != null) t.font = font;
                t.text = text; t.alignment = align; t.fontSize = size; t.color = new Color(0.98f, 0.93f, 0.80f); t.raycastTarget = false;
                t.fontStyle = FontStyles.Bold;
                return t;
            }
            var nameLabel = Label("Name", "Boss", TextAlignmentOptions.BottomLeft, new Vector2(0f, 1f), 34f);
            var phaseLabel = Label("Phase", "", TextAlignmentOptions.BottomRight, new Vector2(1f, 1f), 26f);
            phaseLabel.color = new Color(1f, 0.55f, 0.85f);

            // Two phase dots below the bar centre; both start lit (dotLitColor), SetPhase2 only recolors them.
            UImage Dot(string name, float x)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(UImage));
                go.transform.SetParent(barGo.transform, false);
                var r = (RectTransform)go.transform;
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0f); r.pivot = new Vector2(0.5f, 0f);
                r.anchoredPosition = new Vector2(x, -14f); r.sizeDelta = new Vector2(16f, 16f);
                var img = go.GetComponent<UImage>();
                img.sprite = circle != null ? circle : white;
                img.color = new Color(0.95f, 0.85f, 0.35f);   // dotLitColor
                img.raycastTarget = false;
                return img;
            }
            var dot1 = Dot("PhaseDot1", -14f);
            var dot2 = Dot("PhaseDot2", 14f);

            var bar = barGo.AddComponent<BossHealthBar>();
            var so = new SerializedObject(bar);
            so.FindProperty("group").objectReferenceValue = barGo.GetComponent<CanvasGroup>();
            so.FindProperty("fill").objectReferenceValue = fill;
            so.FindProperty("trail").objectReferenceValue = trail;
            so.FindProperty("nameLabel").objectReferenceValue = nameLabel;
            so.FindProperty("phaseLabel").objectReferenceValue = phaseLabel;
            so.FindProperty("phaseDot1").objectReferenceValue = dot1;
            so.FindProperty("phaseDot2").objectReferenceValue = dot2;
            so.ApplyModifiedPropertiesWithoutUndo();
            barGo.GetComponent<CanvasGroup>().alpha = 0f;
            return bar;
        }
    }
}
