using System.Collections;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace SonTinhThuyTinh.Trailer
{
    // Records one trailer clip as a JPG sequence (Trailer_Output/frames/<clip>/00000.jpg ...), 1920x1080, 30 fps of game time
    // (Time.captureFramerate, so slow machines still give smooth clips). Renders a camera into its own render texture, so the
    // screen-space-overlay canvases (HUD, dialogue box, cutscene cards) are never in the picture. The pixels come back with
    // AsyncGPUReadback and are encoded on worker threads. ffmpeg turns the frames into the trailer (Assets/Trailer/Tools~/assemble_trailer.py).
    public class TrailerCapture : MonoBehaviour
    {
        public const int Width = 1920, Height = 1080, Fps = 30;

        public static TrailerCapture Current { get; private set; }
        public static string OutputRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Trailer_Output", "frames"));

        public bool Done { get; private set; }
        public int Frames { get; private set; }
        public float Seconds => Frames / (float)Fps;
        public Camera FixedCamera { get; set; }
        public string Clip => Path.GetFileName(folder);

        string folder;
        int total, pending;
        RenderTexture rt;

        // camera null = whatever Camera.main is on each frame (the Cinemachine brain camera of a cutscene or of gameplay)
        public static TrailerCapture Begin(string clip, float seconds, Camera camera = null)
        {
            if (Current != null) Destroy(Current.gameObject);
            var go = new GameObject("TrailerCapture " + clip);
            DontDestroyOnLoad(go);
            var c = go.AddComponent<TrailerCapture>();
            c.folder = Path.Combine(OutputRoot, clip);
            if (Directory.Exists(c.folder)) Directory.Delete(c.folder, true);
            Directory.CreateDirectory(c.folder);
            c.total = Mathf.RoundToInt(seconds * Fps);
            c.FixedCamera = camera;
            Current = c;
            Time.captureFramerate = Fps;
            Debug.Log($"[Trailer] recording {clip}: {c.total} frames");
            return c;
        }

        // ends the clip early (the frames so far are kept)
        public void Stop() => total = Frames;

        public IEnumerator WaitDone() { while (!Done) yield return null; }

        void Awake() => rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };

        void Start() => StartCoroutine(Record());

        IEnumerator Record()
        {
            var end = new WaitForEndOfFrame();
            while (Frames < total)
            {
                yield return end;
                if (Frames >= total) break;
                Camera cam = FixedCamera != null ? FixedCamera : Camera.main;
                if (cam == null) continue;
                RenderTexture keep = cam.targetTexture;
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = keep;
                string path = Path.Combine(folder, Frames.ToString("00000") + ".jpg");
                Interlocked.Increment(ref pending);
                AsyncGPUReadback.Request(rt, 0, TextureFormat.RGBA32, req => Save(req, path));
                Frames++;
                if (pending > 24) AsyncGPUReadback.WaitAllRequests();
            }
            while (pending > 0) yield return null;
            Time.captureFramerate = 0;
            Done = true;
            Debug.Log($"[Trailer] done {Path.GetFileName(folder)}: {Frames} frames");
        }

        void Save(AsyncGPUReadbackRequest req, string path)
        {
            if (req.hasError) { Interlocked.Decrement(ref pending); return; }
            byte[] data = req.GetData<byte>().ToArray();
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try { File.WriteAllBytes(path, ImageConversion.EncodeArrayToJPG(data, GraphicsFormat.R8G8B8A8_UNorm, Width, Height, 0, 93)); }
                finally { Interlocked.Decrement(ref pending); }
            });
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
            if (rt != null) rt.Release();
        }
    }
}
