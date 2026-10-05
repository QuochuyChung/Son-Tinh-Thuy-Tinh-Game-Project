using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SonTinhThuyTinh.EditorTools
{
    // Makes the world map picture (Assets/Art/UI/Map/world_map.jpg, docs/progress.md 9.9) from the real scenes: a top-down render of
    // Map_SonTinh (left), Map_HungVuong (middle) and Map_ThuyTinh (right), laid out by WorldMapLayout, with a sea-coloured background,
    // dotted links from the end of each route to the plateau's gates and a gold frame. It also finds where the named places are and
    // stores them in the WorldMapData asset, then rebuilds the map UI of the three scenes so the arrow and labels line up.
    // Run it again after changing any of the three maps. The scenes must already be built (their menu items).
    public static class WorldMapComposer
    {
        const string SonTinhScene = "Assets/Scenes/Map_SonTinh.unity";
        const string PalaceScene = "Assets/Scenes/Map_HungVuong.unity";
        const string ThuyTinhScene = "Assets/Scenes/Map_ThuyTinh.unity";
        const float PalaceEntryX = 62f;   // the plateau's west/east gates (PalaceMapBuilder.EntryX)

        static readonly Color32 Gold = new(246, 204, 92, 255), Dark = new(26, 18, 10, 255);

        [MenuItem("Tools/Son Tinh Thuy Tinh/Rebuild World Map Picture")]
        public static void Rebuild()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("Stop Play mode first."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Vector2Int size = WorldMapLayout.SizePixels;
            var places = new List<(string label, Vector2 position)>();

            // ---- capture the three scenes (each is opened, rendered from above and left untouched)
            Texture2D sonShot, palaceShot, thuyShot;
            Vector2 sonEnd, thuyEnd;
            using (new OpenScene(SonTinhScene))
            {
                sonShot = CaptureRoute();
                Grade(sonShot, 0.40f, 1.25f);
                sonEnd = Position("Exit_To_Next");
                AddPlace(places, WorldMapLayout.SonTinh, "Rừng Rối", Position("Campfire"));
                AddPlace(places, WorldMapLayout.SonTinh, "Khu rừng linh", Position("Crystals"));
                AddPlace(places, WorldMapLayout.SonTinh, "Làng sơn cước", Average("StiltHouse", z => z < 80f));
            }
            using (new OpenScene(PalaceScene))
            {
                palaceShot = CapturePlateau();
                Grade(palaceShot, 0.42f, 1.0f);
                Vector2 palace = Position("Palace_HungVuong");
                if (palace == Vector2.zero) palace = new Vector2(0f, 62f);   // the placeholder compound
                AddPlace(places, WorldMapLayout.Palace, "Cung điện Vua Hùng", palace);
            }
            using (new OpenScene(ThuyTinhScene))
            {
                thuyShot = CaptureRoute();
                Grade(thuyShot, 0.40f, 1.25f);
                thuyEnd = Position("Exit_To_Next");
                AddPlace(places, WorldMapLayout.ThuyTinh, "Vịnh Đầm Lầy", Average("Log", _ => true));
                AddPlace(places, WorldMapLayout.ThuyTinh, "Thác Mờ Sương", Position("Waterfall_ThuyTinhR185"));
                AddPlace(places, WorldMapLayout.ThuyTinh, "Hang Chìm", Position("Crystals"));
            }

            // ---- compose: the sea (colour taken from the plateau's open water), the three pictures, links, vignette, frame
            var canvas = new Color32[size.x * size.y];   // rows from the top
            FillBackground(canvas, size, palaceShot.GetPixel(3, 3));
            BlitRectangle(canvas, size, sonShot, WorldMapLayout.SonTinh.ToPixels(new Vector2(-WorldMapLayout.RouteHalfWidth, WorldMapLayout.RouteZMax)));
            BlitCircle(canvas, size, palaceShot, WorldMapLayout.Palace.ToPixels(new Vector2(-WorldMapLayout.PalaceHalf, WorldMapLayout.PalaceHalf)));
            BlitRectangle(canvas, size, thuyShot, WorldMapLayout.ThuyTinh.ToPixels(new Vector2(-WorldMapLayout.RouteHalfWidth, WorldMapLayout.RouteZMax)));
            Object.DestroyImmediate(sonShot);
            Object.DestroyImmediate(palaceShot);
            Object.DestroyImmediate(thuyShot);

            // dotted links from the end of each route (its gate) to the plateau's gates
            DrawDots(canvas, size, WorldMapLayout.SonTinh.ToPixels(sonEnd), WorldMapLayout.Palace.ToPixels(new Vector2(-PalaceEntryX, 0f)));
            DrawDots(canvas, size, WorldMapLayout.ThuyTinh.ToPixels(thuyEnd), WorldMapLayout.Palace.ToPixels(new Vector2(PalaceEntryX, 0f)));
            Vignette(canvas, size);
            DrawFrame(canvas, size);

            // ---- save (rows from the bottom for the texture)
            var picture = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
            var flipped = new Color32[canvas.Length];
            for (int y = 0; y < size.y; y++) System.Array.Copy(canvas, y * size.x, flipped, (size.y - 1 - y) * size.x, size.x);
            picture.SetPixels32(flipped);
            picture.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(MapHudBuilder.PicturePath));
            File.WriteAllBytes(MapHudBuilder.PicturePath, picture.EncodeToJPG(92));
            Object.DestroyImmediate(picture);
            AssetDatabase.ImportAsset(MapHudBuilder.PicturePath, ImportAssetOptions.ForceUpdate);
            MapHudBuilder.ConfigurePicture();
            MapHudBuilder.SetPlaces(places);

            // ---- the map UI of the three scenes reads the picture size and the places
            SonTinhMapBuilder.RefreshMapHud();
            PalaceMapBuilder.RefreshMapHud();
            ThuyTinhMapBuilder.RefreshMapHud();
            Debug.Log($"World map picture rebuilt: {size.x}x{size.y}, {places.Count} places.");
        }

        // Opens a scene (the previous one is already saved) and makes sure it is left unchanged and not dirty.
        sealed class OpenScene : System.IDisposable
        {
            readonly UnityEngine.SceneManagement.Scene scene;
            readonly List<GameObject> hidden = new();
            readonly List<(MeshRenderer renderer, Material original)> swapped = new();
            readonly Material flatWater;
            readonly bool fog, asyncShaders;
            readonly float lodBias, shadowDistance;
            readonly UniversalRenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

            public OpenScene(string path)
            {
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

                // things that must not show on a map: far-away mountains, the gold gift markers
                foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    if (t.name == "DistantMountains" || t.name == "GiftPickups") { hidden.Add(t.gameObject); t.gameObject.SetActive(false); }

                // The marsh water (reflective screen-space shader) shows only the muddy floor from straight above: use flat teal instead.
                flatWater = new Material(Shader.Find("Sprites/Default")) { color = new Color(0.12f, 0.52f, 0.64f, 0.82f) };
                foreach (MeshRenderer r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    if (r.gameObject.name == "Water" && r.transform.parent != null && r.transform.parent.name == "Generated")
                    {
                        swapped.Add((r, r.sharedMaterial));
                        r.sharedMaterial = flatWater;
                    }

                asyncShaders = ShaderUtil.allowAsyncCompilation;
                ShaderUtil.allowAsyncCompilation = false;   // objects whose shader is still compiling would be left out of the picture
                fog = RenderSettings.fog;
                RenderSettings.fog = false;
                lodBias = QualitySettings.lodBias;
                QualitySettings.lodBias = 200f;   // an orthographic camera far above would otherwise cull every tree
                if (pipeline != null)
                {
                    shadowDistance = pipeline.shadowDistance;
                    pipeline.shadowDistance = 0f;   // no shadows: flat, readable shapes like a map, instead of dark blotches under every tree
                }
            }

            public void Dispose()
            {
                foreach (GameObject go in hidden) go.SetActive(true);
                foreach (var (renderer, original) in swapped) renderer.sharedMaterial = original;
                Object.DestroyImmediate(flatWater);
                ShaderUtil.allowAsyncCompilation = asyncShaders;
                RenderSettings.fog = fog;
                QualitySettings.lodBias = lodBias;
                if (pipeline != null) pipeline.shadowDistance = shadowDistance;
                EditorSceneManager.SaveScene(scene);   // identical to what was loaded; keeps the scene clean so the next open never asks
            }
        }

        // ---------------------------------------------------------------- capture

        static Texture2D CaptureRoute() => Capture(
            new Vector2(0f, (WorldMapLayout.RouteZMin + WorldMapLayout.RouteZMax) * 0.5f),
            new Vector2(WorldMapLayout.RouteHalfWidth * 2f, WorldMapLayout.RouteZMax - WorldMapLayout.RouteZMin));

        static Texture2D CapturePlateau() => Capture(Vector2.zero, new Vector2(WorldMapLayout.PalaceHalf * 2f, WorldMapLayout.PalaceHalf * 2f));

        // Top-down orthographic render of the open scene: world rectangle (centre x/z, size in metres) at PixelsPerMeter.
        static Texture2D Capture(Vector2 centre, Vector2 sizeMeters)
        {
            int w = Mathf.RoundToInt(sizeMeters.x * WorldMapLayout.PixelsPerMeter), h = Mathf.RoundToInt(sizeMeters.y * WorldMapLayout.PixelsPerMeter);
            var go = new GameObject("WorldMapCapture");
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = sizeMeters.y * 0.5f;
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 2000f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.26f, 0.34f);
            go.transform.SetPositionAndRotation(new Vector3(centre.x, 800f, centre.y), Quaternion.Euler(90f, 0f, 0f));   // looking down, image up = world +z

            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            // The first render after opening a scene can come out without objects whose shaders are still compiling: render twice, keep the second.
            cam.Render();
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false) { hideFlags = HideFlags.HideAndDontSave };   // survives the next scene being opened
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(go);
            return tex;
        }

        // ---------------------------------------------------------------- finding the named places

        static Vector2 Position(string objectName)
        {
            Transform best = null;
            int bestChildren = -1;
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == objectName && t.childCount > bestChildren) { best = t; bestChildren = t.childCount; }   // the biggest one if several
            if (best == null) { Debug.LogWarning("World map: no object named " + objectName); return Vector2.zero; }

            // objects whose pivot is not where they are (a waterfall's root sits at the scene origin): use the middle of their meshes
            Bounds bounds = default;
            bool any = false;
            foreach (MeshRenderer r in best.GetComponentsInChildren<MeshRenderer>())
            {
                if (!any) bounds = r.bounds; else bounds.Encapsulate(r.bounds);
                any = true;
            }
            return any ? new Vector2(bounds.center.x, bounds.center.z) : new Vector2(best.position.x, best.position.z);
        }

        static Vector2 Average(string objectName, System.Predicate<float> byZ)
        {
            Vector2 sum = Vector2.zero;
            int count = 0;
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == objectName && byZ(t.position.z)) { sum += new Vector2(t.position.x, t.position.z); count++; }
            if (count == 0) { Debug.LogWarning("World map: no object named " + objectName); return Vector2.zero; }
            return sum / count;
        }

        static void AddPlace(List<(string label, Vector2 position)> places, WorldMapLayout.Frame frame, string label, Vector2 world) =>
            places.Add((label, frame.ToFraction(world)));

        // ---------------------------------------------------------------- compositing (all in rows from the top)

        // Brightens a capture so its average luminance reaches `target` (within limits) and pumps the colours a little.
        static void Grade(Texture2D shot, float target, float saturation)
        {
            Color[] pixels = shot.GetPixels();
            double sum = 0;
            foreach (Color c in pixels) sum += c.r * 0.2126f + c.g * 0.7152f + c.b * 0.0722f;
            float mean = Mathf.Max(0.02f, (float)(sum / pixels.Length));
            float gain = Mathf.Clamp(target / mean, 0.75f, 2.2f);
            for (int i = 0; i < pixels.Length; i++)
            {
                Color c = pixels[i] * gain;
                float luma = c.r * 0.2126f + c.g * 0.7152f + c.b * 0.0722f;
                var grey = new Color(luma, luma, luma);
                c = grey + (c - grey) * saturation;
                pixels[i] = new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), 1f);
            }
            shot.SetPixels(pixels);
            shot.Apply();
        }

        static void FillBackground(Color32[] canvas, Vector2Int size, Color sea)
        {
            Color inner = sea;
            Color outer = sea * 0.68f;
            for (int y = 0; y < size.y; y++)
                for (int x = 0; x < size.x; x++)
                {
                    float dx = (x / (float)size.x - 0.5f) * 2f, dy = (y / (float)size.y - 0.5f) * 2f;
                    float t = Mathf.Clamp01((dx * dx + dy * dy) * 0.55f);
                    float grain = (Mathf.PerlinNoise(x * 0.012f, y * 0.012f) - 0.5f) * 0.05f + (Mathf.PerlinNoise(x * 0.09f, y * 0.09f) - 0.5f) * 0.025f;
                    canvas[y * size.x + x] = (Color32)(Color.Lerp(inner, outer, t) + new Color(grain, grain, grain, 0f));
                }
        }

        // Pastes a capture with soft edges. `topLeft` is the picture pixel (y down) of the capture's top-left corner.
        static void BlitRectangle(Color32[] canvas, Vector2Int size, Texture2D shot, Vector2 topLeft)
        {
            const float feather = 18f;
            Blit(canvas, size, shot, topLeft, (x, y, w, h) => Mathf.Clamp01(Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y)) / feather));
        }

        static void BlitCircle(Color32[] canvas, Vector2Int size, Texture2D shot, Vector2 topLeft)
        {
            const float feather = 46f;
            Blit(canvas, size, shot, topLeft, (x, y, w, h) =>
            {
                float radius = Mathf.Min(w, h) * 0.5f - 3f;
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(w * 0.5f, h * 0.5f));
                return Mathf.Clamp01((radius - d) / feather);
            });
        }

        static void Blit(Color32[] canvas, Vector2Int size, Texture2D shot, Vector2 topLeft, System.Func<int, int, int, int, float> alpha)
        {
            Color32[] pixels = shot.GetPixels32();   // rows from the bottom
            int w = shot.width, h = shot.height;
            int ox = Mathf.RoundToInt(topLeft.x), oy = Mathf.RoundToInt(topLeft.y);
            for (int y = 0; y < h; y++)
            {
                int cy = oy + y;
                if (cy < 0 || cy >= size.y) continue;
                for (int x = 0; x < w; x++)
                {
                    int cx = ox + x;
                    if (cx < 0 || cx >= size.x) continue;
                    float a = alpha(x, y, w, h);
                    if (a <= 0f) continue;
                    Color32 src = pixels[(h - 1 - y) * w + x];
                    Color32 dst = canvas[cy * size.x + cx];
                    canvas[cy * size.x + cx] = new Color32(
                        (byte)Mathf.RoundToInt(Mathf.Lerp(dst.r, src.r, a)), (byte)Mathf.RoundToInt(Mathf.Lerp(dst.g, src.g, a)),
                        (byte)Mathf.RoundToInt(Mathf.Lerp(dst.b, src.b, a)), 255);
                }
            }
        }

        // A dotted gold line between two picture pixels (the climb to the plateau's gate), each dot ringed in dark brown.
        static void DrawDots(Color32[] canvas, Vector2Int size, Vector2 from, Vector2 to)
        {
            float length = Vector2.Distance(from, to);
            int count = Mathf.Max(1, Mathf.FloorToInt(length / 13f));
            for (int i = 0; i <= count; i++)
            {
                Vector2 p = Vector2.Lerp(from, to, i / (float)count);
                Disc(canvas, size, p, 5.2f, Dark);
                Disc(canvas, size, p, 3.4f, Gold);
            }
        }

        static void Disc(Color32[] canvas, Vector2Int size, Vector2 centre, float radius, Color32 color)
        {
            int r = Mathf.CeilToInt(radius) + 1;
            for (int y = -r; y <= r; y++)
                for (int x = -r; x <= r; x++)
                {
                    int px = Mathf.RoundToInt(centre.x) + x, py = Mathf.RoundToInt(centre.y) + y;
                    if (px < 0 || py < 0 || px >= size.x || py >= size.y) continue;
                    float d = Mathf.Sqrt(x * x + y * y);
                    float a = Mathf.Clamp01(radius - d + 0.5f);
                    if (a <= 0f) continue;
                    Color32 dst = canvas[py * size.x + px];
                    canvas[py * size.x + px] = new Color32(
                        (byte)Mathf.RoundToInt(Mathf.Lerp(dst.r, color.r, a)), (byte)Mathf.RoundToInt(Mathf.Lerp(dst.g, color.g, a)),
                        (byte)Mathf.RoundToInt(Mathf.Lerp(dst.b, color.b, a)), 255);
                }
        }

        static void Vignette(Color32[] canvas, Vector2Int size)
        {
            for (int y = 0; y < size.y; y++)
                for (int x = 0; x < size.x; x++)
                {
                    float dx = (x / (float)size.x - 0.5f) * 2f, dy = (y / (float)size.y - 0.5f) * 2f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float shade = 1f - 0.26f * Mathf.SmoothStep(0.62f, 1.25f, d);
                    Color32 c = canvas[y * size.x + x];
                    canvas[y * size.x + x] = new Color32((byte)(c.r * shade), (byte)(c.g * shade), (byte)(c.b * shade), 255);
                }
        }

        // Dark outer edge and a thin gold line inside it.
        static void DrawFrame(Color32[] canvas, Vector2Int size)
        {
            for (int y = 0; y < size.y; y++)
                for (int x = 0; x < size.x; x++)
                {
                    int edge = Mathf.Min(Mathf.Min(x, size.x - 1 - x), Mathf.Min(y, size.y - 1 - y));
                    if (edge < 10) canvas[y * size.x + x] = Dark;
                    else if (edge < 15) canvas[y * size.x + x] = Gold;
                    else if (edge < 18) canvas[y * size.x + x] = Dark;
                }
        }
    }
}
