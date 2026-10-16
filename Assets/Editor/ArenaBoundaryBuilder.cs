using SonTinhThuyTinh.Combat;
using SonTinhThuyTinh.Quest;
using SonTinhThuyTinh.UI;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace SonTinhThuyTinh.EditorTools
{
    // Fight limits and the crocodile's intro, added to the existing scenes (no map rebuild):
    //  - Map_ThuyTinh, under SauChinDuoiArena: an ArenaBoundary round the island (arena radius + 1 m, water blue), the intro cutscene
    //    camera (CinemachineCamera, priority 100, off until the cutscene), and on the boss bar's canvas the letterbox bars and the name
    //    card "SẤU CHÍN ĐUÔI"; all linked to SauChinDuoiBoss.
    //  - Map_SonTinh, under RoosterQuest: an ArenaBoundary inside the boulder ring (ring radius + 1 m, warm orange), linked to
    //    RoosterQuestDirector.
    // SauChinDuoiBuilder.BuildArena and RoosterQuestBuilder.BuildQuest call the same steps, so rebuilding those keeps them.
    public static class ArenaBoundaryBuilder
    {
        const string MaterialPath = "Assets/Art/Combat/Mat_ArenaWall.mat";
        const string ThuyTinhScene = "Assets/Scenes/Map_ThuyTinh.unity";
        const string SonTinhScene = "Assets/Scenes/Map_SonTinh.unity";

        [MenuItem("Tools/Son Tinh Thuy Tinh/Build Fight Limits + Croc Intro (Thuy Tinh, Son Tinh maps)")]
        public static void BuildAll()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("Stop Play mode first."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.OpenScene(ThuyTinhScene, OpenSceneMode.Single);
            var croc = Object.FindFirstObjectByType<SauChinDuoiBoss>(FindObjectsInactive.Include);
            if (croc != null) { AddCrocIntro(croc); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
            else Debug.LogError("ArenaBoundaryBuilder: no SauChinDuoiBoss in " + ThuyTinhScene);

            scene = EditorSceneManager.OpenScene(SonTinhScene, OpenSceneMode.Single);
            var director = Object.FindFirstObjectByType<RoosterQuestDirector>(FindObjectsInactive.Include);
            if (director != null) { AddRoosterBoundary(director); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
            else Debug.LogError("ArenaBoundaryBuilder: no RoosterQuestDirector in " + SonTinhScene);
        }

        // ---------------------------------------------------------------- crocodile

        public static void AddCrocIntro(SauChinDuoiBoss croc)
        {
            var so = new SerializedObject(croc);
            var centre = (Transform)so.FindProperty("arenaCenter").objectReferenceValue;
            float radius = so.FindProperty("arenaRadius").floatValue;
            Transform root = croc.transform.parent;

            var boundary = Boundary(root, centre, radius + 1f, new Color(0.45f, 0.85f, 1f, 0.6f));

            var old = root.Find("CrocIntroCam"); if (old != null) Object.DestroyImmediate(old.gameObject);
            var camGo = new GameObject("CrocIntroCam");
            camGo.transform.SetParent(root, false);
            var cam = camGo.AddComponent<CinemachineCamera>();
            cam.Priority = 100;
            cam.Lens.FieldOfView = 50f;
            camGo.SetActive(false);

            var bar = (BossHealthBar)so.FindProperty("bar").objectReferenceValue;
            CanvasGroup letterbox = null, title = null;
            if (bar != null)
            {
                Transform canvas = bar.GetComponentInParent<Canvas>().transform;
                foreach (string n in new[] { "Letterbox", "IntroTitle" }) { var o = canvas.Find(n); if (o != null) Object.DestroyImmediate(o.gameObject); }
                letterbox = Letterbox(canvas);
                title = Title(canvas, bar.GetComponentInChildren<TMP_Text>(true)?.font);
            }

            so.FindProperty("boundary").objectReferenceValue = boundary;
            so.FindProperty("introCamera").objectReferenceValue = cam;
            so.FindProperty("letterbox").objectReferenceValue = letterbox;
            so.FindProperty("introTitle").objectReferenceValue = title;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"ArenaBoundaryBuilder: crocodile intro + island limit (radius {radius + 1f:F1} m) added.");
        }

        static CanvasGroup Letterbox(Transform canvas)
        {
            var go = new GameObject("Letterbox", typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(canvas, false);
            var rt = (RectTransform)go.transform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            go.transform.SetAsFirstSibling();   // under the boss bar
            foreach (bool top in new[] { true, false })
            {
                var bar = new GameObject(top ? "Top" : "Bottom", typeof(RectTransform), typeof(Image));
                bar.transform.SetParent(go.transform, false);
                var r = (RectTransform)bar.transform;
                r.anchorMin = new Vector2(0f, top ? 1f : 0f); r.anchorMax = new Vector2(1f, top ? 1f : 0f); r.pivot = new Vector2(0.5f, top ? 1f : 0f);
                r.sizeDelta = new Vector2(0f, 120f);
                var img = bar.GetComponent<Image>(); img.color = Color.black; img.raycastTarget = false;
            }
            var g = go.GetComponent<CanvasGroup>(); g.alpha = 0f; g.blocksRaycasts = false; g.interactable = false;
            return g;
        }

        static CanvasGroup Title(Transform canvas, TMP_FontAsset font)
        {
            var go = new GameObject("IntroTitle", typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(canvas, false);
            var rt = (RectTransform)go.transform; rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f); rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 170f); rt.sizeDelta = new Vector2(1200f, 180f);
            TMP_Text Line(string name, string text, float size, float y, Color color)
            {
                var t = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
                t.transform.SetParent(go.transform, false);
                var r = (RectTransform)t.transform; r.anchorMin = new Vector2(0f, 0f); r.anchorMax = new Vector2(1f, 0f); r.pivot = new Vector2(0.5f, 0f);
                r.anchoredPosition = new Vector2(0f, y); r.sizeDelta = new Vector2(0f, size * 1.4f);
                var tmp = t.GetComponent<TextMeshProUGUI>();
                if (font != null) tmp.font = font;
                tmp.text = text; tmp.fontSize = size; tmp.alignment = TextAlignmentOptions.Center; tmp.color = color; tmp.raycastTarget = false;
                tmp.fontStyle = FontStyles.Bold; tmp.characterSpacing = 6f;
                return tmp;
            }
            Line("Name", "SẤU CHÍN ĐUÔI", 86f, 50f, new Color(0.98f, 0.93f, 0.80f));
            Line("Epithet", "Chúa tể vực sâu", 36f, 0f, new Color(0.55f, 0.85f, 1f));
            var g = go.GetComponent<CanvasGroup>(); g.alpha = 0f; g.blocksRaycasts = false; g.interactable = false;
            return g;
        }

        // ---------------------------------------------------------------- rooster

        public static void AddRoosterBoundary(RoosterQuestDirector director)
        {
            var so = new SerializedObject(director);
            var centre = (Transform)so.FindProperty("arenaCenter").objectReferenceValue;
            float radius = so.FindProperty("arenaRadius").floatValue;
            var boundary = Boundary(director.transform, centre, radius + 1f, new Color(1f, 0.7f, 0.35f, 0.5f));
            so.FindProperty("boundary").objectReferenceValue = boundary;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"ArenaBoundaryBuilder: rooster ring limit (radius {radius + 1f:F1} m) added.");
        }

        // ---------------------------------------------------------------- shared

        static ArenaBoundary Boundary(Transform parent, Transform centre, float radius, Color color)
        {
            var old = parent.Find("ArenaBoundary"); if (old != null) Object.DestroyImmediate(old.gameObject);
            var go = new GameObject("ArenaBoundary");
            go.transform.SetParent(parent, false);
            var boundary = go.AddComponent<ArenaBoundary>();
            var b = new SerializedObject(boundary);
            b.FindProperty("center").objectReferenceValue = centre;
            b.FindProperty("radius").floatValue = radius;
            b.FindProperty("color").colorValue = color;
            b.FindProperty("wallMaterial").objectReferenceValue = WallMaterial();
            b.ApplyModifiedPropertiesWithoutUndo();
            return boundary;
        }

        // URP Unlit, transparent alpha blend, both sides, no depth write: the curtain's colour and fade come from ArenaBoundary
        static Material WallMaterial()
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (m != null) return m;
            if (!AssetDatabase.IsValidFolder("Assets/Art/Combat")) AssetDatabase.CreateFolder("Assets/Art", "Combat");
            m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f); m.SetFloat("_Cull", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            m.SetColor("_BaseColor", new Color(0.45f, 0.85f, 1f, 0.6f));
            AssetDatabase.CreateAsset(m, MaterialPath);
            AssetDatabase.SaveAssets();
            return m;
        }
    }
}
