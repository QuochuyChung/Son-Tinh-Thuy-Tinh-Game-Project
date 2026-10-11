using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace SonTinhThuyTinh.EditorTools
{
    // Puts the key-art picture (title, the two heroes, the "Bắt đầu" / "Thoát" buttons painted in) behind Assets/Scenes/MainMenu.unity.
    // Drop the picture at Assets/Art/UI/MainMenu/menu_bg.png (or .jpg), then run Tools > Son Tinh Thuy Tinh > Build Main Menu Art.
    // The picture covers the screen (AspectRatioFitter, Envelope); the existing Button_NewGame / Button_Quit become see-through buttons
    // laid exactly over the painted ones (a soft golden glow on hover / selection), so the MainMenuController keeps working. The old
    // title, subtitle and footer texts are switched off (the picture has them). Safe to run again.
    public static class MainMenuArtBuilder
    {
        const string Folder = "Assets/Art/UI/MainMenu";
        const string ScenePath = "Assets/Scenes/MainMenu.unity";

        // where the painted buttons are on the picture, as fractions of its width / height measured from the top-left (picture 1685 x 934)
        static readonly Rect NewGame = new(645f / 1685f, 425f / 934f, 395f / 1685f, 85f / 934f);
        static readonly Rect Quit = new(645f / 1685f, 523f / 934f, 395f / 1685f, 89f / 934f);

        [MenuItem("Tools/Son Tinh Thuy Tinh/Build Main Menu Art")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("Stop Play mode first."); return; }
            string path = null;
            foreach (string ext in new[] { "png", "jpg", "jpeg" })
                if (File.Exists($"{Folder}/menu_bg.{ext}")) { path = $"{Folder}/menu_bg.{ext}"; break; }
            if (path == null) { Debug.LogError($"MainMenuArtBuilder: put the picture at {Folder}/menu_bg.png (or .jpg) first."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            float ratio = sprite.rect.width / sprite.rect.height;

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var canvas = GameObject.Find("MainMenuCanvas").transform;
            var old = canvas.Find("MenuArt");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            // the picture, as big as needed to cover the whole screen, centred
            var art = new GameObject("MenuArt", typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
            art.transform.SetParent(canvas, false);
            art.transform.SetAsFirstSibling();
            var rt = (RectTransform)art.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            var image = art.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            var fitter = art.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = ratio;

            Place(canvas, rt, "Button_NewGame", NewGame);
            Place(canvas, rt, "Button_Quit", Quit);
            foreach (string name in new[] { "Title", "Subtitle", "Footer" })
            {
                var t = canvas.Find(name);
                if (t != null) t.gameObject.SetActive(false);
            }
            var cam = Object.FindFirstObjectByType<Camera>();
            if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black; }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"MainMenuArtBuilder: {path} ({sprite.rect.width}x{sprite.rect.height}) is the main menu background.");
        }

        // Moves a button under the picture, over the painted one (fractions measured from the top-left), see-through with a glow on hover.
        static void Place(Transform canvas, RectTransform art, string name, Rect topLeftFraction)
        {
            var button = canvas.Find(name);
            if (button == null) button = art.Find(name);
            if (button == null) { Debug.LogWarning("MainMenuArtBuilder: no " + name); return; }
            button.SetParent(art, false);
            var rt = (RectTransform)button;
            rt.anchorMin = new Vector2(topLeftFraction.x, 1f - topLeftFraction.yMax);
            rt.anchorMax = new Vector2(topLeftFraction.xMax, 1f - topLeftFraction.y);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);

            var img = button.GetComponent<Image>();
            img.sprite = null;
            img.color = Color.white;
            img.raycastTarget = true;
            var b = button.GetComponent<Button>();
            b.transition = Selectable.Transition.ColorTint;
            b.targetGraphic = img;
            b.colors = new ColorBlock
            {
                normalColor = new Color(1f, 1f, 1f, 0f),
                highlightedColor = new Color(1f, 0.93f, 0.62f, 0.22f),
                pressedColor = new Color(1f, 0.85f, 0.4f, 0.38f),
                selectedColor = new Color(1f, 0.93f, 0.62f, 0.16f),
                disabledColor = new Color(0f, 0f, 0f, 0.4f),
                colorMultiplier = 1f,
                fadeDuration = 0.12f,
            };
            var label = button.Find("Label");
            if (label != null) label.gameObject.SetActive(false);   // the picture already says it
        }
    }
}
