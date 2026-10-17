using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using SonTinhThuyTinh.Dialogue;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace SonTinhThuyTinh.DevTools
{
    // Why is a cutscene dark? Every 2 s: the sun, the ambient, the sky exposure, the colour grading of every Volume, and every full-screen UI
    // image that is not clear; plus Game view screenshots (Temp/probe_NN.png) while a dialogue plays. Writes Temp/probe.txt. Not part of the game.
    public class BrightnessProbe : MonoBehaviour
    {
        public float seconds = 30f;
        readonly StringBuilder log = new();

        IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            int shot = 0;
            for (float t = 0f; t < seconds; t += 2f)
            {
                yield return new WaitForSecondsRealtime(2f);
                var runner = FindFirstObjectByType<DialogueRunner>(FindObjectsInactive.Include);
                var sb = new StringBuilder($"t={t + 2f:F0} scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name} timeScale={Time.timeScale} dialogue={(runner != null && runner.IsPlaying)}");
                foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l.type == LightType.Directional) sb.Append($" | sun {l.name} int={l.intensity:F2} col={l.color} enabled={l.enabled}");
                sb.Append($" | ambient={RenderSettings.ambientIntensity:F2} sky={(RenderSettings.skybox != null ? RenderSettings.skybox.name : "none")}");
                if (RenderSettings.skybox != null && RenderSettings.skybox.HasProperty("_Exposure")) sb.Append($" exp={RenderSettings.skybox.GetFloat("_Exposure"):F2}");
                foreach (Volume v in FindObjectsByType<Volume>(FindObjectsSortMode.None))
                {
                    var profile = v.profile;
                    string g = "";
                    if (profile != null && profile.TryGet(out ColorAdjustments ca)) g += $" postExp={ca.postExposure.value:F2} filter={ca.colorFilter.value}";
                    if (profile != null && profile.TryGet(out Vignette vg)) g += $" vignette={vg.intensity.value:F2}";
                    sb.Append($" | vol {v.name} w={v.weight:F2}{g}");
                }
                foreach (Image img in FindObjectsByType<Image>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    var rt = (RectTransform)img.transform;
                    bool big = rt.rect.width >= Screen.width * 0.8f && rt.rect.height >= Screen.height * 0.5f;
                    if (big && img.color.a > 0.05f && img.enabled) sb.Append($" | UI {img.name} a={img.color.a:F2} col={img.color} canvas={img.canvas.name}/{img.canvas.sortingOrder}");
                }
                log.AppendLine(sb.ToString());
                if (runner != null && runner.IsPlaying && shot < 16)
                {
                    ScreenCapture.CaptureScreenshot(Path.Combine(Application.dataPath, $"../Temp/probe_{shot++:00}.png"));
                    yield return new WaitForSecondsRealtime(0.5f);
                    typeof(DialogueRunner).GetMethod("ShowNextLine", BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(runner, null);
                }
            }
            File.WriteAllText(Path.Combine(Application.dataPath, "../Temp/probe.txt"), log.ToString());
        }
    }
}
