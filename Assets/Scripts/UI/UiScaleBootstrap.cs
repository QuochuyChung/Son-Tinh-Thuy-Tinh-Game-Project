using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SonTinhThuyTinh.UI
{
    // Shrinks every Canvas Scaler (Scale With Screen Size) by `Shrink`, in every scene and also for canvases the game creates while it
    // runs (boss bars, banners ...), by raising its reference resolution: 1920x1080 becomes 2496x1404, so everything on screen is drawn
    // 1/1.3 as big. Made for small / high-DPI laptop screens where the interface was too big and spilled over.
    // Change `Shrink` (or set PlayerPrefs float "UiShrink") to tune: 1 = off, 1.3 = a quarter smaller, 1.5 = a third smaller.
    public static class UiScaleBootstrap
    {
        public const float DefaultShrink = 1.3f;
        public const string PrefsKey = "UiShrink";

        public static float Shrink { get; private set; } = DefaultShrink;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            try { Shrink = Mathf.Clamp(PlayerPrefs.GetFloat(PrefsKey, DefaultShrink), 1f, 2.5f); }
            catch { Shrink = DefaultShrink; }
            if (Object.FindFirstObjectByType<UiScaler>() != null) return;
            var go = new GameObject("UiScaler") { hideFlags = HideFlags.HideInHierarchy };
            Object.DontDestroyOnLoad(go);
            go.AddComponent<UiScaler>();
        }

        sealed class UiScaler : MonoBehaviour
        {
            readonly HashSet<int> done = new();

            void OnEnable() { SceneManager.sceneLoaded += OnLoaded; Apply(); }
            void OnDisable() => SceneManager.sceneLoaded -= OnLoaded;
            void OnLoaded(Scene scene, LoadSceneMode mode) => Apply();
            void Update() { if (Time.frameCount % 4 == 0) Apply(); }   // canvases made while playing

            void Apply()
            {
                if (Shrink <= 1.001f) return;
                foreach (CanvasScaler scaler in FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize || !done.Add(scaler.GetInstanceID())) continue;
                    scaler.referenceResolution *= Shrink;
                }
            }
        }

        // Keeps state clean when Enter Play Mode Options skip the domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => Shrink = DefaultShrink;
    }
}
