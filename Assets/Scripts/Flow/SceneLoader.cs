using System.Collections;
using SonTinhThuyTinh.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SonTinhThuyTinh.Flow
{
    // Fades to black, loads the scene, fades back in. Creates itself on first use, so no scene needs to contain it.
    public class SceneLoader : MonoBehaviour
    {
        const float FadeDuration = 0.6f;

        static SceneLoader instance;

        CanvasGroup fade;

        public static bool IsLoading { get; private set; }

        public static void Load(string sceneName)
        {
            if (IsLoading) return;

            // A deliberate scene change always leaves pause behind, even if something forgot to resume first.
            Time.timeScale = 1f;

            if (instance == null) instance = Create();
            instance.StartCoroutine(instance.LoadRoutine(sceneName));
        }

        static SceneLoader Create()
        {
            var root = new GameObject(nameof(SceneLoader));
            DontDestroyOnLoad(root);

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            // Lets the black overlay swallow clicks so no button fires mid-transition.
            root.AddComponent<GraphicRaycaster>();

            var image = new GameObject("Fade", typeof(RectTransform), typeof(Image));
            image.transform.SetParent(root.transform, false);
            var rect = (RectTransform)image.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            image.GetComponent<Image>().color = Color.black;

            var loader = root.AddComponent<SceneLoader>();
            loader.fade = root.AddComponent<CanvasGroup>();
            loader.fade.alpha = 0f;
            loader.fade.blocksRaycasts = false;
            return loader;
        }

        IEnumerator LoadRoutine(string sceneName)
        {
            IsLoading = true;
            fade.blocksRaycasts = true;

            yield return CanvasGroupFade.Run(fade, 0f, 1f, FadeDuration);
            yield return SceneManager.LoadSceneAsync(sceneName);
            yield return CanvasGroupFade.Run(fade, 1f, 0f, FadeDuration);

            fade.blocksRaycasts = false;
            IsLoading = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            instance = null;
            IsLoading = false;
            // Enter Play Mode can preserve static/runtime state when domain reload is disabled.
            // Never let a previous paused gameplay session freeze the opening menu.
            Time.timeScale = 1f;
        }
    }
}
