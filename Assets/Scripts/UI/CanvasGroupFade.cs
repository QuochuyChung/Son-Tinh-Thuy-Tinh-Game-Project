using System.Collections;
using UnityEngine;

namespace SonTinhThuyTinh.UI
{
    public static class CanvasGroupFade
    {
        // Unscaled time so fades still run while the game is paused (Time.timeScale = 0).
        public static IEnumerator Run(CanvasGroup group, float from, float to, float duration)
        {
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }

            group.alpha = to;
        }
    }
}
